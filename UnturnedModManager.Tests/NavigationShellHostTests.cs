using System;
using System.Collections.Generic;
using UnturnedModManager.Navigation;
using Xunit;

namespace UnturnedModManager.Tests;

/// <summary>
/// Ticket A: NavigationShell 契约与逻辑宿主状态机单元测试
/// 针对 Ticket A 验收标准与审查回归防线：
/// 1. 验证默认初始状态 (Play, 无次级, 不可后退, 主槽位高亮)
/// 2. 验证构造函数非法输入安全规范化保底，杜绝非法 Key 进入已访问缓存
/// 3. 验证 VisitedKeys 封装安全防御 (外部强制转换修改不污染宿主内部状态)
/// 4. 验证 5 大主槽位顺次与任意切换调度
/// 5. 验证组合合法性不变量 (非法组合直接拒绝并不进入缓存，要求上下文必须显式携带所属子域)
/// 6. 验证 Mods 子域与上下文合法组合流转
/// 7. 验证次级导航 (TaskCenter, About) 激活、主槽位取消高亮与单根来源锚点 (RootReturnContext) 记忆
/// 8. 验证次级平级横向切换不压栈不覆盖来源锚点与重复点击幂等性
/// 9. 验证 NavigateBack 准确恢复先前槽位与子域，以及无锚点或非法锚点时的安全降级回退
/// 10. 验证次级覆盖下点击主槽位直接重定向并清空次级状态
/// 11. 验证 StateChanged 集中派生事件触发
/// 12. 验证 NavigationCacheKey 不可变性与值相等性
/// </summary>
public sealed class NavigationShellHostTests
{
    [Fact]
    public void InitialState_DefaultIsPlayAndHighlightedWithoutSecondary()
    {
        var host = new NavigationShellHost();

        Assert.Equal(PrimarySlot.Play, host.ActiveSlot);
        Assert.Null(host.ActiveSecondary);
        Assert.Null(host.RootReturnContext);
        Assert.False(host.CanNavigateBack);
        Assert.True(host.IsPrimaryHighlighted);
        Assert.Equal(PrimarySlot.Play, host.HighlightedSlot);

        var expectedKey = new NavigationCacheKey(PrimarySlot.Play);
        Assert.Equal(expectedKey, host.CurrentKey);
        Assert.Contains(expectedKey, host.VisitedKeys);
    }

    [Fact]
    public void Constructor_InvalidInputs_NormalizedSafelyWithoutCorruptedKeyInCache()
    {
        // 传入非法 slot、非法 secondary 以及非法 anchor
        var corruptedAnchor = new ReturnContext((PrimarySlot)999);
        var host = new NavigationShellHost(
            initialSlot: (PrimarySlot)888,
            initialSecondary: (SecondaryNavigationTarget)777,
            initialAnchor: corruptedAnchor);

        // 验证已安全规范化
        Assert.Equal(PrimarySlot.Play, host.ActiveSlot);
        Assert.Null(host.ActiveSecondary);
        Assert.Null(host.RootReturnContext);
        Assert.True(host.IsPrimaryHighlighted);
        Assert.False(host.CanNavigateBack);

        // 验证已访问缓存中绝无非法 key
        var expectedKey = new NavigationCacheKey(PrimarySlot.Play);
        Assert.Equal(expectedKey, host.CurrentKey);
        Assert.Single(host.VisitedKeys);
        Assert.Contains(expectedKey, host.VisitedKeys);
        Assert.DoesNotContain(new NavigationCacheKey((PrimarySlot)888), host.VisitedKeys);
    }

