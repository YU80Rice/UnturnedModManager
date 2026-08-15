using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
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
