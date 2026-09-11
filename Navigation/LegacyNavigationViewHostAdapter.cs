using System;
using System.Windows.Controls;
using UnturnedModManager.Pages;
using Wpf.Ui.Controls;

namespace UnturnedModManager.Navigation;

/// <summary>
/// 既有主窗口导航目标交互契约 (便于过渡期宿主解耦与单元测试)
/// </summary>
internal interface ILegacyNavigationTarget
{
    Page? CurrentPage { get; }
    bool CanGoBack { get; }
    bool GoBack();
    bool Navigate(Type pageType);
    void Close();
    void InvokeOnDispatcher(Action action);
    void NavigateToDetail(int modId, NavigationOrigin origin);
    void ApplyCommunitySearch(string query);
}

/// <summary>
/// 面向既有 NavigationView 的过渡期宿主适配器 (Ticket B 生产连通保障)
/// 在 Ticket E 挂载原生 Shell 之前，使既有主窗口与重构后的 AppNavigationService 建立显式宿主绑定，
/// 严格保证单一事实源，杜绝隐式备用状态机，并消除已迁移页面的导航功能回归。
/// </summary>
public sealed class LegacyNavigationViewHostAdapter : INavigationShellHost
{
    private readonly ILegacyNavigationTarget _target;
    private PrimarySlot _activeSlot = PrimarySlot.Play;
    private SecondaryNavigationTarget? _activeSecondary;
    private ReturnContext? _rootReturnContext;
    private WorkshopSubDomain? _currentSubDomain;
    private NavigationContext? _currentContext;

    public LegacyNavigationViewHostAdapter(MainWindow mainWindow, Wpf.Ui.Controls.NavigationView navigationView)
        : this(new MainWindowNavigationTarget(mainWindow, navigationView))
    {
        navigationView.Navigated += OnNavigationViewNavigated;
    }

    internal LegacyNavigationViewHostAdapter(ILegacyNavigationTarget target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public PrimarySlot ActiveSlot => _activeSlot;

    public SecondaryNavigationTarget? ActiveSecondary => _activeSecondary;

    public ReturnContext? RootReturnContext => _rootReturnContext;

    public WorkshopSubDomain? CurrentSubDomain => _currentSubDomain;

    public NavigationContext? CurrentContext => _currentContext;

    public bool CanNavigateBack =>
        _activeSecondary is not null || _target.CanGoBack;

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

        var result = NavigationResult.Success;

        _target.InvokeOnDispatcher(() =>
        {
            if (_activeSecondary is not null)
            {
                _activeSecondary = null;
                _rootReturnContext = null;
            }

            switch (request.TargetSlot)
            {
                case PrimarySlot.Play:
                    _activeSlot = PrimarySlot.Play;
                    _currentSubDomain = null;
                    _currentContext = null;
                    _target.Navigate(typeof(HomePage));
                    break;

                case PrimarySlot.Settings:
                    _activeSlot = PrimarySlot.Settings;
                    _currentSubDomain = null;
                    _currentContext = null;
                    _target.Navigate(typeof(SettingsPage));
                    break;

                case PrimarySlot.Mods:
                    _activeSlot = PrimarySlot.Mods;
                    _currentSubDomain = request.SubDomain ?? WorkshopSubDomain.CommunityDiscovery;
                    _currentContext = request.Context;

                    if (request.Context is PluginDetailContext detail && int.TryParse(detail.ModId, out var modId))
                    {
                        var origin = new NavigationOrigin(
                            _currentSubDomain == WorkshopSubDomain.LocalInstalled ? "返回本地插件" : "返回插件社区",
                            PrimarySlot.Mods,
                            _currentSubDomain,
                            _currentContext);

                        _target.NavigateToDetail(modId, origin);
                    }
                    else if (_currentSubDomain == WorkshopSubDomain.LocalInstalled)
                    {
                        _target.Navigate(typeof(ModListPage));
                    }
                    else
                    {
                        _target.Navigate(typeof(CommunityPage));
                        if (request.Context is CommunitySearchContext searchContext)
                        {
                            _target.ApplyCommunitySearch(searchContext.Query);
                        }
                    }
                    break;

                case PrimarySlot.Data:
                    // 核心修复 [P1]：旧宿主阶段 Data 槽位暂无承载页面，返回明确的 PendingInitialization 状态，严禁空操作后伪装 Success
                    result = NavigationResult.PendingInitialization;
                    return;

                case PrimarySlot.Exit:
                    _activeSlot = PrimarySlot.Exit;
                    _currentSubDomain = null;
                    _currentContext = null;
                    _target.Close();
                    break;
            }

            StateChanged?.Invoke(this, new NavigationStateChangedEventArgs(_activeSlot, null, null));
        });

        return result;
    }

