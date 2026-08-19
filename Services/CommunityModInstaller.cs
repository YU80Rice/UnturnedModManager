using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using UnturnedModManager.Models;

namespace UnturnedModManager.Services;

public sealed class CommunityModInstaller
{
    public static event Action<int>? InstallationChanged;
    private const int MaxArchiveEntries = 4096;
    private const long MaxExpandedBytes = 1024L * 1024 * 1024;
    private static readonly HashSet<string> ProtectedRootFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Unturned.exe", "Unturned_BE.exe", "UnityPlayer.dll"
    };
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _stateRoot;

    public CommunityModInstaller(string? stateRoot = null) => _stateRoot = stateRoot
        ?? Path.Combine(AppDataPaths.RootDirectory, "community-mods");

    public bool IsInstalled(int remoteId) => File.Exists(ManifestPath(remoteId));

    public async Task InstallWithDependenciesAsync(
        CommunityApiClient api,
        CommunityModDetail mod,
        string gameRoot,
        Action<string>? progress = null,
        CancellationToken token = default) =>
        await InstallWithDependenciesDetailedAsync(
            api,
            mod,
            gameRoot,
            progress is null ? null : new Progress<TaskOperationProgress>(value => progress(value.Stage)),
            token);

    /// <summary>安装 Mod 与依赖，并将下载、校验和写入过程作为结构化进度上报。</summary>
    public async Task InstallWithDependenciesDetailedAsync(
        CommunityApiClient api,
        CommunityModDetail mod,
        string gameRoot,
        IProgress<TaskOperationProgress>? progress = null,
        CancellationToken token = default)
    {
        ValidateGameRoot(gameRoot);
        Directory.CreateDirectory(_stateRoot);
        progress?.Report(TaskOperationProgress.At(1, "正在验证游戏目录…"));
        await InstallRecursiveAsync(api, mod, gameRoot, new HashSet<int>(), progress, token);
        progress?.Report(TaskOperationProgress.At(100, "插件及依赖已写入游戏目录"));
    }

    public async Task UpdateAsync(CommunityApiClient api, CommunityModDetail mod, string gameRoot, Action<string>? progress = null, CancellationToken token = default) =>
        await UpdateDetailedAsync(
            api,
            mod,
            gameRoot,
            progress is null ? null : new Progress<TaskOperationProgress>(value => progress(value.Stage)),
            token);

    /// <summary>更新社区 Mod；失败时由原有安装器恢复更新前的文件快照。</summary>
    public async Task UpdateDetailedAsync(
        CommunityApiClient api,
        CommunityModDetail mod,
        string gameRoot,
        IProgress<TaskOperationProgress>? progress = null,
        CancellationToken token = default)
    {
        ValidateGameRoot(gameRoot);
        var previous = LoadManifest(mod.Id) ?? throw new InvalidOperationException("未找到该插件的社区安装记录。");
        progress?.Report(TaskOperationProgress.At(3, "正在确认现有安装记录…"));
        progress?.Report(TaskOperationProgress.At(8, $"正在下载新版本：{mod.DisplayTitle}"));
        var package = await api.DownloadAsync(mod, CreateDownloadProgress(progress, 8, 70, mod.DisplayTitle), token);
        progress?.Report(TaskOperationProgress.At(72, $"下载完成：{package.Source}"));
        progress?.Report(TaskOperationProgress.At(74, "正在备份当前版本…"));
        var snapshots = previous.Files.Select(file =>
        {
            var path = SafeDestination(gameRoot, file.RelativePath);
            var actual = File.Exists(path) ? path : File.Exists(path + ".disabled") ? path + ".disabled" : null;
            return new FileSnapshot(file.RelativePath, actual?.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) == true, actual is null ? null : File.ReadAllBytes(actual));
        }).ToList();
        var removed = Uninstall(mod.Id, gameRoot);
        if (!removed.Success) throw new InvalidOperationException(removed.Message);
        try
        {
            progress?.Report(TaskOperationProgress.At(82, "正在安全写入新版本…"));
            await Task.Run(() => InstallPackage(mod, package, gameRoot), token);
            progress?.Report(TaskOperationProgress.At(100, "更新完成"));
        }
        catch
        {
            foreach (var snapshot in snapshots.Where(s => s.Content is not null))
            {
                var path = SafeDestination(gameRoot, snapshot.RelativePath) + (snapshot.Disabled ? ".disabled" : "");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, snapshot.Content!);
            }
            Directory.CreateDirectory(_stateRoot);
            File.WriteAllText(ManifestPath(previous.RemoteId), JsonSerializer.Serialize(previous, JsonOptions));
            InstallationChanged?.Invoke(previous.RemoteId);
            throw;
        }
    }

    private async Task InstallRecursiveAsync(
        CommunityApiClient api, CommunityModDetail mod, string gameRoot,
        HashSet<int> visiting, IProgress<TaskOperationProgress>? progress, CancellationToken token)
    {
        if (IsInstalled(mod.Id)) return;
        if (!visiting.Add(mod.Id)) throw new InvalidOperationException("检测到循环依赖。");

        foreach (var dependency in mod.Dependencies)
        {
            if (IsInstalled(dependency.Id)) continue;
            progress?.Report(TaskOperationProgress.At(5, $"正在解析依赖：{dependency.Title.Pick()}"));
            var detail = await api.GetModAsync(dependency.Id, token);
            await InstallRecursiveAsync(api, detail, gameRoot, visiting, progress, token);
        }

        progress?.Report(TaskOperationProgress.At(12, $"正在下载：{mod.DisplayTitle}"));
        var download = await api.DownloadAsync(mod, CreateDownloadProgress(progress, 12, 75, mod.DisplayTitle), token);
        progress?.Report(TaskOperationProgress.At(77, $"下载完成：{download.Source}"));
        progress?.Report(TaskOperationProgress.At(80, $"正在校验并安全安装：{mod.DisplayTitle}"));
        await Task.Run(() => InstallPackage(mod, download, gameRoot), token);
        progress?.Report(TaskOperationProgress.At(96, $"已安装：{mod.DisplayTitle}"));
        visiting.Remove(mod.Id);
    }

    private static IProgress<DownloadProgress>? CreateDownloadProgress(
        IProgress<TaskOperationProgress>? progress,
        double start,
        double end,
        string title)
    {
        if (progress is null) return null;
        return new Progress<DownloadProgress>(value =>
        {
            var percent = value.Percent;
            var mapped = percent is null ? start : start + (end - start) * percent.Value / 100d;
            var size = value.TotalBytes is > 0
                ? $"{FormatBytes(value.ReceivedBytes)} / {FormatBytes(value.TotalBytes.Value)}"
                : $"已接收 {FormatBytes(value.ReceivedBytes)}";
            progress.Report(TaskOperationProgress.At(mapped, $"正在下载：{title}（{size}）"));
        });
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1024d / 1024d / 1024d:0.0} GB",
        >= 1024L * 1024 => $"{bytes / 1024d / 1024d:0.0} MB",
        >= 1024 => $"{bytes / 1024d:0.0} KB",
        _ => $"{bytes} B"
    };

    private void InstallPackage(CommunityModDetail mod, DownloadedMod package, string gameRoot)
    {
        var entries = ReadEntries(package);
        if (entries.Count == 0) throw new InvalidDataException("Mod 包中没有可安装文件。");

        var ownership = LoadOwnership(excludeRemoteId: mod.Id);
        var conflicts = entries.Select(e => e.RelativePath)
            .Where(path => ownership.TryGetValue(path, out _)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (conflicts.Count > 0)
            throw new InvalidOperationException("安装被阻止：以下文件属于其他社区 Mod：\n" + string.Join("\n", conflicts.Take(8)));

        var backupDir = Path.Combine(_stateRoot, "backups", mod.Id.ToString());
        var installed = new InstalledCommunityMod
        {
            RemoteId = mod.Id,
            Title = mod.DisplayTitle,
            Version = string.IsNullOrWhiteSpace(package.SourceVersion)
                ? mod.EffectiveVersion
                : package.SourceVersion,
            InstalledAt = DateTimeOffset.Now
        };

        try
        {
            foreach (var entry in entries)
            {
                var destination = SafeDestination(gameRoot, entry.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                string? backup = null;
                if (File.Exists(destination))
                {
                    backup = Path.Combine(backupDir, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(destination, backup, true);
                }
                File.WriteAllBytes(destination, entry.Content);
                installed.Files.Add(new InstalledCommunityFile
                {
                    RelativePath = entry.RelativePath,
                    Sha256 = Convert.ToHexString(SHA256.HashData(entry.Content)),
                    BackupPath = backup
                });
            }
            File.WriteAllText(ManifestPath(mod.Id), JsonSerializer.Serialize(installed, JsonOptions));
            InstallationChanged?.Invoke(mod.Id);
        }
        catch
        {
            Rollback(installed, gameRoot);
            throw;
        }
    }

    public UninstallResult Uninstall(int remoteId, string gameRoot)
    {
        var manifest = LoadManifest(remoteId);
        if (manifest is null) return new UninstallResult(false, "未找到该 Mod 的安装记录。");
        var changed = new List<string>();
        foreach (var file in manifest.Files)
        {
            var destination = SafeDestination(gameRoot, file.RelativePath);
            var actualPath = File.Exists(destination) ? destination : File.Exists(destination + ".disabled") ? destination + ".disabled" : null;
            if (actualPath is null) continue;
            var currentHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(actualPath)));
            if (!currentHash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                changed.Add(file.RelativePath);
        }
        if (changed.Count > 0)
            return new UninstallResult(false, "文件已被用户或其他程序修改。为避免丢失数据，本次未进行任何删除：\n" + string.Join("\n", changed));

        foreach (var file in manifest.Files)
        {
            var destination = SafeDestination(gameRoot, file.RelativePath);
            if (File.Exists(destination)) File.Delete(destination);
            if (File.Exists(destination + ".disabled")) File.Delete(destination + ".disabled");
            if (!string.IsNullOrWhiteSpace(file.BackupPath) && File.Exists(file.BackupPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file.BackupPath, destination, true);
            }
        }
        File.Delete(ManifestPath(remoteId));
        InstallationChanged?.Invoke(remoteId);
        return new UninstallResult(true, $"已卸载 {manifest.Title}。");
    }

    public IReadOnlyList<InstalledCommunityMod> GetInstalledMods()
    {
        if (!Directory.Exists(_stateRoot)) return [];
        return Directory.EnumerateFiles(_stateRoot, "*.json")
            .Select(LoadManifestFile).Where(item => item is not null).Cast<InstalledCommunityMod>().ToList();
    }

    public void Reconcile(string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(_stateRoot)) return;
        foreach (var manifest in GetInstalledMods())
        {
            var anyFileExists = manifest.Files.Any(file =>
            {
                var path = SafeDestination(gameRoot, file.RelativePath);
                return File.Exists(path) || File.Exists(path + ".disabled");
            });
            if (anyFileExists) continue;
            var manifestPath = ManifestPath(manifest.RemoteId);
            if (File.Exists(manifestPath)) File.Delete(manifestPath);
        }
    }

    public InstalledCommunityMod? FindOwner(string gameRelativePath)
    {
        var normalized = gameRelativePath.Replace('\\', '/');
        return GetInstalledMods().FirstOrDefault(mod => mod.Files.Any(file =>
            file.RelativePath.Replace('\\', '/').Equals(normalized, StringComparison.OrdinalIgnoreCase)));
    }

    private List<PackageEntry> ReadEntries(DownloadedMod package)
    {
        if (!Path.GetExtension(package.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return [new PackageEntry(Path.GetFileName(package.FileName), package.Content)];
        using var stream = new MemoryStream(package.Content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        if (archive.Entries.Count > MaxArchiveEntries)
            throw new InvalidDataException($"Mod 包文件数量超过安全限制（{MaxArchiveEntries}）。");
        var result = new List<PackageEntry>();
        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var relative = NormalizeRelativePath(entry.FullName);
            expandedBytes += entry.Length;
            if (expandedBytes > MaxExpandedBytes)
                throw new InvalidDataException("Mod 包解压后体积超过 1 GB 安全限制。");
            using var content = entry.Open();
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            result.Add(new PackageEntry(relative, buffer.ToArray()));
        }
        return result;
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Split('/').Any(p => p is ".." or "." || p.Contains(':')))
            throw new InvalidDataException($"Mod 包含不安全路径：{path}");
        if (!normalized.Contains('/') && ProtectedRootFiles.Contains(normalized))
            throw new InvalidDataException($"社区包不允许覆盖游戏核心文件：{normalized}");
        return normalized;
    }

    private static string SafeDestination(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!destination.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"路径越界：{relative}");
        return destination;
    }

    private Dictionary<string, int> LoadOwnership(int excludeRemoteId)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(_stateRoot)) return result;
        foreach (var file in Directory.EnumerateFiles(_stateRoot, "*.json"))
        {
            var manifest = LoadManifestFile(file);
            if (manifest is null || manifest.RemoteId == excludeRemoteId) continue;
            foreach (var owned in manifest.Files) result[owned.RelativePath] = manifest.RemoteId;
        }
        return result;
    }

    private void Rollback(InstalledCommunityMod manifest, string gameRoot)
    {
        foreach (var file in manifest.Files.AsEnumerable().Reverse())
        {
            var destination = SafeDestination(gameRoot, file.RelativePath);
            try
            {
                if (!string.IsNullOrWhiteSpace(file.BackupPath) && File.Exists(file.BackupPath))
                    File.Copy(file.BackupPath, destination, true);
                else if (File.Exists(destination))
                    File.Delete(destination);
            }
            catch { }
        }
    }

    private InstalledCommunityMod? LoadManifest(int id) => LoadManifestFile(ManifestPath(id));
    private static InstalledCommunityMod? LoadManifestFile(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<InstalledCommunityMod>(File.ReadAllText(path)) : null; }
        catch { return null; }
    }
    private string ManifestPath(int id) => Path.Combine(_stateRoot, $"{id}.json");
    private static void ValidateGameRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "Unturned.exe")))
            throw new InvalidOperationException("请先在设置中选择有效的 Unturned 游戏目录。");
    }
    private sealed record PackageEntry(string RelativePath, byte[] Content);
    private sealed record FileSnapshot(string RelativePath, bool Disabled, byte[]? Content);
}

public sealed record UninstallResult(bool Success, string Message);
