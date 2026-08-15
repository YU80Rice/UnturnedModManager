using System.Formats.Tar;
using System.IO;
using System.IO.Compression;

namespace UnturnedModManager.Services;

public sealed class DxvkService
{
    private const string D3d11 = "d3d11.dll";
    private const string Dxgi = "dxgi.dll";
    private readonly HttpDownloadService _downloads;
    private static readonly DownloadSource[] Sources =
    [
        new("国内镜像（gh-proxy.com）", "https://gh-proxy.com/https://github.com/doitsujin/dxvk/releases/download/v2.4/dxvk-2.4.tar.gz", TimeSpan.FromSeconds(15)),
        new("国内镜像（ghproxy.net）", "https://ghproxy.net/https://github.com/doitsujin/dxvk/releases/download/v2.4/dxvk-2.4.tar.gz", TimeSpan.FromSeconds(15)),
        new("官方 GitHub", "https://github.com/doitsujin/dxvk/releases/download/v2.4/dxvk-2.4.tar.gz", TimeSpan.FromMinutes(10))
    ];

    public DxvkService(HttpDownloadService downloads) => _downloads = downloads;

    public bool IsEnabled(string? gamePath) =>
        !string.IsNullOrWhiteSpace(gamePath)
        && File.Exists(Path.Combine(gamePath, D3d11))
        && File.Exists(Path.Combine(gamePath, Dxgi));

    public async Task<LocalModOperationResult> SetEnabledAsync(
        string? gamePath,
        bool enabled,
        IProgress<OperationProgress>? progress = null,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(gamePath)
            || !File.Exists(Path.Combine(gamePath, "Unturned.exe")))
            return new(false, "请先在设置中配置有效的 Unturned 安装路径。");
        try
        {
            var d3d11 = Path.Combine(gamePath, D3d11);
            var dxgi = Path.Combine(gamePath, Dxgi);
            var d3d11Disabled = d3d11 + ".disabled";
            var dxgiDisabled = dxgi + ".disabled";
            if (enabled)
            {
                if (File.Exists(d3d11) && File.Exists(dxgi))
                {
                    AppSettings.EnableDxvk = true;
                    return new(true, "DXVK 已启用。");
                }
                if (File.Exists(d3d11Disabled) && File.Exists(dxgiDisabled))
                {
                    File.Move(d3d11Disabled, d3d11, overwrite: true);
                    File.Move(dxgiDisabled, dxgi, overwrite: true);
                    AppSettings.EnableDxvk = true;
                    return new(true, "DXVK 已从本地备份恢复。");
                }
                await DeployAsync(gamePath, progress, token);
                AppSettings.EnableDxvk = true;
                return new(true, "DXVK 已启用（DX11 → Vulkan）；建议在相同场景与原生 D3D11 对比帧率和稳定性。");
            }

            MoveToBackup(d3d11);
            MoveToBackup(dxgi);
            AppSettings.EnableDxvk = false;
            return new(true, "DXVK 已关闭，游戏将使用原生 D3D11。");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) { return new(false, $"DXVK 切换失败：{ex.Message}"); }
    }

    public void EnsureConfiguration(string gamePath)
    {
        var processorCount = Environment.ProcessorCount;
        var compilerThreads = Math.Max(2, processorCount - 1);
        var content = $"# 由 Unturned Mod Manager 自动生成\n"
            + $"dxvk.numCompilerThreads = {compilerThreads}\n"
            + "dxvk.enableGraphicsPipelineLibrary = True\n"
            + "dxgi.deferSurfaceCreation = True\n"
            + "dxvk.allowFse = False\n"
            + "dxvk.allowDialogMode = True\n";
        File.WriteAllText(Path.Combine(gamePath, "dxvk.conf"), content);
    }

    private async Task DeployAsync(
        string gamePath,
        IProgress<OperationProgress>? progress,
        CancellationToken token)
    {
        var temporary = Path.Combine(Path.GetTempPath(), $"umm-dxvk-{Guid.NewGuid():N}.tar.gz");
        try
        {
            await _downloads.DownloadAsync(Sources, temporary, progress, token);
            progress?.Report(new OperationProgress(100, "正在提取 DXVK x64 运行库…"));
            var extracted = 0;
            await using var file = new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new TarReader(gzip);
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) is not null)
            {
                token.ThrowIfCancellationRequested();
                if (entry.EntryType != TarEntryType.RegularFile || entry.DataStream is null) continue;
                var name = entry.Name.Replace('\\', '/');
                string? destination = name.EndsWith("/x64/d3d11.dll", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(gamePath, D3d11)
                    : name.EndsWith("/x64/dxgi.dll", StringComparison.OrdinalIgnoreCase)
                        ? Path.Combine(gamePath, Dxgi)
                        : null;
                if (destination is null) continue;
                await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                await entry.DataStream.CopyToAsync(output, token);
                extracted++;
            }
            if (extracted != 2 || !IsEnabled(gamePath))
                throw new InvalidDataException("DXVK 压缩包中缺少 x64/d3d11.dll 或 x64/dxgi.dll。");
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    private static void MoveToBackup(string active)
    {
        if (!File.Exists(active)) return;
        var backup = active + ".disabled";
        if (File.Exists(backup)) File.Delete(backup);
        File.Move(active, backup);
    }
}
