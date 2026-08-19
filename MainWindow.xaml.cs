using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using Wpf.Ui.Controls;
using UnturnedModManager.Services;
using UnturnedModManager.ViewModels;

namespace UnturnedModManager;

public partial class MainWindow : FluentWindow
{
    private readonly ThemeService _themeService = App.Services.Theme;
    private readonly CommunityAuthService _authService = App.Services.Authentication;
    private readonly UserNotificationService _notifications = App.Services.Notifications;
    private readonly LocalModService _localMods = App.Services.LocalMods;
    private readonly ObservableCollection<ToastNotification> _toasts = [];
    private readonly SemaphoreSlim _dropImportGate = new(1, 1);
    private Page? _currentPage;
    private int? _pendingCommunityDetailId;
    public MainWindow()
    {
        InitializeComponent();
        ToastHost.ItemsSource = _toasts;
        _authService.SessionChanged += UpdateAccountVisual;
        _notifications.NoticePublished += OnNoticePublished;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _authService.SessionChanged -= UpdateAccountVisual;
            _notifications.NoticePublished -= OnNoticePublished;
        };
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
        ConsumePendingCommunityDetail();
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
    private void NavigationView_Navigated(object sender, NavigatedEventArgs e) => _currentPage = e.Page as Page;
    private void ApplyPaneVisualState(bool isOpen)
    {
        ThemeToggleText.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        ThemeToggleButton.Margin = isOpen ? new Thickness(12, 4, 12, 12) : new Thickness(0, 4, 0, 12);
        ThemeToggleButton.Width = isOpen ? double.NaN : 64;
        ThemeToggleButton.HorizontalAlignment = isOpen ? System.Windows.HorizontalAlignment.Stretch : System.Windows.HorizontalAlignment.Center;
        AccountNameText.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        AccountContentGrid.Width = isOpen ? double.NaN : 36;
        AccountContentGrid.HorizontalAlignment = isOpen ? System.Windows.HorizontalAlignment.Stretch : System.Windows.HorizontalAlignment.Center;
        AccountButton.HorizontalContentAlignment = isOpen ? System.Windows.HorizontalAlignment.Stretch : System.Windows.HorizontalAlignment.Center;
        AccountButton.Width = isOpen ? double.NaN : 64;
        AccountButton.Margin = isOpen ? new Thickness(12, 4, 12, 4) : new Thickness(0, 4, 0, 4);
        AccountButton.HorizontalAlignment = isOpen ? System.Windows.HorizontalAlignment.Stretch : System.Windows.HorizontalAlignment.Center;
    }
    private async Task RestoreAccountAsync()
    {
        var restored = await _authService.RestoreAsync();
        UpdateAccountVisual();
        if (restored && _authService.CurrentUser is { } user)
            _notifications.Publish(new UserNotice($"欢迎回来，{user.DisplayIdentity}", UserNoticeSeverity.Success));
    }
    private void UpdateAccountVisual()
    {
        var username = _authService.CurrentUser?.Username ?? AppSettings.CommunityUsername;
        var role = _authService.CurrentUser?.Role ?? AppSettings.CommunityRole;
        var identity = string.IsNullOrWhiteSpace(username)
            ? "登录社区账户"
            : $"{CommunityUser.DescribeRole(role)} · {username}";
        AccountNameText.Text = identity;
        AccountAvatarText.Text = string.IsNullOrWhiteSpace(username) ? "?" : username[..1].ToUpperInvariant();
        AccountButton.ToolTip = string.IsNullOrWhiteSpace(username) ? "登录社区账户" : $"社区账户：{identity}";
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
    private void OnNoticePublished(UserNotice notice)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(new Action(() => OnNoticePublished(notice)));
            return;
        }

