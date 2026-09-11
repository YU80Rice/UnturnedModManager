using System;

namespace UnturnedModManager.Navigation;

/// <summary>
/// 模组工坊子域导航切换结果
/// </summary>
public enum NavigationChangeResult
{
    Changed,
    AlreadyActive,
    InvalidTarget,
    Rejected,
    Unavailable
}

/// <summary>
/// 模组工坊单活跃视图枚举 (保证单活跃视图契约)
/// </summary>
public enum WorkshopActiveView
{
    LocalInstalled,
    CommunityDiscovery,
    Detail
}

/// <summary>
/// 模组工坊子域聚合导航核心 Seam 契约 (Ticket C 规范)
/// 作为 Slot 3 内部纯导航调度与自治详情处理的唯一核心接口。
/// </summary>
public interface IPluginWorkshopNavigation
{
    /// <summary>
    /// 当前选中的工坊子域
    /// </summary>
    WorkshopSubDomain CurrentSubDomain { get; }

    /// <summary>
    /// 当前激活展示的视图
    /// </summary>
    WorkshopActiveView ActiveView { get; }

    /// <summary>
    /// 当前是否正处于模组详情展示中
    /// </summary>
    bool IsDetailActive { get; }

    /// <summary>
    /// 当前展示的详情模组 ID (非详情态时为 null)
    /// </summary>
    int? ActiveDetailModId { get; }

    /// <summary>
    /// 导航至指定的工坊子域 (若处于详情态，将关闭详情并回到目标子域)
    /// </summary>
    NavigationChangeResult NavigateTo(WorkshopSubDomain target);

    /// <summary>
    /// 导航至指定的工坊子域并携带上下文
    /// </summary>
    NavigationChangeResult NavigateTo(WorkshopSubDomain target, NavigationContext? context);

    /// <summary>
    /// 自治打开指定模组的社区详情视图
    /// </summary>
    bool ShowDetail(int modId, WorkshopSubDomain? sourceSubDomain = null, NavigationContext? sourceContext = null);

    /// <summary>
    /// 关闭社区详情视图并恢复先前的子域与上下文
    /// </summary>
    bool CloseDetail();

    /// <summary>
    /// 工坊子域变更事件
    /// </summary>
    event EventHandler<WorkshopSubDomain>? SubDomainChanged;

    /// <summary>
    /// 模组详情状态变更事件
    /// </summary>
    event EventHandler<bool>? DetailStateChanged;
}
