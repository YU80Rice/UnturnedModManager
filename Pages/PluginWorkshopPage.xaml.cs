using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UnturnedModManager.Helpers;
using UnturnedModManager.Navigation;
using Wpf.Ui.Controls;

namespace UnturnedModManager.Pages;

/// <summary>
/// 模组工坊纯导航聚合宿主 (Ticket C 规范)
/// 作为 Slot 3 的纯导航外壳，在单一容器 ViewsContainer 内挂载 ModListPage 与 CommunityPage。
/// 通过 Visibility（Visible vs Collapsed）互斥切换保持单活跃视图，隔离焦点命中与可访问性树；
/// 自治消化社区插件详情展示与返回，精准保全和恢复搜索词、筛选条件与列表滚动位置。
/// </summary>
public partial class PluginWorkshopPage : Page, IPluginWorkshopNavigation
{
    private readonly ModListPage _localPage;
    private readonly CommunityPage _communityPage;
    private readonly PluginWorkshopCoordinator _coordinator;
    private CommunityDetailPage? _detailPage;

    public PluginWorkshopPage()
        : this(null, null, null)
    {
    }

    public PluginWorkshopPage(
        ModListPage? localPage = null,
        CommunityPage? communityPage = null,
        PluginWorkshopCoordinator? coordinator = null)
    {
        InitializeComponent();

        _localPage = localPage ?? new ModListPage();
        _communityPage = communityPage ?? new CommunityPage();
        _coordinator = coordinator ?? new PluginWorkshopCoordinator();

        _coordinator.SubDomainChanged += OnSubDomainChanged;
        _coordinator.DetailStateChanged += OnDetailStateChanged;
        _coordinator.CommunitySearchQueryApplied += OnCommunitySearchQueryApplied;

        _localPage.ViewModel.OpenCommunityRequested += OnLocalOpenCommunityRequested;
        _communityPage.ViewModel.OpenModRequested += OnCommunityOpenModRequested;

        Loaded += OnLoaded;
    }

    #region IPluginWorkshopNavigation 契约暴露

    public WorkshopSubDomain CurrentSubDomain => _coordinator.CurrentSubDomain;

    public WorkshopActiveView ActiveView => _coordinator.ActiveView;

    public bool IsDetailActive => _coordinator.IsDetailActive;

    public int? ActiveDetailModId => _coordinator.ActiveDetailModId;

    public PluginWorkshopCoordinator Coordinator => _coordinator;

    public ModListPage LocalPage => _localPage;

    public CommunityPage CommunityPage => _communityPage;

    public event EventHandler<WorkshopSubDomain>? SubDomainChanged;
    public event EventHandler<bool>? DetailStateChanged;

    #endregion

    #region 页面加载与初始化

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 挂载两个子页面实例到各自的 Frame 容器中
        if (LocalHost.Content is null)
        {
            LocalHost.Navigate(_localPage);
        }

        if (CommunityHost.Content is null)
        {
            CommunityHost.Navigate(_communityPage);
        }

