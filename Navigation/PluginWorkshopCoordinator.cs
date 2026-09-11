using System;

namespace UnturnedModManager.Navigation;

/// <summary>
/// 工坊交互上下文快照 (搜索词、分类筛选、排序与滚动条偏移)
/// 用于在子域切换或进入详情后精准恢复用户交互状态。
/// </summary>
public sealed record WorkshopContextSnapshot(
    string? SearchQuery = null,
    string? CategoryKey = null,
    string? Sort = null,
    string? SelectedProfileId = null,
    double ScrollOffset = 0.0,
    DateTime CapturedAt = default);

/// <summary>
/// 模组工坊纯逻辑导航调度状态机与上下文协调器 (Ticket C 核心实现)
/// 负责工坊双子域互斥调度、单活跃视图状态派生、社区详情自治消化与上下文快照保全。
/// 纯逻辑设计，可脱离 UI 线程独立执行自动化单元测试。
/// </summary>
public sealed class PluginWorkshopCoordinator : IPluginWorkshopNavigation
{
    private readonly object _syncLock = new();
    private WorkshopSubDomain _currentSubDomain = WorkshopSubDomain.LocalInstalled;
    private WorkshopActiveView _activeView = WorkshopActiveView.LocalInstalled;
    private int? _activeDetailModId;
    private WorkshopSubDomain _detailSourceSubDomain = WorkshopSubDomain.LocalInstalled;
    private NavigationContext? _detailSourceContext;

    private WorkshopContextSnapshot? _localSnapshot;
    private WorkshopContextSnapshot? _communitySnapshot;

    public PluginWorkshopCoordinator(WorkshopSubDomain initialSubDomain = WorkshopSubDomain.LocalInstalled)
    {
        if (Enum.IsDefined(typeof(WorkshopSubDomain), initialSubDomain))
        {
            _currentSubDomain = initialSubDomain;
            _activeView = initialSubDomain == WorkshopSubDomain.LocalInstalled
                ? WorkshopActiveView.LocalInstalled
                : WorkshopActiveView.CommunityDiscovery;
        }
        else
        {
            _currentSubDomain = WorkshopSubDomain.LocalInstalled;
            _activeView = WorkshopActiveView.LocalInstalled;
        }
    }

    #region IPluginWorkshopNavigation 状态属性

    public WorkshopSubDomain CurrentSubDomain
    {
        get
        {
            lock (_syncLock)
            {
                return _currentSubDomain;
            }
        }
    }

    public WorkshopActiveView ActiveView
    {
        get
        {
            lock (_syncLock)
            {
                return _activeView;
            }
        }
    }

    public bool IsDetailActive
    {
        get
        {
            lock (_syncLock)
            {
                return _activeView == WorkshopActiveView.Detail;
            }
        }
    }

    public int? ActiveDetailModId
    {
        get
        {
            lock (_syncLock)
            {
                return _activeDetailModId;
            }
        }
    }

    public WorkshopSubDomain DetailSourceSubDomain
    {
        get
        {
            lock (_syncLock)
            {
                return _detailSourceSubDomain;
            }
        }
    }

    public NavigationContext? DetailSourceContext
    {
        get
        {
            lock (_syncLock)
            {
                return _detailSourceContext;
            }
        }
    }

    public WorkshopContextSnapshot? LocalSnapshot
    {
        get
        {
            lock (_syncLock)
            {
                return _localSnapshot;
            }
        }
    }

    public WorkshopContextSnapshot? CommunitySnapshot
    {
        get
        {
            lock (_syncLock)
            {
                return _communitySnapshot;
            }
        }
    }

    public event EventHandler<WorkshopSubDomain>? SubDomainChanged;
    public event EventHandler<bool>? DetailStateChanged;
    public event Action<string>? CommunitySearchQueryApplied;

    #endregion

    #region 导航调度核心逻辑

    public NavigationChangeResult NavigateTo(WorkshopSubDomain target)
    {
        return NavigateTo(target, null);
    }

