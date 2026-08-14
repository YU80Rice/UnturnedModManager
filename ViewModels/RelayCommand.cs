using System.Windows.Input;

namespace UnturnedModManager.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public event EventHandler? CanExecuteChanged;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T> : ICommand where T : class
{
    private readonly Action<T> _execute;
    private readonly Predicate<T>? _canExecute;

    public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) =>
        parameter is T value && (_canExecute?.Invoke(value) ?? true);

    public event EventHandler? CanExecuteChanged;

    public void Execute(object? parameter)
    {
        if (parameter is T value && CanExecute(value)) _execute(value);
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
