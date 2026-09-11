using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace UnturnedModManager.Domain.Home;

/// <summary>
/// 首页快照协调器实现 (双代际流转、局部故障隔离、防抖合并、防饿死强制截断与两阶段局部发布)
/// </summary>
public sealed class HomeSnapshotCoordinator : IHomeSnapshotCoordinator
{
    private readonly ILaunchStateAdapter _launchAdapter;
    private readonly IEnvironmentStateAdapter _environmentAdapter;
    private readonly IDiagnosticStateAdapter _diagnosticAdapter;
    private readonly ICommunityContentAdapter _communityAdapter;
    private readonly IReleaseAnnouncementAdapter _announcementAdapter;
    private readonly IMascotPresentationAdapter _mascotAdapter;
    private readonly TimeSpan _debounceDelay;
    private readonly TimeSpan _maxDelayDeadline;
    private readonly TimeProvider _timeProvider;

    private readonly object _stateLock = new();
    private readonly object _debounceLock = new();
    private readonly CancellationTokenSource _lifetimeCts = new();

    private long _refreshGeneration;
    private long _publishedRevision;
    private HomeSnapshot _currentSnapshot;
    private bool _disposed;

    private CancellationTokenSource? _debounceCts;
    private DateTimeOffset? _firstPendingRequestTime;
    private CancellationTokenSource? _activeGenerationCts;

    public HomeSnapshot CurrentSnapshot
    {
        get
        {
            lock (_stateLock)
            {
                return _currentSnapshot;
            }
        }
    }

    public event EventHandler<HomeSnapshotChangedEventArgs>? SnapshotChanged;

    public HomeSnapshotCoordinator(
        ILaunchStateAdapter launchAdapter,
        IEnvironmentStateAdapter environmentAdapter,
        IDiagnosticStateAdapter diagnosticAdapter,
        ICommunityContentAdapter communityAdapter,
        IReleaseAnnouncementAdapter announcementAdapter,
        IMascotPresentationAdapter mascotAdapter,
        TimeSpan? debounceDelay = null,
        TimeSpan? maxDelayDeadline = null,
        TimeProvider? timeProvider = null)
    {
        _launchAdapter = launchAdapter ?? throw new ArgumentNullException(nameof(launchAdapter));
        _environmentAdapter = environmentAdapter ?? throw new ArgumentNullException(nameof(environmentAdapter));
        _diagnosticAdapter = diagnosticAdapter ?? throw new ArgumentNullException(nameof(diagnosticAdapter));
        _communityAdapter = communityAdapter ?? throw new ArgumentNullException(nameof(communityAdapter));
        _announcementAdapter = announcementAdapter ?? throw new ArgumentNullException(nameof(announcementAdapter));
        _mascotAdapter = mascotAdapter ?? throw new ArgumentNullException(nameof(mascotAdapter));

        _debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(35);
        _maxDelayDeadline = maxDelayDeadline ?? TimeSpan.FromMilliseconds(150);
        _timeProvider = timeProvider ?? TimeProvider.System;

        _publishedRevision = 1;
        _currentSnapshot = new HomeSnapshot(
            PublishedRevision: _publishedRevision,
            GeneratedAt: _timeProvider.GetUtcNow(),
            Launch: new LaunchSectionSnapshot(
                ModeTitle: "单人 / 本地战局",
                ModeSubtitle: "环境初始化中...",
                GamePath: "",
                CanLaunch: false,
                ActiveProfileName: "默认方案",
                ActiveModCount: 0,
                IsConfigReady: false),
            Environment: new EnvironmentSectionSnapshot(
                LoaderVersion: "检测中",
                IsLoaderEnabled: false,
                IsGraphicsAccelerationActive: false,
                IsFallbackRendering: false,
                IsEnvironmentHealthy: true),
            Diagnostics: new DiagnosticSectionSnapshot(
                HealthSummary: "尚未分析运行日志",
                UnresolvedIssuesCount: 0,
                CanExportDiagnostics: false),
            Featured: FeedSectionSnapshot<FeaturedPluginItem>.Uninitialized(),
            Announcements: FeedSectionSnapshot<PatchNoteItem>.Uninitialized(),
            Mascot: SafeGetMascot(_mascotAdapter)
        );
    }

