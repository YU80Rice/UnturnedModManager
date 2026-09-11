using System;
using System.Threading;
using System.Threading.Tasks;

namespace UnturnedModManager.Domain.Home;

/// <summary>
/// 首页快照变更事件参数
/// </summary>
public sealed class HomeSnapshotChangedEventArgs(HomeSnapshot snapshot) : EventArgs
{
    public HomeSnapshot Snapshot { get; } = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    public long PublishedRevision => Snapshot.PublishedRevision;
}

/// <summary>
/// 首页快照协调器契约 (绑定于所属宿主生命周期作用域，单向驱动只读不可变投影)
/// </summary>
public interface IHomeSnapshotCoordinator : IDisposable
{
    /// <summary>
    /// 当前最新已发布的完整帧快照
    /// </summary>
    HomeSnapshot CurrentSnapshot { get; }

    /// <summary>
    /// 当组装完成新快照帧时通知订阅者 (按 PublishedRevision 单调递增派发)
    /// </summary>
    event EventHandler<HomeSnapshotChangedEventArgs>? SnapshotChanged;

    /// <summary>
    /// 请求合并刷新 (事件驱动防抖，短时密集触发自动合并，含最大延迟超时防饿死保证)
    /// </summary>
    void RequestRefresh();

    /// <summary>
    /// 异步执行单次刷新
    /// </summary>
    Task RefreshAsync(CancellationToken token = default);
}
