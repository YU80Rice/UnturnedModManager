using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using UnturnedModManager.Models;

namespace UnturnedModManager.Services;

/// <summary>
/// 集中管理社区插件的安装、更新与卸载。它把可观察的运行中任务和可持久化的历史分开：
/// 失败原因会跨重启保留，而重试操作只对仍在本次程序会话中的任务可用。
/// </summary>
public sealed class OperationTaskCenter
{
    private const int HistoryLimit = 120;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly Dictionary<Guid, Func<IProgress<TaskOperationProgress>, CancellationToken, Task>> _retryActions = [];
    private readonly string _historyPath = Path.Combine(AppDataPaths.RootDirectory, "task-history.json");

    public ObservableCollection<OperationTaskItem> Tasks { get; } = [];
    public event EventHandler? TasksChanged;

    public OperationTaskCenter()
    {
        LoadHistory();
    }

    public async Task<OperationTaskItem> RunAsync(
        OperationTaskKind kind,
        int? communityModId,
        string title,
        Func<IProgress<TaskOperationProgress>, CancellationToken, Task> operation,
        CancellationToken token = default)
    {
        var item = new OperationTaskItem
        {
            Kind = kind,
            CommunityModId = communityModId,
            Title = string.IsNullOrWhiteSpace(title) ? "未命名插件" : title.Trim(),
            Status = OperationTaskStatus.Queued,
            Stage = "等待开始"
        };

        Mutate(() =>
        {
            Tasks.Insert(0, item);
            _retryActions[item.Id] = operation;
            NotifyChanged();
        });

        await ExecuteAsync(item, operation, token);
        return item;
    }

    public async Task<bool> RetryAsync(OperationTaskItem? item, CancellationToken token = default)
    {
        if (item is null || item.IsRunning || !_retryActions.TryGetValue(item.Id, out var operation))
            return false;

        await ExecuteAsync(item, operation, token);
        return true;
    }

    public void ClearFinishedHistory()
    {
        Mutate(() =>
        {
            foreach (var item in Tasks.Where(task => !task.IsRunning).ToList())
            {
                _retryActions.Remove(item.Id);
                Tasks.Remove(item);
            }
            SaveHistory();
            NotifyChanged();
        });
    }

    private async Task ExecuteAsync(
        OperationTaskItem item,
        Func<IProgress<TaskOperationProgress>, CancellationToken, Task> operation,
        CancellationToken token)
    {
        Mutate(() =>
        {
            item.AttemptCount++;
            item.Status = OperationTaskStatus.Running;
            item.Progress = 0;
            item.Stage = "正在准备…";
            item.FailureReason = null;
            item.CompletedAt = null;
            item.IsRetryAvailable = false;
            SaveHistory();
            NotifyChanged();
        });

        var progress = new Progress<TaskOperationProgress>(value => Mutate(() =>
        {
            item.Progress = value.Percent;
            item.Stage = string.IsNullOrWhiteSpace(value.Stage) ? "正在处理…" : value.Stage;
            NotifyChanged();
        }));

        try
        {
            await operation(progress, token);
            Mutate(() =>
            {
                item.Status = OperationTaskStatus.Succeeded;
                item.Progress = 100;
                item.Stage = "已完成";
                item.CompletedAt = DateTimeOffset.Now;
                item.IsRetryAvailable = false;
                SaveHistory();
                NotifyChanged();
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Mutate(() =>
            {
                item.Status = OperationTaskStatus.Failed;
                item.Stage = "已取消";
                item.FailureReason = "操作已取消。";
                item.CompletedAt = DateTimeOffset.Now;
                item.IsRetryAvailable = _retryActions.ContainsKey(item.Id);
                SaveHistory();
                NotifyChanged();
            });
            throw;
        }
        catch (Exception exception)
        {
            Mutate(() =>
            {
                item.Status = OperationTaskStatus.Failed;
                item.Stage = "操作失败";
                item.FailureReason = DescribeException(exception);
                item.CompletedAt = DateTimeOffset.Now;
                item.IsRetryAvailable = _retryActions.ContainsKey(item.Id);
                SaveHistory();
                NotifyChanged();
            });
        }
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyPath)) return;
            var records = JsonSerializer.Deserialize<List<OperationTaskItem>>(File.ReadAllText(_historyPath)) ?? [];
            foreach (var record in records.OrderByDescending(item => item.CompletedAt ?? item.CreatedAt).Take(HistoryLimit))
            {
                if (record.Status is OperationTaskStatus.Queued or OperationTaskStatus.Running)
                {
                    record.Status = OperationTaskStatus.Failed;
                    record.Stage = "上次启动中断";
                    record.FailureReason = "应用在此任务完成前关闭；请从插件详情重新执行。";
                    record.CompletedAt ??= DateTimeOffset.Now;
                }
                record.IsRetryAvailable = false;
                Tasks.Add(record);
            }
        }
        catch
        {
            // 历史损坏不影响启动器的核心安装功能；下一次成功操作会重建历史文件。
        }
    }

    private void SaveHistory()
    {
        try
        {
            Directory.CreateDirectory(AppDataPaths.RootDirectory);
            var snapshot = Tasks
                .OrderByDescending(item => item.CompletedAt ?? item.CreatedAt)
                .Take(HistoryLimit)
                .ToList();
            File.WriteAllText(_historyPath, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch
        {
            // 写入历史失败不应掩盖已完成的真实安装结果。
        }
    }

    private void NotifyChanged() => TasksChanged?.Invoke(this, EventArgs.Empty);

    private static string DescribeException(Exception exception)
    {
        var message = exception.Message.Trim();
        return string.IsNullOrWhiteSpace(message) ? "发生未知错误。" : message;
    }

    private static void Mutate(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
            dispatcher.Invoke(action);
        else
            action();
    }
}
