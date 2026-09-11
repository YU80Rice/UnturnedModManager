namespace UnturnedModManager.Navigation;

/// <summary>
/// 页面导航请求
/// </summary>
public sealed record PageNavigationRequest(
    PrimarySlot TargetSlot,
    WorkshopSubDomain? SubDomain = null,
    NavigationContext? Context = null);

/// <summary>
/// 单根来源返回上下文
/// </summary>
public sealed record ReturnContext(
    PrimarySlot SourceSlot,
    WorkshopSubDomain? SourceSubDomain = null,
    NavigationContext? SourceContext = null);
