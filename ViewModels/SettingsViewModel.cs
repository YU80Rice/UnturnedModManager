using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using UnturnedModManager.Models;
using UnturnedModManager.Services;

namespace UnturnedModManager.ViewModels;

public sealed record ThemeChoice(ThemePreference Value, string Label);

public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly GamePathService _gamePaths;
    private readonly IFolderPickerService _folderPicker;
    private readonly ThemeService _themes;
    private readonly CommunityAuthService _authentication;
    private readonly IUserDialogService _dialogs;
    private readonly ThemePackageService _themePackages;
    private string? _currentWallpaperPath;
    private double _wallpaperBlurRadius = 15.0;
    private double _wallpaperDimPercent = 35.0;
    private string _gamePath = "";
    private ThemeChoice? _selectedTheme;
    private ThemePaletteChoice? _selectedPalette;
    private CustomTheme? _selectedCustomTheme;
    private bool _isHomeWelcomeEnabled;
    private bool _isBusy;
    private bool _isShellAssociationRegistered;
    private string _shellAssociationStatusText = "";

    public SettingsViewModel(
        GamePathService gamePaths,
        IFolderPickerService folderPicker,
        ThemeService themes,
        CommunityAuthService authentication,
        IUserDialogService dialogs,
        ThemePackageService? themePackages = null)
    {
        _gamePaths = gamePaths;
        _folderPicker = folderPicker;
        _themes = themes;
        _authentication = authentication;
        _dialogs = dialogs;
        _themePackages = themePackages ?? new ThemePackageService();
        ThemeChoices =
        [
            new(ThemePreference.Light, "浅色"),
            new(ThemePreference.Dark, "深色"),
            new(ThemePreference.System, "跟随系统")
        ];
        PaletteChoices =
        [
            new(ThemePalette.Fluent, "默认 Fluent"),
            new(ThemePalette.WarmPaper, "暖米白 · UMM 蓝"),
            new(ThemePalette.MascotOrange, "吉祥物橙"),
            new(ThemePalette.MistyForest, "松林雾绿"),
            new(ThemePalette.OceanDusk, "深海雾蓝"),
            new(ThemePalette.KleinBlue, "克莱因蓝"),
            new(ThemePalette.Lavender, "夜雾紫")
        ];
        BrowseCommand = new RelayCommand(Browse);
        DetectCommand = new AsyncRelayCommand(DetectAsync, () => !IsBusy);
        SaveCommand = new RelayCommand(Save, () => !IsBusy);
        ManageAccountCommand = new RelayCommand(() => AccountManagementRequested?.Invoke());
        RestartOnboardingCommand = new AsyncRelayCommand(RestartOnboardingAsync, () => !IsBusy);
        ExportThemeCommand = new AsyncRelayCommand(ExportThemeAsync);
        ImportThemeCommand = new AsyncRelayCommand(ImportThemeAsync);
        ResetThemeCommand = new RelayCommand(ResetTheme);
        RepairShellAssociationCommand = new RelayCommand(RepairShellAssociation);
        UnregisterShellAssociationCommand = new RelayCommand(UnregisterShellAssociation);
        _authentication.SessionChanged += OnSessionChanged;
        Load();
    }

    public ObservableCollection<ThemeChoice> ThemeChoices { get; }
    public ObservableCollection<ThemePaletteChoice> PaletteChoices { get; }
    public ObservableCollection<CustomTheme> CustomThemes { get; } = [];
    public CustomTheme? SelectedCustomTheme
    {
        get => _selectedCustomTheme;
        set
        {
            if (!SetProperty(ref _selectedCustomTheme, value) || value is null) return;
            var wallpaper = string.IsNullOrWhiteSpace(value.BackgroundAsset)
                ? null
                : Path.Combine(AppDataPaths.RootDirectory, "themes", value.Id, value.BackgroundAsset);
            _themes.ApplyCustomTheme(value, wallpaper);
        }
    }
    public string GamePath { get => _gamePath; set => SetProperty(ref _gamePath, value); }
    public bool IsHomeWelcomeEnabled
    {
        get => _isHomeWelcomeEnabled;
        set
        {
            if (!SetProperty(ref _isHomeWelcomeEnabled, value)) return;
            AppSettings.IsHomeWelcomeEnabled = value;
        }
    }
    public ThemeChoice? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (!SetProperty(ref _selectedTheme, value) || value is null) return;
            _themes.Apply(value.Value);
        }
    }
    public ThemePaletteChoice? SelectedPalette
    {
        get => _selectedPalette;
        set
        {
            if (!SetProperty(ref _selectedPalette, value) || value is null) return;
            _themes.ApplyPalette(value.Value);
        }
    }

    public string? CurrentWallpaperPath
    {
        get => _currentWallpaperPath;
        private set => SetProperty(ref _currentWallpaperPath, value);
    }

    public double WallpaperBlurRadius
    {
        get => _wallpaperBlurRadius;
        set
        {
            if (SetProperty(ref _wallpaperBlurRadius, Math.Clamp(value, 0.0, 40.0)))
            {
                OnPropertyChanged(nameof(WallpaperBlurLabelText));
            }
        }
    }

    public double WallpaperDimPercent
    {
        get => _wallpaperDimPercent;
        set
        {
            if (SetProperty(ref _wallpaperDimPercent, Math.Clamp(value, 10.0, 80.0)))
            {
                OnPropertyChanged(nameof(WallpaperDimLabelText));
            }
        }
    }

    public string WallpaperBlurLabelText
    {
        get
        {
            var r = (int)Math.Round(WallpaperBlurRadius);
            return r switch
            {
                0 => "0 px (无模糊)",
                <= 15 => $"{r} px (柔和)",
                <= 25 => $"{r} px (毛玻璃)",
                _ => $"{r} px (重度模糊)"
            };
        }
    }

    public string WallpaperDimLabelText => $"{(int)Math.Round(WallpaperDimPercent)}%";
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            ((AsyncRelayCommand)DetectCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)RestartOnboardingCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        }
    }
    public string AccountStatus => _authentication.IsSignedIn
        ? $"已登录：{_authentication.CurrentUser!.Username}"
        : _authentication.IsSessionPending
            ? $"已保存账户：{_authentication.CurrentUser!.Username}（等待联网验证）"
        : string.IsNullOrWhiteSpace(AppSettings.CommunityUsername)
            ? "未登录。登录后可下载插件并同步个人数据。"
            : $"已保存账户：{AppSettings.CommunityUsername}（等待联网验证）";
    public string AccountActionText => _authentication.IsSignedIn ? "管理账户" : "登录社区账户";
    public bool IsShellAssociationRegistered
    {
        get => _isShellAssociationRegistered;
        private set => SetProperty(ref _isShellAssociationRegistered, value);
    }

    public string ShellAssociationStatusText
    {
        get => _shellAssociationStatusText;
        private set => SetProperty(ref _shellAssociationStatusText, value);
    }

    public ICommand BrowseCommand { get; }
    public ICommand DetectCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ManageAccountCommand { get; }
    public ICommand RestartOnboardingCommand { get; }
    public ICommand ExportThemeCommand { get; }
    public ICommand ImportThemeCommand { get; }
    public ICommand ResetThemeCommand { get; }
    public ICommand RepairShellAssociationCommand { get; }
    public ICommand UnregisterShellAssociationCommand { get; }
    public event Action<UserNotice>? NoticeRaised;
    public event Action? AccountManagementRequested;
    public event Action? OnboardingRequested;

    public void Load()
    {
        GamePath = AppSettings.UnturnedInstallPath;
        var preference = ThemeService.Parse(AppSettings.CommunityThemeMode);
        _selectedTheme = ThemeChoices.First(choice => choice.Value == preference);
        OnPropertyChanged(nameof(SelectedTheme));
        _selectedPalette = PaletteChoices.First(choice => choice.Value == ThemeService.ParsePalette(AppSettings.CommunityColorPalette));
        OnPropertyChanged(nameof(SelectedPalette));
        _isHomeWelcomeEnabled = AppSettings.IsHomeWelcomeEnabled;
        OnPropertyChanged(nameof(IsHomeWelcomeEnabled));
        RefreshAccount();
        RefreshCustomThemes();
        RefreshShellAssociationStatus();

        _currentWallpaperPath = AppSettings.LauncherCustomWallpaperPath;
        OnPropertyChanged(nameof(CurrentWallpaperPath));
        _wallpaperBlurRadius = AppSettings.LauncherWallpaperBlurRadius;
        OnPropertyChanged(nameof(WallpaperBlurRadius));
        OnPropertyChanged(nameof(WallpaperBlurLabelText));
        _wallpaperDimPercent = AppSettings.LauncherWallpaperDimOpacity * 100.0;
        OnPropertyChanged(nameof(WallpaperDimPercent));
        OnPropertyChanged(nameof(WallpaperDimLabelText));
    }

    public void SetCustomWallpaper(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            ClearCustomWallpaper();
            return;
        }

        if (!UnturnedModManager.Helpers.WallpaperHelper.IsValidImageFile(path))
        {
            RaiseNotice("所选图片格式不受支持或文件已损坏", UserNoticeSeverity.Warning);
            return;
        }

        AppSettings.LauncherCustomWallpaperPath = path;
        CurrentWallpaperPath = path;
        RaiseNotice("背景壁纸已更新", UserNoticeSeverity.Success);
        NotifyMainWindowWallpaperChanged();
    }

    public void ClearCustomWallpaper()
    {
        AppSettings.LauncherCustomWallpaperPath = null;
        CurrentWallpaperPath = null;
        RaiseNotice("已恢复纯净纯色外观", UserNoticeSeverity.Information);
        NotifyMainWindowWallpaperChanged();
    }

    public void UpdateWallpaperBlur(double blur)
    {
        var clamped = Math.Clamp(blur, 0.0, 40.0);
        AppSettings.LauncherWallpaperBlurRadius = clamped;
        WallpaperBlurRadius = clamped;
        NotifyMainWindowWallpaperEffects();
    }

    public void UpdateWallpaperDimPercent(double percent)
    {
        var clamped = Math.Clamp(percent, 10.0, 80.0);
        AppSettings.LauncherWallpaperDimOpacity = clamped / 100.0;
        WallpaperDimPercent = clamped;
        NotifyMainWindowWallpaperEffects();
    }

    private static void NotifyMainWindowWallpaperChanged()
    {
        var app = System.Windows.Application.Current;
        if (app is null) return;

        if (app.Dispatcher.CheckAccess())
        {
            if (app.Windows.OfType<MainWindow>().FirstOrDefault() is { } mw)
            {
                mw.RefreshCustomWallpaper();
            }
        }
        else if (!app.Dispatcher.HasShutdownStarted && !app.Dispatcher.HasShutdownFinished)
        {
            try
            {
                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (app.Windows.OfType<MainWindow>().FirstOrDefault() is { } mw)
                    {
                        mw.RefreshCustomWallpaper();
                    }
                }));
            }
            catch { }
        }
    }

    private void NotifyMainWindowWallpaperEffects()
    {
        var app = System.Windows.Application.Current;
        if (app is null) return;

        var blur = WallpaperBlurRadius;
        var dim = WallpaperDimPercent / 100.0;

        if (app.Dispatcher.CheckAccess())
        {
            if (app.Windows.OfType<MainWindow>().FirstOrDefault() is { } mw)
            {
                mw.UpdateWallpaperEffects(blur, dim);
            }
        }
        else if (!app.Dispatcher.HasShutdownStarted && !app.Dispatcher.HasShutdownFinished)
        {
            try
            {
                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (app.Windows.OfType<MainWindow>().FirstOrDefault() is { } mw)
                    {
                        mw.UpdateWallpaperEffects(blur, dim);
                    }
                }));
            }
            catch { }
        }
    }

    public void RefreshCustomThemes()
    {
        CustomThemes.Clear();
        foreach (var theme in _themePackages.GetInstalledThemes())
            CustomThemes.Add(theme);
    }

    private void Browse()
    {
        var selected = _folderPicker.PickFolder(GamePath, "选择 Unturned 安装目录");
        if (!string.IsNullOrWhiteSpace(selected)) GamePath = selected;
    }

    private async Task DetectAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _gamePaths.DetectAsync();
            if (path is null)
                RaiseNotice("未能自动找到游戏，请手动选择安装目录。", UserNoticeSeverity.Warning);
            else
            {
                GamePath = path;
                RaiseNotice("已找到 Unturned 安装目录，请确认后保存。", UserNoticeSeverity.Success);
            }
        }
        finally { IsBusy = false; }
    }

    private void Save()
    {
        var path = GamePath.Trim();
        if (!_gamePaths.IsValid(path))
        {
            RaiseNotice("所选目录不是有效的 Unturned 安装目录。", UserNoticeSeverity.Error);
            return;
        }
        AppSettings.UnturnedInstallPath = path;
        GamePath = path;
        RaiseNotice("设置已保存，插件页和启动页会自动使用新路径。", UserNoticeSeverity.Success);
    }

    private async Task RestartOnboardingAsync()
    {
        IsBusy = true;
        try
        {
            var confirmed = await _dialogs.ConfirmAsync(
                "重新运行首次设置",
                "将重新打开游戏目录和主题设置向导。不会删除插件、账户或当前游戏配置。是否继续？");
            if (!confirmed)
                return;

            OnboardingRequested?.Invoke();
            RaiseNotice("首次设置向导已关闭，当前设置已保留。", UserNoticeSeverity.Success);
        }
        finally { IsBusy = false; }
    }

    private async Task ImportThemeAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入 .ummtheme 自定义主题包",
            Filter = "UMM 主题包 (*.ummtheme)|*.ummtheme|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        var result = await Task.Run(() => _themePackages.ImportPackage(dialog.FileName));
        RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
        if (result.Success && result.Theme is not null)
        {
            RefreshCustomThemes();
            SelectedCustomTheme = CustomThemes.FirstOrDefault(t => t.Id == result.Theme.Id);
        }
    }

    private async Task ExportThemeAsync()
    {
        var theme = _themes.CurrentCustomTheme ?? new CustomTheme
        {
            Name = "当前配色方案",
            BaseTheme = _themes.AppliedTheme,
            AccentColor = "#0078D4",
            BackgroundColor = _themes.AppliedTheme == ThemePreference.Dark ? "#1E1E1E" : "#F3F3F3",
            CardBackgroundColor = _themes.AppliedTheme == ThemePreference.Dark ? "#2D2D2D" : "#FFFFFF"
        };

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出 .ummtheme 主题包",
            Filter = "UMM 主题包 (*.ummtheme)|*.ummtheme",
            FileName = $"{theme.Name}.ummtheme"
        };
        if (dialog.ShowDialog() != true) return;

        var result = await Task.Run(() => _themePackages.ExportPackage(theme, _themes.CustomWallpaperPath, dialog.FileName));
        RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
    }

    private void ResetTheme()
    {
        _themes.ResetToDefaultTheme();
        _selectedCustomTheme = null;
        OnPropertyChanged(nameof(SelectedCustomTheme));
        Load();
        RaiseNotice("已恢复默认主题设置。", UserNoticeSeverity.Success);
    }

    private void OnSessionChanged() => RefreshAccount();
    private void RefreshAccount()
    {
        OnPropertyChanged(nameof(AccountStatus));
        OnPropertyChanged(nameof(AccountActionText));
    }

    public void RefreshShellAssociationStatus()
    {
        if (AppDataPaths.IsIsolatedProfile)
        {
            IsShellAssociationRegistered = false;
            ShellAssociationStatusText = "当前处于便携隔离模式（UMM_DATA_DIRECTORY），已禁用系统注册表关联。";
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            IsShellAssociationRegistered = false;
            ShellAssociationStatusText = "当前非 Windows 操作系统，不支持系统文件关联。";
            return;
        }

        var registered = ShellAssociationService.IsRegistered();
        IsShellAssociationRegistered = registered;
        ShellAssociationStatusText = registered
            ? "已就绪：.ummpk（模组包）与 .ummtheme（主题包）已成功关联到当前启动器。"
            : "未完全关联：系统尚未将 .ummpk 与 .ummtheme 关联到当前启动器。";
    }

    private void RepairShellAssociation()
    {
        if (AppDataPaths.IsIsolatedProfile)
        {
            RaiseNotice("当前处于便携隔离模式，无法修改系统文件关联。", UserNoticeSeverity.Warning);
            return;
        }

        var ok = ShellAssociationService.RegisterAssociations();
        RefreshShellAssociationStatus();
        if (ok)
        {
            RaiseNotice("已成功注册并修复 .ummpk 与 .ummtheme 打开方式。", UserNoticeSeverity.Success);
        }
        else
        {
            RaiseNotice("注册文件关联失败，请检查杀毒软件或系统权限。", UserNoticeSeverity.Error);
        }
    }

    private void UnregisterShellAssociation()
    {
        if (AppDataPaths.IsIsolatedProfile)
        {
            RaiseNotice("当前处于便携隔离模式，未修改系统文件关联。", UserNoticeSeverity.Warning);
            return;
        }

        var ok = ShellAssociationService.UnregisterAssociations();
        RefreshShellAssociationStatus();
        if (ok)
        {
            RaiseNotice("已成功解除 .ummpk 与 .ummtheme 的系统文件关联。", UserNoticeSeverity.Success);
        }
        else
        {
            RaiseNotice("解除文件关联失败。", UserNoticeSeverity.Error);
        }
    }

    private void RaiseNotice(string message, UserNoticeSeverity severity) =>
        NoticeRaised?.Invoke(new UserNotice(message, severity));

    public void Dispose() => _authentication.SessionChanged -= OnSessionChanged;
}
