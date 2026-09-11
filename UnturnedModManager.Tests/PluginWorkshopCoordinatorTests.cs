using System;
using UnturnedModManager.Navigation;
using Xunit;

namespace UnturnedModManager.Tests;

/// <summary>
/// Ticket C: PluginWorkshopCoordinator 模组工坊纯导航状态机契约单元测试
/// 针对 Ticket C 验收标准：
/// 1. 验证默认初始状态为 LocalInstalled，非详情态，单活跃视图为 LocalInstalled
/// 2. 验证幂等性：重复导航至当前激活子域返回 AlreadyActive
/// 3. 验证非法目标拦截：传入未定义枚举值返回 InvalidTarget
/// 4. 验证子域互斥切换与 SubDomainChanged 事件精准触发
/// 5. 验证模组详情自治打开 (ShowDetail)：设置 ActiveDetailModId 与 ActiveView=Detail
/// 6. 验证模组详情防御性参数防守 (modId <= 0 返回 false)
/// 7. 验证模组详情自治关闭 (CloseDetail)：精准恢复进入详情前的源子域与视图状态
/// 8. 验证在详情态下调用 NavigateTo 自动关闭详情并激活目标子域
/// 9. 验证携带 PluginDetailContext 时就地自治打开详情
/// 10. 验证携带 CommunitySearchContext 时更新暂存并触发搜索应用
/// 11. 验证单活跃视图不变量：同一时刻仅一个视图为活跃态
/// 12. 验证上下文快照暂存 (CaptureLocalSnapshot / CaptureCommunitySnapshot) 与隔离恢复
/// 13. 验证 HandleNavigationRequest 槽位校验与意图分发
/// </summary>
public sealed class PluginWorkshopCoordinatorTests
{
    [Fact]
    public void Constructor_DefaultState_IsLocalInstalledAndNotDetail()
    {
        var coordinator = new PluginWorkshopCoordinator();

        Assert.Equal(WorkshopSubDomain.LocalInstalled, coordinator.CurrentSubDomain);
        Assert.Equal(WorkshopActiveView.LocalInstalled, coordinator.ActiveView);
        Assert.False(coordinator.IsDetailActive);
        Assert.Null(coordinator.ActiveDetailModId);
        Assert.Null(coordinator.LocalSnapshot);
        Assert.Null(coordinator.CommunitySnapshot);
    }

    [Theory]
    [InlineData(WorkshopSubDomain.LocalInstalled, WorkshopActiveView.LocalInstalled)]
    [InlineData(WorkshopSubDomain.CommunityDiscovery, WorkshopActiveView.CommunityDiscovery)]
    public void Constructor_WithExplicitSubDomain_InitializesCorrectly(
        WorkshopSubDomain initialSubDomain,
        WorkshopActiveView expectedView)
    {
        var coordinator = new PluginWorkshopCoordinator(initialSubDomain);

        Assert.Equal(initialSubDomain, coordinator.CurrentSubDomain);
        Assert.Equal(expectedView, coordinator.ActiveView);
        Assert.False(coordinator.IsDetailActive);
    }

    [Fact]
    public void Constructor_WithInvalidSubDomain_DefaultsToLocalInstalled()
    {
        var coordinator = new PluginWorkshopCoordinator((WorkshopSubDomain)999);

        Assert.Equal(WorkshopSubDomain.LocalInstalled, coordinator.CurrentSubDomain);
        Assert.Equal(WorkshopActiveView.LocalInstalled, coordinator.ActiveView);
    }

    [Fact]
    public void NavigateTo_AlreadyActive_ReturnsAlreadyActiveAndDoesNotFireEvents()
    {
        var coordinator = new PluginWorkshopCoordinator(WorkshopSubDomain.LocalInstalled);
        int eventCount = 0;
        coordinator.SubDomainChanged += (_, _) => eventCount++;

        var result = coordinator.NavigateTo(WorkshopSubDomain.LocalInstalled);

        Assert.Equal(NavigationChangeResult.AlreadyActive, result);
        Assert.Equal(0, eventCount);
        Assert.Equal(WorkshopSubDomain.LocalInstalled, coordinator.CurrentSubDomain);
    }

