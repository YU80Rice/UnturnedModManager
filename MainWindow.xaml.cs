using System.Windows;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using UnturnedModManager.Services;

namespace UnturnedModManager;

public partial class MainWindow : FluentWindow
{
    private readonly ThemeService _themeService = App.Services.Theme;
    private readonly CommunityAuthService _authService = App.Services.Authentication;
    public MainWindow()
    {
        InitializeComponent();
        _authService.SessionChanged += UpdateAccountVisual;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += (_, _) => _authService.SessionChanged -= UpdateAccountVisual;
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestoreWindowBounds();
        _themeService.Initialize(AppSettings.CommunityThemeMode);
        UpdateThemeButton(_themeService.AppliedTheme);
        NavigationView.IsPaneOpen = AppSettings.IsNavigationPaneOpen;
        ApplyPaneVisualState(NavigationView.IsPaneOpen);
        _authService.RestoreCachedUser();
        UpdateAccountVisual();
        NavigationView.Navigate(typeof(Pages.HomePage));
        _ = RestoreAccountAsync();
    }
    private void RestoreWindowBounds()
    {
        Width = Math.Max(MinWidth, AppSettings.WindowWidth);
        Height = Math.Max(MinHeight, AppSettings.WindowHeight);
        if (!double.IsNaN(AppSettings.WindowLeft) && !double.IsNaN(AppSettings.WindowTop))
        {
            Left = AppSettings.WindowLeft;
            Top = AppSettings.WindowTop;
        }
        if (AppSettings.IsWindowMaximized) WindowState = WindowState.Maximized;
    }
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        AppSettings.WindowWidth = bounds.Width;
        AppSettings.WindowHeight = bounds.Height;
        AppSettings.WindowLeft = bounds.Left;
        AppSettings.WindowTop = bounds.Top;
        AppSettings.IsWindowMaximized = WindowState == WindowState.Maximized;
    }
    private void UpdateThemeButton(ThemePreference preference)
    {
        var isLight = preference == ThemePreference.Light;
        ThemeToggleButtonIcon.Symbol = isLight ? SymbolRegular.WeatherMoon24 : SymbolRegular.WeatherSunny24;
        ThemeToggleText.Text = _themeService.CurrentPreference == ThemePreference.System
            ? $"跟随系统（{(isLight ? "浅色" : "深色")}）"
            : isLight ? "浅色模式" : "深色模式";
    }
    public void ApplyThemeFromSettings() { _themeService.Initialize(AppSettings.CommunityThemeMode); UpdateThemeButton(_themeService.AppliedTheme); }
    private void NavigationView_PaneOpened(object sender, RoutedEventArgs e) { AppSettings.IsNavigationPaneOpen = true; ApplyPaneVisualState(true); }
    private void NavigationView_PaneClosed(object sender, RoutedEventArgs e) { AppSettings.IsNavigationPaneOpen = false; ApplyPaneVisualState(false); }
    private void ApplyPaneVisualState(bool isOpen)
    {
        ThemeToggleText.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        ThemeToggleButton.Margin = isOpen ? new Thickness(12, 4, 12, 12) : new Thickness(0, 4, 0, 12);
        ThemeToggleButton.Width = isOpen ? double.NaN : 64;
        ThemeToggleButton.HorizontalAlignment = isOpen ? System.Windows.HorizontalAlignment.Stretch : System.Windows.HorizontalAlignment.Center;
        AccountNameText.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        AccountButton.Width = isOpen ? double.NaN : 64;
        AccountButton.Margin = isOpen ? new Thickness(12, 4, 12, 4) : new Thickness(0, 4, 0, 4);
        AccountButton.HorizontalAlignment = isOpen ? System.Windows.HorizontalAlignment.Stretch : System.Windows.HorizontalAlignment.Center;
    }
    private async Task RestoreAccountAsync() { await _authService.RestoreAsync(); UpdateAccountVisual(); }
    private void UpdateAccountVisual()
    {
        var username = _authService.CurrentUser?.Username ?? AppSettings.CommunityUsername;
        AccountNameText.Text = string.IsNullOrWhiteSpace(username) ? "登录社区账户" : username;
        AccountAvatarText.Text = string.IsNullOrWhiteSpace(username) ? "?" : username[..1].ToUpperInvariant();
        AccountButton.ToolTip = string.IsNullOrWhiteSpace(username) ? "登录社区账户" : $"社区账户：{username}";
        var avatarUrl = _authService.CurrentUser?.AvatarUrl ?? AppSettings.CommunityAvatarUrl;
        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            try
            {
                var absolute = Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri)
                    ? uri
                    : new Uri(CommunityApiClient.BaseUrl + "/" + avatarUrl.TrimStart('/'));
                AccountAvatarImage.Source = new BitmapImage(absolute);
                AccountAvatarImage.Visibility = Visibility.Visible;
                AccountAvatarText.Visibility = Visibility.Collapsed;
                return;
            }
            catch { }
        }
        AccountAvatarImage.Source = null;
        AccountAvatarImage.Visibility = Visibility.Collapsed;
        AccountAvatarText.Visibility = Visibility.Visible;
    }
    private async void AccountButton_Click(object sender, RoutedEventArgs e)
    {
        AccountButton.IsEnabled = false;
        new AccountWindow { Owner = this }.ShowDialog();
        await _authService.RestoreAsync();
        UpdateAccountVisual(); AccountButton.IsEnabled = true;
    }
    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var mode = _themeService.CurrentPreference == ThemePreference.Light ? ThemePreference.Dark : ThemePreference.Light;
        _themeService.Apply(mode); UpdateThemeButton(mode);
    }
}
