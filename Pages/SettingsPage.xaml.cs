using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using UnturnedModManager.Helpers;
using UnturnedModManager.ViewModels;

namespace UnturnedModManager.Pages;

public partial class SettingsPage : Page
{
    private readonly SettingsViewModel _viewModel = App.Services.CreateSettingsViewModel();

    public SettingsPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.NoticeRaised += OnNoticeRaised;
        _viewModel.AccountManagementRequested += OpenAccountManagement;
        _viewModel.OnboardingRequested += RestartOnboarding;
        Loaded += (_, _) => _viewModel.Load();
    }

    private void OpenAccountManagement()
    {
        var owner = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        new AccountWindow { Owner = owner }.ShowDialog();
    }

    private static void RestartOnboarding()
    {
        if (System.Windows.Application.Current is App app)
        {
            // Keep the settings command's async completion separate from opening a modal
            // window. This prevents a nested modal loop from being entered while the command
            // still owns a confirmation dialog.
            _ = app.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(app.RestartOnboarding));
        }
    }

    private void OnNoticeRaised(UserNotice notice)
        => App.Services.Notifications.Publish(notice);

    private void CardPanel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        => ScrollWheelRouter.RouteToNearestScrollViewer(sender, e);
}
