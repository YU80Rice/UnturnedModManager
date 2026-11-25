using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using UnturnedModManager.Controls;
using UnturnedModManager.Controls.AppShell;
using UnturnedModManager.Helpers;
using UnturnedModManager.Pages;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Page = System.Windows.Controls.Page;
using System.Collections.ObjectModel;
using Wpf.Ui.Controls;
using UnturnedModManager.Navigation;
using UnturnedModManager.Services;
using UnturnedModManager.ViewModels;

namespace UnturnedModManager;

public partial class MainWindow : FluentWindow
{
    private readonly ThemeService _themeService = App.Services?.Theme ?? new ThemeService();
    private readonly CommunityAuthService _authService = App.Services?.Authentication ?? new CommunityAuthService();
    private readonly UserNotificationService _notifications = App.Services?.Notifications ?? new UserNotificationService();
    private readonly LocalModService? _localMods = App.Services?.LocalMods;
    private readonly ObservableCollection<ToastNotification> _toasts = [];
    private readonly SemaphoreSlim _dropImportGate = new(1, 1);
    private Page? _currentPage;
    private int? _pendingCommunityDetailId;
    private readonly LaunchEnvironmentState? _launchEnvironment = App.Services?.LaunchEnvironment;
    private readonly GameLaunchCoordinator? _launch = App.Services?.Launch;
    private readonly OperationTaskCenter? _tasks = App.Services?.Tasks;
    private readonly GamePathService _gamePaths = App.Services?.GamePaths ?? new();
    private readonly DispatcherTimer _launchStatusTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _accentSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly DependencyPropertyDescriptor _wheelTintProperty = DependencyPropertyDescriptor.FromProperty(
        ColorWheelControl.SurfaceTintProperty, typeof(ColorWheelControl));
    private readonly Dictionary<Type, Page> _pages = [];
    private CapsuleNavigationHost? _navigationHost;
    private (string? Local, string? Themed, string? AlternateTheme, string Preset)? _wallpaperKey;
    private bool _syncingAccentWheel;
    private (string Hex, double Tint)? _pendingAccent;

    public Page? CurrentPage => _currentPage;

    public MainWindow()
    {
        InitializeComponent();
        ToastHost.ItemsSource = _toasts;
        _authService.SessionChanged += UpdateAccountVisual;
        _notifications.NoticePublished += OnNoticePublished;
        _themeService.ThemeChanged += OnThemeChanged;
        _wheelTintProperty.AddValueChanged(AccentWheel, AccentWheel_SurfaceTintChanged);
        if (_launchEnvironment is not null) _launchEnvironment.Changed += OnLaunchEnvironmentChanged;
        if (_launch is not null) _launch.StateChanged += UpdateLaunchButton;
        if (_tasks is not null) _tasks.TasksChanged += OnTasksChanged;
        _launchStatusTimer.Tick += (_, _) => UpdateLaunchButton();
        _accentSaveTimer.Tick += (_, _) => SavePendingAccent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _navigationHost = new CapsuleNavigationHost(GetPage<PluginWorkshopPage>, Dispatcher);
        _navigationHost.StateChanged += OnNavigationStateChanged;
        AppNavigationService.Current.AttachHost(_navigationHost);

        RestoreWindowBounds();
        ApplyThemeFromSettings();
        _authService.RestoreCachedUser();
        UpdateAccountVisual();
        OnTasksChanged(null, EventArgs.Empty);
        _navigationHost.NavigateTo(new PageNavigationRequest(PrimarySlot.Play));
        ConsumePendingCommunityDetail();
        _launchStatusTimer.Start();
        _ = RestoreAccountAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _launchStatusTimer.Stop();
        SavePendingAccent();
        _wheelTintProperty.RemoveValueChanged(AccentWheel, AccentWheel_SurfaceTintChanged);
        _themeService.ThemeChanged -= OnThemeChanged;
        _authService.SessionChanged -= UpdateAccountVisual;
        _notifications.NoticePublished -= OnNoticePublished;
        if (_tasks is not null) _tasks.TasksChanged -= OnTasksChanged;
        if (_launchEnvironment is not null) _launchEnvironment.Changed -= OnLaunchEnvironmentChanged;
        if (_launch is not null) _launch.StateChanged -= UpdateLaunchButton;
        if (_navigationHost is not null)
        {
            _navigationHost.StateChanged -= OnNavigationStateChanged;
            AppNavigationService.Current.DetachHost(_navigationHost);
            _navigationHost = null;
        }
    }

