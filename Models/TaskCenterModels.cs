using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace UnturnedModManager.Models;

/// <summary>任务中心中可见的操作类别。</summary>
public enum OperationTaskKind
{
    Install,
    Update,
    Uninstall
}

/// <summary>任务的生命周期状态。历史记录只保存完成或失败后的最终状态。</summary>
public enum OperationTaskStatus
{
    Queued,
    Running,
    Succeeded,
    Failed
}

/// <summary>由下载器与安装器上报的结构化进度。</summary>
public sealed record TaskOperationProgress(double Percent, string Stage)
{
    public static TaskOperationProgress At(double percent, string stage) =>
        new(Math.Clamp(percent, 0, 100), stage);
}

/// <summary>原始下载字节进度；总大小未知时 <see cref="TotalBytes"/> 为 null。</summary>
public sealed record DownloadProgress(long ReceivedBytes, long? TotalBytes)
{
    public double? Percent => TotalBytes is > 0 ? ReceivedBytes * 100d / TotalBytes.Value : null;
}

/// <summary>
/// 一个可持久化的操作记录。重试委托仅在当前启动会话保留；重启后仍会保留失败原因，
/// 但需要用户在插件详情重新发起操作。
/// </summary>
public sealed class OperationTaskItem : INotifyPropertyChanged
{
    private OperationTaskStatus _status;
    private double _progress;
    private string _stage = "等待开始";
    private string? _failureReason;
    private DateTimeOffset? _completedAt;
    private int _attemptCount;
    private bool _isRetryAvailable;

    public Guid Id { get; set; } = Guid.NewGuid();
    public OperationTaskKind Kind { get; set; }
    public int? CommunityModId { get; set; }
    public string Title { get; set; } = "未命名插件";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public OperationTaskStatus Status
    {
        get => _status;
        set { if (Set(ref _status, value)) OnPropertyChanged(nameof(StatusText)); }
    }

    public double Progress
    {
        get => _progress;
        set { if (Set(ref _progress, Math.Clamp(value, 0, 100))) OnPropertyChanged(nameof(ProgressText)); }
    }

    public string Stage
    {
        get => _stage;
        set => Set(ref _stage, value);
    }

    public string? FailureReason
    {
        get => _failureReason;
        set { if (Set(ref _failureReason, value)) OnPropertyChanged(nameof(HasFailureReason)); }
    }

    public DateTimeOffset? CompletedAt
    {
        get => _completedAt;
        set => Set(ref _completedAt, value);
    }

    public int AttemptCount
    {
        get => _attemptCount;
        set { if (Set(ref _attemptCount, value)) OnPropertyChanged(nameof(AttemptText)); }
    }

    [JsonIgnore]
    public bool IsRetryAvailable
    {
        get => _isRetryAvailable;
        set => Set(ref _isRetryAvailable, value);
    }

    [JsonIgnore] public bool IsRunning => Status is OperationTaskStatus.Queued or OperationTaskStatus.Running;
    [JsonIgnore] public bool HasFailureReason => !string.IsNullOrWhiteSpace(FailureReason);
    [JsonIgnore] public bool HasCommunityPlugin => CommunityModId is > 0;
    [JsonIgnore] public string KindText => Kind switch
    {
        OperationTaskKind.Install => "安装",
        OperationTaskKind.Update => "更新",
        OperationTaskKind.Uninstall => "卸载",
        _ => "操作"
    };
    [JsonIgnore] public string StatusText => Status switch
    {
        OperationTaskStatus.Queued => "等待执行",
        OperationTaskStatus.Running => "进行中",
        OperationTaskStatus.Succeeded => "已完成",
        OperationTaskStatus.Failed => "失败",
        _ => "未知"
    };
    [JsonIgnore] public string ProgressText => IsRunning ? $"{Math.Round(Progress):0}%" : StatusText;
    [JsonIgnore] public string AttemptText => AttemptCount > 1 ? $"第 {AttemptCount} 次尝试" : "首次尝试";
    [JsonIgnore] public string TimeText => (CompletedAt ?? CreatedAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm");

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