    [Fact]
    public void VisitedKeys_EncapsulationProtected_ExternalMutationDoesNotAffectHost()
    {
        var host = new NavigationShellHost();
        var keys = host.VisitedKeys;

        if (keys is ISet<NavigationCacheKey> mutableSet)
        {
            try
            {
                mutableSet.Clear();
                mutableSet.Add(new NavigationCacheKey(PrimarySlot.Exit));
            }
            catch (NotSupportedException)
            {
                // ImmutableHashSet 显式禁止突变，彻底符合不可变封装预期
            }
        }

        // 宿主内部状态依然保持完整，不受外部强制类型转换影响
        Assert.Contains(new NavigationCacheKey(PrimarySlot.Play), host.VisitedKeys);
        Assert.DoesNotContain(new NavigationCacheKey(PrimarySlot.Exit), host.VisitedKeys);
    }

    [Fact]
    public void NavigateTo_FivePrimarySlots_SucceedsAndTracksCacheKeys()
    {
        var host = new NavigationShellHost();

        var slotsToVisit = new[]
        {
            PrimarySlot.Data,
            PrimarySlot.Mods,
            PrimarySlot.Settings,
            PrimarySlot.Exit,
            PrimarySlot.Play
        };

        foreach (var slot in slotsToVisit)
        {
            var result = host.NavigateTo(new PageNavigationRequest(slot));
            Assert.Equal(NavigationResult.Success, result);
            Assert.Equal(slot, host.ActiveSlot);
            Assert.Null(host.ActiveSecondary);
            Assert.True(host.IsPrimaryHighlighted);
            Assert.Equal(slot, host.HighlightedSlot);
        }

        Assert.Contains(new NavigationCacheKey(PrimarySlot.Data), host.VisitedKeys);
        Assert.Contains(new NavigationCacheKey(PrimarySlot.Mods, WorkshopSubDomain.LocalInstalled), host.VisitedKeys);
        Assert.Contains(new NavigationCacheKey(PrimarySlot.Settings), host.VisitedKeys);
        Assert.Contains(new NavigationCacheKey(PrimarySlot.Exit), host.VisitedKeys);
        Assert.Contains(new NavigationCacheKey(PrimarySlot.Play), host.VisitedKeys);
    }

    [Theory]
    [InlineData(PrimarySlot.Play, WorkshopSubDomain.LocalInstalled, null)]
    [InlineData(PrimarySlot.Play, null, "CommunitySearchContext")]
    [InlineData(PrimarySlot.Play, null, "PluginDetailContext")]
    [InlineData(PrimarySlot.Data, WorkshopSubDomain.CommunityDiscovery, null)]
    [InlineData(PrimarySlot.Data, null, "CommunitySearchContext")]
    [InlineData(PrimarySlot.Settings, WorkshopSubDomain.LocalInstalled, null)]
    [InlineData(PrimarySlot.Settings, null, "CommunitySearchContext")]
    [InlineData(PrimarySlot.Exit, WorkshopSubDomain.CommunityDiscovery, null)]
    [InlineData(PrimarySlot.Exit, null, "PluginDetailContext")]
    [InlineData(PrimarySlot.Mods, WorkshopSubDomain.LocalInstalled, "CommunitySearchContext")]
    [InlineData(PrimarySlot.Mods, null, "CommunitySearchContext")] // 缺少显式 CommunityDiscovery
    [InlineData(PrimarySlot.Mods, null, "PluginDetailContext")]       // 缺少显式子域，禁止通用宿主猜测
    public void NavigateTo_IllegalCombinations_RejectedAndNotCached(
        PrimarySlot slot,
        WorkshopSubDomain? subDomain,
        string? contextType)
    {
        var host = new NavigationShellHost();
        var initialSlot = host.ActiveSlot;
        var initialKey = host.CurrentKey;
        var initialVisitedCount = host.VisitedKeys.Count;

        NavigationContext? context = contextType switch
        {
            "CommunitySearchContext" => new CommunitySearchContext("test-query"),
            "PluginDetailContext" => new PluginDetailContext("999"),
            _ => null
        };

        var request = new PageNavigationRequest(slot, subDomain, context);
        var result = host.NavigateTo(request);

        Assert.Equal(NavigationResult.Rejected, result);
        Assert.Equal(initialSlot, host.ActiveSlot);
        Assert.Equal(initialKey, host.CurrentKey);
        Assert.Equal(initialVisitedCount, host.VisitedKeys.Count);

        var rejectedKey = new NavigationCacheKey(slot, subDomain, context);
        Assert.DoesNotContain(rejectedKey, host.VisitedKeys);
    }

