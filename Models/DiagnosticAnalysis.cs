namespace UnturnedModManager.Models;

/// <summary>
/// 对最近本地 Unity / BepInEx 日志的轻量诊断结论。它提供排障线索，不能替代
/// Windows 转储、驱动调试器或插件作者的正式问题定位。
/// </summary>
public sealed record DiagnosticAnalysis(
    DiagnosticSeverity Severity,
    string Title,
    string Summary,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> SourceFiles)
{
    /// <summary>
    /// 诊断分类。
    /// </summary>
    public DiagnosticCategory Category { get; init; } = DiagnosticCategory.Unknown;

    /// <summary>
    /// 针对该诊断特征的人性化中文排障建议。
    /// </summary>
    public string Recommendation { get; init; } = "";

    /// <summary>
    /// 由 UMM 自身记录的最近一次受管游戏会话摘要。它不包含账户、配置或日志全文。
    /// </summary>
    public string SessionDetail { get; init; } = "";

    public static DiagnosticAnalysis Empty { get; } = new(
        DiagnosticSeverity.Information,
        "尚未分析运行日志",
        "点击“分析最近日志”后，UMM 会只读取本机 Unity、BepInEx 与 Unturned 客户端日志。",
        [],
        []);

    public string Detail => Evidence.Count == 0 ? "" : string.Join(Environment.NewLine, Evidence);
    public string ToReportText() => string.Join(Environment.NewLine,
        $"UMM 本地运行诊断 · {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
        $"结论：{Title}",
        $"分类：{Category}",
        $"说明：{Summary}",
        string.IsNullOrWhiteSpace(Recommendation) ? "" : $"排查建议：{Recommendation}",
        string.IsNullOrWhiteSpace(SessionDetail) ? "" : $"最近一次受管会话：{SessionDetail}",
        Evidence.Count == 0 ? "未发现可摘录的异常线索。" : "线索：" + Environment.NewLine + string.Join(Environment.NewLine, Evidence.Select(item => "- " + item)),
        SourceFiles.Count == 0 ? "未找到可分析的日志文件。" : "来源：" + Environment.NewLine + string.Join(Environment.NewLine, SourceFiles.Select(item => "- " + item)));
}

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error
}

public enum DiagnosticCategory
{
    Unknown,
    Normal,
    MissingDependency,
    BattlEyeConflict,
    DoorstopFailure,
    DxvkFailure,
    UnityCrash,
    PluginException,
    UncleanExit
}
