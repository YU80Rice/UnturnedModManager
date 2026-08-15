using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnturnedModManager.Models;

namespace UnturnedModManager.Services;

public sealed class DiagnosticService
{
    private const int MaximumBytesPerLog = 1_500_000;
    private readonly string _userProfileDirectory;

    public DiagnosticService(string? userProfileDirectory = null) =>
        _userProfileDirectory = userProfileDirectory
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public DiagnosticAnalysis Analyze(string? gamePath)
    {
        var sources = GetLogSources(gamePath).Where(File.Exists).ToList();
        var sessionDetail = BuildSessionDetail();
        if (sources.Count == 0)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Information,
                "没有找到可分析的运行日志",
                "请至少启动一次游戏；UMM 会分析 Unturned 的 Client 日志、Unity Player 日志和 BepInEx 日志。",
                [],
                []), sessionDetail);
        }

        var fatalEvidence = new List<string>();
        var pluginEvidence = new List<string>();
        var dxvkEvidence = new List<string>();
        foreach (var source in sources)
        {
            var content = ReadTail(source);
            CollectEvidence(content, source, FatalPattern, fatalEvidence);
            CollectEvidence(content, source, PluginPattern, pluginEvidence);
            CollectEvidence(content, source, DxvkPattern, dxvkEvidence);
        }

        if (fatalEvidence.Count > 0)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Error,
                "发现 Unity 或进程异常退出线索",
                "日志中出现致命错误、访问冲突或未处理异常。请先导出诊断包；若近期启用了 DXVK 或新插件，可分别关闭后复现进行对比。",
                fatalEvidence.Take(3).ToList(),
                sources), sessionDetail);
        }

        if (dxvkEvidence.Count > 0)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Warning,
                "发现 DXVK / Vulkan 初始化异常线索",
                "这通常与显卡驱动、Vulkan 运行环境或 DXVK 配置有关。建议关闭 DXVK，以原生 D3D11 对比一次。",
                dxvkEvidence.Take(3).ToList(),
                sources), sessionDetail);
        }

        if (pluginEvidence.Count > 0)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Warning,
                "发现 BepInEx / 插件异常线索",
                "日志中包含异常调用。它不一定导致游戏闪退，但可以作为排查插件版本、依赖或配置的起点。",
                pluginEvidence.Take(3).ToList(),
                sources), sessionDetail);
        }

        if (AppSettings.LastSessionCrashed)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Warning,
                "上次受管游戏会话未正常退出",
                "启动器记录到非零退出码，但日志中尚未匹配到具体的致命线索。请导出诊断包，或分别关闭 DXVK、最近新增的插件后复现对比。",
                [],
                sources), sessionDetail);
        }

        return WithSession(new DiagnosticAnalysis(
            DiagnosticSeverity.Information,
            "未发现明确的致命日志特征",
            "最近可用日志中没有匹配到 Unity 崩溃、DXVK 初始化失败或 BepInEx 异常的典型模式。该结果不排除驱动、内存或进程外部终止问题。",
            [],
            sources), sessionDetail);
    }

    public string ExportLogs(string? gamePath, DiagnosticAnalysis? analysis = null)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var folder = Path.Combine(desktop, $"Unturned_模组崩溃诊断_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(folder);
        foreach (var source in GetLogSources(gamePath).Where(File.Exists))
            CopyIfExists(source, Path.Combine(folder, Path.GetFileName(source)));
        File.WriteAllText(Path.Combine(folder, "UMM-诊断摘要.txt"), (analysis ?? Analyze(gamePath)).ToReportText(), Encoding.UTF8);
        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        return folder;
    }

    private IReadOnlyList<string> GetLogSources(string? gamePath)
    {
        var sources = new List<string>();
        if (!string.IsNullOrWhiteSpace(gamePath))
        {
            sources.Add(Path.Combine(gamePath, "Logs", "Client_Prev.log"));
            sources.Add(Path.Combine(gamePath, "Logs", "Client.log"));
            sources.Add(Path.Combine(gamePath, "BepInEx", "LogOutput.log"));
            // DXVK 在游戏根目录写入逐 DLL 日志；它们对 Vulkan 初始化失败最有价值。
            sources.Add(Path.Combine(gamePath, "Unturned_d3d11.log"));
            sources.Add(Path.Combine(gamePath, "Unturned_dxgi.log"));
            sources.Add(Path.Combine(gamePath, "d3d11.log"));
            sources.Add(Path.Combine(gamePath, "dxgi.log"));
        }

        var localLow = Path.Combine(
            _userProfileDirectory,
            "AppData", "LocalLow", "SmartlyDressedGames", "Unturned");
        sources.Add(Path.Combine(localLow, "Player-prev.log"));
        sources.Add(Path.Combine(localLow, "Player.log"));
        return sources.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string ReadTail(string source)
    {
        try
        {
            using var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length > MaximumBytesPerLog)
                stream.Seek(-MaximumBytesPerLog, SeekOrigin.End);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch { return ""; }
    }

    private static void CollectEvidence(string content, string source, Regex pattern, ICollection<string> evidence)
    {
        if (string.IsNullOrWhiteSpace(content) || evidence.Count >= 3) return;
        foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!pattern.IsMatch(line)) continue;
            var normalized = Regex.Replace(line.Trim(), @"\s+", " ");
            if (normalized.Length > 220) normalized = normalized[..220] + "…";
            evidence.Add($"{Path.GetFileName(source)}：{normalized}");
            if (evidence.Count >= 3) return;
        }
    }

    private static readonly Regex FatalPattern = new(
        @"fatal error|crash!!!|access violation|unhandled exception|segmentation fault|player crash|crash detected",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PluginPattern = new(
        @"bepinex.*(exception|error)|exception.*(plugin|bepinex)|could not load.*(plugin|assembly)|missingmethodexception|typeloadexception|dllnotfoundexception",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DxvkPattern = new(
        @"dxvk.*(err|fail|fatal)|vulkan.*(fail|error)|failed.*(vulkan|d3d11|dxgi)|d3d11createdevice.*fail",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static DiagnosticAnalysis WithSession(DiagnosticAnalysis analysis, string sessionDetail) =>
        analysis with { SessionDetail = sessionDetail };

    private static string BuildSessionDetail()
    {
        var endedUtc = AppSettings.LastSessionEndedUtc;
        if (endedUtc is null && AppSettings.LastSessionExitCode is null)
            return "";

        var environment = AppSettings.LastSessionUsedMods ? "模组环境" : "纯净环境";
        if (AppSettings.LastSessionUsedDxvk) environment += "，DXVK 已启用";
        var ended = endedUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "结束时间未知";
        var exit = AppSettings.LastSessionExitCode is { } code ? $"退出码 {code}" : "未记录退出码";
        return $"{ended} · {environment} · {exit}";
    }

    private static void CopyIfExists(string source, string destination)
    {
        if (File.Exists(source)) File.Copy(source, destination, overwrite: true);
    }
}