    [Fact]
    public void NavigateTo_UndefinedEnums_ReturnsInvalidTarget()
    {
        var host = new NavigationShellHost();

        var invalidSlotRequest = new PageNavigationRequest((PrimarySlot)999);
        var result1 = host.NavigateTo(invalidSlotRequest);
        Assert.Equal(NavigationResult.InvalidTarget, result1);

        var invalidSubDomainRequest = new PageNavigationRequest(PrimarySlot.Mods, (WorkshopSubDomain)888);
        var result2 = host.NavigateTo(invalidSubDomainRequest);
        Assert.Equal(NavigationResult.InvalidTarget, result2);
    }

    [Fact]
    public void NavigateTo_ValidModsCombinations_Succeeds()
    {
        var host = new NavigationShellHost();

        // 1. Mods 默认 (LocalInstalled, 无上下文)
        var res1 = host.NavigateTo(new PageNavigationRequest(PrimarySlot.Mods));
        Assert.Equal(NavigationResult.Success, res1);
        Assert.Equal(PrimarySlot.Mods, host.ActiveSlot);
        Assert.Equal(new NavigationCacheKey(PrimarySlot.Mods, WorkshopSubDomain.LocalInstalled, null), host.CurrentKey);

        // 2. Mods 社区发现 + 搜索上下文 (显式指定 CommunityDiscovery)
        var searchContext = new CommunitySearchContext("zombie");
        var res2 = host.NavigateTo(new PageNavigationRequest(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery, searchContext));
        Assert.Equal(NavigationResult.Success, res2);
        Assert.Equal(new NavigationCacheKey(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery, searchContext), host.CurrentKey);

        // 3. Mods 社区发现 + 详情上下文 (显式指定 CommunityDiscovery)
        var detailContext = new PluginDetailContext("42");
        var res3 = host.NavigateTo(new PageNavigationRequest(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery, detailContext));
        Assert.Equal(NavigationResult.Success, res3);
        Assert.Equal(new NavigationCacheKey(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery, detailContext), host.CurrentKey);

        // 4. Mods 本地插件 + 详情上下文 (显式指定 LocalInstalled)
        var localDetailContext = new PluginDetailContext("local-mod-1");
        var res4 = host.NavigateTo(new PageNavigationRequest(PrimarySlot.Mods, WorkshopSubDomain.LocalInstalled, localDetailContext));
        Assert.Equal(NavigationResult.Success, res4);
        Assert.Equal(new NavigationCacheKey(PrimarySlot.Mods, WorkshopSubDomain.LocalInstalled, localDetailContext), host.CurrentKey);
    }

    [Fact]
    public void NavigateToSecondary_ActivatesSecondary_UnhighlightsPrimaryAndRecordsRootAnchor()
    {
        var host = new NavigationShellHost();

        // 先进入 Mods 社区搜索状态
        var searchContext = new CommunitySearchContext("economy");
        host.NavigateTo(new PageNavigationRequest(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery, searchContext));

        // 激活次级目标 TaskCenter
        var res = host.NavigateToSecondary(SecondaryNavigationTarget.TaskCenter);
        Assert.Equal(NavigationResult.Success, res);

        Assert.Equal(SecondaryNavigationTarget.TaskCenter, host.ActiveSecondary);
        Assert.Equal(PrimarySlot.Mods, host.ActiveSlot); // 底层保留原槽位
        Assert.False(host.IsPrimaryHighlighted);
        Assert.Null(host.HighlightedSlot);
        Assert.True(host.CanNavigateBack);

        var expectedAnchor = new ReturnContext(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery, searchContext);
        Assert.Equal(expectedAnchor, host.RootReturnContext);
    }