    public void RequestRefresh()
    {
        lock (_debounceLock)
        {
            if (_disposed) return;

            var now = _timeProvider.GetUtcNow();
            _firstPendingRequestTime ??= now;
            var elapsed = now - _firstPendingRequestTime.Value;

            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            TimeSpan waitTime;
            if (elapsed + _debounceDelay >= _maxDelayDeadline)
            {
                var remaining = _maxDelayDeadline - elapsed;
                waitTime = remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
            }
            else
            {
                waitTime = _debounceDelay;
            }

            if (waitTime == TimeSpan.Zero)
            {
                _firstPendingRequestTime = null;
                _ = Task.Run(() => ExecuteRefreshCycleAsync(CancellationToken.None));
            }
            else
            {
                _ = Task.Delay(waitTime, _timeProvider, token).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully && !token.IsCancellationRequested)
                    {
                        lock (_debounceLock)
                        {
                            _firstPendingRequestTime = null;
                        }
                        _ = ExecuteRefreshCycleAsync(CancellationToken.None);
                    }
                }, TaskScheduler.Default);
            }
        }
    }

    public Task RefreshAsync(CancellationToken token = default)
    {
        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _firstPendingRequestTime = null;
        }

        return ExecuteRefreshCycleAsync(token);
    }

    private async Task ExecuteRefreshCycleAsync(CancellationToken callerToken)
    {
        callerToken.ThrowIfCancellationRequested();

        CancellationTokenSource linkedCts;
        long generation;

        lock (_stateLock)
        {
            if (_disposed) return;

            generation = Interlocked.Increment(ref _refreshGeneration);

            _activeGenerationCts?.Cancel();
            _activeGenerationCts?.Dispose();
            _activeGenerationCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token, callerToken);
            linkedCts = _activeGenerationCts;
        }

        var token = linkedCts.Token;

        try
        {
            // PHASE 1: 快速本地分区 (启动、环境、诊断、吉祥物) 先行组装与局部故障隔离
            var launchTask = SafeGetLaunchAsync(_launchAdapter, token);
            var envTask = SafeGetEnvironmentAsync(_environmentAdapter, token);
            var diagTask = SafeGetDiagnosticAsync(_diagnosticAdapter, token);
            var mascot = SafeGetMascot(_mascotAdapter);

            await Task.WhenAll(launchTask, envTask, diagTask).ConfigureAwait(false);

            if (callerToken.IsCancellationRequested)
            {
                callerToken.ThrowIfCancellationRequested();
            }

            if (token.IsCancellationRequested || Volatile.Read(ref _refreshGeneration) != generation)
            {
                return; // 代际已过期或已被新任务 supersede，放弃发布
            }

            var launch = await launchTask.ConfigureAwait(false);
            var env = await envTask.ConfigureAwait(false);
            var diag = await diagTask.ConfigureAwait(false);

            HomeSnapshot phase1Snapshot;
            lock (_stateLock)
            {
                if (callerToken.IsCancellationRequested)
                {
                    callerToken.ThrowIfCancellationRequested();
                }

                if (_disposed || token.IsCancellationRequested || Volatile.Read(ref _refreshGeneration) != generation)
                    return;

                long newRevision = ++_publishedRevision;
                phase1Snapshot = new HomeSnapshot(
                    PublishedRevision: newRevision,
                    GeneratedAt: _timeProvider.GetUtcNow(),
                    Launch: launch,
                    Environment: env,
                    Diagnostics: diag,
                    Featured: _currentSnapshot.Featured,
                    Announcements: _currentSnapshot.Announcements,
                    Mascot: mascot
                );
                _currentSnapshot = phase1Snapshot;
            }

            PublishSnapshotChanged(phase1Snapshot);

            // PHASE 2: 慢速网络分区 (社区推荐、版本公告) 并发异步获取与局部故障隔离
            var featuredTask = SafeGetFeedAsync(
                t => _communityAdapter.GetFeaturedFeedAsync(t),
                token);
            var announcementsTask = SafeGetFeedAsync(
                t => _announcementAdapter.GetAnnouncementsFeedAsync(t),
                token);

            await Task.WhenAll(featuredTask, announcementsTask).ConfigureAwait(false);

            if (callerToken.IsCancellationRequested)
            {
                callerToken.ThrowIfCancellationRequested();
            }

            if (token.IsCancellationRequested || Volatile.Read(ref _refreshGeneration) != generation)
            {
                return; // 代际已过期，放弃发布慢速分区
            }

            var featured = await featuredTask.ConfigureAwait(false);
            var announcements = await announcementsTask.ConfigureAwait(false);

            HomeSnapshot phase2Snapshot;
            lock (_stateLock)
            {
                if (callerToken.IsCancellationRequested)
                {
                    callerToken.ThrowIfCancellationRequested();
                }

                if (_disposed || token.IsCancellationRequested || Volatile.Read(ref _refreshGeneration) != generation)
                    return;

                long newRevision = ++_publishedRevision;
                phase2Snapshot = new HomeSnapshot(
                    PublishedRevision: newRevision,
                    GeneratedAt: _timeProvider.GetUtcNow(),
                    Launch: _currentSnapshot.Launch,
                    Environment: _currentSnapshot.Environment,
                    Diagnostics: _currentSnapshot.Diagnostics,
                    Featured: featured,
                    Announcements: announcements,
                    Mascot: _currentSnapshot.Mascot
                );
                _currentSnapshot = phase2Snapshot;
            }

            PublishSnapshotChanged(phase2Snapshot);
        }
        catch (OperationCanceledException)
        {
            // Ticket 04 冻结契约：调用者主动传入的取消，必须严格继续传播，不能静默转换为 RanToCompletion
            if (callerToken.IsCancellationRequested)
            {
                throw;
            }

            // 仅吞掉明确属于内部代际 supersede 或 Coordinator 释放的取消
            if (_disposed || _lifetimeCts.IsCancellationRequested || Volatile.Read(ref _refreshGeneration) != generation)
            {
                return;
            }

            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeSnapshotCoordinator] Refresh cycle encountered unexpected error: {ex}");
        }
    }

    private static async Task<LaunchSectionSnapshot> SafeGetLaunchAsync(
        ILaunchStateAdapter adapter,
        CancellationToken token)
    {
        try
        {
            return await adapter.GetLaunchSectionAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeSnapshotCoordinator] Launch adapter failure: {ex}");
            return new LaunchSectionSnapshot(
                ModeTitle: "启动状态检测受阻",
                ModeSubtitle: "启动环境状态读取失败，请检查游戏安装路径",
                GamePath: "",
                CanLaunch: false,
                ActiveProfileName: "未知方案",
                ActiveModCount: 0,
                IsConfigReady: false);
        }
    }

    private static async Task<EnvironmentSectionSnapshot> SafeGetEnvironmentAsync(
        IEnvironmentStateAdapter adapter,
        CancellationToken token)
    {
        try
        {
            return await adapter.GetEnvironmentSectionAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeSnapshotCoordinator] Environment adapter failure: {ex}");
            return new EnvironmentSectionSnapshot(
                LoaderVersion: "检测失败",
                IsLoaderEnabled: false,
                IsGraphicsAccelerationActive: false,
                IsFallbackRendering: true,
                IsEnvironmentHealthy: false);
        }
    }

    private static async Task<DiagnosticSectionSnapshot> SafeGetDiagnosticAsync(
        IDiagnosticStateAdapter adapter,
        CancellationToken token)
    {
        try
        {
            return await adapter.GetDiagnosticSectionAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeSnapshotCoordinator] Diagnostic adapter failure: {ex}");
            return new DiagnosticSectionSnapshot(
                HealthSummary: "诊断分析服务不可用",
                UnresolvedIssuesCount: 1,
                CanExportDiagnostics: false);
        }
    }

    private static MascotSectionSnapshot SafeGetMascot(IMascotPresentationAdapter adapter)
    {
        try
        {
            return adapter.GetMascotSection();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeSnapshotCoordinator] Mascot adapter failure: {ex}");
            return new MascotSectionSnapshot(IsVisible: false, QuoteIndex: 0, CurrentQuote: "");
        }
    }

    private static async Task<FeedSectionSnapshot<T>> SafeGetFeedAsync<T>(
        Func<CancellationToken, Task<FeedSectionSnapshot<T>>> fetcher,
        CancellationToken token)
    {
        try
        {
            return await fetcher(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeSnapshotCoordinator] Feed adapter failure: {ex}");
            var category = ex switch
            {
                TaskCanceledException or TimeoutException => FeedFailureCategory.Timeout,
                System.Net.Http.HttpRequestException => FeedFailureCategory.NetworkUnavailable,
                System.Text.Json.JsonException => FeedFailureCategory.DataCorrupted,
                _ => FeedFailureCategory.ServerUnavailable
            };
            return FeedSectionSnapshot<T>.Failed(category, lastSynchronized: null, canRetry: true);
        }
    }

    private void PublishSnapshotChanged(HomeSnapshot snapshot)
    {
        try
        {
            SnapshotChanged?.Invoke(this, new HomeSnapshotChangedEventArgs(snapshot));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeSnapshotCoordinator] Event handler exception: {ex}");
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }

        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();

        _activeGenerationCts?.Cancel();
        _activeGenerationCts?.Dispose();
        _activeGenerationCts = null;
    }
}
