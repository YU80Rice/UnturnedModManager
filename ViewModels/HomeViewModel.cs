using System.IO;
using System.Diagnostics;
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
    private readonly CommunityAuthService _authentication;
    private readonly LauncherUpdateService _launcherUpdates;
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
    private DiagnosticAnalysis _diagnosticAnalysis = DiagnosticAnalysis.Empty;
    private LauncherUpdateInfo? _availableLauncherUpdate;
    private LauncherReleaseNotesInfo? _latestReleaseNotes;
    private bool _updateCheckRequested;
    private bool _isDownloadingLauncherUpdate;
    private int _launcherUpdatePercentage;
    private string _launcherUpdateText = "";

    public HomeViewModel(
        BepInExService bepInEx,
        DxvkService dxvk,
        GameLaunchService launcher,
        DiagnosticService diagnostics,
        GamePathService gamePaths,
        IUserDialogService dialogs,
        CommunityAuthService authentication,
        LauncherUpdateService launcherUpdates)
    {
        _bepInEx = bepInEx;
        _dxvk = dxvk;
        _launcher = launcher;
        _diagnostics = diagnostics;
        _gamePaths = gamePaths;
        _dialogs = dialogs;
        _authentication = authentication;
        _launcherUpdates = launcherUpdates;
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
        AnalyzeLogsCommand = new AsyncRelayCommand(AnalyzeLogsAsync, () => !IsBusy);
        IgnoreCrashCommand = new RelayCommand(IgnoreCrash);
        AcknowledgeAnnouncementCommand = new RelayCommand(AcknowledgeAnnouncement);
        HideHomeWelcomeCommand = new RelayCommand(() => SetHomeWelcome(false));
        ShowHomeWelcomeCommand = new RelayCommand(() => SetHomeWelcome(true));
        OpenReleaseNotesCommand = new RelayCommand(OpenReleaseNotes);
        DownloadLauncherUpdateCommand = new AsyncRelayCommand(DownloadAndInstallLauncherUpdateAsync, () => HasAvailableLauncherUpdate && !IsBusy);
        _authentication.SessionChanged += OnAccountSessionChanged;
        _runtimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _runtimeTimer.Tick += (_, _) => RefreshRuntimeState();
        _runtimeTimer.Start();
    }

    public string GameRootPath => string.IsNullOrWhiteSpace(AppSettings.UnturnedInstallPath) ? "尚未配置" : AppSettings.UnturnedInstallPath;
    public bool HasValidGamePath => _bepStatus.HasValidGamePath;
    public string RuntimeStatusText => IsGameRunning ? "游戏正在运行" : HasValidGamePath ? "就绪待命" : "待配置游戏路径";
    public string ActiveProfileName { get; private set; } = "未选择方案";
    public string EnabledPluginsText { get; private set; } = "0 / 0 插件";
    public string ActiveRunModeText => GlobalModsEnabled ? "模组 (-NoBattlEye)" : "原版 BattlEye";
    public string ReleaseNotesText => _latestReleaseNotes is { ReleaseNotes.Length: > 0 } notes
        ? notes.ReleaseNotes
        : string.Join(Environment.NewLine, HomeAnnouncementHighlights.Select(line => "• " + line));
    public string BepInExTitle => _bepStatus.Title;
    public string BepInExDetail => _bepStatus.Detail;
    public bool IsBepInExInstalled => _bepStatus.IsInstalled;
    public bool CanInstallBepInEx => _bepStatus.HasValidGamePath && !_bepStatus.IsInstalled;
    public string BepInExRepairButtonText => _bepStatus.IsCurrentVersion
        ? "检查并修复"
        : $"升级到 {BepInExService.SupportedVersion}";
    public bool GlobalModsEnabled
    {
        get => _globalModsEnabled;
        set
        {
            if (!SetProperty(ref _globalModsEnabled, value)) return;
            if (App.Services is { } services) services.LaunchEnvironment.GlobalModsEnabled = value;
            OnPropertyChanged(nameof(ActiveRunModeText));
        }
    }
    public bool DxvkEnabled { get => _dxvkEnabled; set => SetProperty(ref _dxvkEnabled, value); }
    public bool HasGpuInfo => _gpu is not null && !string.IsNullOrWhiteSpace(_gpu.Name);
    public string GpuName => !HasGpuInfo || _gpu is null ? "未识别到显卡型号" : $"{_gpu.Name} · {_gpu.VendorName} {_gpu.ArchitectureName}";
    public string GpuRecommendation => !HasGpuInfo || _gpu is null ? "DXVK 兼容性取决于显卡与驱动，可与原生 D3D11 对照测试。" : $"{_gpu.RecommendationText} — {_gpu.RecommendationDetail}";
    public bool HasCrashAlert => AppSettings.LastSessionCrashed;
    public bool IsHomeWelcomeEnabled => AppSettings.IsHomeWelcomeEnabled;
    public string WelcomeGreeting => _authentication.IsSignedIn && _authentication.CurrentUser is { } user
        ? $"欢迎回来，{user.DisplayIdentity}"
        : "你好，幸存者！";
    public bool HasNewReleaseAnnouncement => IsHomeWelcomeEnabled
        && !string.Equals(
            AppSettings.LastAcknowledgedHomeAnnouncementVersion,
            AppSettings.CurrentHomeAnnouncementVersion,
            StringComparison.Ordinal);
    public bool HasAvailableLauncherUpdate => _availableLauncherUpdate is not null;
    public bool HasHomeAnnouncement => IsHomeWelcomeEnabled && (HasNewReleaseAnnouncement || HasAvailableLauncherUpdate);
    public string HomeAnnouncementVersion => _latestReleaseNotes?.DisplayVersion
        ?? $"v{AppSettings.CurrentHomeAnnouncementVersion}";
    public string HomeAnnouncementTitle => HasAvailableLauncherUpdate
        ? $"发现 {HomeAnnouncementVersion} 新版本"
        : $"{HomeAnnouncementVersion} 更新要点";
    public IReadOnlyList<string> HomeAnnouncementHighlights
    {
        get
        {
            if (_latestReleaseNotes is { } latest)
                return ExtractAnnouncementHighlights(latest.ReleaseNotes, CurrentVersionFallbackHighlights);

            return CurrentVersionFallbackHighlights;
        }
    }
    public bool IsDownloadingLauncherUpdate
    {
        get => _isDownloadingLauncherUpdate;
        private set
        {
            if (!SetProperty(ref _isDownloadingLauncherUpdate, value)) return;
            OnPropertyChanged(nameof(UpdateDownloadButtonText));
            RaiseCommandStates();
        }
    }
    public int LauncherUpdatePercentage { get => _launcherUpdatePercentage; private set => SetProperty(ref _launcherUpdatePercentage, value); }
    public string LauncherUpdateText { get => _launcherUpdateText; private set => SetProperty(ref _launcherUpdateText, value); }
    public string UpdateDownloadButtonText => IsDownloadingLauncherUpdate
        ? $"正在下载 {LauncherUpdatePercentage}%"
        : $"下载并安装 {HomeAnnouncementVersion}";
    public string LauncherUpdateDetail => HasAvailableLauncherUpdate
        ? $"{_availableLauncherUpdate!.ReleaseName} · {_availableLauncherUpdate.Size / 1024d / 1024d:F1} MB · GitHub SHA-256 校验"
        : "";
    public string DiagnosticTitle => _diagnosticAnalysis.Title;
    public string DiagnosticSummary => _diagnosticAnalysis.Summary;
    public string DiagnosticDetail => _diagnosticAnalysis.Detail;
    public bool HasDiagnosticDetail => _diagnosticAnalysis.Evidence.Count > 0;
    public string DiagnosticRecommendation => _diagnosticAnalysis.Recommendation;
    public bool HasDiagnosticRecommendation => !string.IsNullOrWhiteSpace(_diagnosticAnalysis.Recommendation);
    public DiagnosticCategory DiagnosticCategory => _diagnosticAnalysis.Category;
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
            OnPropertyChanged(nameof(RuntimeStatusText));
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
    public ICommand AnalyzeLogsCommand { get; }
    public ICommand IgnoreCrashCommand { get; }
    public ICommand AcknowledgeAnnouncementCommand { get; }
    public ICommand HideHomeWelcomeCommand { get; }
    public ICommand ShowHomeWelcomeCommand { get; }
    public ICommand OpenReleaseNotesCommand { get; }
    public ICommand DownloadLauncherUpdateCommand { get; }
    public event Action<UserNotice>? NoticeRaised;

    public async Task ActivateAsync()
    {
        RefreshAll();
        if (_initialized) return;
        _initialized = true;
        if (!_updateCheckRequested)
        {
            _updateCheckRequested = true;
            _ = CheckForLauncherUpdateAsync();
        }
        await DetectGpuAsync();
        await TryDetectGamePathAsync();
        RefreshAll();
    }

    private void RefreshAll()
    {
        _bepStatus = _bepInEx.GetStatus(AppSettings.UnturnedInstallPath);
        GlobalModsEnabled = _bepInEx.IsGlobalModsEnabled(AppSettings.UnturnedInstallPath);
        DxvkEnabled = _dxvk.IsEnabled(AppSettings.UnturnedInstallPath);
        OnPropertyChanged(nameof(GameRootPath));
        OnPropertyChanged(nameof(HasValidGamePath));
        OnPropertyChanged(nameof(RuntimeStatusText));
        OnPropertyChanged(nameof(BepInExTitle));
        OnPropertyChanged(nameof(BepInExDetail));
        OnPropertyChanged(nameof(IsBepInExInstalled));
        OnPropertyChanged(nameof(CanInstallBepInEx));
        OnPropertyChanged(nameof(BepInExRepairButtonText));
        OnPropertyChanged(nameof(HasCrashAlert));
        OnPropertyChanged(nameof(IsHomeWelcomeEnabled));
        OnPropertyChanged(nameof(WelcomeGreeting));
        OnPropertyChanged(nameof(HasNewReleaseAnnouncement));
        OnPropertyChanged(nameof(HasAvailableLauncherUpdate));
        OnPropertyChanged(nameof(HasHomeAnnouncement));
        OnPropertyChanged(nameof(HomeAnnouncementVersion));
        OnPropertyChanged(nameof(HomeAnnouncementTitle));
        OnPropertyChanged(nameof(HomeAnnouncementHighlights));
        OnPropertyChanged(nameof(LauncherUpdateDetail));
        OnPropertyChanged(nameof(UpdateDownloadButtonText));
        RefreshDiagnosticProperties();
        RefreshRuntimeState();
        RaiseCommandStates();
        if (App.Services is { } services)
        {
            var environment = services.LaunchEnvironment;
            environment.Refresh();
            ActiveProfileName = string.IsNullOrWhiteSpace(environment.ProfileName) ? "未选择方案" : environment.ProfileName;
            EnabledPluginsText = $"{environment.EnabledPluginCount} / {environment.TotalPluginCount} 插件";
            OnPropertyChanged(nameof(ActiveProfileName));
            OnPropertyChanged(nameof(EnabledPluginsText));
        }
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
        if (_gpu is null || string.IsNullOrWhiteSpace(_gpu.Name)) return;
        if (string.Equals(AppSettings.DxvkRecommendationGpuName, _gpu.Name, StringComparison.Ordinal)) return;
        AppSettings.DxvkRecommendedByGpu = _gpu.DxvkRecommendation != DxvkRecommendation.NotRecommended;
        AppSettings.DxvkRecommendationGpuName = _gpu.Name;
        // 显卡已变化时，不能沿用针对旧显卡展示过的兼容性确认。
        AppSettings.HasShownDxvkCompatWarning = false;
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
        await App.Services.Launch.LaunchAsync();
        RefreshAll();
    }

    private async Task ExportLogsAsync()
    {
        IsBusy = true;
        try
        {
            _diagnosticAnalysis = await Task.Run(() => _diagnostics.Analyze(AppSettings.UnturnedInstallPath));
            var folder = await Task.Run(() => _diagnostics.ExportLogs(AppSettings.UnturnedInstallPath, _diagnosticAnalysis));
            AppSettings.LastSessionCrashed = false;
            OnPropertyChanged(nameof(HasCrashAlert));
            RefreshDiagnosticProperties();
            NoticeRaised?.Invoke(new UserNotice(
                $"诊断包已生成：{folder}\n点击此通知可打开所在目录。",
                UserNoticeSeverity.Success,
                () => _diagnostics.OpenExportFolder(folder),
                TimeSpan.FromSeconds(12)));
        }
        catch (Exception ex) { RaiseNotice($"导出失败：{ex.Message}", UserNoticeSeverity.Error); }
        finally { IsBusy = false; }
    }

    private async Task AnalyzeLogsAsync()
    {
        IsBusy = true;
        try
        {
            _diagnosticAnalysis = await Task.Run(() => _diagnostics.Analyze(AppSettings.UnturnedInstallPath));
            RefreshDiagnosticProperties();
            var severity = _diagnosticAnalysis.Severity switch
            {
                DiagnosticSeverity.Error => UserNoticeSeverity.Error,
                DiagnosticSeverity.Warning => UserNoticeSeverity.Warning,
                _ => UserNoticeSeverity.Information
            };
            RaiseNotice(_diagnosticAnalysis.Title, severity);
        }
        finally { IsBusy = false; }
    }

    private void IgnoreCrash()
    {
        AppSettings.LastSessionCrashed = false;
        OnPropertyChanged(nameof(HasCrashAlert));
    }

    private void AcknowledgeAnnouncement()
    {
        AppSettings.LastAcknowledgedHomeAnnouncementVersion = AppSettings.CurrentHomeAnnouncementVersion;
        OnPropertyChanged(nameof(HasNewReleaseAnnouncement));
        OnPropertyChanged(nameof(HasHomeAnnouncement));
    }

    private void SetHomeWelcome(bool enabled)
    {
        AppSettings.IsHomeWelcomeEnabled = enabled;
        OnPropertyChanged(nameof(IsHomeWelcomeEnabled));
        OnPropertyChanged(nameof(HasNewReleaseAnnouncement));
        OnPropertyChanged(nameof(HasHomeAnnouncement));
    }

    private void OpenReleaseNotes()
    {
        try
        {
            var version = _latestReleaseNotes?.DisplayVersion
                ?? $"v{AppSettings.CurrentHomeAnnouncementVersion}";
            var url = $"https://github.com/YU80Rice/UnturnedModManager/releases/tag/{version}";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            RaiseNotice($"无法打开更新日志：{exception.Message}", UserNoticeSeverity.Warning);
        }
    }

    private async Task CheckForLauncherUpdateAsync()
    {
        try
        {
            var current = typeof(HomeViewModel).Assembly.GetName().Version ?? new Version(0, 0, 0);
            var result = await _launcherUpdates.CheckLatestReleaseAsync(current);
            _latestReleaseNotes = result.LatestRelease;
            _availableLauncherUpdate = result.AvailableUpdate;
            OnPropertyChanged(nameof(HasAvailableLauncherUpdate));
            OnPropertyChanged(nameof(HasHomeAnnouncement));
            OnPropertyChanged(nameof(HomeAnnouncementVersion));
            OnPropertyChanged(nameof(HomeAnnouncementTitle));
            OnPropertyChanged(nameof(HomeAnnouncementHighlights));
            OnPropertyChanged(nameof(LauncherUpdateDetail));
            OnPropertyChanged(nameof(UpdateDownloadButtonText));
            RaiseCommandStates();
        }
        catch
        {
            // 检查失败不会打断正常启动；用户仍可从“查看完整更新日志”手动访问 Release。
        }
    }

    private async Task DownloadAndInstallLauncherUpdateAsync()
    {
        var update = _availableLauncherUpdate;
        if (update is null)
            return;

        var confirmed = await _dialogs.ConfirmAsync(
            "下载 UMM 更新",
            $"将从 UMM 官方 GitHub Release 下载 {update.DisplayVersion}（{update.Size / 1024d / 1024d:F1} MB）。\n\n"
            + "下载完成后会校验 GitHub 发布的 SHA-256。只有你再次确认安装时，启动器才会退出并替换 EXE。是否开始下载？");
        if (!confirmed)
            return;

        IsBusy = true;
        IsDownloadingLauncherUpdate = true;
        LauncherUpdatePercentage = 0;
        LauncherUpdateText = "正在准备下载…";
        try
        {
            var progress = new Progress<OperationProgress>(value =>
            {
                LauncherUpdatePercentage = value.Percentage;
                LauncherUpdateText = value.Message;
                OnPropertyChanged(nameof(UpdateDownloadButtonText));
            });
            var downloadedPath = await _launcherUpdates.DownloadAsync(update, progress);
            var install = await _dialogs.ConfirmAsync(
                "更新已下载并校验",
                $"{update.DisplayVersion} 已下载并通过 SHA-256 校验。\n\n"
                + "现在安装会关闭 UMM，替换当前 EXE，并保留旧版本的 .bak 备份。若当前目录没有写入权限，已校验的新 EXE 会直接启动，不会删除旧版本。\n\n是否现在安装？");
            if (!install)
            {
                RaiseNotice($"更新已下载并校验，可稍后从此处再次下载或手动运行：{Path.GetFileName(downloadedPath)}", UserNoticeSeverity.Information);
                return;
            }

            LauncherUpdateService.ScheduleInstallAndRestart(downloadedPath);
            RaiseNotice("更新安装程序已启动，UMM 即将关闭并重新打开新版本。", UserNoticeSeverity.Success);
            System.Windows.Application.Current?.Shutdown();
        }
        catch (OperationCanceledException)
        {
            RaiseNotice("启动器更新下载已取消。", UserNoticeSeverity.Warning);
        }
        catch (Exception ex)
        {
            RaiseNotice($"启动器更新未完成：{ex.Message}", UserNoticeSeverity.Error);
        }
        finally
        {
            IsDownloadingLauncherUpdate = false;
            IsBusy = false;
        }
    }

    private void OnAccountSessionChanged() => OnPropertyChanged(nameof(WelcomeGreeting));

    public static IReadOnlyList<string> ExtractAnnouncementHighlights(
        string releaseNotes,
        IReadOnlyList<string> fallback)
    {
        if (string.IsNullOrWhiteSpace(releaseNotes)) return fallback;

        var highlights = releaseNotes.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            .Select(line => line[2..].Replace("**", "", StringComparison.Ordinal).Replace("`", "", StringComparison.Ordinal).Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(4)
            .ToArray();
        return highlights.Length == 0 ? fallback : highlights;
    }

    private static readonly IReadOnlyList<string> UpdateFallbackHighlights =
    [
        "新版本已由 UMM 官方 GitHub Release 发布，可由你确认后下载。",
        "下载完成后会校验 GitHub 提供的 SHA-256；校验失败不会安装。",
        "确认安装时，UMM 才会退出并替换 EXE，同时保留旧版本 .bak 备份。"
    ];

    private static readonly IReadOnlyList<string> CurrentVersionFallbackHighlights =
    [
        "顶部胶囊用于切换页面，底部指挥舱始终显示当前方案与启动环境。",
        "自由调色环支持浅色、深色和氛围染色，主题可以打包分享。",
        "保留三层壁纸、可调模糊与遮罩，以及本地插件与社区发现。"
    ];

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
    private void RefreshRuntimeState()
    {
        IsGameRunning = _launcher.IsRunning();
        // 游戏退出后的 Process.Exited 回调会更新持久化状态；这里让仍打开着的首页
        // 也能立即呈现异常退出提示，而不必等待重新进入页面。
        OnPropertyChanged(nameof(HasCrashAlert));
    }
    private void RefreshDiagnosticProperties()
    {
        OnPropertyChanged(nameof(DiagnosticTitle));
        OnPropertyChanged(nameof(DiagnosticSummary));
        OnPropertyChanged(nameof(DiagnosticDetail));
        OnPropertyChanged(nameof(HasDiagnosticDetail));
        OnPropertyChanged(nameof(DiagnosticRecommendation));
        OnPropertyChanged(nameof(HasDiagnosticRecommendation));
        OnPropertyChanged(nameof(DiagnosticCategory));
    }
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
        ((AsyncRelayCommand)AnalyzeLogsCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)DownloadLauncherUpdateCommand).RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _runtimeTimer.Stop();
        _authentication.SessionChanged -= OnAccountSessionChanged;
        _operationCts?.Cancel();
        _operationCts?.Dispose();
    }
}
