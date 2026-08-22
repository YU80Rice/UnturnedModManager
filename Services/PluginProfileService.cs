using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UnturnedModManager.Models;

namespace UnturnedModManager.Services;

/// <summary>
/// 以“实体文件 + .disabled 后缀”的方式管理插件方案。
/// 每个 Unturned 安装目录拥有独立方案文件，绝不借助 WinFsp 或挂载式文件系统。
/// </summary>
public sealed class PluginProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly LocalModService _localMods;
    private readonly Func<string?> _gamePathProvider;
    private readonly string _dataRoot;
    private readonly object _sync = new();

    public PluginProfileService(LocalModService localMods)
        : this(localMods, () => AppSettings.UnturnedInstallPath, AppDataPaths.RootDirectory)
    {
    }

    public PluginProfileService(LocalModService localMods, Func<string?> gamePathProvider, string dataRoot)
    {
        _localMods = localMods;
        _gamePathProvider = gamePathProvider;
        _dataRoot = dataRoot;
    }

    public IReadOnlyList<PluginProfile> GetProfiles()
    {
        lock (_sync)
        {
            if (!TryGetStoragePath(out var storagePath, out _) || !TryLoad(storagePath, out var document, out _))
                return [];
            return document.Profiles.OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }

    public string? GetActiveProfileId()
    {
        lock (_sync)
        {
            if (!TryGetStoragePath(out var storagePath, out _) || !TryLoad(storagePath, out var document, out _))
                return null;
            return document.ActiveProfileId;
        }
    }

    public PluginProfileOperationResult CreateFromCurrent(string? name, IReadOnlyCollection<ModItem> currentPlugins)
    {
        lock (_sync)
        {
            var validation = ValidateName(name);
            if (validation is not null) return new(false, validation);
            if (!TryGetStoragePath(out var storagePath, out var error)) return new(false, error);

            if (!TryLoad(storagePath, out var document, out error)) return new(false, error);
            var trimmedName = name!.Trim();
            if (document.Profiles.Any(profile => profile.Name.Equals(trimmedName, StringComparison.CurrentCultureIgnoreCase)))
                return new(false, "已存在同名插件方案，请换一个名称。");

            var profile = CreateSnapshot(trimmedName, currentPlugins);
            document.Profiles.Add(profile);
            document.ActiveProfileId = profile.Id;
            Save(storagePath, document);
            return new(true, $"已从当前状态创建方案“{profile.Name}”。", profile);
        }
    }

    public PluginProfileOperationResult SaveCurrent(string profileId, IReadOnlyCollection<ModItem> currentPlugins)
    {
        lock (_sync)
        {
            if (!TryGetStoragePath(out var storagePath, out var error)) return new(false, error);
            if (!TryLoad(storagePath, out var document, out error)) return new(false, error);
            var profile = document.Profiles.FirstOrDefault(item => item.Id == profileId);
            if (profile is null) return new(false, "该插件方案已不存在，请刷新后重试。");

            profile.Plugins = SnapshotEntries(currentPlugins);
            profile.UpdatedAt = DateTimeOffset.Now;
            document.ActiveProfileId = profile.Id;
            Save(storagePath, document);
            return new(true, $"已保存方案“{profile.Name}”的当前插件状态。", profile);
        }
    }

    public PluginProfileOperationResult Apply(string profileId)
    {
        lock (_sync)
        {
            if (GameLaunchService.IsUnturnedRunning())
                return new(false, "检测到 Unturned 正在运行。请退出游戏后再切换插件方案。");
            if (!TryGetStoragePath(out var storagePath, out var error)) return new(false, error);

            if (!TryLoad(storagePath, out var document, out error)) return new(false, error);
            var profile = document.Profiles.FirstOrDefault(item => item.Id == profileId);
            if (profile is null) return new(false, "该插件方案已不存在，请刷新后重试。");

            var current = _localMods.Scan();
            var desiredStates = profile.Plugins
                .Where(item => IsSafeRelativePath(item.RelativePath))
                .GroupBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Enabled, StringComparer.OrdinalIgnoreCase);
            var currentPaths = current.Select(item => item.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = desiredStates.Keys.Count(path => !currentPaths.Contains(path));

            // 方案是精确启停快照：在当前游戏目录中新出现、但没有被方案记录的插件默认停用，
            // 文件仍保留在 plugins 目录中，用户随时可以在本地插件页重新启用或保存回方案。
            var batch = _localMods.ApplyStates(current.ToDictionary(
                item => item.RelativePath,
                item => desiredStates.TryGetValue(item.RelativePath, out var enabled) && enabled,
                StringComparer.OrdinalIgnoreCase));
            if (!batch.Success) return new(false, batch.Message);

            document.ActiveProfileId = profile.Id;
            profile.UpdatedAt = DateTimeOffset.Now;
            Save(storagePath, document);
            var suffix = missing > 0 ? $" 另有 {missing} 个方案记录的插件当前未安装，已跳过。" : "";
            return new(true, $"已应用方案“{profile.Name}”：{batch.Message}{suffix}", profile);
        }
    }

    public PluginProfileOperationResult Delete(string profileId)
    {
        lock (_sync)
        {
            if (!TryGetStoragePath(out var storagePath, out var error)) return new(false, error);
            if (!TryLoad(storagePath, out var document, out error)) return new(false, error);
            var profile = document.Profiles.FirstOrDefault(item => item.Id == profileId);
            if (profile is null) return new(false, "该插件方案已不存在。");

            document.Profiles.Remove(profile);
            if (document.ActiveProfileId == profile.Id) document.ActiveProfileId = null;
            Save(storagePath, document);
            return new(true, $"已删除方案“{profile.Name}”。插件文件和当前启停状态未被修改。");
        }
    }

    private static readonly HashSet<string> BlockedPackageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".reg", ".com", ".scr", ".msi", ".jar", ".sh", ".pif"
    };

    public PluginProfileOperationResult ExportPackage(string profileId, string outputFilePath)
    {
        lock (_sync)
        {
            var gamePath = _gamePathProvider();
            if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
                return new(false, "Unturned 游戏目录无效，无法导出方案包。");

            if (!TryGetStoragePath(out var storagePath, out var error)) return new(false, error);
            if (!TryLoad(storagePath, out var document, out error)) return new(false, error);
            var profile = document.Profiles.FirstOrDefault(item => item.Id == profileId);
            if (profile is null) return new(false, "指定的插件方案不存在。");

            var pluginsRoot = Path.Combine(gamePath, "BepInEx", "plugins");
            var configRoot = Path.Combine(gamePath, "BepInEx", "config");

            var manifest = new UmmpkManifest
            {
                Name = profile.Name,
                Description = profile.Description,
                Author = profile.Author,
                Version = profile.Version,
                ExportedAt = DateTimeOffset.Now
            };

            var destinationDir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrWhiteSpace(destinationDir)) Directory.CreateDirectory(destinationDir);

            var tempPackage = outputFilePath + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                using (var zip = ZipFile.Open(tempPackage, ZipArchiveMode.Create))
                {
                    foreach (var plugin in profile.Plugins)
                    {
                        if (!IsSafeRelativePath(plugin.RelativePath)) continue;

                        var enabledFile = Path.Combine(pluginsRoot, plugin.RelativePath);
                        var disabledFile = enabledFile + ".disabled";
                        var actualFile = File.Exists(enabledFile) ? enabledFile : (File.Exists(disabledFile) ? disabledFile : null);

                        string sha256 = "";
                        if (actualFile is not null)
                        {
                            var entry = zip.CreateEntry($"BepInEx/plugins/{plugin.RelativePath.Replace('\\', '/')}", CompressionLevel.Optimal);
                            using (var sourceStream = File.OpenRead(actualFile))
                            using (var entryStream = entry.Open())
                            {
                                sourceStream.CopyTo(entryStream);
                            }
                            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(actualFile))).ToLowerInvariant();
                        }

                        manifest.Plugins.Add(new UmmpkPluginEntry
                        {
                            RelativePath = plugin.RelativePath,
                            Enabled = plugin.Enabled,
                            Sha256 = sha256
                        });

                        // 导出对应的前缀/同名配置文件（若存在）
                        if (Directory.Exists(configRoot))
                        {
                            var baseName = Path.GetFileNameWithoutExtension(plugin.RelativePath);
                            foreach (var cfgFile in Directory.EnumerateFiles(configRoot, $"{baseName}.*", SearchOption.AllDirectories))
                            {
                                var relCfg = Path.GetRelativePath(configRoot, cfgFile).Replace('\\', '/');
                                if (!BlockedPackageExtensions.Contains(Path.GetExtension(relCfg)))
                                {
                                    var cfgEntry = zip.CreateEntry($"BepInEx/config/{relCfg}", CompressionLevel.Optimal);
                                    using var cfgSource = File.OpenRead(cfgFile);
                                    using var cfgDest = cfgEntry.Open();
                                    cfgSource.CopyTo(cfgDest);
                                }
                            }
                        }
                    }

                    var manifestEntry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
                    using var manifestStream = new StreamWriter(manifestEntry.Open(), Encoding.UTF8);
                    manifestStream.Write(JsonSerializer.Serialize(manifest, JsonOptions));
                }

                File.Move(tempPackage, outputFilePath, overwrite: true);
                return new(true, $"已导出方案包“{profile.Name}”至：{outputFilePath}", profile);
            }
            catch (Exception ex)
            {
                if (File.Exists(tempPackage)) File.Delete(tempPackage);
                return new(false, $"导出方案包失败：{ex.Message}");
            }
        }
    }

    public PluginProfileOperationResult ImportPackage(string packagePath)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                return new(false, "方案包文件不存在。");

            var gamePath = _gamePathProvider();
            if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
                return new(false, "Unturned 游戏目录无效，无法导入方案包。");

            try
            {
                using var zip = ZipFile.OpenRead(packagePath);
                var manifestEntry = zip.GetEntry("manifest.json");
                if (manifestEntry is null)
                    return new(false, "无效的模组包：未找到 manifest.json 清单。");

                UmmpkManifest manifest;
                using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
                {
                    manifest = JsonSerializer.Deserialize<UmmpkManifest>(reader.ReadToEnd())
                        ?? throw new InvalidDataException("无法解析 manifest.json。");
                }

                var validation = ValidateName(manifest.Name);
                if (validation is not null) return new(false, validation);

                if (zip.Entries.Count > 4096)
                    return new(false, "模组包条目过多，已中止导入。");

                long totalBytes = 0;
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    var normalized = entry.FullName.Replace('\\', '/').TrimStart('/');
                    if (normalized.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var ext = Path.GetExtension(normalized);
                    if (BlockedPackageExtensions.Contains(ext))
                        return new(false, $"模组包包含危险的可执行文件或脚本载荷（{ext}），已阻止导入。");

                    if (!normalized.StartsWith("BepInEx/plugins/", StringComparison.OrdinalIgnoreCase)
                        && !normalized.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase))
                    {
                        return new(false, $"模组包包含白名单目录以外的文件（{normalized}），已阻止导入。");
                    }

                    if (normalized.Contains(".."))
                        return new(false, "模组包包含非法相对路径跳跃。");

                    totalBytes += entry.Length;
                    if (totalBytes > 2L * 1024 * 1024 * 1024)
                        return new(false, "模组包解压后体积超出安全上限（2GB）。");
                }

                var pluginsRoot = Path.Combine(gamePath, "BepInEx", "plugins");
                var configRoot = Path.Combine(gamePath, "BepInEx", "config");
                Directory.CreateDirectory(pluginsRoot);
                Directory.CreateDirectory(configRoot);

                var pluginStates = manifest.Plugins.ToDictionary(
                    p => p.RelativePath.Replace('\\', '/'),
                    p => p.Enabled,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    var normalized = entry.FullName.Replace('\\', '/').TrimStart('/');
                    if (normalized.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (normalized.StartsWith("BepInEx/plugins/", StringComparison.OrdinalIgnoreCase))
                    {
                        var relPath = normalized["BepInEx/plugins/".Length..];
                        var enabled = !pluginStates.TryGetValue(relPath, out var state) || state;
                        var targetFileName = enabled ? relPath : (relPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ? relPath : relPath + ".disabled");
                        var destination = Path.Combine(pluginsRoot, targetFileName);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                        using var entryStream = entry.Open();
                        using var fileStream = File.Create(destination);
                        entryStream.CopyTo(fileStream);
                    }
                    else if (normalized.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase))
                    {
                        var relPath = normalized["BepInEx/config/".Length..];
                        var destination = Path.Combine(configRoot, relPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                        using var entryStream = entry.Open();
                        using var fileStream = File.Create(destination);
                        entryStream.CopyTo(fileStream);
                    }
                }

                if (!TryGetStoragePath(out var storagePath, out var error)) return new(false, error);
                if (!TryLoad(storagePath, out var document, out error)) return new(false, error);

                var existing = document.Profiles.FirstOrDefault(p => p.Name.Equals(manifest.Name, StringComparison.OrdinalIgnoreCase));
                var profile = existing ?? new PluginProfile();
                profile.Name = manifest.Name;
                profile.Description = manifest.Description;
                profile.Author = manifest.Author;
                profile.Version = manifest.Version;
                profile.UpdatedAt = DateTimeOffset.Now;
                profile.Plugins = manifest.Plugins.Select(p => new PluginProfileEntry
                {
                    RelativePath = p.RelativePath,
                    Enabled = p.Enabled
                }).ToList();

                if (existing is null) document.Profiles.Add(profile);
                document.ActiveProfileId = profile.Id;
                Save(storagePath, document);

                return new(true, $"已成功导入并应用模组方案“{profile.Name}”。", profile);
            }
            catch (Exception ex)
            {
                return new(false, $"导入模组包失败：{ex.Message}");
            }
        }
    }

    private static bool TryLoad(string storagePath, out PluginProfileDocument document, out string error)
    {
        document = new PluginProfileDocument();
        error = "";
        try
        {
            document = File.Exists(storagePath)
                ? JsonSerializer.Deserialize<PluginProfileDocument>(File.ReadAllText(storagePath)) ?? new PluginProfileDocument()
                : new PluginProfileDocument();
            return true;
        }
        catch (Exception ex)
        {
            error = $"无法读取插件方案数据，已保留原文件：{ex.Message}";
            return false;
        }
    }

    private static PluginProfile CreateSnapshot(string name, IReadOnlyCollection<ModItem> currentPlugins) => new()
    {
        Name = name,
        Plugins = SnapshotEntries(currentPlugins)
    };

    private static List<PluginProfileEntry> SnapshotEntries(IEnumerable<ModItem> currentPlugins) => currentPlugins
        .Where(item => IsSafeRelativePath(item.RelativePath))
        .GroupBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
        .Select(group => new PluginProfileEntry
        {
            RelativePath = group.Key,
            Enabled = group.Last().IsEnabled
        })
        .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static bool IsSafeRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return false;
        var segments = path.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && segments.All(segment => segment != "." && segment != "..");
    }

    private bool TryGetStoragePath(out string storagePath, out string error)
    {
        storagePath = "";
        error = "";
        var gamePath = _gamePathProvider();
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            error = "请先在设置中选择 Unturned 游戏目录。";
            return false;
        }

        try
        {
            var normalized = Path.GetFullPath(gamePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant()[..16];
            storagePath = Path.Combine(_dataRoot, "plugin-profiles", hash, "profiles.json");
            return true;
        }
        catch
        {
            error = "无法读取当前游戏目录，请在设置中重新选择。";
            return false;
        }
    }

    private static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "请先输入插件方案名称。";
        if (name.Trim().Length > 48) return "插件方案名称不能超过 48 个字符。";
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Any(char.IsControl))
            return "插件方案名称包含 Windows 不支持的字符。";
        return null;
    }

    private static void Save(string storagePath, PluginProfileDocument document)
    {
        var directory = Path.GetDirectoryName(storagePath)!;
        Directory.CreateDirectory(directory);
        var temporary = storagePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(document, JsonOptions));
        File.Move(temporary, storagePath, overwrite: true);
    }

    private sealed class PluginProfileDocument
    {
        public string? ActiveProfileId { get; set; }
        public List<PluginProfile> Profiles { get; set; } = [];
    }
}
