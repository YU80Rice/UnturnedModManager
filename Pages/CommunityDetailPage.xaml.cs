using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UnturnedModManager.Helpers;
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
        ErrorBackButton.Content = BackButton.Content;
        _viewModel = App.Services.CreateCommunityDetailViewModel(id); DataContext = _viewModel;
        _viewModel.NoticeRaised += OnNoticeRaised;
        _viewModel.SignInRequested += OpenSignIn;
        _viewModel.DependencyRequested += OpenDependency;
        _viewModel.ImagePreviewRequested += OpenImagePreview;
        Loaded += async (_, _) => { Focus(); await _viewModel.LoadAsync(); };
    }
    private void OpenSignIn()
    {
        var owner = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        new AccountWindow { Owner = owner }.ShowDialog();
    }

    private void OpenDependency(int id) =>
        AppNavigationService.Current.OpenCommunityDetail(this, id);
    private void OpenImagePreview(string imageUrl)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;
        var owner = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        new ImagePreviewWindow(uri.AbsoluteUri) { Owner = owner }.ShowDialog();
    }
    private void OnNoticeRaised(UserNotice notice)
        => App.Services.Notifications.Publish(notice);
    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        AppNavigationService.Current.ReturnToOrigin(this, _origin);
    }
    private void Page_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) { BackButton_Click(sender, e); e.Handled = true; } }
    private void Page_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        => ScrollWheelRouter.RouteToNearestScrollViewer(sender, e);
}
