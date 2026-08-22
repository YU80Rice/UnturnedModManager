namespace UnturnedModManager.Models;

/// <summary>
/// 一个插件方案是当前游戏目录下所有本地插件的启停快照。
/// 它不复制 DLL，也不使用虚拟盘；切换时仅在 .dll 与 .dll.disabled 之间安全地改名。
/// </summary>
public sealed class PluginProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public List<PluginProfileEntry> Plugins { get; set; } = [];

    public int EnabledPluginCount => Plugins.Count(item => item.Enabled);
    public string Summary => $"{EnabledPluginCount} 个启用 · {Plugins.Count} 个已记录";
}

public sealed class PluginProfileEntry
{
    /// <summary>相对于 BepInEx/plugins 的 DLL 路径，始终不带 .disabled 后缀。</summary>
    public string RelativePath { get; set; } = "";
    public bool Enabled { get; set; }
}

public sealed record PluginProfileOperationResult(bool Success, string Message, PluginProfile? Profile = null);

/// <summary>
/// .ummpk 模组包清单规范。
/// </summary>
public sealed class UmmpkManifest
{
    public string FormatVersion { get; set; } = "1.0.0";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.Now;
    public List<UmmpkPluginEntry> Plugins { get; set; } = [];
}

public sealed class UmmpkPluginEntry
{
    public string RelativePath { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string Sha256 { get; set; } = "";
}