    [Fact]
    public void NavigateTo_InvalidTarget_ReturnsInvalidTarget()
    {
        var coordinator = new PluginWorkshopCoordinator();

        var result = coordinator.NavigateTo((WorkshopSubDomain)12345);

        Assert.Equal(NavigationChangeResult.InvalidTarget, result);
        Assert.Equal(WorkshopSubDomain.LocalInstalled, coordinator.CurrentSubDomain);
    }

    [Fact]
    public void NavigateTo_SubDomainSwitch_ReturnsChangedAndFiresSubDomainChangedEvent()
    {
        var coordinator = new PluginWorkshopCoordinator(WorkshopSubDomain.LocalInstalled);
        WorkshopSubDomain? receivedSubDomain = null;
        coordinator.SubDomainChanged += (_, subDomain) => receivedSubDomain = subDomain;

        var result = coordinator.NavigateTo(WorkshopSubDomain.CommunityDiscovery);

        Assert.Equal(NavigationChangeResult.Changed, result);
        Assert.Equal(WorkshopSubDomain.CommunityDiscovery, coordinator.CurrentSubDomain);
        Assert.Equal(WorkshopActiveView.CommunityDiscovery, coordinator.ActiveView);
        Assert.Equal(WorkshopSubDomain.CommunityDiscovery, receivedSubDomain);
    }

