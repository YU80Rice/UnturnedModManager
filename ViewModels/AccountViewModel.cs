using System.Windows.Input;
using UnturnedModManager.Services;

namespace UnturnedModManager.ViewModels;

public sealed class AccountViewModel : ViewModelBase, IDisposable
{
    private readonly CommunityAuthService _authentication;
    private readonly IUserDialogService _dialogs;
    private CancellationTokenSource? _loginCts;
    private bool _isBusy;

    public AccountViewModel(CommunityAuthService authentication, IUserDialogService dialogs)
    {
        _authentication = authentication;
        _dialogs = dialogs;
        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsBusy && !IsSignedIn);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy && (IsSignedIn || IsSessionPending));
        LogoutCommand = new AsyncRelayCommand(LogoutAsync, () => !IsBusy && HasCachedUser);
        CancelCommand = new RelayCommand(CancelLogin, () => IsBusy);
        _authentication.SessionChanged += RefreshVisual;
        _authentication.RestoreCachedUser();
        RefreshVisual();
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(CanLogin));
            RaiseCommands();
        }
    }
    public bool IsSignedIn => _authentication.IsSignedIn;
    public bool HasCachedUser => _authentication.HasCachedUser;
    public bool IsSessionPending => _authentication.IsSessionPending;
    public bool CanLogin => !IsBusy && !IsSignedIn;
    public string Username => _authentication.CurrentUser?.Username ?? "未登录";
    public string AccountHint => IsSignedIn
        ? "已连接 unmod.online 社区账户"
        : IsSessionPending
            ? "已保存账户，等待网络验证后即可使用社区功能。"
        : IsBusy
            ? "请在浏览器完成人机验证，启动器正在等待安全回调…"
            : "登录后可下载插件并同步社区个人数据。";
    public string AvatarFallback => HasCachedUser && !string.IsNullOrWhiteSpace(Username)
        ? Username[..1].ToUpperInvariant()
        : "?";
    public string? AvatarUrl
    {
        get
        {
            var value = _authentication.CurrentUser?.AvatarUrl ?? AppSettings.CommunityAvatarUrl;
            if (string.IsNullOrWhiteSpace(value)) return null;
            return Uri.TryCreate(value, UriKind.Absolute, out _)
                ? value
                : CommunityApiClient.BaseUrl + "/" + value.TrimStart('/');
        }
    }
    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);

    public ICommand LoginCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand CancelCommand { get; }
    public event Action<UserNotice>? NoticeRaised;

    public async Task RestoreAsync()
    {
        if (string.IsNullOrWhiteSpace(AppSettings.CommunityAuthToken)) return;
        await _authentication.RestoreAsync();
        RefreshVisual();
    }

    private async Task LoginAsync()
    {
        _loginCts?.Cancel();
        _loginCts?.Dispose();
        _loginCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        IsBusy = true;
        RefreshVisual();
        try
        {
            var result = await _authentication.LoginViaBrowserAsync(_loginCts.Token);
            RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
        }
        finally
        {
            IsBusy = false;
            RefreshVisual();
        }
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var success = await _authentication.RestoreAsync();
            RaiseNotice(
                success ? "账户资料已刷新。" : "无法验证登录状态，请检查网络或重新登录。",
                success ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
        }
        finally { IsBusy = false; RefreshVisual(); }
    }

    private async Task LogoutAsync()
    {
        if (!await _dialogs.ConfirmAsync("退出社区账户", "确定退出当前社区账户吗？本地插件和游戏设置不会被删除。")) return;
        IsBusy = true;
        try
        {
            await _authentication.LogoutAsync();
            RaiseNotice("已安全退出社区账户。", UserNoticeSeverity.Information);
        }
        finally { IsBusy = false; RefreshVisual(); }
    }

    public void CancelLogin() => _loginCts?.Cancel();

    private void RefreshVisual()
    {
        OnPropertyChanged(nameof(IsSignedIn));
        OnPropertyChanged(nameof(HasCachedUser));
        OnPropertyChanged(nameof(IsSessionPending));
        OnPropertyChanged(nameof(CanLogin));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(AccountHint));
        OnPropertyChanged(nameof(AvatarFallback));
        OnPropertyChanged(nameof(AvatarUrl));
        OnPropertyChanged(nameof(HasAvatar));
        RaiseCommands();
    }

    private void RaiseCommands()
    {
        ((AsyncRelayCommand)LoginCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)RefreshCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)LogoutCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
    }

    private void RaiseNotice(string message, UserNoticeSeverity severity) =>
        NoticeRaised?.Invoke(new UserNotice(message, severity));

    public void Dispose()
    {
        _loginCts?.Cancel();
        _loginCts?.Dispose();
        _authentication.SessionChanged -= RefreshVisual;
    }
}
