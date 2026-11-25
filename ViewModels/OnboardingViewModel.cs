using System.Collections.ObjectModel;
using System.Windows.Input;
using Color = System.Windows.Media.Color;
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
    private readonly ThemePreference _originalPreference;
    private readonly string _originalAccent;
    private readonly double _originalTint;
    private Color _accentColor;
    private double _surfaceTint;
    private bool _finished;
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
        _originalPreference = _themes.CurrentPreference;
        _originalAccent = _themes.CurrentAccentHex;
        _originalTint = _themes.CurrentSurfaceTint;
        _selectedTheme = ThemeChoices.First(choice => choice.Value == _originalPreference);
        _accentColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString(AppSettings.AccentHex);
        _surfaceTint = AppSettings.SurfaceTint;

        ChangeThemeCommand = new RelayCommand<string>(value =>
            SelectedTheme = ThemeChoices.First(choice => choice.Value == ThemeService.Parse(value)));
        BrowseCommand = new RelayCommand(Browse);
        DetectCommand = new AsyncRelayCommand(DetectAsync, () => !IsBusy);
        NextCommand = new RelayCommand(Next, () => !IsBusy);
        BackCommand = new RelayCommand(Back, () => !IsBusy && CurrentStep > 0);
        FinishCommand = new RelayCommand(Finish, () => !IsBusy);
        SkipCommand = new RelayCommand(Skip, () => !IsBusy);
    }

    public ObservableCollection<ThemeChoice> ThemeChoices { get; }
    public string GamePath
    {
        get => _gamePath;
        set => SetProperty(ref _gamePath, value);
    }

    public bool IsLightSelected => SelectedTheme?.Value == ThemePreference.Light;
    public bool IsDarkSelected => SelectedTheme?.Value == ThemePreference.Dark;
    public bool IsSystemSelected => SelectedTheme?.Value == ThemePreference.System;
    public bool IsDarkTheme => _themes.AppliedTheme == ThemePreference.Dark;
    public Color AccentColor
    {
        get => _accentColor;
        set { if (SetProperty(ref _accentColor, value)) ApplyAccent(persist: false); }
    }
    public double SurfaceTint
    {
        get => _surfaceTint;
        set { if (SetProperty(ref _surfaceTint, Math.Clamp(value, 0, 40))) ApplyAccent(persist: false); }
    }

    private void ApplyAccent(bool persist) =>
        _themes.ApplyAccent($"#{AccentColor.R:X2}{AccentColor.G:X2}{AccentColor.B:X2}", SurfaceTint, persist);

    public void RestorePreview()
    {
        if (_finished) return;
        if (_themes.CurrentPreference != _originalPreference) _themes.Apply(_originalPreference, persist: false);
        if (_themes.CurrentAccentHex != _originalAccent || _themes.CurrentSurfaceTint != _originalTint)
            _themes.ApplyAccent(_originalAccent, _originalTint, persist: false);
    }

    public ThemeChoice? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (!SetProperty(ref _selectedTheme, value) || value is null) return;
            _themes.Apply(value.Value, persist: false);
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(IsLightSelected));
            OnPropertyChanged(nameof(IsDarkSelected));
            OnPropertyChanged(nameof(IsSystemSelected));
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
    public string StepText => $"步骤 {CurrentStep + 1} / 3";
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

    public ICommand ChangeThemeCommand { get; }
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
            _themes.Apply(theme.Value);
        }
        ApplyAccent(persist: true);
        _finished = true;
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