    [Fact]
    public void NavigateToSecondary_HorizontalSwitchBetweenSecondaries_DoesNotOverwriteRootAnchor()
    {
        var host = new NavigationShellHost();

        // 进入 Settings
        host.NavigateTo(new PageNavigationRequest(PrimarySlot.Settings));

        // 首次激活 TaskCenter
        host.NavigateToSecondary(SecondaryNavigationTarget.TaskCenter);
        var originalAnchor = host.RootReturnContext;
        Assert.NotNull(originalAnchor);
        Assert.Equal(PrimarySlot.Settings, originalAnchor!.SourceSlot);

        // 横向切换至 About (平级切换不连续压栈，不覆盖单根来源锚点)
        var switchRes = host.NavigateToSecondary(SecondaryNavigationTarget.About);
        Assert.Equal(NavigationResult.Success, switchRes);
        Assert.Equal(SecondaryNavigationTarget.About, host.ActiveSecondary);
        Assert.Equal(originalAnchor, host.RootReturnContext); // 来源锚点保持原样
        Assert.True(host.CanNavigateBack);

        // 重复点击当前已激活项具备幂等性
        var idempotentRes = host.NavigateToSecondary(SecondaryNavigationTarget.About);
        Assert.Equal(NavigationResult.Success, idempotentRes);
        Assert.Equal(SecondaryNavigationTarget.About, host.ActiveSecondary);
        Assert.Equal(originalAnchor, host.RootReturnContext);
    }

    [Fact]
    public void NavigateBack_FromSecondary_RestoresSourceSlotAndSubDomain()
    {
        var host = new NavigationShellHost();

        var searchContext = new CommunitySearchContext("vehicles");
        host.NavigateTo(new PageNavigationRequest(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery, searchContext));

        host.NavigateToSecondary(SecondaryNavigationTarget.TaskCenter);
        Assert.True(host.CanNavigateBack);

        // 执行后退导航
        var backRes = host.NavigateBack();
        Assert.Equal(NavigationResult.Success, backRes);

        // 恢复先前的槽位、子域与上下文
        Assert.Null(host.ActiveSecondary);
        Assert.Null(host.RootReturnContext);
        Assert.Equal(PrimarySlot.Mods, host.ActiveSlot);
        Assert.True(host.IsPrimaryHighlighted);
        Assert.Equal(PrimarySlot.Mods, host.HighlightedSlot);
        Assert.False(host.CanNavigateBack);

        var expectedRestoredKey = new NavigationCacheKey(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery, searchContext);
        Assert.Equal(expectedRestoredKey, host.CurrentKey);
    }

    [Fact]
    public void NavigateBack_WhenNoHistory_ReturnsRejected()
    {
        var host = new NavigationShellHost();
        Assert.False(host.CanNavigateBack);

        var result = host.NavigateBack();
        Assert.Equal(NavigationResult.Rejected, result);
    }

    [Fact]
    public void NavigateBack_SecondaryWithoutAnchor_SafelyFallsBackToPlay()
    {
        // 模拟直接从次级启动或来源锚点丢失场景
        var host = new NavigationShellHost(initialSecondary: SecondaryNavigationTarget.About, initialAnchor: null);

        Assert.Equal(SecondaryNavigationTarget.About, host.ActiveSecondary);
        Assert.Null(host.RootReturnContext);
        Assert.True(host.CanNavigateBack);

        var result = host.NavigateBack();
        Assert.Equal(NavigationResult.Success, result);
        Assert.Null(host.ActiveSecondary);
        Assert.Equal(PrimarySlot.Play, host.ActiveSlot);
        Assert.True(host.IsPrimaryHighlighted);
        Assert.False(host.CanNavigateBack);
    }

