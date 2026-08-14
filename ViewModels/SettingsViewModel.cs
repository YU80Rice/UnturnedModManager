using System.Collections.ObjectModel;
using System.Windows.Input;
using UnturnedModManager.Services;

namespace UnturnedModManager.ViewModels;

public sealed record ThemeChoice(ThemePreference Value, string Label);

public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly GamePathService _gamePaths;
    private readonly IFolderPickerService _folderPicker;
    private readonly ThemeService _themes;
    private readonly CommunityAuthService _authentication;
    private string _gamePath = "";
    private ThemeChoice? _selectedTheme;
    private bool _isBusy;

    public SettingsViewModel(
        GamePathService gamePaths,
        IFolderPickerService folderPicker,
        ThemeService themes,
        CommunityAuthService authentication)
    {
        _gamePaths = gamePaths;
        _folderPicker = folderPicker;
        _themes = themes;
        _authentication = authentication;
        ThemeChoices =
        [
            new(ThemePreference.Light, "浅色"),
            new(ThemePreference.Dark, "深色"),
            new(ThemePreference.System, "跟随系统")
        ];
        BrowseCommand = new RelayCommand(Browse);
        DetectCommand = new AsyncRelayCommand(DetectAsync, () => !IsBusy);
        SaveCommand = new RelayCommand(Save, () => !IsBusy);
        ManageAccountCommand = new RelayCommand(() => AccountManagementRequested?.Invoke());
        _authentication.SessionChanged += OnSessionChanged;
        Load();
    }

    public ObservableCollection<ThemeChoice> ThemeChoices { get; }
    public string GamePath { get => _gamePath; set => SetProperty(ref _gamePath, value); }
    public ThemeChoice? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (!SetProperty(ref _selectedTheme, value) || value is null) return;
            _themes.Apply(value.Value);
        }
    }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            ((AsyncRelayCommand)DetectCommand).RaiseCanExecuteChanged();
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
    public event Action<UserNotice>? NoticeRaised;
    public event Action? AccountManagementRequested;

    public void Load()
    {
        GamePath = AppSettings.UnturnedInstallPath;
        var preference = ThemeService.Parse(AppSettings.CommunityThemeMode);
        _selectedTheme = ThemeChoices.First(choice => choice.Value == preference);
        OnPropertyChanged(nameof(SelectedTheme));
        RefreshAccount();
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
