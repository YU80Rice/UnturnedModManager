using System;

namespace UnturnedModManager.Navigation;

/// <summary>
/// 导航状态变更事件参数
/// 当处于次级状态时，PrimarySlot 为 null，UI 据此取消左侧主槽位高亮。
/// </summary>
public sealed class NavigationStateChangedEventArgs(
    PrimarySlot? primarySlot,
    SecondaryNavigationTarget? secondaryTarget,
    ReturnContext? returnContext) : EventArgs
{
    public PrimarySlot? PrimarySlot { get; } = primarySlot;
    public SecondaryNavigationTarget? SecondaryTarget { get; } = secondaryTarget;
    public ReturnContext? ReturnContext { get; } = returnContext;
}
