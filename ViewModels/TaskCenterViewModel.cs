using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using UnturnedModManager.Models;
using UnturnedModManager.Services;

namespace UnturnedModManager.ViewModels;

/// <summary>任务中心的薄 ViewModel；运行、历史和重试语义都留在服务层。</summary>
public sealed class TaskCenterViewModel : ViewModelBase, IDisposable
{
    private readonly OperationTaskCenter _tasks;
    private readonly AsyncRelayCommand<OperationTaskItem> _retryCommand;
    private readonly RelayCommand _clearHistoryCommand;
    private readonly RelayCommand<OperationTaskItem> _openPluginCommand;

    public TaskCenterViewModel(OperationTaskCenter tasks)
    {
        _tasks = tasks;
        Tasks = new ReadOnlyObservableCollection<OperationTaskItem>(_tasks.Tasks);
        _retryCommand = new AsyncRelayCommand<OperationTaskItem>(RetryAsync, item => item.IsRetryAvailable && !item.IsRunning);
        _clearHistoryCommand = new RelayCommand(ClearHistory, () => _tasks.Tasks.Any(item => !item.IsRunning));
        _openPluginCommand = new RelayCommand<OperationTaskItem>(item =>
        {
            if (item.CommunityModId is > 0) OpenPluginRequested?.Invoke(item.CommunityModId.Value);
        }, item => item.CommunityModId is > 0);
        _tasks.TasksChanged += OnTasksChanged;
        _tasks.Tasks.CollectionChanged += OnCollectionChanged;
    }

    public ReadOnlyObservableCollection<OperationTaskItem> Tasks { get; }
    public bool HasTasks => Tasks.Count > 0;
    public bool HasActiveTasks => Tasks.Any(item => item.IsRunning);
    public int ActiveTaskCount => Tasks.Count(item => item.IsRunning);
    public string SummaryText => HasActiveTasks
        ? $"有 {ActiveTaskCount} 个任务正在运行；完成和失败的操作会保留在此处。"
        : HasTasks ? "以下保留本次与之前启动记录的操作历史。" : "还没有任务。安装、更新或卸载社区插件后会显示在这里。";
    public ICommand RetryCommand => _retryCommand;
    public ICommand ClearHistoryCommand => _clearHistoryCommand;
    public ICommand OpenPluginCommand => _openPluginCommand;
    public event Action<int>? OpenPluginRequested;

    private async Task RetryAsync(OperationTaskItem item)
    {
        await _tasks.RetryAsync(item);
    }

    private void ClearHistory() => _tasks.ClearFinishedHistory();

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateState();
    private void OnTasksChanged(object? sender, EventArgs e) => UpdateState();
    private void UpdateState()
    {
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasActiveTasks));
        OnPropertyChanged(nameof(ActiveTaskCount));
        OnPropertyChanged(nameof(SummaryText));
        _retryCommand.RaiseCanExecuteChanged();
        _clearHistoryCommand.RaiseCanExecuteChanged();
        _openPluginCommand.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _tasks.TasksChanged -= OnTasksChanged;
        _tasks.Tasks.CollectionChanged -= OnCollectionChanged;
    }
}
