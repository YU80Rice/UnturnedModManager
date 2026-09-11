using System;

namespace UnturnedModManager.Navigation;

/// <summary>
/// NavigationShell 宿主核心契约 (Ticket A 规范接口)
/// 作为当前活动窗口宿主范围内唯一的导航状态机与事实源。
/// </summary>
public interface INavigationShellHost
{
    /// <summary>
    /// 当前活跃的主槽位
    /// </summary>
    PrimarySlot ActiveSlot { get; }

    /// <summary>
    /// 当前激活的次级目标 (为 null 时表示处于主槽位视图)
    /// </summary>
    SecondaryNavigationTarget? ActiveSecondary { get; }

    /// <summary>
    /// 当前是否能够执行返回导航
    /// </summary>
    bool CanNavigateBack { get; }

    /// <summary>
    /// 导航至指定的主槽位页面 (若处于次级导航，将重定向并清空次级状态)
    /// </summary>
    NavigationResult NavigateTo(PageNavigationRequest request);

    /// <summary>
    /// 导航至指定的次级页面 (记录单根来源快照，取消主槽位高亮)
    /// </summary>
    NavigationResult NavigateToSecondary(SecondaryNavigationTarget secondaryTarget);

    /// <summary>
    /// 执行后退导航，恢复先前的槽位与子路由
    /// </summary>
    NavigationResult NavigateBack();

    /// <summary>
    /// 导航状态变更事件
    /// </summary>
    event EventHandler<NavigationStateChangedEventArgs>? StateChanged;
}
