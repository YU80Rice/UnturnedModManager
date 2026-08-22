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
    private string _gamePath = "";
    private ThemeChoice? _selectedTheme;
    private ThemePaletteChoice? _selectedPalette;
    private CustomTheme? _selectedCustomTheme;
    private bool _isHomeWelcomeEnabled;
    private bool _isBusy;

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

    public ICommand BrowseCommand { get; }
    public ICommand DetectCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ManageAccountCommand { get; }
    public ICommand RestartOnboardingCommand { get; }
    public ICommand ExportThemeCommand { get; }
    public ICommand ImportThemeCommand { get; }
    public ICommand ResetThemeCommand { get; }
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
    private void RaiseNotice(string message, UserNoticeSeverity severity) =>
        NoticeRaised?.Invoke(new UserNotice(message, severity));

    public void Dispose() => _authentication.SessionChanged -= OnSessionChanged;
}
