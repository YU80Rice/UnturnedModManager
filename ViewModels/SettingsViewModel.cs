using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using UnturnedModManager.Windows;
using Color = System.Windows.Media.Color;
using UnturnedModManager.Models;
using UnturnedModManager.Services;

namespace UnturnedModManager.ViewModels;

public sealed record ThemeChoice(ThemePreference Value, string Label);
public sealed record WallpaperChoice(string Id, string Label);

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
    private Color _accentColor;
    private double _surfaceTint;
    private WallpaperChoice? _selectedWallpaperPreset;
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
        WallpaperPresets =
        [
            new("solid", "纯净底色"),
            new("ember", "PEI 篝火夕阳"),
            new("mist", "雾岚松林"),
            new("graphite", "极光几何星云")
        ];
        ChangeThemeCommand = new RelayCommand<string>(parameter =>
            SelectedTheme = ThemeChoices.First(choice => choice.Value == ThemeService.Parse(parameter)));
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
        _themes.ThemeChanged += OnThemeChanged;
        Load();
    }

    public ObservableCollection<ThemeChoice> ThemeChoices { get; }
    public IReadOnlyList<WallpaperChoice> WallpaperPresets { get; }
    public WallpaperChoice? SelectedWallpaperPreset
    {
        get => _selectedWallpaperPreset;
        set
        {
            if (value is not null && !Equals(value, _selectedWallpaperPreset))
                ApplyWallpaperPreset(value);
        }
    }
    public ObservableCollection<CustomTheme> CustomThemes { get; } = [];
    public CustomTheme? SelectedCustomTheme
    {
        get => _selectedCustomTheme;
        set
        {
            if (!SetProperty(ref _selectedCustomTheme, value) || value is null) return;
            string? WallpaperPath(string? asset) => string.IsNullOrWhiteSpace(asset)
                ? null
                : Path.Combine(AppDataPaths.RootDirectory, "themes", value.Id, asset);
            _themes.ApplyCustomTheme(value, WallpaperPath(value.BackgroundAsset), WallpaperPath(value.LightBackgroundAsset));
        }
    }
    public string GamePath
    {
        get => _gamePath;
        set
        {
            if (SetProperty(ref _gamePath, value))
            {
                OnPropertyChanged(nameof(IsGamePathValid));
                OnPropertyChanged(nameof(GamePathHint));
            }
        }
    }
    public bool IsGamePathValid => _gamePaths.IsValid(GamePath.Trim());
    public string GamePathHint => IsGamePathValid ? "已验证 Unturned.exe" : "请选择包含 Unturned.exe 的目录";
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
    public bool IsLightSelected => SelectedTheme?.Value == ThemePreference.Light;
    public bool IsDarkSelected => SelectedTheme?.Value == ThemePreference.Dark;
    public bool IsSystemSelected => SelectedTheme?.Value == ThemePreference.System;
    public bool IsDarkTheme => _themes.AppliedTheme == ThemePreference.Dark;
    public string CurrentThemeLabel => _themes.CurrentCustomTheme?.Name ?? "自由调色";

    public Color AccentColor
    {
        get => _accentColor;
        set
        {
            if (SetProperty(ref _accentColor, value))
                _themes.ApplyAccent(ThemeService.ToHex(value), SurfaceTint);
        }
    }

    public double SurfaceTint
    {
        get => _surfaceTint;
        set
        {
            if (SetProperty(ref _surfaceTint, AppSettings.NormalizeSurfaceTint(value)))
                _themes.ApplyAccent(ThemeService.ToHex(AccentColor), _surfaceTint);
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

    public ICommand ChangeThemeCommand { get; }
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
        OnThemeChanged(_themes.CurrentPreference);
        _isHomeWelcomeEnabled = AppSettings.IsHomeWelcomeEnabled;
        OnPropertyChanged(nameof(IsHomeWelcomeEnabled));
        RefreshAccount();
        RefreshCustomThemes();
        RefreshShellAssociationStatus();

        _currentWallpaperPath = AppSettings.LauncherCustomWallpaperPath;
        OnPropertyChanged(nameof(CurrentWallpaperPath));
        _selectedWallpaperPreset = CurrentWallpaperPath is null
            ? WallpaperPresets.FirstOrDefault(choice => choice.Id == AppSettings.LauncherWallpaperPreset)
              ?? (_themes.CustomWallpaperPath is null ? WallpaperPresets[0] : null)
            : null;
        OnPropertyChanged(nameof(SelectedWallpaperPreset));
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
        SetProperty(ref _selectedWallpaperPreset, null, nameof(SelectedWallpaperPreset));
        RaiseNotice("背景壁纸已更新", UserNoticeSeverity.Success);
        NotifyMainWindowWallpaperChanged();
    }

    public void ClearCustomWallpaper()
    {
        ApplyWallpaperPreset(WallpaperPresets[0]);
        RaiseNotice("已恢复纯净底色", UserNoticeSeverity.Information);
    }

    private void ApplyWallpaperPreset(WallpaperChoice preset)
    {
        AppSettings.LauncherWallpaperPreset = preset.Id;
        AppSettings.LauncherCustomWallpaperPath = null;
        CurrentWallpaperPath = null;
        SetProperty(ref _selectedWallpaperPreset, preset, nameof(SelectedWallpaperPreset));
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
        SyncCustomThemeSelection();
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

        var preview = await Task.Run(() => _themePackages.InspectPackagePreview(dialog.FileName));
        if (preview is null)
        {
            RaiseNotice("无法读取主题包，请确认文件完整且格式正确。", UserNoticeSeverity.Warning);
            return;
        }

        var window = new ThemePackageImportWindow(preview, _themePackages, _themes);
        if (System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault() is { } owner)
            window.Owner = owner;
        if (window.ShowDialog() == true && window.Result is { } result)
        {
            RefreshCustomThemes();
            RaiseNotice(result.Message, UserNoticeSeverity.Success);
        }
    }

    private async Task ExportThemeAsync()
    {
        var current = _themes.CurrentCustomTheme;
        var dark = ThemeService.DeriveColors(AccentColor, true, SurfaceTint);
        var light = ThemeService.DeriveColors(AccentColor, false, SurfaceTint);
        var theme = new CustomTheme
        {
            Name = current?.Name ?? "当前配色方案",
            Author = current?.Author ?? "",
            Description = current?.Description ?? "",
            Version = current?.Version ?? "1.0.0",
            BaseTheme = _themes.AppliedTheme,
            AccentColor = _themes.CurrentAccentHex,
            BackgroundColor = ThemeService.ToHex(dark["AppSemanticWindowBackgroundBrush"]),
            CardBackgroundColor = ThemeService.ToHex(dark["AppSemanticCardSurfaceBrush"]),
            CardOpacity = current?.CardOpacity ?? .94,
            CardBorderRadius = current?.CardBorderRadius ?? 12,
            LightBackgroundColor = ThemeService.ToHex(light["AppSemanticWindowBackgroundBrush"]),
            LightCardBackgroundColor = ThemeService.ToHex(light["AppSemanticCardSurfaceBrush"]),
            LightCardOpacity = current?.LightCardOpacity ?? .96,
            LightCardBorderRadius = current?.LightCardBorderRadius ?? 12
        };

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出 .ummtheme 主题包",
            Filter = "UMM 主题包 (*.ummtheme)|*.ummtheme",
            FileName = $"{theme.Name}.ummtheme"
        };
        if (dialog.ShowDialog() != true) return;

        var usePackageWallpaper = string.IsNullOrEmpty(AppSettings.LauncherWallpaperPreset);
        var darkWallpaper = CurrentWallpaperPath ?? (usePackageWallpaper ? _themes.CustomWallpaperDarkPath : null);
        var lightWallpaper = CurrentWallpaperPath is null && usePackageWallpaper ? _themes.CustomWallpaperLightPath : null;
        var result = await Task.Run(() => _themePackages.ExportDualModePackage(theme, darkWallpaper, lightWallpaper, dialog.FileName));
        RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
    }

    private void ResetTheme()
    {
        _themes.ResetToDefaultTheme();
        SyncCustomThemeSelection();
        RaiseNotice("已恢复默认主题设置。", UserNoticeSeverity.Success);
    }

    private void OnThemeChanged(ThemePreference preference)
    {
        SetProperty(ref _selectedTheme, ThemeChoices.First(choice => choice.Value == preference), nameof(SelectedTheme));
        OnPropertyChanged(nameof(IsLightSelected));
        OnPropertyChanged(nameof(IsDarkSelected));
        OnPropertyChanged(nameof(IsSystemSelected));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(CurrentThemeLabel));
        SetProperty(ref _accentColor, ThemeService.ParseColor(_themes.CurrentAccentHex), nameof(AccentColor));
        SetProperty(ref _surfaceTint, _themes.CurrentSurfaceTint, nameof(SurfaceTint));
        SyncCustomThemeSelection();
    }

    private void SyncCustomThemeSelection()
    {
        var activeId = _themes.CurrentCustomTheme?.Id;
        SetProperty(ref _selectedCustomTheme, CustomThemes.FirstOrDefault(theme => theme.Id == activeId), nameof(SelectedCustomTheme));
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

    public void Dispose()
    {
        _authentication.SessionChanged -= OnSessionChanged;
        _themes.ThemeChanged -= OnThemeChanged;
    }
}
