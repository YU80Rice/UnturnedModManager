using System.Windows.Input;

namespace UnturnedModManager.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _running;
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object? parameter) => !_running && (_canExecute?.Invoke() ?? true);
    public event EventHandler? CanExecuteChanged;
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await _execute(); } finally { _running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncRelayCommand<T> : ICommand where T : class
{
    private readonly Func<T, Task> _execute;
    private readonly Predicate<T>? _canExecute;
    private bool _running;

    public AsyncRelayCommand(Func<T, Task> execute, Predicate<T>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) =>
        !_running && parameter is T value && (_canExecute?.Invoke(value) ?? true);

    public event EventHandler? CanExecuteChanged;

    public async void Execute(object? parameter)
    {
        if (parameter is not T value || !CanExecute(value)) return;
        _running = true;
        RaiseCanExecuteChanged();
        try { await _execute(value); }
        finally
        {
            _running = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
