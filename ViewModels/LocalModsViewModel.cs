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
    private readonly PluginProfileService _profiles;
    private readonly IUserDialogService _dialogs;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private CancellationTokenSource? _refreshCts;
    private ViewState _state = ViewState.Idle;
    private string _fingerprint = "";
    private bool _initialized;
    private bool _communitySyncCompleted;
    private PluginProfile? _selectedProfile;
    private string _newProfileName = "";
    private string _profileStatusText = "尚未创建插件方案。";

    public LocalModsViewModel()
        : this(new CommunityApiClient(), new CommunityModInstaller(), null, new UserDialogService())
    {
    }

    public LocalModsViewModel(
        CommunityApiClient api,
        CommunityModInstaller installer,
        LocalModService? localMods,
        IUserDialogService dialogs,
        PluginProfileService? profiles = null)
    {
        _api = api;
        _installer = installer;
        _localMods = localMods ?? new LocalModService(installer);
        _profiles = profiles ?? new PluginProfileService(_localMods);
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
        CreateProfileCommand = new AsyncRelayCommand(CreateProfileAsync, () => !string.IsNullOrWhiteSpace(NewProfileName));
        ApplyProfileCommand = new AsyncRelayCommand(ApplyProfileAsync, () => SelectedProfile is not null);
        SaveProfileCommand = new AsyncRelayCommand(SaveProfileAsync, () => SelectedProfile is not null);
        DeleteProfileCommand = new AsyncRelayCommand(DeleteProfileAsync, () => SelectedProfile is not null);
    }

    public ObservableCollection<ModItem> Mods { get; } = [];
    public ObservableCollection<PluginProfile> PluginProfiles { get; } = [];
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
    public string ProfileStatusText { get => _profileStatusText; private set => SetProperty(ref _profileStatusText, value); }
    public string NewProfileName
    {
        get => _newProfileName;
        set
        {
            if (!SetProperty(ref _newProfileName, value)) return;
            RaiseProfileCommandCanExecuteChanged();
        }
    }
    public PluginProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value)) return;
            RaiseProfileCommandCanExecuteChanged();
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand OpenCommunityCommand { get; }
    public ICommand ToggleCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand CreateProfileCommand { get; }
    public ICommand ApplyProfileCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }

    public event Action<int>? OpenCommunityRequested;
    public event Action<UserNotice>? NoticeRaised;

    public async Task ActivateAsync()
    {
        RefreshProfiles();
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
            RefreshProfiles();
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
                item.RemoteVersion = remote.EffectiveVersion;
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

    private Task CreateProfileAsync()
    {
        var result = _profiles.CreateFromCurrent(NewProfileName, Mods.ToList());
        RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
        if (!result.Success) return Task.CompletedTask;

        NewProfileName = "";
        RefreshProfiles(result.Profile?.Id);
        return Task.CompletedTask;
    }

    private async Task ApplyProfileAsync()
    {
        var profile = SelectedProfile;
        if (profile is null) return;
        var confirmed = await _dialogs.ConfirmAsync(
            "应用插件方案",
            $"确定应用“{profile.Name}”吗？\n\n"
            + "UMM 会让已记录的插件恢复到保存时的启停状态；当前已安装、但未包含在该方案中的插件会被停用。"
            + "不会删除 DLL、社区安装记录或 BepInEx 配置。请确保 Unturned 已退出。");
        if (!confirmed) return;

        var result = await Task.Run(() => _profiles.Apply(profile.Id));
        RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
        if (!result.Success) return;

        RefreshProfiles(profile.Id);
        await RefreshAsync(force: true);
    }

    private Task SaveProfileAsync()
    {
        var profile = SelectedProfile;
        if (profile is null) return Task.CompletedTask;

        var result = _profiles.SaveCurrent(profile.Id, Mods.ToList());
        RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
        if (result.Success) RefreshProfiles(profile.Id);
        return Task.CompletedTask;
    }

    private async Task DeleteProfileAsync()
    {
        var profile = SelectedProfile;
        if (profile is null) return;
        if (!await _dialogs.ConfirmAsync(
                "删除插件方案",
                $"确定删除方案“{profile.Name}”吗？\n\n只会删除该方案的启停记录，不会删除任何插件文件或修改当前状态。"))
            return;

        var result = _profiles.Delete(profile.Id);
        RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Warning);
        if (result.Success) RefreshProfiles();
    }

    private void RefreshProfiles(string? preferredProfileId = null)
    {
        var selectedId = preferredProfileId ?? SelectedProfile?.Id ?? _profiles.GetActiveProfileId();
        var profiles = _profiles.GetProfiles();
        PluginProfiles.Clear();
        foreach (var profile in profiles) PluginProfiles.Add(profile);

        SelectedProfile = selectedId is null
            ? null
            : PluginProfiles.FirstOrDefault(profile => profile.Id == selectedId);
        var active = PluginProfiles.FirstOrDefault(profile => profile.Id == _profiles.GetActiveProfileId());
        ProfileStatusText = active is null
            ? "尚未应用插件方案；本地插件仍按当前启停状态运行。"
            : $"当前生效：{active.Name}（{active.Summary}）";
    }

    private void RaiseProfileCommandCanExecuteChanged()
    {
        ((AsyncRelayCommand)CreateProfileCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ApplyProfileCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)SaveProfileCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)DeleteProfileCommand).RaiseCanExecuteChanged();
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