        var toast = ToastNotification.From(notice);
        while (_toasts.Count >= 3)
            _toasts.RemoveAt(0);
        _toasts.Add(toast);
        _ = DismissToastAsync(toast);
    }

    private async Task DismissToastAsync(ToastNotification toast)
    {
        try
        {
            var duration = toast.DisplayDuration
                ?? TimeSpan.FromSeconds(toast.Severity == UserNoticeSeverity.Error ? 8 : 5);
            await Task.Delay(duration);
            if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                await Dispatcher.InvokeAsync(() => _toasts.Remove(toast));
        }
        catch { }
    }

    private void ToastBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ToastNotification toast } || !toast.HasAction)
            return;

        try
        {
            toast.InvokeAction();
            _toasts.Remove(toast);
            e.Handled = true;
        }
        catch (Exception exception)
        {
            _notifications.Publish(new UserNotice($"无法打开诊断包目录：{exception.Message}", UserNoticeSeverity.Error));
        }
    }

    private void Window_PreviewDragEnter(object sender, System.Windows.DragEventArgs e) => UpdateDropEffect(e);

    private void Window_PreviewDragOver(object sender, System.Windows.DragEventArgs e) => UpdateDropEffect(e);

    private async void Window_PreviewDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!TryGetDroppedFiles(e, out var files))
            return;

        e.Handled = true;
        if (!files.Any(LocalModService.IsSupportedImportFile))
        {
            _notifications.Publish(new UserNotice("仅支持拖入 .dll 或包含 BepInEx/plugins 结构的 .zip 插件包。", UserNoticeSeverity.Warning));
            return;
        }
        if (!_dropImportGate.Wait(0))
        {
            _notifications.Publish(new UserNotice("已有插件导入任务正在进行，请稍候。", UserNoticeSeverity.Information));
            return;
        }

        try
        {
            var result = await Task.Run(() => _localMods.Import(files));
            _notifications.Publish(new UserNotice(
                result.Message,
                result.Imported > 0 ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning));

            if (result.Imported > 0 && _currentPage is Pages.ModListPage localModsPage)
                await localModsPage.RefreshAfterExternalImportAsync();
        }
        catch (Exception exception)
        {
            _notifications.Publish(new UserNotice($"拖放导入失败：{exception.Message}", UserNoticeSeverity.Error));
        }
        finally
        {
            _dropImportGate.Release();
        }
    }

    private static void UpdateDropEffect(System.Windows.DragEventArgs e)
    {
        if (!TryGetDroppedFiles(e, out var files)) return;

        e.Effects = files.Any(LocalModService.IsSupportedImportFile)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private static bool TryGetDroppedFiles(System.Windows.DragEventArgs e, out string[] files)
    {
        files = [];
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            || e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] dropped)
            return false;

        files = dropped;
        return true;
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

    /// <summary>
    /// Opens a community detail as a list → detail flow, so Back always returns to the community
    /// list rather than dropping the user onto an unrelated top-level page.
    /// </summary>
    public void OpenCommunityDetail(int modId)
    {
        if (modId <= 0)
            return;

        if (!IsLoaded)
        {
            _pendingCommunityDetailId = modId;
            return;
        }

        if (_currentPage is Pages.CommunityPage currentCommunity)
        {
            AppNavigationService.Current.OpenCommunityDetail(currentCommunity, modId);
            return;
        }

        void OpenWhenCommunityIsReady(object? _, NavigatedEventArgs args)
        {
            if (args.Page is not Pages.CommunityPage community)
                return;

            NavigationView.Navigated -= OpenWhenCommunityIsReady;
            _currentPage = community;
            AppNavigationService.Current.OpenCommunityDetail(community, modId);
        }

        NavigationView.Navigated += OpenWhenCommunityIsReady;
        if (!NavigationView.Navigate(typeof(Pages.CommunityPage)))
        {
            NavigationView.Navigated -= OpenWhenCommunityIsReady;
            _pendingCommunityDetailId = modId;
        }
    }

    private void ConsumePendingCommunityDetail()
    {
        if (_pendingCommunityDetailId is not { } modId)
            return;

        _pendingCommunityDetailId = null;
        OpenCommunityDetail(modId);
    }
}