    [Fact]
    public void NavigateBack_SecondaryWithInvalidAnchor_SafelyFallsBackToPlay()
    {
        // 模拟来源锚点被破坏或枚举值非法的极端场景
        var corruptedAnchor = new ReturnContext((PrimarySlot)999);
        var host = new NavigationShellHost(initialSecondary: SecondaryNavigationTarget.TaskCenter, initialAnchor: corruptedAnchor);

        Assert.Equal(SecondaryNavigationTarget.TaskCenter, host.ActiveSecondary);
        Assert.True(host.CanNavigateBack);

        var result = host.NavigateBack();
        Assert.Equal(NavigationResult.Success, result);
        Assert.Null(host.ActiveSecondary);
        Assert.Equal(PrimarySlot.Play, host.ActiveSlot); // 安全回退至默认 Play 槽位
        Assert.True(host.IsPrimaryHighlighted);
        Assert.False(host.CanNavigateBack);
    }

    [Fact]
    public void NavigateTo_PrimarySlotWhileInSecondary_RedirectsAndClearsSecondary()
    {
        var host = new NavigationShellHost();

        host.NavigateTo(new PageNavigationRequest(PrimarySlot.Data));
        host.NavigateToSecondary(SecondaryNavigationTarget.TaskCenter);
        Assert.NotNull(host.ActiveSecondary);
        Assert.NotNull(host.RootReturnContext);

        // 用户直接点击左侧五槽位之一 (如 Settings)
        var result = host.NavigateTo(new PageNavigationRequest(PrimarySlot.Settings));

        Assert.Equal(NavigationResult.Success, result);
        Assert.Null(host.ActiveSecondary);
        Assert.Null(host.RootReturnContext);
        Assert.Equal(PrimarySlot.Settings, host.ActiveSlot);
        Assert.True(host.IsPrimaryHighlighted);
        Assert.Equal(PrimarySlot.Settings, host.HighlightedSlot);
        Assert.False(host.CanNavigateBack);
    }

    [Fact]
    public void StateChanged_FiresWithExpectedDerivations()
    {
        var host = new NavigationShellHost();
        var events = new List<NavigationStateChangedEventArgs>();
        host.StateChanged += (_, e) => events.Add(e);

        // 1. 主槽位切换
        host.NavigateTo(new PageNavigationRequest(PrimarySlot.Data));
        Assert.Single(events);
        Assert.Equal(PrimarySlot.Data, events[0].PrimarySlot);
        Assert.Null(events[0].SecondaryTarget);
        Assert.Null(events[0].ReturnContext);

        // 2. 次级激活 (PrimarySlot 必须为 null，代表取消高亮)
        host.NavigateToSecondary(SecondaryNavigationTarget.TaskCenter);
        Assert.Equal(2, events.Count);
        Assert.Null(events[1].PrimarySlot);
        Assert.Equal(SecondaryNavigationTarget.TaskCenter, events[1].SecondaryTarget);
        Assert.NotNull(events[1].ReturnContext);
        Assert.Equal(PrimarySlot.Data, events[1].ReturnContext!.SourceSlot);

        // 3. 返回主页面 (PrimarySlot 恢复 Data)
        host.NavigateBack();
        Assert.Equal(3, events.Count);
        Assert.Equal(PrimarySlot.Data, events[2].PrimarySlot);
        Assert.Null(events[2].SecondaryTarget);
        Assert.Null(events[2].ReturnContext);
    }

    [Fact]
    public void NavigationCacheKey_ImmutableValueEquality_WorksCorrectly()
    {
        var key1 = new NavigationCacheKey(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery, new CommunitySearchContext("test"));
        var key2 = new NavigationCacheKey(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery, new CommunitySearchContext("test"));
        var key3 = new NavigationCacheKey(PrimarySlot.Mods, WorkshopSubDomain.CommunityDiscovery, new CommunitySearchContext("other"));

        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
        Assert.NotEqual(key1, key3);

        // 校验 IsValid 辅助方法
        Assert.True(key1.IsValid(out var failRes, out var reason));
        Assert.Equal(NavigationResult.Success, failRes);
        Assert.Null(reason);
    }
}
