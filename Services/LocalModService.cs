using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnturnedModManager.Models;

namespace UnturnedModManager.Services;

/// <summary>
/// 本地插件文件系统的唯一入口。页面和 ViewModel 不再自行拼接路径或直接移动文件。
/// </summary>
public sealed class LocalModService
{
    private const int MaxArchiveEntries = 4096;
    private const long MaxExpandedArchiveBytes = 1024L * 1024 * 1024;
    private readonly CommunityModInstaller _installer;
    private readonly Func<string?> _gamePathProvider;

    public LocalModService(CommunityModInstaller installer)
        : this(installer, () => AppSettings.UnturnedInstallPath)
    {
    }

    public LocalModService(CommunityModInstaller installer, Func<string?> gamePathProvider)
    {
        _installer = installer;
        _gamePathProvider = gamePathProvider;
    }

    private PluginProfileService? _profileService;
    private ThemePackageService? _themePackageService;

    public void SetProfileService(PluginProfileService profileService)
    {
        _profileService = profileService;
    }

    public void SetThemePackageService(ThemePackageService themePackageService)
    {
        _themePackageService = themePackageService;
    }

    public string? GetPluginsPath()
    {
        var gamePath = _gamePathProvider();
        if (string.IsNullOrWhiteSpace(gamePath)) return null;
        return Path.Combine(gamePath, "BepInEx", "plugins");
    }

    public static bool IsSupportedImportFile(string path) =>
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".ummpk", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".ummtheme", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<ModItem> Scan()
    {
        var gamePath = _gamePathProvider();
        if (string.IsNullOrWhiteSpace(gamePath)) return [];
        _installer.Reconcile(gamePath);
        var pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
        if (!Directory.Exists(pluginsPath)) return [];

        var candidates = Directory.EnumerateFiles(pluginsPath, "*", SearchOption.AllDirectories)
            .Where(IsPluginFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 同时存在 Foo.dll 和 Foo.dll.disabled 时，以已启用文件为准，避免重复条目和含糊的开关状态。
        var enabledFiles = candidates
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return candidates
            .Where(path => !path.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase)
                || !enabledFiles.Contains(Path.GetFullPath(path[..^".disabled".Length])))
            .Select(path => CreateItem(path, pluginsPath, gamePath))
            .ToList();
    }

    public LocalModOperationResult SetEnabled(ModItem item, bool enabled)
    {
        try
        {
            var enabledPath = item.FullPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? item.FullPath[..^".disabled".Length]
                : item.FullPath;
            var disabledPath = enabledPath + ".disabled";
            var source = enabled ? disabledPath : enabledPath;
            var destination = enabled ? enabledPath : disabledPath;

            if (!File.Exists(source))
            {
                var currentStateMatches = enabled ? File.Exists(enabledPath) : File.Exists(disabledPath);
                return currentStateMatches
                    ? new(true, enabled ? "插件已启用。" : "插件已停用。")
                    : new(false, $"找不到插件文件：{item.FileName}");
            }

            if (File.Exists(destination))
                return new(false, $"无法切换状态：目标文件已存在（{Path.GetFileName(destination)}）。");

            File.Move(source, destination);
            item.FullPath = destination;
            item.IsEnabled = enabled;
            return new(true, enabled ? $"已启用 {item.DisplayTitle}。" : $"已停用 {item.DisplayTitle}。");
        }
        catch (Exception ex)
        {
            return new(false, $"无法更改插件状态：{ex.Message}");
        }
    }

    /// <summary>
    /// 在验证全部目标后批量切换本地插件状态。任何移动失败都会按相反顺序回滚，
    /// 因此插件方案不会留下半完成的启停状态。
    /// </summary>
    public LocalModBatchOperationResult ApplyStates(IReadOnlyDictionary<string, bool> desiredStates)
    {
        var items = Scan();
        var changes = new List<(ModItem Item, string Source, string Destination, bool Enabled)>();

        foreach (var item in items)
        {
            var enabled = desiredStates.TryGetValue(item.RelativePath, out var requested) && requested;
            if (item.IsEnabled == enabled) continue;

            var enabledPath = item.FullPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? item.FullPath[..^".disabled".Length]
                : item.FullPath;
            var source = enabled ? enabledPath + ".disabled" : enabledPath;
            var destination = enabled ? enabledPath : enabledPath + ".disabled";

            if (!File.Exists(source))
                return new(false, 0, $"找不到插件文件：{item.FileName}");
            if (File.Exists(destination))
                return new(false, 0, $"无法切换“{item.FileName}”：目标文件已存在（{Path.GetFileName(destination)}）。");

            changes.Add((item, source, destination, enabled));
        }

        var completed = new List<(ModItem Item, string Source, string Destination, bool Enabled)>();
        try
        {
            foreach (var change in changes)
            {
                File.Move(change.Source, change.Destination);
                completed.Add(change);
                change.Item.FullPath = change.Destination;
                change.Item.IsEnabled = change.Enabled;
            }

            return new(true, completed.Count, completed.Count == 0
                ? "当前插件状态已经符合该方案。"
                : $"已切换 {completed.Count} 个插件状态。");
        }
        catch (Exception ex)
        {
            foreach (var change in completed.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(change.Destination) && !File.Exists(change.Source))
                        File.Move(change.Destination, change.Source);
                    change.Item.FullPath = change.Source;
                    change.Item.IsEnabled = !change.Enabled;
                }
                catch { /* 保留原始错误信息；下次刷新时可显示实际磁盘状态。 */ }
            }

