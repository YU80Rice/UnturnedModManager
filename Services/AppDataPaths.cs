using System.IO;

namespace UnturnedModManager.Services;

/// <summary>
/// UMM 的用户数据根目录。正常运行使用当前 Windows 用户的 AppData；设置
/// <c>UMM_DATA_DIRECTORY</c> 后可把整个配置、缓存、社区安装记录和插件方案隔离到指定目录，
/// 便于发布包验收、自动化测试和便携式调试。该变量不会改变游戏目录或插件目录。
/// </summary>
public static class AppDataPaths
{
    private const string OverrideVariable = "UMM_DATA_DIRECTORY";

    public static bool IsIsolatedProfile { get; } = !string.IsNullOrWhiteSpace(
        Environment.GetEnvironmentVariable(OverrideVariable));

    public static string RootDirectory { get; } = ResolveRootDirectory();

    public static string CommunityCacheDirectory =>
        Path.Combine(RootDirectory, "cache", "community");

    private static string ResolveRootDirectory()
    {
        var overrideDirectory = Environment.GetEnvironmentVariable(OverrideVariable);
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            try { return Path.GetFullPath(overrideDirectory.Trim()); }
            catch { /* 非法覆盖路径时回退到 Windows 用户目录。 */ }
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UnturnedModManager");
    }
}
