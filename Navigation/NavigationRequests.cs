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

/// <summary>
/// 模组社区详情导航请求 (Ticket B 强类型无底层 UI 泄漏契约)
/// </summary>
public sealed record CommunityDetailRequest(
    int ModId,
    WorkshopSubDomain? SourceSubDomain = null,
    NavigationContext? SourceContext = null);

/// <summary>
/// 强类型导航返回来源信息 (完全消除对底层 Page 控件实例的依赖)
/// </summary>
public sealed record NavigationOrigin(
    string BackLabel,
    PrimarySlot SourceSlot = PrimarySlot.Mods,
    WorkshopSubDomain? SourceSubDomain = null,
    NavigationContext? SourceContext = null)
{
    public WorkshopSubDomain? SubDomain => SourceSubDomain;
    public NavigationContext? Context => SourceContext;
}

