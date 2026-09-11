using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnturnedModManager.Navigation;
using UnturnedModManager.Services;
using Xunit;

namespace UnturnedModManager.Tests;

/// <summary>
/// Ticket B: Legacy NavigationFacade 兼容桥与无底层 UI 泄漏请求契约测试
/// 针对 Ticket B 验收标准与架构规范（包含审查意见修复）：
/// 1. 验证默认无活动宿主挂载，严禁隐式创建第二套导航状态机
/// 2. 验证未挂载宿主时所有导航操作返回受控失败 (Rejected)，ActiveSlot 抛出明确异常绝不伪装成 Play
/// 3. 验证 AttachHost / DetachHost 生命周期与置空语义
/// 4. 验证 ActiveSlot, ActiveSecondary, CanNavigateBack 状态与挂载宿主双向同步（单一事实源）
/// 5. 验证 NavigateTo, NavigateToSecondary, NavigateBack 直接原子委托与宿主状态一致性
/// 6. 验证 OpenCommunityDetail 强类型结构化请求转发与子域映射 (CommunityDiscovery vs LocalInstalled)
/// 7. 验证 OpenCommunityDetail 防御性异常参数拦截 (null 拒收, ModId <= 0 InvalidTarget)
/// 8. 验证 ReturnToOrigin 细分场景：次级返回、结构化来源恢复与安全保底回退
/// 9. 验证既有页面便捷导航方法在挂载宿主下的正确调度
/// 10. 验证 StateChanged 集中派生事件中继与宿主切换时的事件生命周期重绑
/// 11. 验证 AttachHost(null) 抛出 ArgumentNullException 防守
/// 12. 验证并发 Attach/Detach 与 Navigate 竞态安全与原子宿主捕获
/// </summary>
public sealed class LegacyNavigationFacadeAdapterTests : IDisposable
{
    public LegacyNavigationFacadeAdapterTests()
    {
        AppNavigationService.Current.ResetForTesting();
    }

    public void Dispose()
    {
        AppNavigationService.Current.ResetForTesting();
    }

    [Fact]
    public void Constructor_DefaultHost_IsNullAndHasNoImplicitStateMachine()
    {
        var facade = new AppNavigationService();

        Assert.Null(facade.Host);
        Assert.False(facade.HasHost);
        Assert.Null(facade.ActiveSecondary);
        Assert.False(facade.CanNavigateBack);
        Assert.Null(facade.ActiveSlotOrNull);
        Assert.Throws<InvalidOperationException>(() => facade.ActiveSlot);
    }

    [Fact]
    public void Operations_WhenUnattached_ReturnControlledRejectionAndDoNotCreateHistory()
    {
        var facade = new AppNavigationService();

        // 所有导航操作在无宿主时严格返回受控失败，不创建任何隐式历史或状态
        Assert.Equal(NavigationResult.Rejected, facade.NavigateTo(new PageNavigationRequest(PrimarySlot.Play)));
        Assert.Equal(NavigationResult.Rejected, facade.NavigateToSecondary(SecondaryNavigationTarget.TaskCenter));
        Assert.Equal(NavigationResult.Rejected, facade.NavigateBack());
        Assert.Equal(NavigationResult.Rejected, facade.OpenCommunityDetail(1001));
        Assert.Equal(NavigationResult.Rejected, facade.ReturnToOrigin());
        Assert.Equal(NavigationResult.Rejected, facade.NavigateToPlay());
        Assert.Equal(NavigationResult.Rejected, facade.NavigateToSettings());

        Assert.False(facade.HasHost);
        Assert.Null(facade.Host);
    }

    [Fact]
    public void AttachHost_ValidHost_SwitchesDelegationTarget()
    {
        var customHost = new NavigationShellHost(initialSlot: PrimarySlot.Settings);
        var facade = new AppNavigationService();

        facade.AttachHost(customHost);

        Assert.Same(customHost, facade.Host);
        Assert.True(facade.HasHost);
        Assert.Equal(PrimarySlot.Settings, facade.ActiveSlot);
        Assert.Equal(PrimarySlot.Settings, facade.ActiveSlotOrNull);
    }

