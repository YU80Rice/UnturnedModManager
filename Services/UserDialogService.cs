using System.Windows;

namespace UnturnedModManager.Services;

public interface IUserDialogService
{
    Task<bool> ConfirmAsync(string title, string message);
}

public sealed class UserDialogService : IUserDialogService
{
    public Task<bool> ConfirmAsync(string title, string message)
    {
        var result = System.Windows.MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return Task.FromResult(result == System.Windows.MessageBoxResult.Yes);
    }
}
