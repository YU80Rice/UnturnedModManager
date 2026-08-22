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
    private readonly string _launcherDirectory;

    public DiagnosticService(string? userProfileDirectory = null, string? launcherDirectory = null)
    {
        _userProfileDirectory = userProfileDirectory
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _launcherDirectory = launcherDirectory ?? AppContext.BaseDirectory;
    }

    public DiagnosticAnalysis Analyze(string? gamePath)
    {
        var sources = GetLogSources(gamePath).Where(File.Exists).ToList();
        var sessionDetail = BuildSessionDetail();
        if (sources.Count == 0)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Information,
                "没有找到可分析的运行日志",
                "请至少启动一次游戏；UMM 会分析 Unturned 的 Client 日志、Unity Player 日志、BepInEx 日志与 Doorstop 引导日志。",
                [],
                [])
            {
                Category = DiagnosticCategory.Normal,
                Recommendation = "暂未发现日志，启动游戏复现一次后即可获取排查建议。"
            }, sessionDetail);
        }

        var doorstopEvidence = new List<string>();
        var battlEyeEvidence = new List<string>();
        var missingDepEvidence = new List<string>();
        var fatalEvidence = new List<string>();
        var dxvkEvidence = new List<string>();
        var pluginEvidence = new List<string>();

        foreach (var source in sources)
        {
            var content = ReadTail(source);
            CollectEvidence(content, source, DoorstopPattern, doorstopEvidence);
            CollectEvidence(content, source, BattlEyePattern, battlEyeEvidence);
            CollectEvidence(content, source, MissingDepPattern, missingDepEvidence);
            CollectEvidence(content, source, FatalPattern, fatalEvidence);
            CollectEvidence(content, source, DxvkPattern, dxvkEvidence);
            CollectEvidence(content, source, PluginPattern, pluginEvidence);
        }

        if (doorstopEvidence.Count > 0)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Error,
                "发现 Doorstop / Mono 注入失败线索",
                "Doorstop 引导器未能成功载入 Mono 运行时或 winhttp.dll 注入异常。",
                doorstopEvidence.Take(3).ToList(),
                sources)
            {
                Category = DiagnosticCategory.DoorstopFailure,
                Recommendation = "建议在首页点击“修复环境”，重新部署完整的 BepInEx 5.4.23.5 Mono 环境，并检查安全软件是否拦截了 winhttp.dll。"
            }, sessionDetail);
        }

        if (battlEyeEvidence.Count > 0)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Error,
                "发现 BattlEye 反作弊拦截与冲突线索",
                "日志中记录到 BattlEye 阻止了 BepInEx 注入或检测到受管程序冲突。",
                battlEyeEvidence.Take(3).ToList(),
                sources)
            {
                Category = DiagnosticCategory.BattlEyeConflict,
                Recommendation = "请确保在启动器首页使用“模组模式”（自动附加 -NoBattlEye 参数启动），请勿在官方 BattlEye 安全服务器上加载 BepInEx 插件。"
            }, sessionDetail);
        }

        if (missingDepEvidence.Count > 0)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Warning,
                "发现插件前置依赖缺失线索",
                "日志中出现 TypeLoadException、MissingMethodException 或找不到依赖程序集错误。",
                missingDepEvidence.Take(3).ToList(),
                sources)
            {
                Category = DiagnosticCategory.MissingDependency,
                Recommendation = "请检查最近安装的 Mod 是否缺少核心前置库（如 Rocket / OpenMod 或特定前置 DLL），或尝试更新该插件至最新版本。"
            }, sessionDetail);
        }

        if (fatalEvidence.Count > 0)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Error,
                "发现 Unity 或进程异常退出线索",
                "日志中出现致命错误、访问冲突或未处理异常。请先导出诊断包；若近期启用了 DXVK 或新插件，可分别关闭后复现进行对比。",
                fatalEvidence.Take(3).ToList(),
                sources)
            {
                Category = DiagnosticCategory.UnityCrash,
                Recommendation = "Unity 底层发生崩溃（如内存访问冲突）。建议使用二分法停用最近新增的插件排查冲突源，或导出脱敏诊断包联系作者。"
            }, sessionDetail);
        }

        if (dxvkEvidence.Count > 0)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Warning,
                "发现 DXVK / Vulkan 初始化异常线索",
                "这通常与显卡驱动、Vulkan 运行环境或 DXVK 配置有关。建议关闭 DXVK，以原生 D3D11 对比一次。",
                dxvkEvidence.Take(3).ToList(),
                sources)
            {
                Category = DiagnosticCategory.DxvkFailure,
                Recommendation = "建议在设置中关闭 DXVK，以原生 D3D11 启动对比；若需使用 DXVK，请更新显卡驱动并确保支持 Vulkan 1.3。"
            }, sessionDetail);
        }

        if (pluginEvidence.Count > 0)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Warning,
                "发现 BepInEx / 插件异常线索",
                "日志中包含异常调用。它不一定导致游戏闪退，但可以作为排查插件版本、依赖或配置的起点。",
                pluginEvidence.Take(3).ToList(),
                sources)
            {
                Category = DiagnosticCategory.PluginException,
                Recommendation = "可根据日志线索检查对应插件的配置文件是否损坏，或向该插件作者反馈报错堆栈。"
            }, sessionDetail);
        }

        if (AppSettings.LastSessionCrashed)
        {
            return WithSession(new DiagnosticAnalysis(
                DiagnosticSeverity.Warning,
                "上次受管游戏会话未正常退出",
                "启动器记录到非零退出码，但日志中尚未匹配到具体的致命线索。请导出诊断包，或分别关闭 DXVK、最近新增的插件后复现对比。",
                [],
                sources)
            {
                Category = DiagnosticCategory.UncleanExit,
                Recommendation = "游戏未正常退出。可导出脱敏诊断包，或分别尝试关闭 DXVK 及最近新增的插件进行排除。"
            }, sessionDetail);
        }

        return WithSession(new DiagnosticAnalysis(
            DiagnosticSeverity.Information,
            "未发现明确的致命日志特征",
            "最近可用日志中没有匹配到 Unity 崩溃、DXVK 初始化失败或 BepInEx 异常的典型模式。该结果不排除驱动、内存或进程外部终止问题。",
            [],
            sources)
        {
            Category = DiagnosticCategory.Normal,
            Recommendation = "当前运行环境状态良好，未检测到明显异常。"
        }, sessionDetail);
    }

    public string ExportLogs(string? gamePath, DiagnosticAnalysis? analysis = null)
    {
        var exportRoot = Path.GetFullPath(_launcherDirectory);
        var folder = Path.Combine(exportRoot, $"UMM-诊断包_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(folder);
        foreach (var source in GetLogSources(gamePath).Where(File.Exists))
            CopyIfExists(source, Path.Combine(folder, Path.GetFileName(source)));
        var report = (analysis ?? Analyze(gamePath)).ToReportText();
        var sanitizedReport = SanitizeText(report, _userProfileDirectory);
        File.WriteAllText(Path.Combine(folder, "UMM-诊断摘要.txt"), sanitizedReport, Encoding.UTF8);
        return folder;
    }

    public static string SanitizeText(string text, string? userProfileDirectory = null)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sanitized = text;

        if (!string.IsNullOrWhiteSpace(userProfileDirectory))
        {
            var userDir = userProfileDirectory.TrimEnd('\\', '/');
            sanitized = sanitized.Replace(userDir, @"C:\Users\<USER>", StringComparison.OrdinalIgnoreCase);
        }

        sanitized = Regex.Replace(sanitized, @"token=[a-zA-Z0-9_\-\.]+", "token=<REDACTED>", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"(Bearer\s+)[a-zA-Z0-9_\-\.]+", "$1<REDACTED>", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"([A-Za-z]:\\Users\\)[^\\]+", "$1<USER>", RegexOptions.IgnoreCase);

        return sanitized;
    }

    public void OpenExportFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            throw new DirectoryNotFoundException($"诊断包目录不存在：{folder}");

        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }

    private IReadOnlyList<string> GetLogSources(string? gamePath)
    {
        var sources = new List<string>();
        if (!string.IsNullOrWhiteSpace(gamePath))
        {
            sources.Add(Path.Combine(gamePath, "doorstop.log"));
            sources.Add(Path.Combine(gamePath, "doorstop_prev.log"));
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

    private static readonly Regex DoorstopPattern = new(
        @"\[doorstop.*error\]|failed to load mono|doorstop.*(fail|cannot find)|mono-2\.0.*error",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BattlEyePattern = new(
        @"battleye.*(blocked|violation|refused|corrupted)|beservice.*(violation|integrity|block)|blocked loading of.*(winhttp|doorstop)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MissingDepPattern = new(
        @"typeloadexception|missingmethodexception|could not load.*(type|assembly)|filenotfoundexception.*assembly",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FatalPattern = new(
        @"fatal error|crash!!!|access violation|unhandled exception|segmentation fault|player crash|crash detected",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PluginPattern = new(
        @"bepinex.*(exception|error)|exception.*(plugin|bepinex)|dllnotfoundexception",
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
