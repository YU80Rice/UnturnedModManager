using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using UnturnedModManager.Helpers;
using UnturnedModManager.Models;
using UnturnedModManager.Services;

namespace UnturnedModManager.ViewModels;

public sealed class HomeViewModel : ViewModelBase, IDisposable
{
    private readonly BepInExService _bepInEx;
    private readonly DxvkService _dxvk;
    private readonly GameLaunchService _launcher;
    private readonly DiagnosticService _diagnostics;
    private readonly GamePathService _gamePaths;
    private readonly IUserDialogService _dialogs;
    private readonly DispatcherTimer _runtimeTimer;
    private CancellationTokenSource? _operationCts;
    private bool _initialized;
    private bool _globalModsEnabled;
    private bool _dxvkEnabled;
    private bool _isBusy;
    private bool _isGameRunning;
    private bool _hasOperationProgress;
    private int _operationPercentage;
    private string _operationText = "";
    private BepInExStatus _bepStatus = new(false, false, false, null, "BepInEx 未安装", "尚未检测");
    private GpuInfo? _gpu;

    public HomeViewModel(
        BepInExService bepInEx,
        DxvkService dxvk,
        GameLaunchService launcher,
        DiagnosticService diagnostics,
        GamePathService gamePaths,
        IUserDialogService dialogs)
    {
        _bepInEx = bepInEx;
        _dxvk = dxvk;
        _launcher = launcher;
        _diagnostics = diagnostics;
        _gamePaths = gamePaths;
        _dialogs = dialogs;
        LaunchCommand = new AsyncRelayCommand(LaunchAsync, () => CanLaunch);
        InstallCommand = new AsyncRelayCommand(() => DeployBepInExAsync("安装", requireConfirmation: true), () => !IsBusy);
        RepairCommand = new AsyncRelayCommand(
            () => DeployBepInExAsync(_bepStatus.IsCurrentVersion ? "修复" : "升级", requireConfirmation: true),
            () => !IsBusy);
        UninstallCommand = new AsyncRelayCommand(UninstallBepInExAsync, () => !IsBusy && IsBepInExInstalled);
        ToggleGlobalModsCommand = new RelayCommand(ToggleGlobalMods, () => !IsBusy);
        ToggleDxvkCommand = new AsyncRelayCommand(ToggleDxvkAsync, () => !IsBusy);
        CancelOperationCommand = new RelayCommand(CancelOperation, () => IsBusy);
        ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync, () => !IsBusy);
        IgnoreCrashCommand = new RelayCommand(IgnoreCrash);
        _runtimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _runtimeTimer.Tick += (_, _) => RefreshRuntimeState();
        _runtimeTimer.Start();
    }

    public string BepInExTitle => _bepStatus.Title;
    public string BepInExDetail => _bepStatus.Detail;
    public bool IsBepInExInstalled => _bepStatus.IsInstalled;
    public bool CanInstallBepInEx => _bepStatus.HasValidGamePath && !_bepStatus.IsInstalled;
    public string BepInExRepairButtonText => _bepStatus.IsCurrentVersion
        ? "检查并修复"
        : $"升级到 {BepInExService.SupportedVersion}";
    public bool GlobalModsEnabled { get => _globalModsEnabled; set => SetProperty(ref _globalModsEnabled, value); }
    public bool DxvkEnabled { get => _dxvkEnabled; set => SetProperty(ref _dxvkEnabled, value); }
    public bool HasGpuInfo => _gpu is not null && !string.IsNullOrWhiteSpace(_gpu.Name);
    public string GpuName => _gpu is null ? "" : $"{_gpu.Name} · {_gpu.VendorName} {_gpu.ArchitectureName}";
    public string GpuRecommendation => _gpu is null ? "" : $"{_gpu.RecommendationText} — {_gpu.RecommendationDetail}";
    public bool HasCrashAlert => AppSettings.LastSessionCrashed;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(CanLaunch));
            RaiseCommandStates();
        }
    }
    public bool IsGameRunning
    {
        get => _isGameRunning;
        private set
        {
            if (!SetProperty(ref _isGameRunning, value)) return;
            OnPropertyChanged(nameof(CanLaunch));
            OnPropertyChanged(nameof(LaunchButtonText));
            RaiseCommandStates();
        }
    }
    public bool CanLaunch => !IsBusy && !IsGameRunning;
    public bool HasOperationProgress { get => _hasOperationProgress; private set => SetProperty(ref _hasOperationProgress, value); }
    public string LaunchButtonText => IsGameRunning ? "游戏正在运行" : "启动游戏";
    public int OperationPercentage { get => _operationPercentage; private set => SetProperty(ref _operationPercentage, value); }
    public string OperationText { get => _operationText; private set => SetProperty(ref _operationText, value); }

    public ICommand LaunchCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand RepairCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand ToggleGlobalModsCommand { get; }
    public ICommand ToggleDxvkCommand { get; }
    public ICommand CancelOperationCommand { get; }
    public ICommand ExportLogsCommand { get; }
    public ICommand IgnoreCrashCommand { get; }
    public event Action<UserNotice>? NoticeRaised;

    public async Task ActivateAsync()
    {
        RefreshAll();
        if (_initialized) return;
        _initialized = true;
        await DetectGpuAsync();
        await TryDetectGamePathAsync();
        RefreshAll();
    }

    private void RefreshAll()
    {
        _bepStatus = _bepInEx.GetStatus(AppSettings.UnturnedInstallPath);
        GlobalModsEnabled = _bepInEx.IsGlobalModsEnabled(AppSettings.UnturnedInstallPath);
        DxvkEnabled = _dxvk.IsEnabled(AppSettings.UnturnedInstallPath);
        OnPropertyChanged(nameof(BepInExTitle));
        OnPropertyChanged(nameof(BepInExDetail));
        OnPropertyChanged(nameof(IsBepInExInstalled));
        OnPropertyChanged(nameof(CanInstallBepInEx));
        OnPropertyChanged(nameof(BepInExRepairButtonText));
        OnPropertyChanged(nameof(HasCrashAlert));
        RefreshRuntimeState();
        RaiseCommandStates();
    }

    private async Task TryDetectGamePathAsync()
    {
        if (!string.IsNullOrWhiteSpace(AppSettings.UnturnedInstallPath)) return;
        var detected = await _gamePaths.DetectAsync();
        if (string.IsNullOrWhiteSpace(detected)) return;
        if (!await _dialogs.ConfirmAsync("发现 Unturned", $"检测到游戏安装目录：\n\n{detected}\n\n是否将其设为默认路径？")) return;
        AppSettings.UnturnedInstallPath = detected;
        RaiseNotice("游戏路径配置成功。", UserNoticeSeverity.Success);
    }

    private async Task DetectGpuAsync()
    {
        try { _gpu = await Task.Run(GpuDetector.DetectPrimary); }
        catch { _gpu = null; }
        OnPropertyChanged(nameof(HasGpuInfo));
        OnPropertyChanged(nameof(GpuName));
        OnPropertyChanged(nameof(GpuRecommendation));
        if (_gpu is null || AppSettings.DxvkRecommendedByGpu is not null) return;
        AppSettings.DxvkRecommendedByGpu = _gpu.DxvkRecommendation != DxvkRecommendation.NotRecommended;
        if (AppSettings.DxvkRecommendedByGpu == false && !AppSettings.EnableDxvk)
            DxvkEnabled = false;
    }

    private void ToggleGlobalMods()
    {
        var requested = GlobalModsEnabled;
        var result = _bepInEx.SetGlobalModsEnabled(AppSettings.UnturnedInstallPath, requested);
        if (!result.Success) GlobalModsEnabled = !requested;
        RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Error);
        RefreshAll();
    }

    private async Task ToggleDxvkAsync()
    {
        var requested = DxvkEnabled;
        if (requested && AppSettings.DxvkRecommendedByGpu == false && !AppSettings.HasShownDxvkCompatWarning)
        {
            var confirmed = await _dialogs.ConfirmAsync(
                "DXVK 兼容性提示",
                "检测到当前显卡架构可能无法良好支持 DXVK 2.4，启用后可能严重降低帧率。\n\n是否仍要启用？");
            AppSettings.HasShownDxvkCompatWarning = true;
            if (!confirmed) { DxvkEnabled = false; return; }
        }

        BeginOperation(requested ? "准备启用 DXVK…" : "正在关闭 DXVK…");
        try
        {
            var result = await _dxvk.SetEnabledAsync(
                AppSettings.UnturnedInstallPath,
                requested,
                CreateProgress(),
                _operationCts!.Token);
            RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Error);
        }
        catch (OperationCanceledException) { RaiseNotice("DXVK 操作已取消。", UserNoticeSeverity.Warning); }
        finally { EndOperation(); RefreshAll(); }
    }

    private async Task DeployBepInExAsync(string operationName, bool requireConfirmation)
    {
        var gamePath = AppSettings.UnturnedInstallPath;
        if (!_gamePaths.IsValid(gamePath))
        {
            RaiseNotice("请先在设置中配置有效的 Unturned 安装路径。", UserNoticeSeverity.Warning);
            return;
        }
        var message = operationName switch
        {
            "安装" => $"启动器将下载并安装 BepInEx {BepInExService.SupportedVersion}（win_x64，Unity Mono / winhttp doorstop）。是否继续？",
            "升级" => $"将升级到社区统一要求的 BepInEx {BepInExService.SupportedVersion}；现有 plugins、config 与社区安装记录会保留。是否继续？",
            _ => $"将重新下载并覆盖 BepInEx {BepInExService.SupportedVersion} 核心环境；社区插件与配置不会被删除。是否继续？"
        };
        if (requireConfirmation && !await _dialogs.ConfirmAsync($"{operationName} BepInEx", message)) return;

        BeginOperation($"准备{operationName} BepInEx…");
        try
        {
            await _bepInEx.DeployAsync(gamePath, CreateProgress(), _operationCts!.Token);
            _bepInEx.SetGlobalModsEnabled(gamePath, true);
            RaiseNotice($"BepInEx {operationName}完成。", UserNoticeSeverity.Success);
        }
        catch (OperationCanceledException) { RaiseNotice($"BepInEx {operationName}已取消。", UserNoticeSeverity.Warning); }
        catch (Exception ex) { RaiseNotice($"BepInEx {operationName}失败：{ex.Message}", UserNoticeSeverity.Error); }
        finally { EndOperation(); RefreshAll(); }
    }

    private async Task UninstallBepInExAsync()
    {
        var gamePath = AppSettings.UnturnedInstallPath;
        if (!_gamePaths.IsValid(gamePath))
        {
            RaiseNotice("请先在设置中配置有效的 Unturned 安装路径。", UserNoticeSeverity.Warning);
            return;
        }
        if (_launcher.IsRunning())
        {
            RaiseNotice("请先退出 Unturned，再卸载插件环境。", UserNoticeSeverity.Warning);
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "卸载 BepInEx 插件环境",
            "将移除 BepInEx 核心、winhttp doorstop 与启动配置。\n\n"
            + "玩家的 plugins、config、cache、日志及社区安装记录会保留，之后重新安装环境即可继续使用。\n\n"
            + "是否继续？");
        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var result = await Task.Run(() => _bepInEx.Uninstall(gamePath));
            RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Success : UserNoticeSeverity.Error);
        }
        finally
        {
            IsBusy = false;
            RefreshAll();
        }
    }

    private async Task LaunchAsync()
    {
        var gamePath = AppSettings.UnturnedInstallPath;
        if (!_gamePaths.IsValid(gamePath))
        {
            RaiseNotice("请先在设置中配置有效的 Unturned 安装路径。", UserNoticeSeverity.Warning);
            return;
        }
        var modsEnabled = GlobalModsEnabled;
        if (!_bepInEx.GetStatus(gamePath).IsInstalled && modsEnabled)
        {
            if (await _dialogs.ConfirmAsync("模组环境未安装", "是否现在安装 BepInEx，然后继续启动模组模式？"))
            {
                await DeployBepInExAsync("安装", requireConfirmation: false);
                if (!_bepInEx.GetStatus(gamePath).IsInstalled) return;
                modsEnabled = true;
            }
            else
            {
                if (!await _dialogs.ConfirmAsync("以纯净模式启动", "是否改为使用 BattlEye 的纯净模式启动？")) return;
                modsEnabled = false;
                _bepInEx.SetGlobalModsEnabled(gamePath, false);
                GlobalModsEnabled = false;
            }
        }
        var result = _launcher.Launch(gamePath, modsEnabled);
        RaiseNotice(result.Message, result.Success ? UserNoticeSeverity.Information : UserNoticeSeverity.Error);
        RefreshRuntimeState();
    }

    private async Task ExportLogsAsync()
    {
        IsBusy = true;
        try
        {
            var folder = await Task.Run(() => _diagnostics.ExportLogs(AppSettings.UnturnedInstallPath));
            AppSettings.LastSessionCrashed = false;
            OnPropertyChanged(nameof(HasCrashAlert));
            RaiseNotice($"诊断日志已导出：{Path.GetFileName(folder)}", UserNoticeSeverity.Success);
        }
        catch (Exception ex) { RaiseNotice($"导出失败：{ex.Message}", UserNoticeSeverity.Error); }
        finally { IsBusy = false; }
    }

    private void IgnoreCrash()
    {
        AppSettings.LastSessionCrashed = false;
        OnPropertyChanged(nameof(HasCrashAlert));
    }

    private void BeginOperation(string text)
    {
        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource();
        OperationPercentage = 0;
        OperationText = text;
        HasOperationProgress = true;
        IsBusy = true;
    }

    private void EndOperation()
    {
        IsBusy = false;
        HasOperationProgress = false;
        OperationPercentage = 0;
        OperationText = "";
    }

    private IProgress<OperationProgress> CreateProgress() => new Progress<OperationProgress>(value =>
    {
        OperationPercentage = value.Percentage;
        OperationText = value.Message;
    });

    private void CancelOperation() => _operationCts?.Cancel();
    private void RefreshRuntimeState() => IsGameRunning = _launcher.IsRunning();
    private void RaiseNotice(string message, UserNoticeSeverity severity) =>
        NoticeRaised?.Invoke(new UserNotice(message, severity));

    private void RaiseCommandStates()
    {
        ((AsyncRelayCommand)LaunchCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)InstallCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)RepairCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)UninstallCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ToggleGlobalModsCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ToggleDxvkCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelOperationCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ExportLogsCommand).RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _runtimeTimer.Stop();
        _operationCts?.Cancel();
        _operationCts?.Dispose();
    }
}
