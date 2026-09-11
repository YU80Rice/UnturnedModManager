using System;
using System.Collections.Immutable;

namespace UnturnedModManager.Domain.Home;

/// <summary>
/// 首页纯只读不可变投影快照 (平台中立、无 Command、无 ViewModel、无底层 DTO)
/// </summary>
public sealed record HomeSnapshot(
    long PublishedRevision,
    DateTimeOffset GeneratedAt,
    LaunchSectionSnapshot Launch,
    EnvironmentSectionSnapshot Environment,
    DiagnosticSectionSnapshot Diagnostics,
    FeedSectionSnapshot<FeaturedPluginItem> Featured,
    FeedSectionSnapshot<PatchNoteItem> Announcements,
    MascotSectionSnapshot Mascot
);

public sealed record LaunchSectionSnapshot(
    string ModeTitle,
    string ModeSubtitle,
    string GamePath,
    bool CanLaunch,
    string ActiveProfileName,
    int ActiveModCount,
    bool IsConfigReady
);

public sealed record EnvironmentSectionSnapshot(
    string LoaderVersion,
    bool IsLoaderEnabled,
    bool IsGraphicsAccelerationActive,
    bool IsFallbackRendering,
    bool IsEnvironmentHealthy
);

public sealed record DiagnosticSectionSnapshot(
    string HealthSummary,
    int UnresolvedIssuesCount,
    bool CanExportDiagnostics
);

public sealed record MascotSectionSnapshot(
    bool IsVisible,
    int QuoteIndex,
    string CurrentQuote
);

public sealed record FeaturedPluginItem(
    string PluginId,
    string DisplayName,
    string ShortDescription,
    string CategoryTag,
    string Version
);

public sealed record PatchNoteItem(
    string VersionTag,
    DateTimeOffset ReleasedAt,
    ImmutableArray<string> Highlights)
{
    public ImmutableArray<string> Highlights { get; init; } = Highlights.IsDefault ? ImmutableArray<string>.Empty : Highlights;
}

public enum FeedDataState
{
    LiveOnline,   // 实时在线最新数据
    CachedValid,  // 命中本地有效缓存 (TTL 周期内)
    CachedStale,  // 本地缓存已过期，因网络受阻降级展示
    Empty,        // 远端正常连接但内容为空
    Failed        // 网络失败且无可用本地缓存
}

public enum FeedFailureCategory
{
    NetworkUnavailable, // 无网络连接 / DNS 失败
    ServerUnavailable,  // 远端服务故障 / 5xx / 维护中
    Timeout,            // 请求超时未响应
    DataCorrupted       // 数据格式错误或反序列化失败
}

/// <summary>
/// 内容投递分区快照 (使用真正不可变集合，保持纯值语义)
/// </summary>
public sealed record FeedSectionSnapshot<T>
{
    public FeedDataState State { get; }
    public bool IsInitialized { get; }
    public ImmutableArray<T> Items { get; }
    public DateTimeOffset? LastSynchronized { get; }
    public bool CanRetry { get; }
    public FeedFailureCategory? FailureCategory { get; }

