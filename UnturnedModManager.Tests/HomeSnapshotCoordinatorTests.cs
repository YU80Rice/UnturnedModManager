using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using UnturnedModManager.Domain.Home;
using Xunit;

namespace UnturnedModManager.Tests;

/// <summary>
/// Ticket D: HomeSnapshot Coordinator 与只读投影管道单元测试
/// 针对 Ticket D 验收标准与架构防线：
/// 1. 验证不可变模型集合不变量 (杜绝派发 default ImmutableArray)
/// 2. 验证五态模型与失败枚举互斥绑定
/// 3. 验证纯函数 GetCacheAge 时钟回拨安全截断至 TimeSpan.Zero
/// 4. 验证初始快照未初始化语义 (IsInitialized == false)
/// 5. 验证双代际防覆盖机制 (迟到旧代际异步任务自动丢弃，不污染最新快照)
/// 6. 验证两阶段分批局部发布 (Phase 1 快速本地先行发布，Phase 2 慢速网络随后发布，PublishedRevision 单调自增)
/// 7. 验证防抖合并与最大延迟防饿死强制截断 (Max Delay Deadline)
/// 8. 验证五态优雅降级模型 (LiveOnline, CachedValid, CachedStale, Empty, Failed)
/// 9. 验证 Scoped 宿主生命周期与 Dispose 安全取消
/// 10. 验证吉祥物动态安全 Insets 规约与极端视口防御降级
/// </summary>
public sealed class HomeSnapshotCoordinatorTests
{
    [Fact]
    public void ModelInvariants_DefaultArrays_NormalizedToEmpty()
    {
        // 传入 default 的 ImmutableArray
        var feed = new FeedSectionSnapshot<FeaturedPluginItem>(
            FeedDataState.LiveOnline,
            IsInitialized: true,
            Items: default,
            LastSynchronized: DateTimeOffset.UtcNow,
            CanRetry: true);

        Assert.False(feed.Items.IsDefault);
        Assert.True(feed.Items.IsEmpty);

        var patchNote = new PatchNoteItem("v2.2.1", DateTimeOffset.UtcNow, default);
        Assert.False(patchNote.Highlights.IsDefault);
        Assert.True(patchNote.Highlights.IsEmpty);
    }

    [Fact]
    public void ModelInvariants_FailedAndEmptyStateRules_Enforced()
    {
        // Failed 状态下若未传 failureCategory，自动兜底为 NetworkUnavailable
        var failed = new FeedSectionSnapshot<FeaturedPluginItem>(
            FeedDataState.Failed,
            IsInitialized: true,
            Items: ImmutableArray<FeaturedPluginItem>.Empty,
            LastSynchronized: null,
            CanRetry: true,
            FailureCategory: null);

        Assert.Equal(FeedFailureCategory.NetworkUnavailable, failed.FailureCategory);

        // 非 Failed 状态下若传入 failureCategory，自动归一化为 null
        var online = new FeedSectionSnapshot<FeaturedPluginItem>(
            FeedDataState.LiveOnline,
            IsInitialized: true,
            Items: ImmutableArray.Create(new FeaturedPluginItem("1", "插件", "描述", "分类", "1.0")),
            LastSynchronized: DateTimeOffset.UtcNow,
            CanRetry: true,
            FailureCategory: FeedFailureCategory.ServerUnavailable);

        Assert.Null(online.FailureCategory);

        // Empty 状态下 Items 自动归一化为空集合
        var empty = FeedSectionSnapshot<FeaturedPluginItem>.Empty(DateTimeOffset.UtcNow);
        Assert.Equal(FeedDataState.Empty, empty.State);
        Assert.True(empty.Items.IsEmpty);
        Assert.Null(empty.FailureCategory);
    }

    [Fact]
    public void PureFunction_GetCacheAge_TruncatesNegativeClockRollback()
    {
        var syncTime = new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);
        var feed = FeedSectionSnapshot<FeaturedPluginItem>.CachedValid(
            ImmutableArray.Create(new FeaturedPluginItem("1", "插件", "描述", "分类", "1.0")),
            syncTime);

        // 正常流逝 10 分钟
        var future = syncTime.AddMinutes(10);
        Assert.Equal(TimeSpan.FromMinutes(10), feed.GetCacheAge(future));

