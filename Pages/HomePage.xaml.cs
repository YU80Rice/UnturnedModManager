using System.Windows;
using System.Windows.Controls;
using UnturnedModManager.ViewModels;

namespace UnturnedModManager.Pages;

public partial class HomePage : Page
{
    private readonly HomeViewModel _viewModel = App.Services.CreateHomeViewModel();

    public HomePage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.NoticeRaised += OnNoticeRaised;
        Loaded += async (_, _) => await _viewModel.ActivateAsync();
    }

    private void OnNoticeRaised(UserNotice notice)
        => App.Services.Notifications.Publish(notice);
}
