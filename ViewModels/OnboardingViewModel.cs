using System.Collections.ObjectModel;
using System.Windows.Input;
using UnturnedModManager.Services;

namespace UnturnedModManager.ViewModels;

/// <summary>
/// 首次启动引导。它只负责收集最小必要设置，不把用户锁在强制登录或强制安装流程中。
/// </summary>
public sealed class OnboardingViewModel : ViewModelBase
{
    private readonly GamePathService _gamePaths;
    private readonly IFolderPickerService _folderPicker;
    private readonly ThemeService _themes;
    private string _gamePath = "";
    private ThemeChoice? _selectedTheme;
    private ThemePaletteChoice? _selectedPalette;
    private int _step;
    private bool _isBusy;
    private string _message = "";
    private UserNoticeSeverity _messageSeverity = UserNoticeSeverity.Information;

    public OnboardingViewModel(
        GamePathService gamePaths,
        IFolderPickerService folderPicker,
        ThemeService themes)
    {
        _gamePaths = gamePaths;
        _folderPicker = folderPicker;
        _themes = themes;
        _gamePath = AppSettings.UnturnedInstallPath;
        ThemeChoices =
        [
            new(ThemePreference.System, "跟随系统"),
            new(ThemePreference.Light, "浅色"),
            new(ThemePreference.Dark, "深色")
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
        var currentTheme = ThemeService.Parse(AppSettings.CommunityThemeMode);
        _selectedTheme = ThemeChoices.First(choice => choice.Value == currentTheme);
        _selectedPalette = PaletteChoices.First(choice => choice.Value == ThemeService.ParsePalette(AppSettings.CommunityColorPalette));

        BrowseCommand = new RelayCommand(Browse);
        DetectCommand = new AsyncRelayCommand(DetectAsync, () => !IsBusy);
        NextCommand = new RelayCommand(Next, () => !IsBusy);
        BackCommand = new RelayCommand(Back, () => !IsBusy && CurrentStep > 0);
        FinishCommand = new RelayCommand(Finish, () => !IsBusy);
        SkipCommand = new RelayCommand(Skip, () => !IsBusy);
    }

    public ObservableCollection<ThemeChoice> ThemeChoices { get; }
    public ObservableCollection<ThemePaletteChoice> PaletteChoices { get; }
    public string GamePath
    {
        get => _gamePath;
        set => SetProperty(ref _gamePath, value);
    }

    public ThemePaletteChoice? SelectedPalette
    {
        get => _selectedPalette;
        set
        {
            if (!SetProperty(ref _selectedPalette, value) || value is null) return;
            _themes.ApplyPalette(value.Value, persist: false);
        }
    }

    public ThemeChoice? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (!SetProperty(ref _selectedTheme, value) || value is null) return;
            _themes.Apply(value.Value, persist: false);
        }
    }

    public int CurrentStep
    {
        get => _step;
        private set
        {
            if (!SetProperty(ref _step, value)) return;
            OnPropertyChanged(nameof(IsWelcomeStep));
            OnPropertyChanged(nameof(IsThemeStep));
            OnPropertyChanged(nameof(IsFinishStep));
            OnPropertyChanged(nameof(StepText));
            OnPropertyChanged(nameof(NextButtonText));
            ((RelayCommand)BackCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsWelcomeStep => CurrentStep == 0;
    public bool IsThemeStep => CurrentStep == 1;
    public bool IsFinishStep => CurrentStep == 2;
    public string StepText => $"第 {CurrentStep + 1} 步，共 3 步";
    public string NextButtonText => IsWelcomeStep ? "下一步" : "继续";
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            ((AsyncRelayCommand)DetectCommand).RaiseCanExecuteChanged();
            ((RelayCommand)NextCommand).RaiseCanExecuteChanged();
            ((RelayCommand)BackCommand).RaiseCanExecuteChanged();
            ((RelayCommand)FinishCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SkipCommand).RaiseCanExecuteChanged();
        }
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public UserNoticeSeverity MessageSeverity
    {
        get => _messageSeverity;
        private set => SetProperty(ref _messageSeverity, value);
    }

    public ICommand BrowseCommand { get; }
    public ICommand DetectCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand FinishCommand { get; }
    public ICommand SkipCommand { get; }
    public event Action? Completed;

    private void Browse()
    {
        var selected = _folderPicker.PickFolder(GamePath, "选择 Unturned 安装目录");
        if (!string.IsNullOrWhiteSpace(selected))
        {
            GamePath = selected;
            SetMessage(_gamePaths.IsValid(GamePath)
                ? "已选择有效的 Unturned 安装目录。"
                : "该目录中没有找到 Unturned.exe，请重新选择。",
                _gamePaths.IsValid(GamePath) ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
        }
    }

    private async Task DetectAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _gamePaths.DetectAsync();
            if (path is null)
                SetMessage("没有自动找到 Unturned，请点击浏览手动选择。", UserNoticeSeverity.Warning);
            else
            {
                GamePath = path;
                SetMessage("已找到 Unturned 安装目录。", UserNoticeSeverity.Success);
            }
        }
        finally { IsBusy = false; }
    }

    private void Next()
    {
        if (IsWelcomeStep && !string.IsNullOrWhiteSpace(GamePath) && !_gamePaths.IsValid(GamePath))
        {
            SetMessage("所选目录不是有效的 Unturned 安装目录；也可以暂时跳过，稍后在设置中配置。", UserNoticeSeverity.Warning);
            return;
        }

        CurrentStep = Math.Min(2, CurrentStep + 1);
        ClearMessage();
    }

    private void Back()
    {
        CurrentStep = Math.Max(0, CurrentStep - 1);
        ClearMessage();
    }

    private void Finish()
    {
        if (_gamePaths.IsValid(GamePath))
            AppSettings.UnturnedInstallPath = GamePath.Trim();

        if (SelectedTheme is { } theme)
        {
            AppSettings.CommunityThemeMode = theme.Value.ToString();
            _themes.Apply(theme.Value);
        }
        if (SelectedPalette is { } palette)
        {
            AppSettings.CommunityColorPalette = palette.Value.ToString();
            _themes.ApplyPalette(palette.Value);
        }

        AppSettings.IsOnboardingCompleted = true;
        Completed?.Invoke();
    }

    private void Skip()
    {
        AppSettings.IsOnboardingCompleted = true;
        Completed?.Invoke();
    }

    private void SetMessage(string message, UserNoticeSeverity severity)
    {
        Message = message;
        MessageSeverity = severity;
    }

    private void ClearMessage()
    {
        Message = "";
        MessageSeverity = UserNoticeSeverity.Information;
    }
}