        UpdateViewVisibility();
    }

    #endregion

    #region 顶部双子域切换与导航核心

    private void SubDomainLocalBtn_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(WorkshopSubDomain.LocalInstalled);
    }

    private void SubDomainCommunityBtn_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(WorkshopSubDomain.CommunityDiscovery);
    }

    public NavigationChangeResult NavigateTo(WorkshopSubDomain target)
    {
        return NavigateTo(target, null);
    }

    public NavigationChangeResult NavigateTo(WorkshopSubDomain target, NavigationContext? context)
    {
        // 1. 在离开当前视图前，精准捕获当前子域的交互快照
        CaptureCurrentViewSnapshot();

        // 2. 调用状态机执行子域转换
        var result = _coordinator.NavigateTo(target, context);

        // 3. 同步更新宿主可见性与按钮状态
        UpdateViewVisibility();

        // 4. 恢复进入目标子域的交互快照
        RestoreTargetViewSnapshot(target);

        return result;
    }

    public bool ShowDetail(int modId, WorkshopSubDomain? sourceSubDomain = null, NavigationContext? sourceContext = null)
    {
        if (modId <= 0)
        {
            return false;
        }

        // 1. 捕获进入详情前的源视图快照
        CaptureCurrentViewSnapshot();

        var resolvedSource = sourceSubDomain ?? _coordinator.CurrentSubDomain;
        var shown = _coordinator.ShowDetail(modId, resolvedSource, sourceContext);
        if (!shown)
        {
            return false;
        }

        // 2. 构造详情页实例并挂接自治返回委托
        var origin = new NavigationOrigin(
            resolvedSource == WorkshopSubDomain.LocalInstalled ? "返回本地插件" : "返回插件社区",
            PrimarySlot.Mods,
            resolvedSource,
            sourceContext);

        _detailPage = new CommunityDetailPage(modId, origin, () => CloseDetail());
        DetailHost.Navigate(_detailPage);

        // 3. 切换视图状态为详情激活
        UpdateViewVisibility();
        return true;
    }

    public bool CloseDetail()
    {
        if (!_coordinator.IsDetailActive)
        {
            return false;
        }

        var sourceSubDomain = _coordinator.DetailSourceSubDomain;
        var closed = _coordinator.CloseDetail();
        if (!closed)
        {
            return false;
        }

        // 卸载详情页内容
        DetailHost.Content = null;
        _detailPage = null;

        // 恢复源子视图可见性
        UpdateViewVisibility();

        // 精准恢复返回子域的交互快照
        RestoreTargetViewSnapshot(sourceSubDomain);
        return true;
    }

    public NavigationChangeResult HandleNavigationRequest(PageNavigationRequest request)
    {
        if (request is null || request.TargetSlot != PrimarySlot.Mods)
        {
            return NavigationChangeResult.Rejected;
        }

        var targetSubDomain = request.SubDomain ?? _coordinator.CurrentSubDomain;
        return NavigateTo(targetSubDomain, request.Context);
    }

    #endregion

    #region 单活跃视图物理隔离与焦点管理

    private void UpdateViewVisibility()
    {
        var view = _coordinator.ActiveView;

        if (view == WorkshopActiveView.Detail)
        {
            // 详情态：隐藏顶部双子域切换栏，给予详情页面完整的沉浸展示空间
            HeaderArea.Visibility = Visibility.Collapsed;
            DetailHost.Visibility = Visibility.Visible;

            // 未激活子视图严格 Collapsed，彻底移出命中测试、Tab 导航与可访问性树
            LocalHost.Visibility = Visibility.Collapsed;
            CommunityHost.Visibility = Visibility.Collapsed;

            DetailHost.Focus();
        }
        else if (view == WorkshopActiveView.LocalInstalled)
        {
            HeaderArea.Visibility = Visibility.Visible;
            LocalHost.Visibility = Visibility.Visible;

            CommunityHost.Visibility = Visibility.Collapsed;
            DetailHost.Visibility = Visibility.Collapsed;

            UpdateSubDomainButtonStyles(isLocalActive: true);
        }
        else
        {
            HeaderArea.Visibility = Visibility.Visible;
            CommunityHost.Visibility = Visibility.Visible;

            LocalHost.Visibility = Visibility.Collapsed;
            DetailHost.Visibility = Visibility.Collapsed;

            UpdateSubDomainButtonStyles(isLocalActive: false);
        }
    }

    private void UpdateSubDomainButtonStyles(bool isLocalActive)
    {
        if (isLocalActive)
        {
            SubDomainLocalBtn.Appearance = ControlAppearance.Primary;
            SubDomainLocalBtn.Foreground = (System.Windows.Media.Brush)FindResource("TextOnAccentFillColorPrimaryBrush");

            SubDomainCommunityBtn.Appearance = ControlAppearance.Secondary;
            SubDomainCommunityBtn.ClearValue(ForegroundProperty);
        }
        else
        {
            SubDomainLocalBtn.Appearance = ControlAppearance.Secondary;
            SubDomainLocalBtn.ClearValue(ForegroundProperty);

            SubDomainCommunityBtn.Appearance = ControlAppearance.Primary;
            SubDomainCommunityBtn.Foreground = (System.Windows.Media.Brush)FindResource("TextOnAccentFillColorPrimaryBrush");
        }
    }

    #endregion

    #region 上下文快照保全与恢复实现

    private void CaptureCurrentViewSnapshot()
    {
        if (_coordinator.IsDetailActive)
        {
            return;
        }

        if (_coordinator.CurrentSubDomain == WorkshopSubDomain.LocalInstalled)
        {
            var offset = GetScrollViewerOffset(LocalHost);
            _coordinator.CaptureLocalSnapshot(offset);
        }
        else
        {
            var offset = GetScrollViewerOffset(CommunityHost);
            var query = _communityPage.ViewModel.SearchText;
            var category = _communityPage.ViewModel.SelectedCategory?.Key;
            var sort = _communityPage.ViewModel.Sort;
            _coordinator.CaptureCommunitySnapshot(query, category, sort, offset);
        }
    }

    private void RestoreTargetViewSnapshot(WorkshopSubDomain target)
    {
        if (target == WorkshopSubDomain.LocalInstalled)
        {
            if (_coordinator.LocalSnapshot is { } localSnap && localSnap.ScrollOffset > 0)
            {
                ApplyScrollOffset(LocalHost, localSnap.ScrollOffset);
            }
        }
        else
        {
            if (_coordinator.CommunitySnapshot is { } commSnap)
            {
                if (commSnap.SearchQuery is not null && _communityPage.ViewModel.SearchText != commSnap.SearchQuery)
                {
                    _communityPage.ViewModel.SearchText = commSnap.SearchQuery;
                }

                if (commSnap.ScrollOffset > 0)
                {
                    ApplyScrollOffset(CommunityHost, commSnap.ScrollOffset);
                }
            }
        }
    }

    private static double GetScrollViewerOffset(FrameworkElement container)
    {
        var viewer = FindChildScrollViewer(container);
        return viewer?.VerticalOffset ?? 0.0;
    }

    private void ApplyScrollOffset(FrameworkElement container, double offset)
    {
        if (offset <= 0) return;

        void Scroll()
        {
            var viewer = FindChildScrollViewer(container);
            viewer?.ScrollToVerticalOffset(offset);
        }

        // 立即尝试恢复
        Scroll();

        // 在布局排版完成后再次确保定位精准还原
        Dispatcher.BeginInvoke(new Action(Scroll), DispatcherPriority.Loaded);
    }

    private static ScrollViewer? FindChildScrollViewer(DependencyObject? element)
    {
        if (element is null) return null;
        if (element is ScrollViewer sv) return sv;

        int count = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            var found = FindChildScrollViewer(child);
            if (found is not null) return found;
        }

        return null;
    }

    #endregion

    #region 事件转发与回调处理

    private void OnLocalOpenCommunityRequested(int id)
    {
        ShowDetail(id, WorkshopSubDomain.LocalInstalled);
    }

    private void OnCommunityOpenModRequested(int id)
    {
        ShowDetail(id, WorkshopSubDomain.CommunityDiscovery);
    }

    private void OnSubDomainChanged(object? sender, WorkshopSubDomain subDomain)
    {
        SubDomainChanged?.Invoke(this, subDomain);
    }

    private void OnDetailStateChanged(object? sender, bool isDetail)
    {
        DetailStateChanged?.Invoke(this, isDetail);
    }

    private void OnCommunitySearchQueryApplied(string query)
    {
        _communityPage.ViewModel.SearchText = query;
    }

    private void Page_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollWheelRouter.RouteToNearestScrollViewer(sender, e);
    }

    #endregion
}
