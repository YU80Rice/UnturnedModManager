using System.IO;
using System.IO.Compression;

namespace UnturnedModManager.Services;

public sealed record BepInExStatus(bool HasValidGamePath, bool IsInstalled, string Title, string Detail);

public sealed class BepInExService
{
    private const string WinHttpDll = "winhttp.dll";
    private const string WinHttpDisabled = "winhttp.dll.disabled";
    private const string CoreRelativePath = @"BepInEx\core\BepInEx.dll";
    private readonly HttpDownloadService _downloads;
    private static readonly DownloadSource[] Sources =
    [
        new("国内镜像", "https://mirror.ghproxy.com/https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip", TimeSpan.FromSeconds(8)),
        new("官方 GitHub", "https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip", TimeSpan.FromMinutes(10))
    ];

    public BepInExService(HttpDownloadService downloads) => _downloads = downloads;

    public BepInExStatus GetStatus(string? gamePath)
    {
        var valid = IsGamePathValid(gamePath);
        var installed = valid && File.Exists(Path.Combine(gamePath!, CoreRelativePath));
        return installed
            ? new(true, true, "BepInEx 已就绪", $"检测到加载器：{CoreRelativePath}")
            : new(valid, false, "BepInEx 未安装", valid
                ? "游戏路径有效，但未找到 BepInEx 加载器。"
                : "未配置有效的 Unturned 安装路径，无法检测模组环境。");
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

    private static bool IsGamePathValid(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Directory.Exists(path)
        && File.Exists(Path.Combine(path, "Unturned.exe"));
}