    [Fact]
    public void ShowDetail_ValidModId_SetsDetailActiveAndFiresDetailStateChanged()
    {
        var coordinator = new PluginWorkshopCoordinator(WorkshopSubDomain.LocalInstalled);
        bool? detailState = null;
        coordinator.DetailStateChanged += (_, state) => detailState = state;

        var shown = coordinator.ShowDetail(101, WorkshopSubDomain.LocalInstalled);

        Assert.True(shown);
        Assert.True(coordinator.IsDetailActive);
        Assert.Equal(101, coordinator.ActiveDetailModId);
        Assert.Equal(WorkshopActiveView.Detail, coordinator.ActiveView);
        Assert.Equal(WorkshopSubDomain.LocalInstalled, coordinator.DetailSourceSubDomain);
        Assert.True(detailState);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public void ShowDetail_InvalidModId_ReturnsFalseAndPreservesState(int invalidModId)
    {
        var coordinator = new PluginWorkshopCoordinator(WorkshopSubDomain.LocalInstalled);

        var shown = coordinator.ShowDetail(invalidModId);

        Assert.False(shown);
        Assert.False(coordinator.IsDetailActive);
        Assert.Null(coordinator.ActiveDetailModId);
        Assert.Equal(WorkshopActiveView.LocalInstalled, coordinator.ActiveView);
    }

    [Fact]
    public void ShowDetail_IdempotentSameMod_DoesNotDuplicateEvents()
    {
        var coordinator = new PluginWorkshopCoordinator();
        coordinator.ShowDetail(42);

        int eventCount = 0;
        coordinator.DetailStateChanged += (_, _) => eventCount++;

        var shown = coordinator.ShowDetail(42);

        Assert.True(shown);
        Assert.Equal(0, eventCount);
        Assert.Equal(42, coordinator.ActiveDetailModId);
    }

    [Fact]
    public void CloseDetail_WhenActive_ReturnsTrueRestoresSourceSubDomainAndFiresEvents()
    {
        var coordinator = new PluginWorkshopCoordinator(WorkshopSubDomain.LocalInstalled);
        coordinator.NavigateTo(WorkshopSubDomain.CommunityDiscovery);
        coordinator.ShowDetail(202, WorkshopSubDomain.CommunityDiscovery);

        bool detailClosed = false;
        WorkshopSubDomain? restoredSubDomain = null;
        coordinator.DetailStateChanged += (_, state) => { if (!state) detailClosed = true; };
        coordinator.SubDomainChanged += (_, subDomain) => restoredSubDomain = subDomain;

        var closed = coordinator.CloseDetail();

        Assert.True(closed);
        Assert.False(coordinator.IsDetailActive);
        Assert.Null(coordinator.ActiveDetailModId);
        Assert.Equal(WorkshopSubDomain.CommunityDiscovery, coordinator.CurrentSubDomain);
        Assert.Equal(WorkshopActiveView.CommunityDiscovery, coordinator.ActiveView);
        Assert.True(detailClosed);
        Assert.Equal(WorkshopSubDomain.CommunityDiscovery, restoredSubDomain);
    }

    [Fact]
    public void CloseDetail_WhenNotActive_ReturnsFalse()
    {
        var coordinator = new PluginWorkshopCoordinator();

        var closed = coordinator.CloseDetail();

        Assert.False(closed);
        Assert.False(coordinator.IsDetailActive);
    }

    [Fact]
    public void NavigateTo_WhileInDetail_ClosesDetailAndNavigatesToTarget()
    {
        var coordinator = new PluginWorkshopCoordinator(WorkshopSubDomain.LocalInstalled);
        coordinator.ShowDetail(77);
        Assert.True(coordinator.IsDetailActive);

        var result = coordinator.NavigateTo(WorkshopSubDomain.CommunityDiscovery);

        Assert.Equal(NavigationChangeResult.Changed, result);
        Assert.False(coordinator.IsDetailActive);
        Assert.Null(coordinator.ActiveDetailModId);
        Assert.Equal(WorkshopSubDomain.CommunityDiscovery, coordinator.CurrentSubDomain);
        Assert.Equal(WorkshopActiveView.CommunityDiscovery, coordinator.ActiveView);
    }

    [Fact]
    public void NavigateTo_WithPluginDetailContext_OpensDetailAutonomouslyAndFiresEvents()
    {
        var coordinator = new PluginWorkshopCoordinator(WorkshopSubDomain.LocalInstalled);
        bool? detailState = null;
        WorkshopSubDomain? changedSubDomain = null;

        coordinator.DetailStateChanged += (_, state) => detailState = state;
        coordinator.SubDomainChanged += (_, sub) => changedSubDomain = sub;

        var result = coordinator.NavigateTo(
            WorkshopSubDomain.CommunityDiscovery,
            new PluginDetailContext("88"));

        Assert.Equal(NavigationChangeResult.Changed, result);
        Assert.True(coordinator.IsDetailActive);
        Assert.Equal(88, coordinator.ActiveDetailModId);
        Assert.Equal(WorkshopActiveView.Detail, coordinator.ActiveView);
        Assert.Equal(WorkshopSubDomain.CommunityDiscovery, coordinator.CurrentSubDomain);
        Assert.True(detailState);
        Assert.Equal(WorkshopSubDomain.CommunityDiscovery, changedSubDomain);
    }

    [Fact]
    public void NavigateTo_WithInvalidPluginDetailContext_ReturnsRejected()
    {
        var coordinator = new PluginWorkshopCoordinator(WorkshopSubDomain.LocalInstalled);

        var result = coordinator.NavigateTo(
            WorkshopSubDomain.CommunityDiscovery,
            new PluginDetailContext("invalid-id"));

        Assert.Equal(NavigationChangeResult.Rejected, result);
        Assert.False(coordinator.IsDetailActive);
    }

    [Fact]
    public void NavigateTo_WithCommunitySearchContext_AppliesSearchQuery()
    {
        var coordinator = new PluginWorkshopCoordinator(WorkshopSubDomain.LocalInstalled);
        string? appliedQuery = null;
        coordinator.CommunitySearchQueryApplied += q => appliedQuery = q;

        var result = coordinator.NavigateTo(
            WorkshopSubDomain.CommunityDiscovery,
            new CommunitySearchContext("VehiclePlus"));

        Assert.Equal(NavigationChangeResult.Changed, result);
        Assert.Equal("VehiclePlus", appliedQuery);
        Assert.NotNull(coordinator.CommunitySnapshot);
        Assert.Equal("VehiclePlus", coordinator.CommunitySnapshot.SearchQuery);
    }

    [Fact]
    public void ContextSnapshot_CaptureAndIsolation_WorksCorrectly()
    {
        var coordinator = new PluginWorkshopCoordinator();

        // 1. 捕获本地列表滚动位移与配置方案 ID
        coordinator.CaptureLocalSnapshot(142.5, "profile-alpha");
        Assert.NotNull(coordinator.LocalSnapshot);
        Assert.Equal(142.5, coordinator.LocalSnapshot.ScrollOffset);
        Assert.Equal("profile-alpha", coordinator.LocalSnapshot.SelectedProfileId);

        // 2. 捕获社区列表状态（搜索词、分类、排序、滚动位移）
        coordinator.CaptureCommunitySnapshot("Rocket", "mechanics", "downloads", 320.0);
        Assert.NotNull(coordinator.CommunitySnapshot);
        Assert.Equal("Rocket", coordinator.CommunitySnapshot.SearchQuery);
        Assert.Equal("mechanics", coordinator.CommunitySnapshot.CategoryKey);
        Assert.Equal("downloads", coordinator.CommunitySnapshot.Sort);
        Assert.Equal(320.0, coordinator.CommunitySnapshot.ScrollOffset);

        // 3. 验证两者隔离：修改社区快照不影响本地快照
        coordinator.CaptureCommunitySnapshot("Economy", null, "newest", 0.0);
        Assert.Equal(142.5, coordinator.LocalSnapshot.ScrollOffset);
        Assert.Equal("profile-alpha", coordinator.LocalSnapshot.SelectedProfileId);
        Assert.Equal("Economy", coordinator.CommunitySnapshot.SearchQuery);

        // 4. 清理快照
        coordinator.ClearSnapshots();
        Assert.Null(coordinator.LocalSnapshot);
        Assert.Null(coordinator.CommunitySnapshot);
    }

    [Fact]
    public void SingleActiveView_Invariant_OnlyOneViewActiveAtAnyTime()
    {
        var coordinator = new PluginWorkshopCoordinator(WorkshopSubDomain.LocalInstalled);

        // 状态 1: LocalInstalled
        Assert.Equal(WorkshopActiveView.LocalInstalled, coordinator.ActiveView);
        Assert.False(coordinator.IsDetailActive);

        // 状态 2: CommunityDiscovery
        coordinator.NavigateTo(WorkshopSubDomain.CommunityDiscovery);
        Assert.Equal(WorkshopActiveView.CommunityDiscovery, coordinator.ActiveView);
        Assert.False(coordinator.IsDetailActive);

        // 状态 3: Detail
        coordinator.ShowDetail(55);
        Assert.Equal(WorkshopActiveView.Detail, coordinator.ActiveView);
        Assert.True(coordinator.IsDetailActive);

        // 状态 4: 返回恢复
        coordinator.CloseDetail();
        Assert.Equal(WorkshopActiveView.CommunityDiscovery, coordinator.ActiveView);
        Assert.False(coordinator.IsDetailActive);
    }

    [Fact]
    public void HandleNavigationRequest_ModsSlotWithDetail_OpensDetail()
    {
        var coordinator = new PluginWorkshopCoordinator();

        var request = new PageNavigationRequest(
            PrimarySlot.Mods,
            WorkshopSubDomain.LocalInstalled,
            new PluginDetailContext("99"));

        var result = coordinator.HandleNavigationRequest(request);

        Assert.Equal(NavigationChangeResult.Changed, result);
        Assert.True(coordinator.IsDetailActive);
        Assert.Equal(99, coordinator.ActiveDetailModId);
    }

    [Fact]
    public void HandleNavigationRequest_NonModsSlot_ReturnsRejected()
    {
        var coordinator = new PluginWorkshopCoordinator();

        var request = new PageNavigationRequest(PrimarySlot.Play);

        var result = coordinator.HandleNavigationRequest(request);

        Assert.Equal(NavigationChangeResult.Rejected, result);
    }

    [Fact]
    public void HandleNavigationRequest_NullRequest_ReturnsRejected()
    {
        var coordinator = new PluginWorkshopCoordinator();

        var result = coordinator.HandleNavigationRequest(null!);

        Assert.Equal(NavigationChangeResult.Rejected, result);
    }
}
