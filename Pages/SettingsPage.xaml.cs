using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UnturnedModManager.ViewModels;
using Wpf.Ui.Controls;

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
            app.RestartOnboarding();
    }

    private void OnNoticeRaised(UserNotice notice)
    {
        StatusInfoBar.Message = notice.Message;
        StatusInfoBar.Severity = notice.Severity switch
        {
            UserNoticeSeverity.Success => InfoBarSeverity.Success,
            UserNoticeSeverity.Warning => InfoBarSeverity.Warning,
            UserNoticeSeverity.Error => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational
        };
        StatusInfoBar.IsOpen = true;
    }

    private void CardPanel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject current) return;
        while (current is not null)
        {
            if (current is ScrollViewer viewer)
            {
                viewer.ScrollToVerticalOffset(viewer.VerticalOffset - e.Delta);
                e.Handled = true;
                return;
            }
            current = VisualTreeHelper.GetParent(current);
        }
    }
}