    [Fact]
    public void AttachHost_NullHost_ThrowsArgumentNullException()
    {
        var facade = new AppNavigationService();

        Assert.Throws<ArgumentNullException>(() => facade.AttachHost(null!));
    }

    [Fact]
    public void DetachHost_SetsHostToNullAndStopsCreatingSecondStateMachine()
    {
        var customHost = new NavigationShellHost(initialSlot: PrimarySlot.Settings);
        var facade = new AppNavigationService();

        facade.AttachHost(customHost);
        Assert.Same(customHost, facade.Host);
        Assert.True(facade.HasHost);

        facade.DetachHost(customHost);
        Assert.Null(facade.Host);
        Assert.False(facade.HasHost);
        Assert.Throws<InvalidOperationException>(() => facade.ActiveSlot);
        Assert.Null(facade.ActiveSlotOrNull);
    }

    [Fact]
    public void DetachHost_MismatchedHost_DoesNotDetachCurrent()
    {
        var hostA = new NavigationShellHost(initialSlot: PrimarySlot.Settings);
        var hostB = new NavigationShellHost(initialSlot: PrimarySlot.Mods);
        var facade = new AppNavigationService();

        facade.AttachHost(hostA);
        facade.DetachHost(hostB);

        Assert.Same(hostA, facade.Host);
        Assert.Equal(PrimarySlot.Settings, facade.ActiveSlot);
    }

    [Fact]
    public void SingleSourceOfTruth_OperationsSynchronizeInBothDirections()
    {
        var host = new NavigationShellHost();
        var facade = new AppNavigationService(host);

        // 1. 通过 facade 导航，直接影响 host
        var res1 = facade.NavigateTo(new PageNavigationRequest(PrimarySlot.Mods));
        Assert.Equal(NavigationResult.Success, res1);
        Assert.Equal(PrimarySlot.Mods, host.ActiveSlot);
        Assert.Equal(PrimarySlot.Mods, facade.ActiveSlot);

        // 2. 直接操作 host，即时反映在 facade
        var res2 = host.NavigateTo(new PageNavigationRequest(PrimarySlot.Settings));
        Assert.Equal(NavigationResult.Success, res2);
        Assert.Equal(PrimarySlot.Settings, host.ActiveSlot);
        Assert.Equal(PrimarySlot.Settings, facade.ActiveSlot);

        // 3. 次级导航与返回同步
        facade.NavigateToSecondary(SecondaryNavigationTarget.TaskCenter);
        Assert.Equal(SecondaryNavigationTarget.TaskCenter, host.ActiveSecondary);
        Assert.Equal(SecondaryNavigationTarget.TaskCenter, facade.ActiveSecondary);
        Assert.True(facade.CanNavigateBack);
        Assert.True(host.CanNavigateBack);

        facade.NavigateBack();
        Assert.Null(host.ActiveSecondary);
        Assert.Null(facade.ActiveSecondary);
        Assert.Equal(PrimarySlot.Settings, host.ActiveSlot);
        Assert.Equal(PrimarySlot.Settings, facade.ActiveSlot);
    }

