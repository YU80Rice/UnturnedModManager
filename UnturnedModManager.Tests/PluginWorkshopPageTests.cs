using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UnturnedModManager.Helpers;
using UnturnedModManager.Navigation;
using UnturnedModManager.Pages;
using Wpf.Ui.Controls;
using Xunit;

namespace UnturnedModManager.Tests;

/// <summary>
/// Ticket C: PluginWorkshopPage 模组工坊纯导航聚合外壳与单活跃视图契约测试
/// 针对 Ticket C 验收标准：
/// 1. 验证具备本地插件与社区发现双子域切换导航栏
/// 2. 验证单一容器内挂载 ModListPage 与 CommunityPage，以 Visibility 控制活跃态
/// 3. 验证未激活视图设为 Collapsed，移出焦点树与命中测试
/// 4. 验证 Slot 3 自治消化社区插件详情展示 (ShowDetail) 与返回 (CloseDetail)
/// 5. 验证子域切换状态隔离与单活跃视图契约
/// </summary>
[Collection("WpfThemeCollection")]
public sealed class PluginWorkshopPageTests
{
    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = Application.Current ?? new Application();
                EnsureTestResources(app);
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null)
        {
            throw new Exception("STA test execution failed", exception);
        }
    }

    private static void EnsureTestResources(Application app)
    {
        if (!app.Resources.Contains("BoolToVisibility"))
        {
            app.Resources["BoolToVisibility"] = new BooleanToVisibilityConverter();
        }

        if (!app.Resources.Contains("InverseBoolToVisibility"))
        {
            app.Resources["InverseBoolToVisibility"] = new InverseBooleanToVisibilityConverter();
        }

        if (!app.Resources.Contains("TextOnAccentFillColorPrimaryBrush"))
        {
            app.Resources["TextOnAccentFillColorPrimaryBrush"] = Brushes.White;
        }

        if (!app.Resources.Contains("ControlFillColorDefaultBrush"))
        {
            app.Resources["ControlFillColorDefaultBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0x20, 0x27));
        }

        if (!app.Resources.Contains("ControlStrokeColorDefaultBrush"))
        {
            app.Resources["ControlStrokeColorDefaultBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x2A, 0x34, 0x41));
        }

        if (!app.Resources.Contains("TextFillColorPrimaryBrush"))
        {
            app.Resources["TextFillColorPrimaryBrush"] = Brushes.White;
        }

        if (!app.Resources.Contains("TextFillColorSecondaryBrush"))
        {
            app.Resources["TextFillColorSecondaryBrush"] = Brushes.LightGray;
        }
    }

    [Theory]
    [InlineData(820, 1)]
    [InlineData(821, 2)]
    [InlineData(1100, 2)]
    [InlineData(1101, 3)]
    public void CommunityCards_UseSpecifiedViewportBreakpoints(int width, int expectedColumns) =>
        Assert.Equal(expectedColumns, CommunityPage.GetColumnCount(width));

    [Fact]
    public void Page_Instantiation_InitializesWithSingleActiveLocalView()
    {
        RunOnSta(() =>
        {
            var page = new PluginWorkshopPage();

            Assert.Equal(WorkshopSubDomain.LocalInstalled, page.CurrentSubDomain);
            Assert.Equal(WorkshopActiveView.LocalInstalled, page.ActiveView);
            Assert.False(page.IsDetailActive);
            Assert.Null(page.ActiveDetailModId);

            Assert.NotNull(page.LocalPage);
            Assert.NotNull(page.CommunityPage);
            Assert.NotNull(page.Coordinator);

            // 契约断言：初始态 LocalHost 处于激活态，其他视图必须 Collapsed
            Assert.Equal(Visibility.Visible, page.LocalHost.Visibility);
            Assert.Equal(Visibility.Collapsed, page.CommunityHost.Visibility);
            Assert.Equal(Visibility.Collapsed, page.DetailHost.Visibility);
        });
    }

    [Fact]
    public void Page_NavigateToCommunity_TogglesVisibilityAndIsolatesFocus()
    {
        RunOnSta(() =>
        {
            var page = new PluginWorkshopPage();

            var result = page.NavigateTo(WorkshopSubDomain.CommunityDiscovery);

            Assert.Equal(NavigationChangeResult.Changed, result);
            Assert.Equal(WorkshopSubDomain.CommunityDiscovery, page.CurrentSubDomain);
            Assert.Equal(WorkshopActiveView.CommunityDiscovery, page.ActiveView);

            // 契约断言：切换到社区时，社区宿主 Visible，本地与详情宿主 Collapsed
            Assert.Equal(Visibility.Collapsed, page.LocalHost.Visibility);
            Assert.Equal(Visibility.Visible, page.CommunityHost.Visibility);
            Assert.Equal(Visibility.Collapsed, page.DetailHost.Visibility);
        });
    }

    [Fact]
    public void Page_NavigateToSameSubDomain_IsIdempotent()
    {
        RunOnSta(() =>
        {
            var page = new PluginWorkshopPage();

            var result = page.NavigateTo(WorkshopSubDomain.LocalInstalled);

            Assert.Equal(NavigationChangeResult.AlreadyActive, result);
            Assert.Equal(Visibility.Visible, page.LocalHost.Visibility);
            Assert.Equal(Visibility.Collapsed, page.CommunityHost.Visibility);
        });
    }

    [Fact]
    public void Page_ShowDetail_ActivatesDetailAndCollapsesSubPages()
    {
        RunOnSta(() =>
        {
            var page = new PluginWorkshopPage();

            var shown = page.ShowDetail(108, WorkshopSubDomain.LocalInstalled);

            Assert.True(shown);
            Assert.True(page.IsDetailActive);
            Assert.Equal(108, page.ActiveDetailModId);
            Assert.Equal(WorkshopActiveView.Detail, page.ActiveView);

            // 契约断言：详情激活时，单活跃视图为 DetailHost，本地与社区完全 Collapsed
            Assert.Equal(Visibility.Visible, page.DetailHost.Visibility);
            Assert.Equal(Visibility.Collapsed, page.LocalHost.Visibility);
            Assert.Equal(Visibility.Collapsed, page.CommunityHost.Visibility);
            Assert.Equal(Visibility.Collapsed, page.HeaderArea.Visibility);
        });
    }

    [Fact]
    public void Page_CloseDetail_RestoresSourceSubDomainAndHeader()
    {
        RunOnSta(() =>
        {
            var page = new PluginWorkshopPage();
            page.NavigateTo(WorkshopSubDomain.CommunityDiscovery);
            page.ShowDetail(205, WorkshopSubDomain.CommunityDiscovery);

            Assert.True(page.IsDetailActive);

            var closed = page.CloseDetail();

            Assert.True(closed);
            Assert.False(page.IsDetailActive);
            Assert.Null(page.ActiveDetailModId);
            Assert.Equal(WorkshopSubDomain.CommunityDiscovery, page.CurrentSubDomain);

            // 契约断言：关闭详情后恢复源子域与标题切换栏
            Assert.Equal(Visibility.Visible, page.HeaderArea.Visibility);
            Assert.Equal(Visibility.Visible, page.CommunityHost.Visibility);
            Assert.Equal(Visibility.Collapsed, page.LocalHost.Visibility);
            Assert.Equal(Visibility.Collapsed, page.DetailHost.Visibility);
        });
    }

    [Fact]
    public void Page_HandleNavigationRequest_DispatchesCorrectly()
    {
        RunOnSta(() =>
        {
            var page = new PluginWorkshopPage();

            var request = new PageNavigationRequest(
                PrimarySlot.Mods,
                WorkshopSubDomain.CommunityDiscovery,
                new PluginDetailContext("77"));

            var result = page.HandleNavigationRequest(request);

            Assert.Equal(NavigationChangeResult.Changed, result);
            Assert.True(page.IsDetailActive);
            Assert.Equal(77, page.ActiveDetailModId);
            Assert.Equal(Visibility.Visible, page.DetailHost.Visibility);
            Assert.Equal(Visibility.Collapsed, page.LocalHost.Visibility);
            Assert.Equal(Visibility.Collapsed, page.CommunityHost.Visibility);
        });
    }

    [Fact]
    public void Page_ContentHostStructure_UsesFrameHostsRequiredByPageParentRule()
    {
        RunOnSta(() =>
        {
            var page = new PluginWorkshopPage();

            // 契约断言 [P1]：WPF 规定 Page 只能以 Window 或 Frame 为父级，违规时在真实布局期抛
            // InvalidOperationException("Page 只能具有 Window 或 Frame 父级")（栈经 ContentPresenter.BuildVisualTree）。
            // ModListPage / CommunityPage / CommunityDetailPage 都是 Page，因此宿主必须是 Frame；
            // 历史实现用 ContentControl 承载，发布版一进模组工坊即崩溃。
            var localHost = Assert.IsType<Frame>(page.LocalHost);
            var communityHost = Assert.IsType<Frame>(page.CommunityHost);
            var detailHost = Assert.IsType<Frame>(page.DetailHost);

            // 仍然不产生多 Journal：只赋 Content、从不调用 Navigate，导航 UI 也隐藏。
            Assert.Equal(System.Windows.Navigation.NavigationUIVisibility.Hidden, localHost.NavigationUIVisibility);
            Assert.Equal(System.Windows.Navigation.NavigationUIVisibility.Hidden, communityHost.NavigationUIVisibility);
            Assert.Equal(System.Windows.Navigation.NavigationUIVisibility.Hidden, detailHost.NavigationUIVisibility);

            // 子页面内容在首次激活时才挂载，因此这里只断言详情宿主初始为空，
            // 不强制 LocalHost/CommunityHost 在构造后立即带内容。
            Assert.Null(detailHost.Content);
            Assert.True(localHost.Content is null || ReferenceEquals(localHost.Content, page.LocalPage));
            Assert.True(communityHost.Content is null || ReferenceEquals(communityHost.Content, page.CommunityPage));
        });
    }

    [Fact]
    public void Page_SubPagesCustomOpenDetailAction_WiredDirectlyToSingleOwnerShowDetail()
    {
        RunOnSta(() =>
        {
            var page = new PluginWorkshopPage();

            // 契约断言 [P1]：子页面 CustomOpenDetailAction 必须由外壳单向接管，杜绝外部多重监听与双重导航
            Assert.NotNull(page.LocalPage.CustomOpenDetailAction);
            Assert.NotNull(page.CommunityPage.CustomOpenDetailAction);

            // 模拟触发子页面的详情导航委托
            page.CommunityPage.CustomOpenDetailAction!(456);

            Assert.True(page.IsDetailActive);
            Assert.Equal(456, page.ActiveDetailModId);
            Assert.Equal(WorkshopActiveView.Detail, page.ActiveView);
            Assert.Equal(WorkshopSubDomain.CommunityDiscovery, page.Coordinator.DetailSourceSubDomain);
            Assert.Equal(Visibility.Visible, page.DetailHost.Visibility);
            Assert.Equal(Visibility.Collapsed, page.CommunityHost.Visibility);
        });
    }

    [Fact]
    public void Page_HandleNavigationRequest_RejectsNonModsSlot()
    {
        RunOnSta(() =>
        {
            var page = new PluginWorkshopPage();

            var request = new PageNavigationRequest(PrimarySlot.Settings);
            var result = page.HandleNavigationRequest(request);

            Assert.Equal(NavigationChangeResult.Rejected, result);
        });
    }
}
