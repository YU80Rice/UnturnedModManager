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

    private bool _loadingSettings;

    public static readonly DependencyProperty WallpaperColumnsProperty = DependencyProperty.Register(
        nameof(WallpaperColumns), typeof(int), typeof(SettingsPage), new PropertyMetadata(4));

    public int WallpaperColumns => (int)GetValue(WallpaperColumnsProperty);

    public SettingsPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.NoticeRaised += OnNoticeRaised;
        _viewModel.AccountManagementRequested += OpenAccountManagement;
        _viewModel.OnboardingRequested += RestartOnboarding;
        Loaded += (_, _) =>
        {
            _loadingSettings = true;
            try { _viewModel.Load(); }
            finally { _loadingSettings = false; }
        };
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

    private void TopCards_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WelcomeAccountCard is null) return;
        var viewport = ActualWidth > 0 ? ActualWidth : e.NewSize.Width + 52;
        var stacked = viewport <= 1100;
        TopCardGap.Width = new GridLength(stacked ? 0 : 14);
        AccountColumn.Width = new GridLength(stacked ? 0 : 1, GridUnitType.Star);
        Grid.SetRow(WelcomeAccountCard, stacked ? 1 : 0);
        Grid.SetColumn(WelcomeAccountCard, stacked ? 0 : 2);
        SetCurrentValue(WallpaperColumnsProperty, viewport <= 820 ? 1 : 4);
    }

    private void SelectWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择启动器背景壁纸",
            Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.SetCustomWallpaper(dialog.FileName);
        }
    }

    private void ClearWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearCustomWallpaper();
    }

    private void WallpaperBlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsLoaded && !_loadingSettings) _viewModel.UpdateWallpaperBlur(e.NewValue);
    }

    private void WallpaperDimSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsLoaded && !_loadingSettings) _viewModel.UpdateWallpaperDimPercent(e.NewValue);
    }
}