    [Fact]
    public void OpenCommunityDetail_StructuredRequest_ForwardsWithCorrectSubDomainAndContext()
    {
        var host = new NavigationShellHost();
        var facade = new AppNavigationService(host);

        // 1. 默认子域 (CommunityDiscovery)
        var request1 = new CommunityDetailRequest(1001);
        var res1 = facade.OpenCommunityDetail(request1);

        Assert.Equal(NavigationResult.Success, res1);
        Assert.Equal(PrimarySlot.Mods, host.ActiveSlot);
        var expectedKey1 = new NavigationCacheKey(
            PrimarySlot.Mods,
            WorkshopSubDomain.CommunityDiscovery,
            new PluginDetailContext("1001"));
        Assert.Equal(expectedKey1, host.CurrentKey);

        // 2. 显式指定子域 (LocalInstalled)
        var request2 = new CommunityDetailRequest(2002, WorkshopSubDomain.LocalInstalled);
        var res2 = facade.OpenCommunityDetail(request2);

        Assert.Equal(NavigationResult.Success, res2);
        Assert.Equal(PrimarySlot.Mods, host.ActiveSlot);
        var expectedKey2 = new NavigationCacheKey(
            PrimarySlot.Mods,
            WorkshopSubDomain.LocalInstalled,
            new PluginDetailContext("2002"));
        Assert.Equal(expectedKey2, host.CurrentKey);
    }

