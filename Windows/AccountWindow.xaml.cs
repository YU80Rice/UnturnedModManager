using System.Windows;
using UnturnedModManager.ViewModels;

namespace UnturnedModManager;

public partial class AccountWindow : Window
{
    private readonly AccountViewModel _viewModel = App.Services.CreateAccountViewModel();

    public AccountWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.NoticeRaised += OnNoticeRaised;
        Loaded += async (_, _) => await _viewModel.RestoreAsync();
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void OnNoticeRaised(UserNotice notice)
        => App.Services.Notifications.Publish(notice);
}
