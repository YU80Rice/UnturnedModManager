using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using UnturnedModManager.Models;
using UnturnedModManager.Services;

namespace UnturnedModManager.ViewModels;

public sealed class CommunityDetailViewModel : ViewModelBase, IDisposable
{
    private readonly CommunityApiClient _api;
    private readonly CommunityModInstaller _installer;
    private readonly IUserDialogService _dialogs;
    private readonly CommunityAuthService _authentication;
    private readonly OperationTaskCenter _tasks;
    private readonly int _id;
    private readonly AsyncRelayCommand _installCommand;
    private readonly AsyncRelayCommand _updateCommand;
    private readonly AsyncRelayCommand _uninstallCommand;
    private readonly AsyncRelayCommand _retryCommand;
    private CommunityModDetail? _mod;
    private ViewState _state = ViewState.Idle;
    private string _errorMessage = "";
    private bool _isBusy;
    private static readonly Regex MarkdownImagePattern = new(
        @"!\[[^\]]*\]\(\s*(?<url>https?://[^\s)]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public CommunityDetailViewModel(int id)
        : this(id, new CommunityApiClient(), new CommunityModInstaller(), new UserDialogService(), new CommunityAuthService(), new OperationTaskCenter())
    {
    }

    public CommunityDetailViewModel(
        int id,
        CommunityApiClient api,
        CommunityModInstaller installer,
        IUserDialogService dialogs,
        CommunityAuthService authentication,
        OperationTaskCenter? tasks = null)
    {
        _id = id;
        _api = api;
        _installer = installer;
        _dialogs = dialogs;
        _authentication = authentication;
        _tasks = tasks ?? new OperationTaskCenter();
        _installCommand = new AsyncRelayCommand(InstallAsync, () => CanInstall);
        _updateCommand = new AsyncRelayCommand(UpdateAsync, () => CanUpdate);
        _uninstallCommand = new AsyncRelayCommand(UninstallAsync, () => CanUninstall);
        _retryCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        SignInCommand = new RelayCommand(() => SignInRequested?.Invoke());
        OpenDependencyCommand = new RelayCommand<CommunityDependency>(dependency => DependencyRequested?.Invoke(dependency.Id));
        OpenImagePreviewCommand = new RelayCommand<string>(url => ImagePreviewRequested?.Invoke(url));
        OpenCoverPreviewCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrWhiteSpace(CoverUrl)) ImagePreviewRequested?.Invoke(CoverUrl);
        }, () => HasCover);
        _authentication.SessionChanged += OnAuthenticationChanged;
    }

    public string Title => _mod?.DisplayTitle ?? "插件详情";
    public string Meta => _mod is null
        ? ""
        : $"{_mod.Meta}\n分类：{_mod.CategoryDisplay}  ·  文件：{FormatSize(_mod.FileSize)}";
    public string Body => _mod?.DisplayBody ?? "";
    public string Dependencies => _mod?.DependencySummary ?? "正在读取依赖信息…";
    public ObservableCollection<CommunityDependency> DependencyItems { get; } = [];
    public ObservableCollection<string> GalleryImages { get; } = [];
    public bool HasDependencies => DependencyItems.Count > 0;
    public bool HasGalleryImages => GalleryImages.Count > 0;
    public string? CoverUrl => _mod?.CoverUrl;
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);
    public string AuthorText => _mod?.AuthorName ?? "未知作者";
    public string VersionText => _mod is null ? "—" : _mod.Version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? _mod.Version : $"v{_mod.Version}";
    public string DownloadsText => _mod is null ? "—" : $"{_mod.Downloads:N0}";
    public string LikesText => _mod is null ? "—" : $"{_mod.LikeCount:N0}";
    public string FileSizeText => _mod is null ? "—" : FormatSize(_mod.FileSize);
    public string CategoryText => _mod?.CategoryDisplay ?? "其他";
    public string DependencyCountText => _mod is null ? "—" : _mod.Dependencies.Count.ToString();
    public string InstallationStatusText => _mod is null
        ? "正在读取"
        : IsInstalled ? (HasUpdate ? "可更新" : "已安装") : "未安装";
    public bool IsLoading => State == ViewState.Loading;
    public bool HasError => State == ViewState.Error;
    public bool IsEmpty => State == ViewState.Empty;
    public bool IsInstalled => _mod is not null && _installer.IsInstalled(_mod.Id);
    public bool HasUpdate
    {
        get
        {
            if (_mod is null) return false;
            var installed = _installer.GetInstalledMods().FirstOrDefault(item => item.RemoteId == _mod.Id);
            return installed is not null && !VersionsEqual(installed.Version, _mod.Version);
        }
    }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(CanInstall));
            OnPropertyChanged(nameof(CanUpdate));
            OnPropertyChanged(nameof(CanUninstall));
            RaiseCommandStates();
        }
    }
    public bool RequiresLogin => _mod is not null && !_authentication.IsSignedIn && (!IsInstalled || HasUpdate);
    public bool CanInstall => _mod is not null && _authentication.IsSignedIn && !IsInstalled && !IsBusy && !IsLoading;
    public bool CanUpdate => _mod is not null && _authentication.IsSignedIn && HasUpdate && !IsBusy && !IsLoading;
    public bool CanUninstall => _mod is not null && IsInstalled && !IsBusy;
    public ViewState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value)) return;
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(CanInstall));
            OnPropertyChanged(nameof(CanUpdate));
            RaiseCommandStates();
        }
    }
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand InstallCommand => _installCommand;
    public ICommand UpdateCommand => _updateCommand;
    public ICommand UninstallCommand => _uninstallCommand;
    public ICommand RetryCommand => _retryCommand;
    public ICommand SignInCommand { get; }
    public ICommand OpenDependencyCommand { get; }
    public ICommand OpenImagePreviewCommand { get; }
    public ICommand OpenCoverPreviewCommand { get; }
    public event Action<UserNotice>? NoticeRaised;
    public event Action? SignInRequested;
    public event Action<int>? DependencyRequested;
    public event Action<string>? ImagePreviewRequested;

    public async Task LoadAsync()
    {
        _installer.Reconcile(AppSettings.UnturnedInstallPath);
        ErrorMessage = "";
        State = ViewState.Loading;
        try
        {
            _mod = await _api.GetModAsync(_id);
            DependencyItems.Clear();
            foreach (var dependency in _mod.Dependencies)
                DependencyItems.Add(dependency);
            LoadPreviewImages(_mod);
            NotifyModState();
            State = ViewState.Loaded;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载插件详情失败：{ex.Message}";
            State = ViewState.Error;
        }
    }

    private async Task InstallAsync()
    {
        if (_mod is null) return;
        IsBusy = true;
        try
        {
            var task = await _tasks.RunAsync(
                OperationTaskKind.Install,
                _mod.Id,
                _mod.DisplayTitle,
                (progress, token) => _installer.InstallWithDependenciesDetailedAsync(
                    _api, _mod, AppSettings.UnturnedInstallPath, progress, token));
            NotifyInstallationState();
            RaiseNotice(
                task.Status == OperationTaskStatus.Succeeded
                    ? "插件及其依赖已安装。可在“任务中心”查看完整记录。"
                    : $"安装失败：{task.FailureReason}。可在“任务中心”重试。",
                task.Status == OperationTaskStatus.Succeeded ? UserNoticeSeverity.Success : UserNoticeSeverity.Error);
        }
        catch (Exception ex)
        {
            RaiseNotice(ex.Message, UserNoticeSeverity.Error);
        }
        finally { IsBusy = false; }
    }

    private async Task UpdateAsync()
    {
        if (_mod is null) return;
        IsBusy = true;
        try
        {
            var task = await _tasks.RunAsync(
                OperationTaskKind.Update,
                _mod.Id,
                _mod.DisplayTitle,
                (progress, token) => _installer.UpdateDetailedAsync(
                    _api, _mod, AppSettings.UnturnedInstallPath, progress, token));
            NotifyInstallationState();
            RaiseNotice(
                task.Status == OperationTaskStatus.Succeeded
                    ? $"{_mod.DisplayTitle} 已更新至 {_mod.Version}。可在“任务中心”查看完整记录。"
                    : $"更新失败：{task.FailureReason}。可在“任务中心”重试。",
                task.Status == OperationTaskStatus.Succeeded ? UserNoticeSeverity.Success : UserNoticeSeverity.Error);
        }
        catch (Exception ex)
        {
            RaiseNotice($"更新失败：{ex.Message}", UserNoticeSeverity.Error);
        }
        finally { IsBusy = false; }
    }

    private async Task UninstallAsync()
    {
        if (_mod is null) return;
        if (!await _dialogs.ConfirmAsync(
                "确认卸载",
                $"确定卸载“{_mod.DisplayTitle}”吗？\n\n将删除安装清单记录的全部文件；被修改的文件会受到安全保护。"))
            return;

        IsBusy = true;
        try
        {
            UninstallResult? result = null;
            var task = await _tasks.RunAsync(
                OperationTaskKind.Uninstall,
                _mod.Id,
                _mod.DisplayTitle,
                async (progress, token) =>
                {
                    progress.Report(TaskOperationProgress.At(10, "正在校验插件文件是否被修改…"));
                    result = await Task.Run(
                        () => _installer.Uninstall(_mod.Id, AppSettings.UnturnedInstallPath),
                        token);
                    if (!result.Success) throw new InvalidOperationException(result.Message);
                    progress.Report(TaskOperationProgress.At(100, "已卸载并恢复可恢复文件"));
                });
            NotifyInstallationState();
            RaiseNotice(
                task.Status == OperationTaskStatus.Succeeded
                    ? result?.Message ?? "插件已卸载。"
                    : $"卸载失败：{task.FailureReason}。可在“任务中心”查看原因。",
                task.Status == OperationTaskStatus.Succeeded ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
        }
        catch (Exception ex)
        {
            RaiseNotice($"卸载失败：{ex.Message}", UserNoticeSeverity.Error);
        }
        finally { IsBusy = false; }
    }

    private void NotifyModState()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Meta));
        OnPropertyChanged(nameof(Body));
        OnPropertyChanged(nameof(Dependencies));
        OnPropertyChanged(nameof(HasDependencies));
        OnPropertyChanged(nameof(HasGalleryImages));
        OnPropertyChanged(nameof(CoverUrl));
        OnPropertyChanged(nameof(HasCover));
        OnPropertyChanged(nameof(AuthorText));
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(DownloadsText));
        OnPropertyChanged(nameof(LikesText));
        OnPropertyChanged(nameof(FileSizeText));
        OnPropertyChanged(nameof(CategoryText));
        OnPropertyChanged(nameof(DependencyCountText));
        NotifyInstallationState();
    }

    private void NotifyInstallationState()
    {
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(CanUpdate));
        OnPropertyChanged(nameof(CanUninstall));
        OnPropertyChanged(nameof(RequiresLogin));
        OnPropertyChanged(nameof(InstallationStatusText));
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        _installCommand.RaiseCanExecuteChanged();
        _updateCommand.RaiseCanExecuteChanged();
        _uninstallCommand.RaiseCanExecuteChanged();
        _retryCommand.RaiseCanExecuteChanged();
        if (OpenCoverPreviewCommand is RelayCommand coverCommand) coverCommand.RaiseCanExecuteChanged();
    }

    private void RaiseNotice(string message, UserNoticeSeverity severity) =>
        NoticeRaised?.Invoke(new UserNotice(message, severity));

    private void OnAuthenticationChanged()
    {
        OnPropertyChanged(nameof(RequiresLogin));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(CanUpdate));
        RaiseCommandStates();
    }

    private static bool VersionsEqual(string left, string right) =>
        left.Trim().TrimStart('v', 'V').Equals(
            right.Trim().TrimStart('v', 'V'),
            StringComparison.OrdinalIgnoreCase);

    private static string FormatSize(long bytes) => bytes >= 1048576
        ? $"{bytes / 1048576d:F1} MB"
        : bytes >= 1024
            ? $"{bytes / 1024d:F1} KB"
            : $"{bytes} B";

    private void LoadPreviewImages(CommunityModDetail mod)
    {
        GalleryImages.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(mod.CoverUrl))
            seen.Add(mod.CoverUrl);

        foreach (Match match in MarkdownImagePattern.Matches(mod.DisplayBody))
        {
            var url = match.Groups["url"].Value.Trim().TrimEnd('.', ',', ';');
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && seen.Add(uri.AbsoluteUri))
                GalleryImages.Add(uri.AbsoluteUri);
        }
    }

    public void Dispose()
    {
        _authentication.SessionChanged -= OnAuthenticationChanged;
        _api.Dispose();
    }
}