    [Fact]
    public void OpenCommunityDetail_ConvenienceOverloads_MatchStructuredRequest()
    {
        var host = new NavigationShellHost();
        var facade = new AppNavigationService(host);

        var res = facade.OpenCommunityDetail(8888, WorkshopSubDomain.CommunityDiscovery);

        Assert.Equal(NavigationResult.Success, res);
        Assert.Equal(PrimarySlot.Mods, host.ActiveSlot);
        var expectedKey = new NavigationCacheKey(
            PrimarySlot.Mods,
            WorkshopSubDomain.CommunityDiscovery,
            new PluginDetailContext("8888"));
        Assert.Equal(expectedKey, host.CurrentKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public void OpenCommunityDetail_InvalidModId_ReturnsInvalidTarget(int invalidModId)
    {
        var host = new NavigationShellHost();
        var facade = new AppNavigationService(host);

        var res1 = facade.OpenCommunityDetail(invalidModId);
        Assert.Equal(NavigationResult.InvalidTarget, res1);

        var res2 = facade.OpenCommunityDetail(new CommunityDetailRequest(invalidModId));
        Assert.Equal(NavigationResult.InvalidTarget, res2);
    }

    [Fact]
    public void OpenCommunityDetail_NullRequest_ReturnsRejected()
    {
        var host = new NavigationShellHost();
        var facade = new AppNavigationService(host);

        var res = facade.OpenCommunityDetail((CommunityDetailRequest)null!);

        Assert.Equal(NavigationResult.Rejected, res);
    }

    [Fact]
    public void NavigateTo_NullRequest_ReturnsRejected()
    {
        var host = new NavigationShellHost();
        var facade = new AppNavigationService(host);

        var res = facade.NavigateTo(null!);

        Assert.Equal(NavigationResult.Rejected, res);
    }

    [Fact]
    public void ReturnToOrigin_WhenCanNavigateBack_DelegatesToNavigateBack()
    {
        var host = new NavigationShellHost();
        var facade = new AppNavigationService(host);

        // 先到设置，再激活关于
        facade.NavigateToSettings();
        facade.NavigateToAbout();
        Assert.True(facade.CanNavigateBack);

        // 调用 ReturnToOrigin
        var res = facade.ReturnToOrigin(new NavigationOrigin("返回本地插件", PrimarySlot.Mods, WorkshopSubDomain.LocalInstalled));

        Assert.Equal(NavigationResult.Success, res);
        Assert.Null(facade.ActiveSecondary);
        Assert.Equal(PrimarySlot.Settings, facade.ActiveSlot);
    }

    [Fact]
    public void ReturnToOrigin_WhenCannotNavigateBack_UsesProvidedOrigin()
    {
        var host = new NavigationShellHost(initialSlot: PrimarySlot.Play);
        var facade = new AppNavigationService(host);

        Assert.False(facade.CanNavigateBack);

        var origin = new NavigationOrigin("返回本地插件", PrimarySlot.Mods, WorkshopSubDomain.LocalInstalled);
        var res = facade.ReturnToOrigin(origin);

        Assert.Equal(NavigationResult.Success, res);
        Assert.Equal(PrimarySlot.Mods, facade.ActiveSlot);
        var expectedKey = new NavigationCacheKey(PrimarySlot.Mods, WorkshopSubDomain.LocalInstalled, null);
        Assert.Equal(expectedKey, host.CurrentKey);
    }

    [Fact]
    public void ReturnToOrigin_WhenCannotNavigateBackAndNullOrigin_FallsBackToCommunityDiscovery()
    {
        var host = new NavigationShellHost(initialSlot: PrimarySlot.Play);
        var facade = new AppNavigationService(host);

        Assert.False(facade.CanNavigateBack);

        var res = facade.ReturnToOrigin(null);

        Assert.Equal(NavigationResult.Success, res);
        Assert.Equal(PrimarySlot.Mods, facade.ActiveSlot);
        var expectedKey = new NavigationCacheKey(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery, null);
        Assert.Equal(expectedKey, host.CurrentKey);
    }

    [Fact]
    public void ConvenienceMethods_NavigateToExpectedSlotsAndSecondaries()
    {
        var host = new NavigationShellHost();
        var facade = new AppNavigationService(host);

        facade.NavigateToPlay();
        Assert.Equal(PrimarySlot.Play, facade.ActiveSlot);

        facade.NavigateToData();
        Assert.Equal(PrimarySlot.Data, facade.ActiveSlot);

        facade.NavigateToMods(WorkshopSubDomain.LocalInstalled);
        Assert.Equal(PrimarySlot.Mods, facade.ActiveSlot);

        facade.NavigateToSettings();
        Assert.Equal(PrimarySlot.Settings, facade.ActiveSlot);

        facade.NavigateToAbout();
        Assert.Equal(SecondaryNavigationTarget.About, facade.ActiveSecondary);

        facade.NavigateToTaskCenter();
        Assert.Equal(SecondaryNavigationTarget.TaskCenter, facade.ActiveSecondary);

        facade.NavigateBack();
        Assert.Null(facade.ActiveSecondary);
        Assert.Equal(PrimarySlot.Settings, facade.ActiveSlot);
    }

    [Fact]
    public void StateChanged_RelayedThroughFacade_FiresOnFacadeAndHostChanges()
    {
        var host = new NavigationShellHost();
        var facade = new AppNavigationService(host);
        var receivedEvents = new List<NavigationStateChangedEventArgs>();

        facade.StateChanged += (_, args) => receivedEvents.Add(args);

        // 1. 通过 facade 触发
        facade.NavigateToSettings();
        Assert.Single(receivedEvents);
        Assert.Equal(PrimarySlot.Settings, receivedEvents[0].PrimarySlot);

        // 2. 通过 host 直接触发
        host.NavigateToSecondary(SecondaryNavigationTarget.About);
        Assert.Equal(2, receivedEvents.Count);
        Assert.Equal(SecondaryNavigationTarget.About, receivedEvents[1].SecondaryTarget);

        // 3. 切换 host 后，旧 host 事件不再触发，新 host 事件正常触发
        var newHost = new NavigationShellHost();
        facade.AttachHost(newHost);

        host.NavigateTo(new PageNavigationRequest(PrimarySlot.Play));
        Assert.Equal(2, receivedEvents.Count); // 旧 host 事件不再捕获

        newHost.NavigateTo(new PageNavigationRequest(PrimarySlot.Data));
        Assert.Equal(3, receivedEvents.Count); // 新 host 事件正常捕获
        Assert.Equal(PrimarySlot.Data, receivedEvents[2].PrimarySlot);
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentAttachDetachAndNavigation_MaintainsHostConsistency()
    {
        var facade = new AppNavigationService();
        var exceptions = new List<Exception>();
        var hosts = new List<NavigationShellHost>();
        for (int i = 0; i < 5; i++)
        {
            hosts.Add(new NavigationShellHost(initialSlot: (PrimarySlot)i));
        }

        var tasks = new List<Task>();
        for (int i = 0; i < 30; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int step = 0; step < 50; step++)
                    {
                        if (idx % 3 == 0)
                        {
                            var targetHost = hosts[(idx + step) % hosts.Count];
                            facade.AttachHost(targetHost);
                        }
                        else if (idx % 3 == 1)
                        {
                            facade.DetachHost();
                        }
                        else
                        {
                            // 执行并发导航转发
                            var navResult = facade.NavigateTo(new PageNavigationRequest(PrimarySlot.Mods));
                            Assert.True(navResult is NavigationResult.Success or NavigationResult.Rejected);

                            var detailResult = facade.OpenCommunityDetail(100 + step);
                            Assert.True(detailResult is NavigationResult.Success or NavigationResult.Rejected);

                            var backResult = facade.NavigateBack();
                            Assert.True(backResult is NavigationResult.Success or NavigationResult.Rejected);
                        }

                        // 状态读取安全性
                        _ = facade.HasHost;
                        _ = facade.Host;
                        _ = facade.CanNavigateBack;
                        _ = facade.ActiveSecondary;
                        _ = facade.ActiveSlotOrNull;
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Empty(exceptions);
    }

    [Fact]
    public void StateChanged_DelayedEventFromDetachedHost_IsNotRelayed()
    {
        var host = new TestNavigationHost();
        var facade = new AppNavigationService();
        facade.AttachHost(host);

        var receivedEvents = new List<NavigationStateChangedEventArgs>();
        facade.StateChanged += (_, e) => receivedEvents.Add(e);

        // 正常触发
        host.RaiseStateChanged(host, new NavigationStateChangedEventArgs(PrimarySlot.Play, null, null));
        Assert.Single(receivedEvents);

        // Detach 后，即便 host 迟发事件，也严禁被 facade 中继
        facade.DetachHost(host);
        host.RaiseStateChanged(host, new NavigationStateChangedEventArgs(PrimarySlot.Settings, null, null));
        Assert.Single(receivedEvents);

        // 重新 Attach 另一个 host，旧 host 触发事件不被中继，伪造 sender 的事件也不被中继
        var newHost = new TestNavigationHost();
        facade.AttachHost(newHost);

        // 来自旧 host 的延迟事件 -> 丢弃
        host.RaiseStateChanged(host, new NavigationStateChangedEventArgs(PrimarySlot.Mods, null, null));
        Assert.Single(receivedEvents);

        // 伪造 sender (非当前挂载 host) -> 丢弃
        newHost.RaiseStateChanged("fakeSender", new NavigationStateChangedEventArgs(PrimarySlot.Settings, null, null));
        Assert.Single(receivedEvents);

        // 正常来自 newHost 的事件 -> 正常中继
        newHost.RaiseStateChanged(newHost, new NavigationStateChangedEventArgs(PrimarySlot.Data, null, null));
        Assert.Equal(2, receivedEvents.Count);
        Assert.Equal(PrimarySlot.Data, receivedEvents[1].PrimarySlot);
    }

    private sealed class TestNavigationHost : INavigationShellHost
    {
        public PrimarySlot ActiveSlot { get; set; } = PrimarySlot.Play;
        public SecondaryNavigationTarget? ActiveSecondary { get; set; }
        public bool CanNavigateBack { get; set; }

        public event EventHandler<NavigationStateChangedEventArgs>? StateChanged;

        public void RaiseStateChanged(object? sender, NavigationStateChangedEventArgs e)
        {
            StateChanged?.Invoke(sender, e);
        }

        public NavigationResult NavigateTo(PageNavigationRequest request) => NavigationResult.Success;
        public NavigationResult NavigateToSecondary(SecondaryNavigationTarget target) => NavigationResult.Success;
        public NavigationResult NavigateBack() => NavigationResult.Success;
    }
}

