using System;
using System.Windows.Controls;
using UnturnedModManager.Pages;
using Wpf.Ui.Controls;

namespace UnturnedModManager.Navigation;

/// <summary>
/// 面向既有 NavigationView 的过渡期宿主适配器 (Ticket B 生产连通保障)
/// 在 Ticket E 挂载原生 Shell 之前，使既有主窗口与重构后的 AppNavigationService 建立显式宿主绑定，
/// 严格保证单一事实源，杜绝隐式备用状态机，并消除已迁移页面的导航功能回归。
/// </summary>
public sealed class LegacyNavigationViewHostAdapter : INavigationShellHost
{
    private readonly MainWindow _mainWindow;
    private readonly Wpf.Ui.Controls.NavigationView _navigationView;
    private PrimarySlot _activeSlot = PrimarySlot.Play;
    private SecondaryNavigationTarget? _activeSecondary;
    private ReturnContext? _rootReturnContext;

    public LegacyNavigationViewHostAdapter(MainWindow mainWindow, Wpf.Ui.Controls.NavigationView navigationView)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _navigationView = navigationView ?? throw new ArgumentNullException(nameof(navigationView));
        _navigationView.Navigated += OnNavigationViewNavigated;
    }

    public PrimarySlot ActiveSlot => _activeSlot;

    public SecondaryNavigationTarget? ActiveSecondary => _activeSecondary;

    public bool CanNavigateBack =>
        _activeSecondary is not null || (_mainWindow.CurrentPage?.NavigationService?.CanGoBack == true);

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

        return (NavigationResult)_mainWindow.Dispatcher.Invoke(() =>
        {
            if (_activeSecondary is not null)
            {
                _activeSecondary = null;
                _rootReturnContext = null;
            }

            _activeSlot = request.TargetSlot;

            switch (request.TargetSlot)
            {
                case PrimarySlot.Play:
                    _navigationView.Navigate(typeof(HomePage));
                    break;

                case PrimarySlot.Settings:
                    _navigationView.Navigate(typeof(SettingsPage));
                    break;

                case PrimarySlot.Mods:
                    if (request.Context is PluginDetailContext detail && int.TryParse(detail.ModId, out var modId))
                    {
                        var origin = new NavigationOrigin(
                            request.SubDomain == WorkshopSubDomain.LocalInstalled ? "返回本地插件" : "返回插件社区",
                            PrimarySlot.Mods,
                            request.SubDomain ?? WorkshopSubDomain.CommunityDiscovery);

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
                            _navigationView.Navigate(request.SubDomain == WorkshopSubDomain.LocalInstalled
                                ? typeof(ModListPage)
                                : typeof(CommunityPage));
                        }
                    }
                    else if (request.SubDomain == WorkshopSubDomain.LocalInstalled)
                    {
                        _navigationView.Navigate(typeof(ModListPage));
                    }
                    else
                    {
                        _navigationView.Navigate(typeof(CommunityPage));
                    }
                    break;

                case PrimarySlot.Data:
                    break;

                case PrimarySlot.Exit:
                    _mainWindow.Close();
                    break;
            }

            StateChanged?.Invoke(this, new NavigationStateChangedEventArgs(_activeSlot, null, null));
            return NavigationResult.Success;
        });
    }

    public NavigationResult NavigateToSecondary(SecondaryNavigationTarget secondaryTarget)
    {
        if (!Enum.IsDefined(typeof(SecondaryNavigationTarget), secondaryTarget))
        {
            return NavigationResult.InvalidTarget;
        }

        return (NavigationResult)_mainWindow.Dispatcher.Invoke(() =>
        {
            if (_activeSecondary is null)
            {
                _rootReturnContext = new ReturnContext(_activeSlot);
            }

            _activeSecondary = secondaryTarget;

            switch (secondaryTarget)
            {
                case SecondaryNavigationTarget.TaskCenter:
                    _navigationView.Navigate(typeof(TaskCenterPage));
                    break;

                case SecondaryNavigationTarget.About:
                    _navigationView.Navigate(typeof(AboutPage));
                    break;
            }

            StateChanged?.Invoke(this, new NavigationStateChangedEventArgs(null, _activeSecondary, _rootReturnContext));
            return NavigationResult.Success;
        });
    }

    public NavigationResult NavigateBack()
    {
        return (NavigationResult)_mainWindow.Dispatcher.Invoke(() =>
        {
            var currentNav = _mainWindow.CurrentPage?.NavigationService;
            if (currentNav?.CanGoBack == true)
            {
                currentNav.GoBack();
                return NavigationResult.Success;
            }

            if (_activeSecondary is not null)
            {
                var targetSlot = _rootReturnContext?.SourceSlot ?? PrimarySlot.Play;
                _activeSecondary = null;
                _rootReturnContext = null;
                _activeSlot = targetSlot;

                switch (targetSlot)
                {
                    case PrimarySlot.Settings:
                        _navigationView.Navigate(typeof(SettingsPage));
                        break;
                    case PrimarySlot.Mods:
                        _navigationView.Navigate(typeof(CommunityPage));
                        break;
                    default:
                        _navigationView.Navigate(typeof(HomePage));
                        break;
                }

                StateChanged?.Invoke(this, new NavigationStateChangedEventArgs(_activeSlot, null, null));
                return NavigationResult.Success;
            }

            return NavigationResult.Rejected;
        });
    }

    private void OnNavigationViewNavigated(object sender, NavigatedEventArgs e)
    {
        var page = e.Page as Page;
        switch (page)
        {
            case HomePage:
                _activeSlot = PrimarySlot.Play;
                _activeSecondary = null;
                break;
            case ModListPage:
                _activeSlot = PrimarySlot.Mods;
                _activeSecondary = null;
                break;
            case CommunityPage:
                _activeSlot = PrimarySlot.Mods;
                _activeSecondary = null;
                break;
            case SettingsPage:
                _activeSlot = PrimarySlot.Settings;
                _activeSecondary = null;
                break;
            case TaskCenterPage:
                _activeSecondary = SecondaryNavigationTarget.TaskCenter;
                break;
            case AboutPage:
                _activeSecondary = SecondaryNavigationTarget.About;
                break;
        }
    }
}