    public void RefreshCustomWallpaper() => RefreshWallpaper(force: true);

    private void RefreshWallpaper(bool force = false)
    {
        var light = _themeService.AppliedTheme == ThemePreference.Light;
        var key = (AppSettings.LauncherCustomWallpaperPath,
            light ? _themeService.CustomWallpaperLightPath : _themeService.CustomWallpaperDarkPath,
            light ? _themeService.CustomWallpaperDarkPath : _themeService.CustomWallpaperLightPath,
            AppSettings.LauncherWallpaperPreset);
        if (!force && _wallpaperKey == key) return;
        _wallpaperKey = key;
        WallpaperImage.Source = WallpaperHelper.ResolveSource(key.Item1, key.Item2, key.Item3, key.Item4);
        var hasWallpaper = WallpaperImage.Source is not null;
        WallpaperImage.Visibility = WallpaperDimOverlay.Visibility = hasWallpaper ? Visibility.Visible : Visibility.Collapsed;
        _themeService.SetWallpaperPresence(hasWallpaper);
        UpdateWallpaperEffects(AppSettings.LauncherWallpaperBlurRadius, AppSettings.LauncherWallpaperDimOpacity);
    }

    public void UpdateWallpaperEffects(double blurRadius, double dimOpacity)
    {
        WallpaperBlurEffect.Radius = Math.Clamp(blurRadius, 0.0, 40.0);
        WallpaperDimOverlay.Opacity = Math.Clamp(dimOpacity, 0.10, 0.80);
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
    private void UpdateThemeButton(ThemePreference applied)
    {
        var isLight = applied == ThemePreference.Light;
        var action = isLight ? "切换为深色模式" : "切换为浅色模式";
        ThemeToggleButtonIcon.Symbol = isLight ? SymbolRegular.WeatherMoon24 : SymbolRegular.WeatherSunny24;
        ThemeToggleButton.ToolTip = action;
        AutomationProperties.SetName(ThemeToggleButton, action);
        _syncingAccentWheel = true;
        try
        {
            AccentWheel.IsDarkTheme = !isLight;
            AccentWheel.AccentColor = (Color)ColorConverter.ConvertFromString(_themeService.CurrentAccentHex);
            AccentWheel.SurfaceTint = _themeService.CurrentSurfaceTint;
        }
        finally { _syncingAccentWheel = false; }
    }

    private void OnThemeChanged(ThemePreference preference)
    {
        UpdateThemeButton(_themeService.AppliedTheme);
        RefreshWallpaper();
    }

    public void ApplyThemeFromSettings()
    {
        _themeService.Initialize(AppSettings.CommunityThemeMode);
    }

    private void AccentWheel_AccentColorChanged(object sender, RoutedPropertyChangedEventArgs<Color> e) => ApplyAccentPreview();
    private void AccentWheel_SurfaceTintChanged(object? sender, EventArgs e) => ApplyAccentPreview();
    private void AccentPopup_Closed(object? sender, EventArgs e) => SavePendingAccent();

    private void ApplyAccentPreview()
    {
        if (_syncingAccentWheel || !AccentPopup.IsOpen) return;
        var hex = ThemeService.ToHex(AccentWheel.AccentColor);
        var tint = AccentWheel.SurfaceTint;
        if (hex == _themeService.CurrentAccentHex && tint == _themeService.CurrentSurfaceTint) return;
        _themeService.ApplyAccent(hex, tint, persist: false);
        _pendingAccent = (hex, tint);
        _accentSaveTimer.Stop();
        _accentSaveTimer.Start();
    }

    private void SavePendingAccent()
    {
        _accentSaveTimer.Stop();
        if (_pendingAccent is not { } pending) return;
        _pendingAccent = null;
        if (pending.Hex != _themeService.CurrentAccentHex || pending.Tint != _themeService.CurrentSurfaceTint) return;
        AppSettings.AccentHex = pending.Hex;
        AppSettings.SurfaceTint = pending.Tint;
    }

    private T GetPage<T>() where T : Page, new()
    {
        if (!_pages.TryGetValue(typeof(T), out var page)) _pages.Add(typeof(T), page = new T());
        return (T)page;
    }

    private void NavigationTab_Click(object sender, RoutedEventArgs e)
    {
        var navigation = AppNavigationService.Current;
        switch ((sender as FrameworkElement)?.Tag as string)
        {
            case "Play": navigation.NavigateToPlay(); break;
            case "Mods": navigation.NavigateToSlot(PrimarySlot.Mods); break;
            case "TaskCenter": navigation.NavigateToTaskCenter(); break;
            case "Settings": navigation.NavigateToSettings(); break;
            case "About": navigation.NavigateToAbout(); break;
        }
    }

    private void OnNavigationStateChanged(object? sender, NavigationStateChangedEventArgs e)
    {
        if (_navigationHost is not { } host) return;
        if (host.ActiveSlot == PrimarySlot.Exit) { Close(); return; }
        var secondary = host.ActiveSecondary;
        PlayTab.IsChecked = secondary is null && host.ActiveSlot == PrimarySlot.Play;
        ModsTab.IsChecked = secondary is null && host.ActiveSlot == PrimarySlot.Mods;
        SettingsTab.IsChecked = secondary is null && host.ActiveSlot == PrimarySlot.Settings;
        TasksTab.IsChecked = secondary == SecondaryNavigationTarget.TaskCenter;
        AboutTab.IsChecked = secondary == SecondaryNavigationTarget.About;
        TaskBadge.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty,
            TasksTab.IsChecked == true ? "TextOnAccentFillColorPrimaryBrush" : "AccentFillColorDefaultBrush");
        TaskBadgeText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
            TasksTab.IsChecked == true ? "AccentFillColorDefaultBrush" : "TextOnAccentFillColorPrimaryBrush");