    public NavigationResult NavigateToSecondary(SecondaryNavigationTarget secondaryTarget)
    {
        if (!Enum.IsDefined(typeof(SecondaryNavigationTarget), secondaryTarget))
        {
            return NavigationResult.InvalidTarget;
        }

        if (_activeSecondary == secondaryTarget)
        {
            return NavigationResult.Success;
        }

        var result = NavigationResult.Success;

        _target.InvokeOnDispatcher(() =>
        {
            // 核心修复 [P1]：记录完整的单根来源快照 (包括当前主槽位、子域与结构化上下文)
            if (_activeSecondary is null)
            {
                _rootReturnContext = new ReturnContext(_activeSlot, _currentSubDomain, _currentContext);
            }

            _activeSecondary = secondaryTarget;

            switch (secondaryTarget)
            {
                case SecondaryNavigationTarget.TaskCenter:
                    _target.Navigate(typeof(TaskCenterPage));
                    break;

                case SecondaryNavigationTarget.About:
                    _target.Navigate(typeof(AboutPage));
                    break;
            }

            StateChanged?.Invoke(this, new NavigationStateChangedEventArgs(null, _activeSecondary, _rootReturnContext));
        });

        return result;
    }

    public NavigationResult NavigateBack()
    {
        var result = NavigationResult.Rejected;

        _target.InvokeOnDispatcher(() =>
        {
            // 1. 若处于次级导航，优先恢复次级单根来源锚点 (即便 journal 不可用也能精准恢复子域与上下文)
            if (_activeSecondary is not null)
            {
                var returnContext = _rootReturnContext;
                var targetSlot = returnContext?.SourceSlot ?? PrimarySlot.Play;
                var targetSubDomain = returnContext?.SourceSubDomain;
                var targetContext = returnContext?.SourceContext;

                _activeSecondary = null;
                _rootReturnContext = null;
                _activeSlot = targetSlot;
                _currentSubDomain = targetSubDomain;
                _currentContext = targetContext;

                switch (targetSlot)
                {
                    case PrimarySlot.Settings:
                        _target.Navigate(typeof(SettingsPage));
                        break;

                    case PrimarySlot.Mods:
                        if (targetContext is PluginDetailContext detail && int.TryParse(detail.ModId, out var modId))
                        {
                            var origin = new NavigationOrigin(
                                targetSubDomain == WorkshopSubDomain.LocalInstalled ? "返回本地插件" : "返回插件社区",
                                PrimarySlot.Mods,
                                targetSubDomain,
                                targetContext);

                            _target.NavigateToDetail(modId, origin);
                        }
                        else if (targetSubDomain == WorkshopSubDomain.LocalInstalled)
                        {
                            _target.Navigate(typeof(ModListPage));
                        }
                        else
                        {
                            _target.Navigate(typeof(CommunityPage));
                            if (targetContext is CommunitySearchContext searchContext)
                            {
                                _target.ApplyCommunitySearch(searchContext.Query);
                            }
                        }
                        break;

                    case PrimarySlot.Play:
                    default:
                        _activeSlot = PrimarySlot.Play;
                        _currentSubDomain = null;
                        _currentContext = null;
                        _target.Navigate(typeof(HomePage));
                        break;
                }

                StateChanged?.Invoke(this, new NavigationStateChangedEventArgs(_activeSlot, null, null));
                result = NavigationResult.Success;
                return;
            }

            // 2. 次级为空时，检查宿主内部页面 Journal (如从模组详情页返回前置模组列表/社区)
            if (_target.CanGoBack)
            {
                if (_target.GoBack())
                {
                    result = NavigationResult.Success;
                    return;
                }
            }

            result = NavigationResult.Rejected;
        });

        return result;
    }