        // 本地系统时钟回拨 5 分钟 (遇负时间差截断至 TimeSpan.Zero)
        var past = syncTime.AddMinutes(-5);
        Assert.Equal(TimeSpan.Zero, feed.GetCacheAge(past));

        // 无同步时间时返回 null
        var uninit = FeedSectionSnapshot<FeaturedPluginItem>.Uninitialized();
        Assert.Null(uninit.GetCacheAge(future));
    }

    [Fact]
    public void InitialState_BaselineUninitializedSemantics()
    {
        using var coordinator = CreateTestCoordinator();

        var initial = coordinator.CurrentSnapshot;
        Assert.Equal(1, initial.PublishedRevision);
        Assert.False(initial.Featured.IsInitialized);
        Assert.False(initial.Announcements.IsInitialized);
        Assert.Equal(FeedDataState.Empty, initial.Featured.State);
        Assert.Equal(FeedDataState.Empty, initial.Announcements.State);
        Assert.True(initial.Featured.Items.IsEmpty);
        Assert.True(initial.Announcements.Items.IsEmpty);
        Assert.False(initial.Launch.CanLaunch);
    }

    [Fact]
    public async Task TwoPhasePartialPublishing_MonotonicRevisionsAndDataProgression()
    {
        var fastLocalReady = new TaskCompletionSource<bool>();
        var slowRemoteReady = new TaskCompletionSource<bool>();

        var launchAdapter = new StubLaunchAdapter
        {
            Handler = async _ =>
            {
                await fastLocalReady.Task;
                return new LaunchSectionSnapshot("单人", "就绪", "C:\\Game", true, "方案A", 3, true);
            }
        };

        var communityAdapter = new StubCommunityAdapter
        {
            Handler = async _ =>
            {
                await slowRemoteReady.Task;
                return FeedSectionSnapshot<FeaturedPluginItem>.LiveOnline(
                    ImmutableArray.Create(new FeaturedPluginItem("101", "热门插件", "简介", "武器", "1.0")),
                    DateTimeOffset.UtcNow);
            }
        };

        using var coordinator = CreateTestCoordinator(
            launchAdapter: launchAdapter,
            communityAdapter: communityAdapter);

        var publishedSnapshots = new List<HomeSnapshot>();
        coordinator.SnapshotChanged += (_, e) =>
        {
            publishedSnapshots.Add(e.Snapshot);
        };

        // 启动异步刷新
        var refreshTask = coordinator.RefreshAsync();

        // 放行 Phase 1 (快速本地)
        fastLocalReady.SetResult(true);

        // 等待 Phase 1 发布
        await Task.Delay(50);
        Assert.Single(publishedSnapshots);
        var phase1 = publishedSnapshots[0];
        Assert.Equal(2, phase1.PublishedRevision);
        Assert.True(phase1.Launch.CanLaunch);
        Assert.Equal("方案A", phase1.Launch.ActiveProfileName);
        Assert.False(phase1.Featured.IsInitialized); // 慢速网络尚未完成，保持上一帧

        // 放行 Phase 2 (慢速网络)
        slowRemoteReady.SetResult(true);
        await refreshTask;

        // 等待 Phase 2 发布
        Assert.Equal(2, publishedSnapshots.Count);
        var phase2 = publishedSnapshots[1];
        Assert.Equal(3, phase2.PublishedRevision);
        Assert.True(phase2.Launch.CanLaunch); // 保持 Phase 1 数据
        Assert.True(phase2.Featured.IsInitialized);
        Assert.Equal(FeedDataState.LiveOnline, phase2.Featured.State);
        Assert.Single(phase2.Featured.Items);
        Assert.Equal("热门插件", phase2.Featured.Items[0].DisplayName);
    }

    [Fact]
    public async Task DualGeneration_OlderSlowTasksSupersededAndDiscarded()
    {
        var gen1SlowTcs = new TaskCompletionSource<FeedSectionSnapshot<FeaturedPluginItem>>();
        var gen2SlowTcs = new TaskCompletionSource<FeedSectionSnapshot<FeaturedPluginItem>>();

        int callCount = 0;
        var communityAdapter = new StubCommunityAdapter
        {
            Handler = _ =>
            {
                int count = Interlocked.Increment(ref callCount);
                return count == 1 ? gen1SlowTcs.Task : gen2SlowTcs.Task;
            }
        };

        using var coordinator = CreateTestCoordinator(communityAdapter: communityAdapter);

        var publishedSnapshots = new List<HomeSnapshot>();
        coordinator.SnapshotChanged += (_, e) => publishedSnapshots.Add(e.Snapshot);

        // 启动代际 1 刷新
        var task1 = coordinator.RefreshAsync();
        await Task.Delay(30);

        // 在代际 1 的慢速网络尚未返回时，启动代际 2 刷新 (将代际 1 supersede)
        var task2 = coordinator.RefreshAsync();
        await Task.Delay(30);

        // 放行代际 2 的慢速网络
        gen2SlowTcs.SetResult(FeedSectionSnapshot<FeaturedPluginItem>.LiveOnline(
            ImmutableArray.Create(new FeaturedPluginItem("GEN2", "代际2插件", "描述", "分类", "2.0")),
            DateTimeOffset.UtcNow));
        await task2;

        var snapshotAfterGen2 = coordinator.CurrentSnapshot;
        Assert.Equal("GEN2", snapshotAfterGen2.Featured.Items[0].PluginId);

        // 此时迟到的代际 1 慢速网络返回
        gen1SlowTcs.SetResult(FeedSectionSnapshot<FeaturedPluginItem>.LiveOnline(
            ImmutableArray.Create(new FeaturedPluginItem("GEN1", "代际1老旧插件", "描述", "分类", "1.0")),
            DateTimeOffset.UtcNow));

        await Task.Delay(50);

        // 验证代际 1 的老数据已被安全丢弃，当前快照绝无被污染！
        Assert.Equal("GEN2", coordinator.CurrentSnapshot.Featured.Items[0].PluginId);
    }

    [Fact]
    public async Task CoalescingDebounce_RapidSuccessiveRequests_CoalescedIntoSingleExecution()
    {
        int launchFetchCount = 0;
        var launchAdapter = new StubLaunchAdapter
        {
            Handler = _ =>
            {
                Interlocked.Increment(ref launchFetchCount);
                return ValueTask.FromResult(new LaunchSectionSnapshot("单人", "就绪", "C:\\Game", true, "方案", 0, true));
            }
        };

        using var coordinator = CreateTestCoordinator(
            launchAdapter: launchAdapter,
            debounceDelay: TimeSpan.FromMilliseconds(40),
            maxDelayDeadline: TimeSpan.FromMilliseconds(200));

        // 密集触发 10 次 RequestRefresh
        for (int i = 0; i < 10; i++)
        {
            coordinator.RequestRefresh();
            await Task.Delay(5);
        }

        // 等待防抖触发完成
        await Task.Delay(100);

        // 验证 10 次密集触发被合并为单次执行
        Assert.Equal(1, launchFetchCount);
    }

    [Fact]
    public async Task MaxDelayDeadline_ContinuousBursts_ForcefullyTruncatesAndExecutes()
    {
        int fetchCount = 0;
        var launchAdapter = new StubLaunchAdapter
        {
            Handler = _ =>
            {
                Interlocked.Increment(ref fetchCount);
                return ValueTask.FromResult(new LaunchSectionSnapshot("单人", "就绪", "C:\\Game", true, "方案", 0, true));
            }
        };

        // 防抖 40ms，最大截断 90ms
        using var coordinator = CreateTestCoordinator(
            launchAdapter: launchAdapter,
            debounceDelay: TimeSpan.FromMilliseconds(40),
            maxDelayDeadline: TimeSpan.FromMilliseconds(90));

        var startTime = DateTimeOffset.UtcNow;
        // 持续发送刷新请求，每个间隔 20ms (小于 40ms 防抖，若无最大超时则会无限延期饿死)
        while ((DateTimeOffset.UtcNow - startTime).TotalMilliseconds < 150)
        {
            coordinator.RequestRefresh();
            await Task.Delay(15);
        }

        // 验证防饿死机制：即使请求不断，也至少强制触发了一次执行
        Assert.True(fetchCount >= 1);
    }

    [Fact]
    public void PolymorphicOfflineDegradation_AllFiveStatesHandled()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new FeaturedPluginItem("1", "模组", "简介", "类别", "1.0");

        var live = FeedSectionSnapshot<FeaturedPluginItem>.LiveOnline(ImmutableArray.Create(item), now);
        Assert.Equal(FeedDataState.LiveOnline, live.State);
        Assert.True(live.IsInitialized);
        Assert.Null(live.FailureCategory);
        Assert.Single(live.Items);

        var valid = FeedSectionSnapshot<FeaturedPluginItem>.CachedValid(ImmutableArray.Create(item), now.AddMinutes(-5));
        Assert.Equal(FeedDataState.CachedValid, valid.State);
        Assert.True(valid.IsInitialized);
        Assert.Null(valid.FailureCategory);

        var stale = FeedSectionSnapshot<FeaturedPluginItem>.CachedStale(
            ImmutableArray.Create(item), now.AddHours(-2));
        Assert.Equal(FeedDataState.CachedStale, stale.State);
        Assert.True(stale.IsInitialized);
        Assert.Null(stale.FailureCategory);

        var empty = FeedSectionSnapshot<FeaturedPluginItem>.Empty(now);
        Assert.Equal(FeedDataState.Empty, empty.State);
        Assert.True(empty.IsInitialized);
        Assert.True(empty.Items.IsEmpty);

        var failed = FeedSectionSnapshot<FeaturedPluginItem>.Failed(FeedFailureCategory.Timeout);
        Assert.Equal(FeedDataState.Failed, failed.State);
        Assert.True(failed.IsInitialized);
        Assert.Equal(FeedFailureCategory.Timeout, failed.FailureCategory);
        Assert.True(failed.Items.IsEmpty);
    }

    [Fact]
    public async Task ScopedLifecycle_Dispose_CancelsWorkAndStopsEvents()
    {
        var tcs = new TaskCompletionSource<bool>();
        var slowAdapter = new StubCommunityAdapter
        {
            Handler = async token =>
            {
                tcs.SetResult(true);
                await Task.Delay(1000, token);
                return FeedSectionSnapshot<FeaturedPluginItem>.Empty();
            }
        };

        var coordinator = CreateTestCoordinator(communityAdapter: slowAdapter);
        int eventCount = 0;
        coordinator.SnapshotChanged += (_, _) => eventCount++;

        var task = coordinator.RefreshAsync();
        await tcs.Task; // 等待任务进入

        coordinator.Dispose();
        await task; // Dispose 会触发取消，不抛出未经处理的未捕获异常

        // 验证 Dispose 后没有多余事件派发
        Assert.True(eventCount <= 1); // 仅可能触发过 Phase 1，Phase 2 被取消
    }

    [Theory]
    [InlineData(false, 1280, 800, 1.0, false, 0.0, 0.0, false)] // 隐藏小助理
    [InlineData(true, 1280, 800, 1.0, true, 96.0, 1.0, true)]   // 标准 1080p/720p 视口
    [InlineData(true, 1280, 800, 1.5, true, 144.0, 1.5, true)]  // 150% 高 DPI
    [InlineData(true, 920, 580, 1.0, true, 72.0, 0.75, true)]   // 低于 960x600：防御性缩放
    [InlineData(true, 750, 480, 1.0, false, 0.0, 0.0, false)]   // 低于 800x500 极限视口：防御性自动隐藏
    public void MascotLayoutPolicy_CalculatesAccurateSafetyInsets(
        bool isVisible,
        double width,
        double height,
        double scale,
        bool expectedVisible,
        double expectedInset,
        double expectedScale,
        bool expectedHitTest)
    {
        var spec = MascotLayoutPolicy.Calculate(isVisible, width, height, scale);

        Assert.Equal(expectedVisible, spec.IsVisible);
        Assert.Equal(expectedInset, spec.SafeBottomInset);
        Assert.Equal(expectedScale, spec.ScaleFactor);
        Assert.Equal(expectedHitTest, spec.HitTestEnabled);
    }

    [Fact]
    public void MascotPresentationAdapter_RotatesQuotesProperly()
    {
        var adapter = new StubMascotAdapter();
        var quote1 = adapter.GetMascotSection();
        Assert.Equal(0, quote1.QuoteIndex);

        var quote2 = adapter.NextQuote();
        Assert.Equal(1, quote2.QuoteIndex);
        Assert.NotEqual(quote1.CurrentQuote, quote2.CurrentQuote);
    }

    private static HomeSnapshotCoordinator CreateTestCoordinator(
        ILaunchStateAdapter? launchAdapter = null,
        IEnvironmentStateAdapter? envAdapter = null,
        IDiagnosticStateAdapter? diagAdapter = null,
        ICommunityContentAdapter? communityAdapter = null,
        IReleaseAnnouncementAdapter? announcementAdapter = null,
        IMascotPresentationAdapter? mascotAdapter = null,
        TimeSpan? debounceDelay = null,
        TimeSpan? maxDelayDeadline = null)
    {
        return new HomeSnapshotCoordinator(
            launchAdapter ?? new StubLaunchAdapter(),
            envAdapter ?? new StubEnvironmentAdapter(),
            diagAdapter ?? new StubDiagnosticAdapter(),
            communityAdapter ?? new StubCommunityAdapter(),
            announcementAdapter ?? new StubAnnouncementAdapter(),
            mascotAdapter ?? new StubMascotAdapter(),
            debounceDelay ?? TimeSpan.FromMilliseconds(20),
            maxDelayDeadline ?? TimeSpan.FromMilliseconds(80));
    }

    private sealed class StubLaunchAdapter : ILaunchStateAdapter
    {
        public Func<CancellationToken, ValueTask<LaunchSectionSnapshot>>? Handler { get; set; }

        public ValueTask<LaunchSectionSnapshot> GetLaunchSectionAsync(CancellationToken token = default)
        {
            return Handler != null
                ? Handler(token)
                : ValueTask.FromResult(new LaunchSectionSnapshot("单人", "就绪", "C:\\Unturned", true, "方案", 5, true));
        }
    }

    private sealed class StubEnvironmentAdapter : IEnvironmentStateAdapter
    {
        public Func<CancellationToken, ValueTask<EnvironmentSectionSnapshot>>? Handler { get; set; }

        public ValueTask<EnvironmentSectionSnapshot> GetEnvironmentSectionAsync(CancellationToken token = default)
        {
            return Handler != null
                ? Handler(token)
                : ValueTask.FromResult(new EnvironmentSectionSnapshot("v5.4.23", true, true, false, true));
        }
    }

    private sealed class StubDiagnosticAdapter : IDiagnosticStateAdapter
    {
        public Func<CancellationToken, ValueTask<DiagnosticSectionSnapshot>>? Handler { get; set; }

        public ValueTask<DiagnosticSectionSnapshot> GetDiagnosticSectionAsync(CancellationToken token = default)
        {
            return Handler != null
                ? Handler(token)
                : ValueTask.FromResult(new DiagnosticSectionSnapshot("环境健康", 0, true));
        }
    }

    private sealed class StubCommunityAdapter : ICommunityContentAdapter
    {
        public Func<CancellationToken, Task<FeedSectionSnapshot<FeaturedPluginItem>>>? Handler { get; set; }

        public Task<FeedSectionSnapshot<FeaturedPluginItem>> GetFeaturedFeedAsync(CancellationToken token = default)
        {
            return Handler != null
                ? Handler(token)
                : Task.FromResult(FeedSectionSnapshot<FeaturedPluginItem>.LiveOnline(
                    ImmutableArray.Create(new FeaturedPluginItem("1", "插件", "简介", "分类", "1.0")),
                    DateTimeOffset.UtcNow));
        }
    }

    private sealed class StubAnnouncementAdapter : IReleaseAnnouncementAdapter
    {
        public Func<CancellationToken, Task<FeedSectionSnapshot<PatchNoteItem>>>? Handler { get; set; }

        public Task<FeedSectionSnapshot<PatchNoteItem>> GetAnnouncementsFeedAsync(CancellationToken token = default)
        {
            return Handler != null
                ? Handler(token)
                : Task.FromResult(FeedSectionSnapshot<PatchNoteItem>.LiveOnline(
                    ImmutableArray.Create(new PatchNoteItem("v2.2.1", DateTimeOffset.UtcNow, ImmutableArray.Create("更新点1"))),
                    DateTimeOffset.UtcNow));
        }
    }

    private sealed class StubMascotAdapter : IMascotPresentationAdapter
    {
        private int _index;
        private static readonly string[] Quotes = ["台词1", "台词2", "台词3"];

        public MascotSectionSnapshot GetMascotSection()
        {
            return new MascotSectionSnapshot(true, _index, Quotes[_index % Quotes.Length]);
        }

        public MascotSectionSnapshot NextQuote()
        {
            _index = (_index + 1) % Quotes.Length;
            return GetMascotSection();
        }
    }
}
