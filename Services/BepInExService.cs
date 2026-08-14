using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace UnturnedModManager.Services;

public sealed record BepInExStatus(
    bool HasValidGamePath,
    bool IsInstalled,
    bool IsCurrentVersion,
    string? InstalledVersion,
    string Title,
    string Detail);

public sealed class BepInExService
{
    public const string SupportedVersion = "5.4.23.5";
    private const string WinHttpDll = "winhttp.dll";
    private const string WinHttpDisabled = "winhttp.dll.disabled";
    private const string CoreRelativePath = @"BepInEx\core\BepInEx.dll";
    private const int CommunityPackageId = 4;
    private const string ExpectedPackageSha256 = "82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4";
    private const string ExpectedChangelogSha256 = "B184D858CB0FF6614CA8CE0247FB0D186CABC91ED7E4D4D9D57EA3D744CE98B5";
    private readonly HttpDownloadService _downloads;
    private static readonly DownloadSource[] Sources =
    [
        new("unmod.online 社区源", $"https://unmod.online/api/mods/{CommunityPackageId}/file", TimeSpan.FromSeconds(15), RequiresCommunityAuth: true),
        new("国内镜像（gh-proxy.com）", "https://gh-proxy.com/https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip", TimeSpan.FromSeconds(15)),
        new("国内镜像（ghproxy.net）", "https://ghproxy.net/https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip", TimeSpan.FromSeconds(15)),
        new("官方 GitHub", "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip", TimeSpan.FromMinutes(10))
    ];

    public BepInExService(HttpDownloadService downloads) => _downloads = downloads;

    public BepInExStatus GetStatus(string? gamePath)
    {
        var valid = IsGamePathValid(gamePath);
        if (!valid)
            return new(false, false, false, null, "BepInEx 未安装", "未配置有效的 Unturned 安装路径，无法检测插件环境。");

        var corePath = Path.Combine(gamePath!, CoreRelativePath);
        if (!File.Exists(corePath))
            return new(true, false, false, null, "BepInEx 未安装", "游戏路径有效，但未找到 BepInEx 加载器。");

        var version = ReadInstalledVersion(corePath);
        var current = string.Equals(version, SupportedVersion, StringComparison.OrdinalIgnoreCase);
        return current
            ? new(true, true, true, version, $"BepInEx {SupportedVersion} 已就绪", "win_x64 · Unity Mono · winhttp doorstop")
            : new(true, true, false, version, $"BepInEx {version ?? "未知版本"} 已安装",
                $"社区插件基线为 {SupportedVersion}；建议执行升级或修复。");
    }

    public bool IsGlobalModsEnabled(string? gamePath) =>
        IsGamePathValid(gamePath) && File.Exists(Path.Combine(gamePath!, WinHttpDll));

    public LocalModOperationResult SetGlobalModsEnabled(string? gamePath, bool enabled)
    {
        if (!IsGamePathValid(gamePath))
            return new(false, "请先在设置中配置有效的 Unturned 安装路径。");
        var active = Path.Combine(gamePath!, WinHttpDll);
        var disabled = Path.Combine(gamePath!, WinHttpDisabled);
        try
        {
            if (enabled)
            {
                if (File.Exists(active)) return new(true, "全局模组环境已启用。");
                if (!File.Exists(disabled)) return new(false, "未找到 winhttp.dll，请先安装或修复 BepInEx。");
                File.Move(disabled, active);
                return new(true, "已启用全局模组环境。");
            }
            if (!File.Exists(active))
                return File.Exists(disabled)
                    ? new(true, "全局模组环境已禁用。")
                    : new(false, "未找到 winhttp.dll，BepInEx 可能未正确安装。");
            if (File.Exists(disabled)) File.Delete(disabled);
            File.Move(active, disabled);
            return new(true, "已禁用全局模组环境。");
        }
        catch (Exception ex) { return new(false, $"切换模组环境失败：{ex.Message}"); }
    }

    public void EnsureModFileState(string gamePath, bool enabled)
    {
        var active = Path.Combine(gamePath, WinHttpDll);
        var disabled = Path.Combine(gamePath, WinHttpDisabled);
        if (enabled && !File.Exists(active) && File.Exists(disabled)) File.Move(disabled, active);
        if (!enabled && File.Exists(active))
        {
            if (File.Exists(disabled)) File.Delete(disabled);
            File.Move(active, disabled);
        }
    }

    public async Task DeployAsync(
        string gamePath,
        IProgress<OperationProgress>? progress = null,
        CancellationToken token = default)
    {
        if (!IsGamePathValid(gamePath)) throw new InvalidOperationException("Unturned 游戏路径无效。");
        var temporary = Path.Combine(Path.GetTempPath(), $"umm-bepinex-{Guid.NewGuid():N}.zip");
        try
        {
            await _downloads.DownloadAsync(Sources, temporary, progress, token);
            progress?.Report(new OperationProgress(100, "正在验证 BepInEx 官方安装包…"));
            await VerifyPackageAsync(temporary, token);
            progress?.Report(new OperationProgress(100, "正在解压 BepInEx…"));
            ZipFile.ExtractToDirectory(temporary, gamePath, overwriteFiles: true);
            if (!File.Exists(Path.Combine(gamePath, CoreRelativePath)))
                throw new InvalidDataException("下载包已解压，但未找到 BepInEx 核心文件。");
            progress?.Report(new OperationProgress(100, "BepInEx 部署完成"));
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    public LocalModOperationResult Uninstall(string? gamePath)
    {
        if (!IsGamePathValid(gamePath))
            return new(false, "请先在设置中配置有效的 Unturned 安装路径。");

        try
        {
            foreach (var relativePath in new[]
            {
                WinHttpDll,
                WinHttpDisabled,
                "doorstop_config.ini",
                ".doorstop_version"
            })
            {
                var path = Path.Combine(gamePath!, relativePath);
                if (File.Exists(path)) File.Delete(path);
            }

            var corePath = Path.Combine(gamePath!, "BepInEx", "core");
            if (Directory.Exists(corePath)) Directory.Delete(corePath, recursive: true);
            DeleteFileIfHashMatches(Path.Combine(gamePath!, "changelog.txt"), ExpectedChangelogSha256);

            return new(true,
                "BepInEx 核心环境已卸载；plugins、config、cache、日志及社区安装记录均已保留。");
        }
        catch (Exception ex)
        {
            return new(false, $"卸载 BepInEx 环境失败：{ex.Message}");
        }
    }

    private static async Task VerifyPackageAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
        if (!actual.Equals(ExpectedPackageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("BepInEx 安装包校验失败，文件可能损坏或并非官方发布版本。");
    }

    private static void DeleteFileIfHashMatches(string path, string expectedSha256)
    {
        if (!File.Exists(path)) return;
        var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        if (actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase)) File.Delete(path);
    }

    private static string? ReadInstalledVersion(string corePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(corePath);
            var value = info.FileVersion ?? info.ProductVersion;
            var match = Regex.Match(value ?? "", @"\d+\.\d+\.\d+\.\d+");
            return match.Success ? match.Value : null;
        }
        catch { return null; }
    }

    private static bool IsGamePathValid(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Directory.Exists(path)
        && File.Exists(Path.Combine(path, "Unturned.exe"));
}