    private void OnNavigationViewNavigated(object sender, NavigatedEventArgs e)
    {
        var page = e.Page as Page;
        switch (page)
        {
            case HomePage:
                _activeSlot = PrimarySlot.Play;
                _activeSecondary = null;
                _currentSubDomain = null;
                _currentContext = null;
                break;
            case ModListPage:
                _activeSlot = PrimarySlot.Mods;
                _activeSecondary = null;
                _currentSubDomain = WorkshopSubDomain.LocalInstalled;
                _currentContext = null;
                break;
            case CommunityPage:
                _activeSlot = PrimarySlot.Mods;
                _activeSecondary = null;
                _currentSubDomain = WorkshopSubDomain.CommunityDiscovery;
                break;
            case SettingsPage:
                _activeSlot = PrimarySlot.Settings;
                _activeSecondary = null;
                _currentSubDomain = null;
                _currentContext = null;
                break;
            case TaskCenterPage:
                _activeSecondary = SecondaryNavigationTarget.TaskCenter;
                break;
            case AboutPage:
                _activeSecondary = SecondaryNavigationTarget.About;
                break;
        }
    }

    private sealed class MainWindowNavigationTarget : ILegacyNavigationTarget
    {
        private readonly MainWindow _mainWindow;
        private readonly Wpf.Ui.Controls.NavigationView _navigationView;

        public MainWindowNavigationTarget(MainWindow mainWindow, Wpf.Ui.Controls.NavigationView navigationView)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _navigationView = navigationView ?? throw new ArgumentNullException(nameof(navigationView));
        }

        public Page? CurrentPage => _mainWindow.CurrentPage;

        public bool CanGoBack => _mainWindow.CurrentPage?.NavigationService?.CanGoBack == true;

        public bool GoBack()
        {
            if (_mainWindow.CurrentPage?.NavigationService?.CanGoBack == true)
            {
                _mainWindow.CurrentPage.NavigationService.GoBack();
                return true;
            }
            return false;
        }

        public bool Navigate(Type pageType) => _navigationView.Navigate(pageType);

        public void Close() => _mainWindow.Close();

        public void InvokeOnDispatcher(Action action)
        {
            if (_mainWindow.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _mainWindow.Dispatcher.Invoke(action);
            }
        }

        public void NavigateToDetail(int modId, NavigationOrigin origin)
        {
            var currentNav = _mainWindow.CurrentPage?.NavigationService;
            if (currentNav is not null)
            {
                currentNav.Navigate(new CommunityDetailPage(modId, origin));
            }
            else
            {
                void OpenWhenReady(object? sender, NavigatedEventArgs args)
                {
                    _navigationView.Navigated -= OpenWhenReady;
                    (args.Page as Page)?.NavigationService?.Navigate(new CommunityDetailPage(modId, origin));
                }
                _navigationView.Navigated += OpenWhenReady;
                _navigationView.Navigate(origin.SourceSubDomain == WorkshopSubDomain.LocalInstalled
                    ? typeof(ModListPage)
                    : typeof(CommunityPage));
            }
        }

        public void ApplyCommunitySearch(string query)
        {
            if (_mainWindow.CurrentPage is CommunityPage communityPage)
            {
                communityPage.ViewModel.SearchText = query;
            }
            else
            {
                void ApplyWhenReady(object? sender, NavigatedEventArgs args)
                {
                    _navigationView.Navigated -= ApplyWhenReady;
                    if (args.Page is CommunityPage readyCommunity)
                    {
                        readyCommunity.ViewModel.SearchText = query;
                    }
                }
                _navigationView.Navigated += ApplyWhenReady;
            }
        }
    }
}