            return new(false, 0, $"切换插件方案失败，已尝试回滚：{ex.Message}");
        }
    }

    public LocalModOperationResult UninstallManual(ModItem item)
    {
        try
        {
            var enabledPath = item.FullPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? item.FullPath[..^".disabled".Length]
                : item.FullPath;
            var disabledPath = enabledPath + ".disabled";
            var removed = false;
            if (File.Exists(enabledPath)) { File.Delete(enabledPath); removed = true; }
            if (File.Exists(disabledPath)) { File.Delete(disabledPath); removed = true; }
            return removed
                ? new(true, $"已卸载 {item.DisplayTitle}。")
                : new(false, "插件文件已经不存在，列表将自动刷新。");
        }
        catch (Exception ex)
        {
            return new(false, $"卸载失败：{ex.Message}");
        }
    }

    public LocalModImportResult Import(IEnumerable<string> sourceFiles)
    {
        var gameRoot = _gamePathProvider();
        if (string.IsNullOrWhiteSpace(gameRoot) || !File.Exists(Path.Combine(gameRoot, "Unturned.exe")))
            return new(0, 0, "请先在设置中选择有效的 Unturned 游戏目录。");

        var prepared = new List<ImportEntry>();
        var skipped = 0;
        var failures = new List<string>();

        foreach (var source in sourceFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(source) || !IsSupportedImportFile(source))
            {
                skipped++;
                continue;
            }

            try
            {
                if (source.EndsWith(".ummtheme", StringComparison.OrdinalIgnoreCase))
                {
                    if (_themePackageService is not null)
                    {
                        var themeResult = _themePackageService.ImportPackage(source);
                        if (themeResult.Success)
                        {
                            return new LocalModImportResult(1, skipped, themeResult.Message);
                        }
                        throw new InvalidDataException(themeResult.Message);
                    }
                    skipped++;
                }
                else if (source.EndsWith(".ummpk", StringComparison.OrdinalIgnoreCase))
                {
                    if (_profileService is not null)
                    {
                        var profileResult = _profileService.ImportPackage(source);
                        if (profileResult.Success)
                        {
                            return new LocalModImportResult(profileResult.Profile?.Plugins.Count ?? 1, skipped, profileResult.Message);
                        }
                        throw new InvalidDataException(profileResult.Message);
                    }
                    prepared.AddRange(ReadBepInExPackage(source));
                }
                else if (source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    prepared.AddRange(ReadBepInExPackage(source));
                }
                else
                {
                    prepared.Add(new ImportEntry(
                        $"BepInEx/plugins/{Path.GetFileName(source)}",
                        source,
                        null,
                        null,
                        new FileInfo(source).Length,
                        true));
                }
            }
            catch (Exception ex)
            {
                skipped++;
                failures.Add($"{Path.GetFileName(source)}：{ex.Message}");
            }
        }

        if (prepared.Count == 0)
        {
            var emptyMessage = "没有识别到可导入的 .dll 或有效的 BepInEx 插件包。";
            if (skipped > 0) emptyMessage += $" 已跳过 {skipped} 个不支持或无效的文件。";
            if (failures.Count > 0) emptyMessage += "\n" + string.Join("\n", failures.Take(3));
            return new(0, skipped, emptyMessage);
        }

        var duplicates = prepared
            .GroupBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Take(3)
            .ToList();
        if (duplicates.Count > 0)
            return new(0, skipped + prepared.Count,
                "导入被取消：拖入的文件存在重复安装目标，无法安全决定覆盖顺序：\n" + string.Join("\n", duplicates));

        var stagingRoot = Path.Combine(gameRoot, $".umm-import-{Guid.NewGuid():N}");
        var committed = new List<CommittedImport>();
        try
        {
            var remainingArchiveBytes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in prepared)
            {
                var staged = SafeDestination(Path.Combine(stagingRoot, "files"), entry.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
                if (entry.ArchivePath is null)
                {
                    File.Copy(entry.SourceFile!, staged, overwrite: false);
                    continue;
                }

                var archivePath = Path.GetFullPath(entry.ArchivePath);
                var remaining = remainingArchiveBytes.GetValueOrDefault(archivePath, MaxExpandedArchiveBytes);
                WriteStagedArchiveEntry(entry, staged, ref remaining);
                remainingArchiveBytes[archivePath] = remaining;
            }

            foreach (var entry in prepared)
            {
                var destination = SafeDestination(gameRoot, entry.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                string? backup = null;
                if (File.Exists(destination))
                {
                    backup = SafeDestination(Path.Combine(stagingRoot, "backup"), entry.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(destination, backup, overwrite: true);
                }

                var staged = SafeDestination(Path.Combine(stagingRoot, "files"), entry.RelativePath);
                File.Move(staged, destination, overwrite: true);
                committed.Add(new CommittedImport(destination, backup));
            }

            var pluginCount = prepared.Count(entry => entry.IsPlugin);
            var message = $"已导入 {pluginCount} 个插件文件，共写入 {prepared.Count} 个 BepInEx 文件。";
            if (skipped > 0) message += $" 已跳过 {skipped} 个不支持或无效的文件。";
            if (failures.Count > 0) message += "\n" + string.Join("\n", failures.Take(3));
            return new(pluginCount, skipped, message);
        }
        catch (Exception ex)
        {
            RollbackImports(committed);
            return new(0, skipped + prepared.Count, $"导入失败，已尝试恢复原有文件：{ex.Message}");
        }
        finally
        {
            try { if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true); }
            catch { }
        }
    }

    public LocalModOperationResult OpenPluginsFolder()
    {
        var path = GetPluginsPath();
        if (path is null) return new(false, "请先在设置中选择 Unturned 游戏目录。");
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            return new(true, "");
        }
        catch (Exception ex)
        {
            return new(false, $"无法打开插件目录：{ex.Message}");
        }
    }

    public string GetFingerprint()
    {
        var path = GetPluginsPath();
        if (path is null || !Directory.Exists(path)) return "missing";
        try
        {
            return string.Join('|', Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Where(IsPluginFile)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .Select(file =>
                {
                    var info = new FileInfo(file);
                    return $"{Path.GetRelativePath(path, file)}:{info.Length}:{info.LastWriteTimeUtc.Ticks}";
                }));
        }
        catch { return Guid.NewGuid().ToString(); }
    }

    private ModItem CreateItem(string path, string pluginsPath, string gamePath)
    {
        var disabled = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
        var enabledPath = disabled ? path[..^".disabled".Length] : path;
        var fileName = Path.GetFileName(enabledPath);
        var gameRelativePath = Path.GetRelativePath(gamePath, enabledPath);
        var owner = _installer.FindOwner(gameRelativePath);
        var assembly = ReadAssembly(path);

        return new ModItem
        {
            AssemblyName = assembly.Name ?? Path.GetFileNameWithoutExtension(fileName),
            FileName = fileName,
            RelativePath = Path.GetRelativePath(pluginsPath, enabledPath),
            FullPath = path,
            IsEnabled = !disabled,
            InstallTime = $"安装时间：{File.GetLastWriteTime(path):yyyy-MM-dd HH:mm:ss}",
            InstalledVersion = owner?.Version ?? assembly.Version?.ToString() ?? "",
            CommunityModId = owner?.RemoteId,
            CommunityTitle = owner?.Title ?? "",
            IsCommunityManaged = owner is not null
        };
    }

    private static AssemblyName ReadAssembly(string path)
    {
        try { return AssemblyName.GetAssemblyName(path); }
        catch
        {
            var name = Path.GetFileNameWithoutExtension(path.Replace(".disabled", "", StringComparison.OrdinalIgnoreCase));
            return new AssemblyName(name);
        }
    }

    private static bool IsPluginFile(string path) =>
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<ImportEntry> ReadBepInExPackage(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaxArchiveEntries)
            throw new InvalidDataException($"插件包文件数量超过安全限制（{MaxArchiveEntries}）。");

        var entries = new List<ImportEntry>();
        string? wrapper = null;
        long expandedBytes = 0;
        var hasPluginDll = false;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;

            var segments = SplitArchivePath(entry.FullName);
            var bepinIndex = Array.FindIndex(segments, segment =>
                segment.Equals("BepInEx", StringComparison.OrdinalIgnoreCase));
            if (bepinIndex < 0)
                throw new InvalidDataException("压缩包必须包含 BepInEx/plugins 目录结构。");

            var currentWrapper = string.Join('/', segments.Take(bepinIndex));
            if (wrapper is null) wrapper = currentWrapper;
            else if (!wrapper.Equals(currentWrapper, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("压缩包包含多个不一致的 BepInEx 根目录。");

            var relativeSegments = segments[bepinIndex..];
            if (relativeSegments.Length < 3)
                throw new InvalidDataException("BepInEx 包内文件必须位于 plugins 或 config 子目录。");

            var area = relativeSegments[1];
            if (!area.Equals("plugins", StringComparison.OrdinalIgnoreCase)
                && !area.Equals("config", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"压缩包不允许写入 BepInEx/{area}。仅允许 plugins 与 config。");

            if (entry.Length < 0 || entry.Length > MaxExpandedArchiveBytes - expandedBytes)
                throw new InvalidDataException("压缩包解压后体积超过 1 GB 安全限制。");
            expandedBytes += entry.Length;

            var relativePath = string.Join('/', relativeSegments);
            var isPluginDll = area.Equals("plugins", StringComparison.OrdinalIgnoreCase)
                && relativePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            hasPluginDll |= isPluginDll;
            entries.Add(new ImportEntry(relativePath, null, archivePath, entry.FullName, entry.Length, isPluginDll));
        }

        if (!hasPluginDll)
            throw new InvalidDataException("压缩包未包含 BepInEx/plugins 下的 .dll 插件文件。");

        return entries;
    }

    private static string[] SplitArchivePath(string fullName)
    {
        var normalized = fullName.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidDataException("压缩包包含空路径。");

        var segments = normalized.Split('/', StringSplitOptions.None);
        if (segments.Any(segment => string.IsNullOrWhiteSpace(segment)
            || segment is "." or ".."
            || segment.Contains(':')))
            throw new InvalidDataException($"压缩包包含不安全路径：{fullName}");
        return segments;
    }

    private static string SafeDestination(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!destination.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"安装路径越界：{relativePath}");
        return destination;
    }

    private static void WriteStagedArchiveEntry(ImportEntry entry, string destination, ref long remainingArchiveBytes)
    {
        using var archive = ZipFile.OpenRead(entry.ArchivePath!);
        var source = archive.GetEntry(entry.ArchiveEntryName!)
            ?? throw new InvalidDataException("压缩包内容在校验后发生变化，已取消导入。");
        if (source.Length != entry.Length)
            throw new InvalidDataException("压缩包内容大小在校验后发生变化，已取消导入。");
        using var input = source.Open();
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        CopyToWithArchiveLimit(input, output, ref remainingArchiveBytes);
    }

    internal static void CopyToWithArchiveLimit(Stream input, Stream output, ref long remainingArchiveBytes)
    {
        if (remainingArchiveBytes < 0)
            throw new InvalidDataException("压缩包解压配额无效。");

        var buffer = new byte[81920];
        while (true)
        {
            var bytesRead = input.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0) return;
            if (bytesRead > remainingArchiveBytes)
                throw new InvalidDataException("压缩包实际解压体积超过 1 GB 安全限制。");

            output.Write(buffer, 0, bytesRead);
            remainingArchiveBytes -= bytesRead;
        }
    }

    private static void RollbackImports(IEnumerable<CommittedImport> committed)
    {
        foreach (var item in committed.Reverse())
        {
            try
            {
                if (item.BackupPath is not null && File.Exists(item.BackupPath))
                    File.Copy(item.BackupPath, item.Destination, overwrite: true);
                else if (File.Exists(item.Destination))
                    File.Delete(item.Destination);
            }
            catch { }
        }
    }

    private sealed record ImportEntry(
        string RelativePath,
        string? SourceFile,
        string? ArchivePath,
        string? ArchiveEntryName,
        long Length,
        bool IsPlugin);

    private sealed record CommittedImport(string Destination, string? BackupPath);
}

public sealed record LocalModOperationResult(bool Success, string Message);
public sealed record LocalModImportResult(int Imported, int Skipped, string Message);
public sealed record LocalModBatchOperationResult(bool Success, int Changed, string Message);
