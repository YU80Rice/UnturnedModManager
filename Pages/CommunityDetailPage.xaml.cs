using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UnturnedModManager.Services;
using UnturnedModManager.ViewModels;

namespace UnturnedModManager.Pages;

public partial class CommunityDetailPage : Page
{
    private readonly CommunityDetailViewModel _viewModel;
    private readonly PageNavigationOrigin? _origin;
    public CommunityDetailPage(int id, PageNavigationOrigin? origin = null)
    {
        InitializeComponent();
        _origin = origin;
        BackButton.Content = origin?.BackLabel ?? "返回上一级";
        _viewModel = App.Services.CreateCommunityDetailViewModel(id); DataContext = _viewModel;
        _viewModel.NoticeRaised += OnNoticeRaised;
        _viewModel.SignInRequested += OpenSignIn;
        _viewModel.DependencyRequested += OpenDependency;
        Loaded += async (_, _) => { Focus(); await _viewModel.LoadAsync(); };
        Unloaded += (_, _) => _viewModel.Dispose();
    }
    private void OpenSignIn()
    {
        var owner = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        new AccountWindow { Owner = owner }.ShowDialog();
    }

    private void OpenDependency(int id) =>
        AppNavigationService.Current.OpenCommunityDetail(this, id);
    private void OnNoticeRaised(UserNotice notice)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => OnNoticeRaised(notice)); return; }
        StatusInfoBar.Message = notice.Message;
        StatusInfoBar.Severity = notice.Severity switch
        {
            UserNoticeSeverity.Success => Wpf.Ui.Controls.InfoBarSeverity.Success,
            UserNoticeSeverity.Warning => Wpf.Ui.Controls.InfoBarSeverity.Warning,
            UserNoticeSeverity.Error => Wpf.Ui.Controls.InfoBarSeverity.Error,
            _ => Wpf.Ui.Controls.InfoBarSeverity.Informational
        };
        StatusInfoBar.IsOpen = true;
    }
    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        AppNavigationService.Current.ReturnToOrigin(this, _origin);
    }
    private void Page_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) { BackButton_Click(sender, e); e.Handled = true; } }
    private void Page_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject current) return;
        while (current is not null) { if (current is ScrollViewer viewer) { viewer.ScrollToVerticalOffset(viewer.VerticalOffset - e.Delta); e.Handled = true; return; } current = VisualTreeHelper.GetParent(current); }
    }
}
