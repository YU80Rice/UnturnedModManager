using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace UnturnedModManager.Navigation;

/// <summary>
/// NavigationShell 纯逻辑宿主与单一导航状态机实现
/// 负责 5 槽位调度、次级入口覆盖、单根来源锚点记忆与来源安全恢复。
/// 与 WPF 控件完全解耦，可进行高保真纯逻辑单元测试。
/// </summary>
public sealed class NavigationShellHost : INavigationShellHost
{
    private ImmutableHashSet<NavigationCacheKey> _visitedKeys = ImmutableHashSet<NavigationCacheKey>.Empty;
    private PrimarySlot _activeSlot;
    private SecondaryNavigationTarget? _activeSecondary;
    private ReturnContext? _rootReturnContext;
    private WorkshopSubDomain? _currentSubDomain;
    private NavigationContext? _currentContext;

    public NavigationShellHost(
        PrimarySlot initialSlot = PrimarySlot.Play,
        SecondaryNavigationTarget? initialSecondary = null,
        ReturnContext? initialAnchor = null)
    {
        // 1. 校验并规范化 initialSlot
        if (!Enum.IsDefined(typeof(PrimarySlot), initialSlot))
        {
            _activeSlot = PrimarySlot.Play;
        }
        else
        {
            _activeSlot = initialSlot;
        }

        if (_activeSlot == PrimarySlot.Mods)
        {
            _currentSubDomain = WorkshopSubDomain.LocalInstalled;
        }

        // 2. 校验并规范化 initialSecondary
        if (initialSecondary.HasValue && !Enum.IsDefined(typeof(SecondaryNavigationTarget), initialSecondary.Value))
        {
            _activeSecondary = null;
        }
        else
        {
            _activeSecondary = initialSecondary;
        }

        // 3. 校验并规范化 initialAnchor
        if (initialAnchor is not null &&
            NavigationCacheKey.IsValid(initialAnchor.SourceSlot, initialAnchor.SourceSubDomain, initialAnchor.SourceContext, out _, out _))
        {
            _rootReturnContext = initialAnchor;
        }
        else
        {
            _rootReturnContext = null;
        }

        // 4. 确保只有合法的缓存键能进入已访问集合
        var initialKey = CurrentKey;
        if (initialKey.IsValid(out _, out _))
        {
            _visitedKeys = _visitedKeys.Add(initialKey);
        }
        else
        {
            _activeSlot = PrimarySlot.Play;
            _currentSubDomain = null;
            _currentContext = null;
            _visitedKeys = _visitedKeys.Add(CurrentKey);
        }
    }

    public PrimarySlot ActiveSlot => _activeSlot;

    public SecondaryNavigationTarget? ActiveSecondary => _activeSecondary;

    public ReturnContext? RootReturnContext => _rootReturnContext;

    public bool IsPrimaryHighlighted => _activeSecondary is null;

    public PrimarySlot? HighlightedSlot => _activeSecondary is null ? _activeSlot : null;

    public bool CanNavigateBack => _activeSecondary is not null;

    public NavigationCacheKey CurrentKey => new(_activeSlot, _currentSubDomain, _currentContext);

    /// <summary>
    /// 返回已访问缓存键的不可变集合快照，零多余内存分配且彻底杜绝外部篡改。
    /// </summary>
    public IReadOnlySet<NavigationCacheKey> VisitedKeys => _visitedKeys;

    public event EventHandler<NavigationStateChangedEventArgs>? StateChanged;

    public NavigationResult NavigateTo(PageNavigationRequest request)
    {
        if (request is null)
        {
            return NavigationResult.Rejected;
        }

        if (!NavigationCacheKey.IsValid(request.TargetSlot, request.SubDomain, request.Context, out var failureResult, out _))
        {
            return failureResult;
        }

        // 若处于次级导航，重定向至主槽位并清空次级状态与单根锚点
        if (_activeSecondary is not null)
        {
            _activeSecondary = null;
            _rootReturnContext = null;
        }

        _activeSlot = request.TargetSlot;
        if (_activeSlot == PrimarySlot.Mods)
        {
            _currentSubDomain = request.SubDomain ?? WorkshopSubDomain.LocalInstalled;
            _currentContext = request.Context;
        }
        else
        {
            _currentSubDomain = null;
            _currentContext = null;
        }

        _visitedKeys = _visitedKeys.Add(CurrentKey);
        OnStateChanged(new NavigationStateChangedEventArgs(_activeSlot, null, null));
        return NavigationResult.Success;
    }

    public NavigationResult NavigateToSecondary(SecondaryNavigationTarget secondaryTarget)
    {
        if (!Enum.IsDefined(typeof(SecondaryNavigationTarget), secondaryTarget))
        {
            return NavigationResult.InvalidTarget;
        }

        // 重复激活当前次级项保持幂等
        if (_activeSecondary == secondaryTarget)
        {
            return NavigationResult.Success;
        }

        // 若初次从主槽位进入次级体系，创建单根来源锚点快照
        if (_activeSecondary is null)
        {
            _rootReturnContext = new ReturnContext(_activeSlot, _currentSubDomain, _currentContext);
        }
        // 若在次级之间平级切换 (如 TaskCenter ↔ About)，保持原有单根来源锚点不被覆盖

        _activeSecondary = secondaryTarget;

        // 次级激活时，PrimarySlot 置 null，代表通知 UI 取消主槽位高亮
        OnStateChanged(new NavigationStateChangedEventArgs(null, _activeSecondary, _rootReturnContext));
        return NavigationResult.Success;
    }

    public NavigationResult NavigateBack()
    {
        if (!CanNavigateBack)
        {
            return NavigationResult.Rejected;
        }

        // 处理次级状态返回
        if (_activeSecondary is not null)
        {
            if (_rootReturnContext is not null &&
                NavigationCacheKey.IsValid(_rootReturnContext.SourceSlot, _rootReturnContext.SourceSubDomain, _rootReturnContext.SourceContext, out _, out _))
            {
                _activeSlot = _rootReturnContext.SourceSlot;
                _currentSubDomain = _rootReturnContext.SourceSubDomain;
                _currentContext = _rootReturnContext.SourceContext;
            }
            else
            {
                // 来源锚点缺失或不可用时的安全降级保底：回退到默认 Play 槽位
                _activeSlot = PrimarySlot.Play;
                _currentSubDomain = null;
                _currentContext = null;
            }

            _activeSecondary = null;
            _rootReturnContext = null;

            _visitedKeys = _visitedKeys.Add(CurrentKey);
            OnStateChanged(new NavigationStateChangedEventArgs(_activeSlot, null, null));
            return NavigationResult.Success;
        }

        return NavigationResult.Rejected;
    }

    private void OnStateChanged(NavigationStateChangedEventArgs args)
    {
        StateChanged?.Invoke(this, args);
    }
}