    public FeedSectionSnapshot(
        FeedDataState State,
        bool IsInitialized,
        ImmutableArray<T> Items,
        DateTimeOffset? LastSynchronized,
        bool CanRetry,
        FeedFailureCategory? FailureCategory = null)
    {
        var normalizedItems = Items.IsDefault ? ImmutableArray<T>.Empty : Items;

        // 1. 未初始化状态守卫：IsInitialized == false 时不得与已加载/失败状态组合，且 Items 必须为空、不得包含同步时间或失败原因
        if (!IsInitialized)
        {
            if (State != FeedDataState.Empty)
            {
                throw new ArgumentException("Uninitialized feed section cannot be in a loaded or failed state.", nameof(State));
            }
            if (!normalizedItems.IsEmpty)
            {
                throw new ArgumentException("Uninitialized feed section cannot contain items.", nameof(Items));
            }
            if (LastSynchronized.HasValue)
            {
                throw new ArgumentException("Uninitialized feed section cannot have a synchronization timestamp.", nameof(LastSynchronized));
            }
            if (FailureCategory.HasValue)
            {
                throw new ArgumentException("Uninitialized feed section cannot have a failure category.", nameof(FailureCategory));
            }
        }
        else
        {
            // 2. 已初始化状态守卫
            switch (State)
            {
                case FeedDataState.Failed:
                    // Failed 必须有失败原因，且不应携带有效 Items
                    if (!FailureCategory.HasValue)
                    {
                        throw new ArgumentException("Failed feed section must specify a failure category.", nameof(FailureCategory));
                    }
                    if (!normalizedItems.IsEmpty)
                    {
                        throw new ArgumentException("Failed feed section cannot contain items.", nameof(Items));
                    }
                    break;

                case FeedDataState.LiveOnline:
                case FeedDataState.CachedValid:
                case FeedDataState.CachedStale:
                    // LiveOnline、CachedValid、CachedStale 必须有非空 Items 与 LastSynchronized
                    if (normalizedItems.IsEmpty)
                    {
                        throw new ArgumentException($"Feed state '{State}' must contain at least one item.", nameof(Items));
                    }
                    if (!LastSynchronized.HasValue)
                    {
                        throw new ArgumentException($"Feed state '{State}' must specify a LastSynchronized timestamp.", nameof(LastSynchronized));
                    }
                    if (FailureCategory.HasValue)
                    {
                        throw new ArgumentException($"Loaded feed state '{State}' cannot specify a failure category.", nameof(FailureCategory));
                    }
                    break;

                case FeedDataState.Empty:
                    // Empty 必须为空 Items，且不得包含失败原因
                    if (!normalizedItems.IsEmpty)
                    {
                        throw new ArgumentException("Empty feed section cannot contain items.", nameof(Items));
                    }
                    if (FailureCategory.HasValue)
                    {
                        throw new ArgumentException("Empty feed section cannot specify a failure category.", nameof(FailureCategory));
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(State), State, "Unknown feed data state.");
            }
        }

        this.State = State;
        this.IsInitialized = IsInitialized;
        this.Items = normalizedItems;
        this.LastSynchronized = LastSynchronized;
        this.CanRetry = CanRetry;
        this.FailureCategory = FailureCategory;
    }

    public void Deconstruct(
        out FeedDataState state,
        out bool isInitialized,
        out ImmutableArray<T> items,
        out DateTimeOffset? lastSynchronized,
        out bool canRetry,
        out FeedFailureCategory? failureCategory)
    {
        state = State;
        isInitialized = IsInitialized;
        items = Items;
        lastSynchronized = LastSynchronized;
        canRetry = CanRetry;
        failureCategory = FailureCategory;
    }

    /// <summary>
    /// 显式计算缓存年龄 (传入权威基准时间，遇时钟回拨自动截断至零，杜绝漂移与负值)
    /// </summary>
    public TimeSpan? GetCacheAge(DateTimeOffset baselineTime)
    {
        if (!LastSynchronized.HasValue) return null;
        var diff = baselineTime - LastSynchronized.Value;
        return diff < TimeSpan.Zero ? TimeSpan.Zero : diff;
    }

    public static FeedSectionSnapshot<T> Uninitialized() => new(
        FeedDataState.Empty,
        IsInitialized: false,
        ImmutableArray<T>.Empty,
        LastSynchronized: null,
        CanRetry: true);

    public static FeedSectionSnapshot<T> LiveOnline(ImmutableArray<T> items, DateTimeOffset synchronizedAt) => new(
        FeedDataState.LiveOnline,
        IsInitialized: true,
        items,
        LastSynchronized: synchronizedAt,
        CanRetry: true);

    public static FeedSectionSnapshot<T> CachedValid(ImmutableArray<T> items, DateTimeOffset synchronizedAt) => new(
        FeedDataState.CachedValid,
        IsInitialized: true,
        items,
        LastSynchronized: synchronizedAt,
        CanRetry: true);

    public static FeedSectionSnapshot<T> CachedStale(ImmutableArray<T> items, DateTimeOffset synchronizedAt) => new(
        FeedDataState.CachedStale,
        IsInitialized: true,
        items,
        LastSynchronized: synchronizedAt,
        CanRetry: true);

    public static FeedSectionSnapshot<T> Empty(DateTimeOffset? synchronizedAt = null) => new(
        FeedDataState.Empty,
        IsInitialized: true,
        ImmutableArray<T>.Empty,
        LastSynchronized: synchronizedAt,
        CanRetry: true);

    public static FeedSectionSnapshot<T> Failed(FeedFailureCategory category, DateTimeOffset? lastSynchronized = null, bool canRetry = true) => new(
        FeedDataState.Failed,
        IsInitialized: true,
        ImmutableArray<T>.Empty,
        LastSynchronized: lastSynchronized,
        CanRetry: canRetry,
        FailureCategory: category);
}