        Page page = secondary switch
        {
            SecondaryNavigationTarget.TaskCenter => GetPage<TaskCenterPage>(),
            SecondaryNavigationTarget.About => GetPage<AboutPage>(),
            _ => host.ActiveSlot switch
            {
                PrimarySlot.Mods => GetPage<PluginWorkshopPage>(),
                PrimarySlot.Settings => GetPage<SettingsPage>(),
                _ => GetPage<HomePage>()
            }
        };
        if (!ReferenceEquals(PageHost.Content, page)) PageHost.Navigate(page);
        if (_launchEnvironment is not null) _launchEnvironment.Refresh();
        else OnLaunchEnvironmentChanged(null, EventArgs.Empty);
    }

    private void PageHost_Navigated(object sender, NavigationEventArgs e)
    {
        _currentPage = e.Content as Page;
        while (PageHost.CanGoBack) PageHost.RemoveBackEntry();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.BrowserBack || (e.Key == Key.System && e.SystemKey == Key.Left && System.Windows.Input.Keyboard.Modifiers == ModifierKeys.Alt))
            e.Handled = AppNavigationService.Current.NavigateBack() == NavigationResult.Success;
    }

    private void OnTasksChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => OnTasksChanged(sender, e))); return; }
        var active = _tasks?.Tasks.Count(task => task.IsRunning) ?? 0;
        TaskBadge.Visibility = active > 0 ? Visibility.Visible : Visibility.Collapsed;
        TaskBadgeText.Text = active > 99 ? "99+" : active.ToString();
        AutomationProperties.SetName(TasksTab, active > 0 ? $"任务中心，{active} 项正在处理" : "任务中心");
    }

    private void OnLaunchEnvironmentChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => OnLaunchEnvironmentChanged(sender, e))); return; }
        if (_launchEnvironment is { } state)
        {
            ProfileSummaryText.Text = state.ProfileSummaryText;
            ProfileSummaryText.ToolTip = state.ProfileSummaryText;
            ProfileStatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,
                state.GlobalModsEnabled ? "AppSuccessTextBrush" : "TextFillColorTertiaryBrush");
            RunModeText.Text = state.RunModeText;
            EnvironmentText.Text = state.EnvironmentText;
        }
        var hasGame = _gamePaths.IsValid(AppSettings.UnturnedInstallPath);
        SteamStatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,
            hasGame ? "AppSuccessTextBrush" : "AppWarningTextBrush");
        SteamStatusText.Text = hasGame ? "Unturned · Steam 304930" : "Unturned · 未设置路径";
        SteamStatusText.ToolTip = hasGame ? AppSettings.UnturnedInstallPath : "请在游戏设置中选择安装路径";
        UpdateLaunchButton();
    }

    private void UpdateLaunchButton()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(UpdateLaunchButton)); return; }
        var launching = _launch?.IsLaunching == true;
        var running = !launching && _launch?.IsGameRunning == true;
        var text = launching ? "正在启动…" : running ? "游戏运行中" : "开始游戏";
        if (LaunchButtonText.Text != text)
        {
            LaunchButtonText.Text = text;
            AutomationProperties.SetName(LaunchButton, text);
        }
        LaunchButton.IsEnabled = _launch is not null && !running && !launching;
    }

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_launch is not null) await _launch.LaunchAsync();
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
        AccountNameText.Text = string.IsNullOrWhiteSpace(username) ? "登录社区账户" : username;
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
        var intents = files.Select(path => ShellAssociationService.TryParseFileIntent(path, out var intent) ? intent : null)
            .OfType<ShellFileIntent>().ToArray();
        var plugins = files.Where(path => !RequiresPackageWizard(path) && LocalModService.IsSupportedImportFile(path)).ToArray();
        if (intents.Length == 0 && plugins.Length == 0)
        {
            _notifications.Publish(new UserNotice("仅支持拖入 .ummpk、.ummtheme、.dll 或包含 BepInEx/plugins 结构的 .zip。", UserNoticeSeverity.Warning));
            return;
        }
        if (!_dropImportGate.Wait(0))
        {
            _notifications.Publish(new UserNotice("已有导入任务正在进行，请稍候。", UserNoticeSeverity.Information));
            return;
        }

        try
        {
            foreach (var intent in intents)
            {
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                HandleShellFileIntent(intent, () => completion.TrySetResult());
                await completion.Task;
            }
            if (_localMods is null || plugins.Length == 0) return;
            var result = await Task.Run(() => _localMods.Import(plugins));
            _notifications.Publish(new UserNotice(result.Message,
                result.Imported > 0 ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning));
            if (result.Imported > 0)
            {
                if (_currentPage is ILocalModsRefreshable localModsPage)
                    await localModsPage.RefreshAfterExternalImportAsync();
                _launchEnvironment?.Refresh();
            }
        }
        catch (Exception exception)
        {
            _notifications.Publish(new UserNotice($"拖放导入失败：{exception.Message}", UserNoticeSeverity.Error));
        }
        finally { _dropImportGate.Release(); }
    }

    private static void UpdateDropEffect(System.Windows.DragEventArgs e)
    {
        if (!TryGetDroppedFiles(e, out var files)) return;
        var supported = files.Any(path => RequiresPackageWizard(path)
            ? ShellAssociationService.TryParseFileIntent(path, out _) : LocalModService.IsSupportedImportFile(path));
        e.Effects = supported ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private static bool RequiresPackageWizard(string path) =>
        path.EndsWith(ShellAssociationService.ModPackageExtension, StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(ShellAssociationService.ThemePackageExtension, StringComparison.OrdinalIgnoreCase);

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
        var mode = _themeService.AppliedTheme == ThemePreference.Light ? ThemePreference.Dark : ThemePreference.Light;
        _themeService.Apply(mode);
    }

    /// <summary>
    /// Opens a community detail as a list → detail flow, so Back always returns to the community
    /// list rather than dropping the user onto an unrelated top-level page.
    /// </summary>
    public void OpenCommunityDetail(int modId)
    {
        if (modId <= 0)
            return;

        if (_navigationHost is null)
        {
            _pendingCommunityDetailId = modId;
            return;
        }

        AppNavigationService.Current.OpenCommunityDetail(modId);
    }

    private void ConsumePendingCommunityDetail()
    {
        if (_pendingCommunityDetailId is not { } modId)
            return;

        _pendingCommunityDetailId = null;
        OpenCommunityDetail(modId);
    }

    public event Action<ShellFileIntent, Action>? ShellFileIntentReceived;

    public void HandleShellFileIntent(ShellFileIntent intent, Action onCompleted)
    {
        // 1. 强制执行不可绕过的多层沙箱预检
        var sandboxResult = App.Services.Sandbox.Inspect(intent);
        if (!sandboxResult.IsValid)
        {
            var alertWindow = new Windows.SandboxAlertWindow(sandboxResult)
            {
                Owner = this
            };
            alertWindow.ShowDialog();
            onCompleted();
            return;
        }

        // 2. 沙箱通过后，流转到各向导或后续处理
        if (intent.Type == ShellFileIntentType.ModPackage)
        {
            var plan = App.Services.PluginProfiles.InspectPackagePlan(intent.FilePath);
            var wizard = new Windows.ModPackageImportWindow(plan, App.Services.PluginProfiles)
            {
                Owner = this
            };

            var confirmed = wizard.ShowDialog() == true;
            if (confirmed && wizard.Result?.Success == true)
            {
                _notifications.Publish(new UserNotice(wizard.Result.Message, UserNoticeSeverity.Success));
                if (_currentPage is ILocalModsRefreshable localModsPage)
                {
                    _ = localModsPage.RefreshAfterExternalImportAsync();
                }
                _launchEnvironment?.Refresh();
            }

            onCompleted();
            return;
        }

        // 3. 主题包导入向导
        if (intent.Type == ShellFileIntentType.ThemePackage)
        {
            var preview = App.Services.ThemePackages.InspectPackagePreview(intent.FilePath);
            if (preview is null)
            {
                _notifications.Publish(new UserNotice("未能解析该主题包，可能已损坏。", UserNoticeSeverity.Error));
                onCompleted();
                return;
            }

            var wizard = new Windows.ThemePackageImportWindow(preview, App.Services.ThemePackages, _themeService)
            {
                Owner = this
            };

            var confirmed = wizard.ShowDialog() == true;
            if (confirmed && wizard.Result?.Success == true)
            {
                UpdateThemeButton(_themeService.AppliedTheme);
                _notifications.Publish(new UserNotice(
                    wizard.AppliedImmediately
                        ? $"已成功导入并应用主题“{wizard.Result.Theme?.Name}”。"
                        : $"已成功导入主题“{wizard.Result.Theme?.Name}”至主题库。",
                    UserNoticeSeverity.Success));
            }

            onCompleted();
            return;
        }

        if (ShellFileIntentReceived is not null)
        {
            ShellFileIntentReceived.Invoke(intent, onCompleted);
            return;
        }

        _notifications.Publish(new UserNotice(
            $"沙箱预检通过：{System.IO.Path.GetFileName(intent.FilePath)}",
            UserNoticeSeverity.Success));
        onCompleted();
    }
}