    public NavigationChangeResult NavigateTo(WorkshopSubDomain target, NavigationContext? context)
    {
        if (!Enum.IsDefined(typeof(WorkshopSubDomain), target))
        {
            return NavigationChangeResult.InvalidTarget;
        }

        bool subDomainChanged = false;
        bool detailClosed = false;
        bool detailOpened = false;

        lock (_syncLock)
        {
            // 1. 如果请求携带详情上下文，直接就地自治打开详情
            if (context is PluginDetailContext detail)
            {
                if (int.TryParse(detail.ModId, out var modId) && modId > 0)
                {
                    if (_currentSubDomain != target)
                    {
                        _currentSubDomain = target;
                        subDomainChanged = true;
                    }

                    detailOpened = ShowDetailInternal(modId, target, context);
                }
                else
                {
                    return NavigationChangeResult.Rejected;
                }
            }

            if (detailOpened)
            {
                // 退出锁后统一派发通知
            }
            else
            {
                // 2. 如果携带搜索上下文，更新暂存搜索词并准备应用
            if (context is CommunitySearchContext search)
            {
                _communitySnapshot = (_communitySnapshot ?? new WorkshopContextSnapshot()) with
                {
                    SearchQuery = search.Query,
                    CapturedAt = DateTime.UtcNow
                };
            }

            // 3. 处理详情态下的子域导航：关闭详情并跳转目标子域
            if (_activeView == WorkshopActiveView.Detail)
            {
                _activeView = target == WorkshopSubDomain.LocalInstalled
                    ? WorkshopActiveView.LocalInstalled
                    : WorkshopActiveView.CommunityDiscovery;
                _activeDetailModId = null;
                detailClosed = true;

                if (_currentSubDomain != target)
                {
                    _currentSubDomain = target;
                    subDomainChanged = true;
                }

                NotifyStateChanges(subDomainChanged, detailClosed, context);
                return NavigationChangeResult.Changed;
            }

            // 4. 幂等性校验：如果目标子域与当前一致且无详情态，返回 AlreadyActive
            if (_currentSubDomain == target)
            {
                if (context is CommunitySearchContext searchContext)
                {
                    CommunitySearchQueryApplied?.Invoke(searchContext.Query);
                }
                return NavigationChangeResult.AlreadyActive;
            }

            // 5. 执行常规子域切换
            _currentSubDomain = target;
            _activeView = target == WorkshopSubDomain.LocalInstalled
                ? WorkshopActiveView.LocalInstalled
                : WorkshopActiveView.CommunityDiscovery;
            subDomainChanged = true;
            }
        }

        if (detailOpened)
        {
            if (subDomainChanged)
            {
                SubDomainChanged?.Invoke(this, target);
            }
            DetailStateChanged?.Invoke(this, true);
            return NavigationChangeResult.Changed;
        }

        NotifyStateChanges(subDomainChanged, detailClosed, context);
        return NavigationChangeResult.Changed;
    }

    public bool ShowDetail(int modId, WorkshopSubDomain? sourceSubDomain = null, NavigationContext? sourceContext = null)
    {
        if (modId <= 0)
        {
            return false;
        }

        bool stateChanged;
        lock (_syncLock)
        {
            stateChanged = ShowDetailInternal(modId, sourceSubDomain, sourceContext);
        }

        if (stateChanged)
        {
            DetailStateChanged?.Invoke(this, true);
        }

        return true;
    }

    public bool CloseDetail()
    {
        WorkshopSubDomain restoredSubDomain;
        lock (_syncLock)
        {
            if (_activeView != WorkshopActiveView.Detail)
            {
                return false;
            }

            _activeDetailModId = null;
            _activeView = _detailSourceSubDomain == WorkshopSubDomain.LocalInstalled
                ? WorkshopActiveView.LocalInstalled
                : WorkshopActiveView.CommunityDiscovery;
            _currentSubDomain = _detailSourceSubDomain;
            restoredSubDomain = _currentSubDomain;
        }

        DetailStateChanged?.Invoke(this, false);
        SubDomainChanged?.Invoke(this, restoredSubDomain);
        return true;
    }

    private bool ShowDetailInternal(int modId, WorkshopSubDomain? sourceSubDomain, NavigationContext? sourceContext)
    {
        if (_activeView == WorkshopActiveView.Detail && _activeDetailModId == modId)
        {
            return false;
        }

        _activeDetailModId = modId;
        _detailSourceSubDomain = sourceSubDomain ?? _currentSubDomain;
        _detailSourceContext = sourceContext;
        _activeView = WorkshopActiveView.Detail;
        return true;
    }

    private void NotifyStateChanges(bool subDomainChanged, bool detailClosed, NavigationContext? context)
    {
        if (detailClosed)
        {
            DetailStateChanged?.Invoke(this, false);
        }

        if (subDomainChanged)
        {
            SubDomainChanged?.Invoke(this, _currentSubDomain);
        }

        if (context is CommunitySearchContext searchContext)
        {
            CommunitySearchQueryApplied?.Invoke(searchContext.Query);
        }
    }

    #endregion

    #region 上下文快照暂存与精准恢复

    public void CaptureLocalSnapshot(double scrollOffset, string? selectedProfileId = null)
    {
        lock (_syncLock)
        {
            _localSnapshot = new WorkshopContextSnapshot(
                SelectedProfileId: selectedProfileId,
                ScrollOffset: Math.Max(0.0, scrollOffset),
                CapturedAt: DateTime.UtcNow);
        }
    }

    public void CaptureCommunitySnapshot(
        string? searchQuery,
        string? categoryKey,
        string? sort,
        double scrollOffset)
    {
        lock (_syncLock)
        {
            _communitySnapshot = new WorkshopContextSnapshot(
                SearchQuery: searchQuery,
                CategoryKey: categoryKey,
                Sort: sort,
                ScrollOffset: Math.Max(0.0, scrollOffset),
                CapturedAt: DateTime.UtcNow);
        }
    }

    public void ClearSnapshots()
    {
        lock (_syncLock)
        {
            _localSnapshot = null;
            _communitySnapshot = null;
        }
    }

    #endregion

    #region PageNavigationRequest 意图分发

    public NavigationChangeResult HandleNavigationRequest(PageNavigationRequest request)
    {
        if (request is null || request.TargetSlot != PrimarySlot.Mods)
        {
            return NavigationChangeResult.Rejected;
        }

        var targetSubDomain = request.SubDomain ?? _currentSubDomain;
        return NavigateTo(targetSubDomain, request.Context);
    }

    #endregion
}
