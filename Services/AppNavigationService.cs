using System;
using UnturnedModManager.Navigation;

namespace UnturnedModManager.Services;

/// <summary>
/// 既有导航服务兼容门面适配器 (Ticket B 规范)
/// 将既有旧调用无损转发至当前活动 INavigationShellHost 宿主，
/// 彻底消解底层 UI Page 依赖，保证单一历史栈事实源与零双重栈隐患。
/// 无活动宿主时，兼容门面严格返回受控失败，禁止隐式创建第二套导航状态机。
/// </summary>
public sealed class AppNavigationService : INavigationShellHost
{
    private static readonly AppNavigationService _current = new();

    /// <summary>
    /// 全局兼容门面单例入口
    /// </summary>
    public static AppNavigationService Current => _current;

    private readonly object _syncLock = new();
    private INavigationShellHost? _host;

    /// <summary>
    /// 构造门面适配器实例。默认无宿主挂载；测试环境如需宿主，必须显式注入或调用 AttachHost。
    /// </summary>
    public AppNavigationService(INavigationShellHost? host = null)
    {
        _host = host;
        if (_host is not null)
        {
            _host.StateChanged += OnHostStateChanged;
        }
    }

    /// <summary>
    /// 当前是否挂载了活动宿主
    /// </summary>
    public bool HasHost
    {
        get
        {
            lock (_syncLock)
            {
                return _host is not null;
            }
        }
    }

    /// <summary>
    /// 当前挂载的活动 INavigationShellHost 宿主（未挂载时返回 null）
    /// </summary>
    public INavigationShellHost? Host
    {
        get
        {
            lock (_syncLock)
            {
                return _host;
            }
        }
    }

    /// <summary>
    /// 挂载当前活动窗口或容器的 INavigationShellHost 宿主
    /// </summary>
    public void AttachHost(INavigationShellHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        lock (_syncLock)
        {
            if (ReferenceEquals(_host, host))
            {
                return;
            }

            if (_host is not null)
            {
                _host.StateChanged -= OnHostStateChanged;
            }

            _host = host;
            _host.StateChanged += OnHostStateChanged;
        }
    }

    /// <summary>
    /// 解绑指定宿主（或当前宿主），置空引用，严格杜绝隐式产生备用状态机
    /// </summary>
    public void DetachHost(INavigationShellHost? host = null)
    {
        lock (_syncLock)
        {
            if (host is not null && !ReferenceEquals(_host, host))
            {
                return;
            }

            if (_host is not null)
            {
                _host.StateChanged -= OnHostStateChanged;
                _host = null;
            }
        }
    }

    /// <summary>
    /// 重置门面状态，供自动化测试隔离使用
    /// </summary>
    public void ResetForTesting()
    {
        DetachHost();
    }

    #region INavigationShellHost 核心状态与委托转发

    /// <summary>
    /// 当前活跃主槽位。未挂载宿主时抛出明确异常，绝不伪装成真实 Play 状态。
    /// </summary>
    public PrimarySlot ActiveSlot
    {
        get
        {
            lock (_syncLock)
            {
                if (_host is null)
                {
                    throw new InvalidOperationException("当前尚未挂载活动的 INavigationShellHost 宿主。");
                }

                return _host.ActiveSlot;
            }
        }
    }

    /// <summary>
    /// 安全查询当前主槽位（未挂载时返回 null）
    /// </summary>
    public PrimarySlot? ActiveSlotOrNull
    {
        get
        {
            lock (_syncLock)
            {
                return _host?.ActiveSlot;
            }
        }
    }

    public SecondaryNavigationTarget? ActiveSecondary
    {
        get
        {
            lock (_syncLock)
            {
                return _host?.ActiveSecondary;
            }
        }
    }

    public bool CanNavigateBack
    {
        get
        {
            lock (_syncLock)
            {
                return _host?.CanNavigateBack ?? false;
            }
        }
    }

    // Snapshot under the gate, then release it before calling a host that can marshal to the UI
    // dispatcher and raise StateChanged on that thread.
    public NavigationResult NavigateTo(PageNavigationRequest request) => request is null
        ? NavigationResult.Rejected
        : Host?.NavigateTo(request) ?? NavigationResult.Rejected;

    public NavigationResult NavigateToSecondary(SecondaryNavigationTarget secondaryTarget) =>
        Host?.NavigateToSecondary(secondaryTarget) ?? NavigationResult.Rejected;

    public NavigationResult NavigateBack() => Host?.NavigateBack() ?? NavigationResult.Rejected;

    public event EventHandler<NavigationStateChangedEventArgs>? StateChanged;

    private void OnHostStateChanged(object? sender, NavigationStateChangedEventArgs e)
    {
        lock (_syncLock)
        {
            if (_host is null || !ReferenceEquals(sender, _host))
            {
                return;
            }
        }

        StateChanged?.Invoke(this, e);
    }

    #endregion

    #region 结构化模组详情请求转发 (无 UI Page 泄漏)

    /// <summary>
    /// 导航至模组社区详情 (强类型结构化请求)
    /// </summary>
    public NavigationResult OpenCommunityDetail(CommunityDetailRequest request)
    {
        if (request is null)
        {
            return NavigationResult.Rejected;
        }

        if (request.ModId <= 0)
        {
            return NavigationResult.InvalidTarget;
        }

        var subDomain = request.SourceSubDomain ?? WorkshopSubDomain.CommunityDiscovery;
        var pageRequest = new PageNavigationRequest(
            PrimarySlot.Mods,
            subDomain,
            new PluginDetailContext(request.ModId.ToString()));

        return NavigateTo(pageRequest);
    }

    /// <summary>
    /// 导航至模组社区详情 (便捷重载)
    /// </summary>
    public NavigationResult OpenCommunityDetail(
        int modId,
        WorkshopSubDomain? sourceSubDomain = null,
        NavigationContext? sourceContext = null)
    {
        return OpenCommunityDetail(new CommunityDetailRequest(modId, sourceSubDomain, sourceContext));
    }

    /// <summary>
    /// 返回至上一级或来源锚点 (无 UI Page 泄漏)
    /// </summary>
    public NavigationResult ReturnToOrigin(NavigationOrigin? origin = null)
    {
        if (Host is not { } host) return NavigationResult.Rejected;
        if (host.CanNavigateBack) return host.NavigateBack();
        return host.NavigateTo(origin is null
            ? new PageNavigationRequest(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery)
            : new PageNavigationRequest(origin.SourceSlot, origin.SubDomain, origin.Context));
    }

    #endregion

    #region 既有旧页面便捷导航方法

    public NavigationResult NavigateToSlot(PrimarySlot slot)
    {
        return NavigateTo(new PageNavigationRequest(slot));
    }

    public NavigationResult NavigateToPlay() => NavigateToSlot(PrimarySlot.Play);

    public NavigationResult NavigateToData() => NavigateToSlot(PrimarySlot.Data);

    public NavigationResult NavigateToMods(
        WorkshopSubDomain subDomain = WorkshopSubDomain.LocalInstalled,
        NavigationContext? context = null)
    {
        return NavigateTo(new PageNavigationRequest(PrimarySlot.Mods, subDomain, context));
    }

    public NavigationResult NavigateToSettings() => NavigateToSlot(PrimarySlot.Settings);

    public NavigationResult NavigateToAbout() => NavigateToSecondary(SecondaryNavigationTarget.About);

    public NavigationResult NavigateToTaskCenter() => NavigateToSecondary(SecondaryNavigationTarget.TaskCenter);

    #endregion
}
