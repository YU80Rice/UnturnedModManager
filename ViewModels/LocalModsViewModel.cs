using System.Collections.ObjectModel;
using System.Windows.Input;
using UnturnedModManager.Models;
using UnturnedModManager.Services;

namespace UnturnedModManager.ViewModels;

public enum UserNoticeSeverity { Information, Success, Warning, Error }
public sealed record UserNotice(string Message, UserNoticeSeverity Severity);

/// <summary>
/// 本地插件页的状态与用例协调器。文件操作位于 LocalModService，页面只负责显示和输入路由。
/// </summary>
public sealed class LocalModsViewModel : ViewModelBase, IDisposable
{
    private readonly CommunityApiClient _api;
    private readonly CommunityModInstaller _installer;
    private readonly LocalModService _localMods;
    private readonly IUserDialogService _dialogs;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private CancellationTokenSource? _refreshCts;
    private ViewState _state = ViewState.Idle;
    private string _fingerprint = "";
    private bool _initialized;
    private bool _communitySyncCompleted;

    public LocalModsViewModel()
        : this(new CommunityApiClient(), new CommunityModInstaller(), null, new UserDialogService())
    {
    }

    public LocalModsViewModel(
        CommunityApiClient api,
        CommunityModInstaller installer,
        LocalModService? localMods,
        IUserDialogService dialogs)
    {
        _api = api;
        _installer = installer;
        _localMods = localMods ?? new LocalModService(installer);
        _dialogs = dialogs;
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(force: true));
        OpenFolderCommand = new AsyncRelayCommand(() =>
        {
            OpenFolder();
            return Task.CompletedTask;
        });
        OpenCommunityCommand = new RelayCommand<ModItem>(OpenCommunity);
        ToggleCommand = new RelayCommand<ModItem>(Toggle);
        UpdateCommand = new AsyncRelayCommand<ModItem>(UpdateAsync);
        UninstallCommand = new AsyncRelayCommand<ModItem>(UninstallAsync);
    }

    public ObservableCollection<ModItem> Mods { get; } = [];
    public ViewState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value)) return;
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }
    public bool IsLoading => State == ViewState.Loading;
    public bool IsEmpty => State == ViewState.Empty;
    public string CountText => $"共 {Mods.Count} 个插件";

    public ICommand RefreshCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand OpenCommunityCommand { get; }
    public ICommand ToggleCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand UninstallCommand { get; }

    public event Action<int>? OpenCommunityRequested;
    public event Action<UserNotice>? NoticeRaised;

    public async Task ActivateAsync()
    {
        var currentFingerprint = _localMods.GetFingerprint();
        if (!_initialized || !currentFingerprint.Equals(_fingerprint, StringComparison.Ordinal))
        {
            await RefreshAsync(force: true);
            return;
        }

        if (!_communitySyncCompleted)
            await ResumeCommunitySyncAsync();
    }

    public void Deactivate() => _refreshCts?.Cancel();

    public async Task ImportAsync(IEnumerable<string> files)
    {
        var result = _localMods.Import(files);
        RaiseNotice(result.Message, result.Imported > 0 ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
        if (result.Imported > 0) await RefreshAsync(force: true);
    }

    private async Task RefreshAsync(bool force)
    {
        var currentFingerprint = _localMods.GetFingerprint();
        if (!force && _initialized && currentFingerprint.Equals(_fingerprint, StringComparison.Ordinal)) return;

        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        await _refreshGate.WaitAsync();
        try
        {
            State = ViewState.Loading;
            var items = _localMods.Scan();
            Mods.Clear();
            foreach (var item in items) Mods.Add(item);
            NotifyCollectionState();

            _fingerprint = _localMods.GetFingerprint();
            _initialized = true;
            _communitySyncCompleted = false;
            State = Mods.Count == 0 ? ViewState.Empty : ViewState.Loaded;
            await SynchronizeCommunityAsync(Mods.ToList(), token);
            _communitySyncCompleted = true;
        }
        catch (OperationCanceledException)
        {
            State = Mods.Count == 0 ? ViewState.Empty : ViewState.Loaded;
        }
        catch (Exception ex)
        {
            State = Mods.Count == 0 ? ViewState.Empty : ViewState.Loaded;
            RaiseNotice($"刷新本地插件失败：{ex.Message}", UserNoticeSeverity.Error);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task ResumeCommunitySyncAsync()
    {
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;
        await _refreshGate.WaitAsync();
        try
        {
            await SynchronizeCommunityAsync(Mods.ToList(), token);
            _communitySyncCompleted = true;
        }
        catch (OperationCanceledException) { }
        finally { _refreshGate.Release(); }
    }

    private async Task SynchronizeCommunityAsync(IReadOnlyList<ModItem> items, CancellationToken token)
    {
        foreach (var item in items)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                CommunityMod? remote = item.CommunityModId is int id
                    ? await _api.GetModAsync(id, token)
                    : await _api.FindBestMatchAsync(item.AssemblyName, token);
                if (remote is null) continue;
                item.CommunityModId = remote.Id;
                item.CommunityTitle = remote.DisplayTitle;
                item.RemoteVersion = remote.Version;
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // 单项在线匹配失败不应阻塞本地启停、卸载与导入。
            }
        }
    }

    private void Toggle(ModItem item)
    {
        var requestedState = item.IsEnabled;
        var result = _localMods.SetEnabled(item, requestedState);
        if (!result.Success) item.IsEnabled = !requestedState;
        RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Error);
        _fingerprint = _localMods.GetFingerprint();
    }

    private void OpenCommunity(ModItem item)
    {
        if (item.CommunityModId is int id) OpenCommunityRequested?.Invoke(id);
        else RaiseNotice("未在社区找到可验证的同名插件。", UserNoticeSeverity.Information);
    }

    private async Task UpdateAsync(ModItem item)
    {
        if (item is not { CommunityModId: int id, IsCommunityManaged: true }) return;
        try
        {
            var detail = await _api.GetModAsync(id);
            await _installer.UpdateAsync(
                _api,
                detail,
                AppSettings.UnturnedInstallPath,
                message => RaiseNotice(message, UserNoticeSeverity.Information));
            RaiseNotice($"{item.DisplayTitle} 已更新。", UserNoticeSeverity.Success);
            await RefreshAsync(force: true);
        }
        catch (Exception ex)
        {
            RaiseNotice($"更新失败：{ex.Message}", UserNoticeSeverity.Error);
        }
    }

    private async Task UninstallAsync(ModItem item)
    {
        var scope = item.IsCommunityManaged
            ? "该社区插件安装清单记录的全部文件"
            : $"本地文件 {item.RelativePath}";
        if (!await _dialogs.ConfirmAsync(
                "确认卸载",
                $"确定卸载“{item.DisplayTitle}”吗？\n\n将删除{scope}，此操作无法撤销。"))
            return;

        LocalModOperationResult result;
        if (item is { IsCommunityManaged: true, CommunityModId: int id })
        {
            var uninstall = _installer.Uninstall(id, AppSettings.UnturnedInstallPath);
            result = new LocalModOperationResult(uninstall.Success, uninstall.Message);
        }
        else
        {
            result = _localMods.UninstallManual(item);
        }

        RaiseNotice(
            result.Message,
            result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
        await RefreshAsync(force: true);
    }

    private void OpenFolder()
    {
        var result = _localMods.OpenPluginsFolder();
        if (!result.Success) RaiseNotice(result.Message, UserNoticeSeverity.Warning);
    }

    private void NotifyCollectionState()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void RaiseNotice(string message, UserNoticeSeverity severity)
    {
        if (!string.IsNullOrWhiteSpace(message)) NoticeRaised?.Invoke(new UserNotice(message, severity));
    }

    public void Dispose()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshGate.Dispose();
        _api.Dispose();
    }
}
