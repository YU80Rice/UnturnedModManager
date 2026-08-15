using UnturnedModManager.ViewModels;

namespace UnturnedModManager.Services;

/// <summary>
/// 应用组合根。长生命周期服务只创建一次，页面 ViewModel 通过工厂获得显式依赖。
/// </summary>
public sealed class AppServices : IDisposable
{
    public ThemeService Theme { get; } = new();
    public SingleInstanceService SingleInstance { get; } = new();
    public CommunityAuthService Authentication { get; } = new();
    public CommunityCacheService CommunityCache { get; } = new();
    public CommunityModInstaller CommunityInstaller { get; } = new();
    public OperationTaskCenter Tasks { get; } = new();
    public UserNotificationService Notifications { get; } = new();
    public LauncherUpdateService LauncherUpdates { get; } = new();
    public IUserDialogService Dialogs { get; } = new UserDialogService();
    public GamePathService GamePaths { get; } = new();
    public IFolderPickerService FolderPicker { get; } = new WindowsFolderPickerService();
    public HttpDownloadService Downloads { get; } = new();
    public BepInExService BepInEx { get; }
    public DxvkService Dxvk { get; }
    public GameLaunchService GameLauncher { get; }
    public DiagnosticService Diagnostics { get; } = new();
    public LocalModService LocalMods { get; }
    public PluginProfileService PluginProfiles { get; }
    public AppNavigationService Navigation => AppNavigationService.Current;

    public AppServices()
    {
        LocalMods = new LocalModService(CommunityInstaller);
        PluginProfiles = new PluginProfileService(LocalMods);
        BepInEx = new BepInExService(Downloads);
        Dxvk = new DxvkService(Downloads);
        GameLauncher = new GameLaunchService(BepInEx, Dxvk);
    }

    public LocalModsViewModel CreateLocalModsViewModel() => new(
        new CommunityApiClient(CommunityCache),
        CommunityInstaller,
        LocalMods,
        Dialogs,
        PluginProfiles);

    public CommunityViewModel CreateCommunityViewModel() => new(new CommunityApiClient(CommunityCache));

    public CommunityDetailViewModel CreateCommunityDetailViewModel(int id) => new(
        id,
        new CommunityApiClient(CommunityCache),
        CommunityInstaller,
        Dialogs,
        Authentication,
        Tasks);

    public TaskCenterViewModel CreateTaskCenterViewModel() => new(Tasks);

    public SettingsViewModel CreateSettingsViewModel() => new(
        GamePaths,
        FolderPicker,
        Theme,
        Authentication,
        Dialogs);

    public AccountViewModel CreateAccountViewModel() => new(Authentication, Dialogs);

    public OnboardingViewModel CreateOnboardingViewModel() => new(
        GamePaths,
        FolderPicker,
        Theme);

    public HomeViewModel CreateHomeViewModel() => new(
        BepInEx,
        Dxvk,
        GameLauncher,
        Diagnostics,
        GamePaths,
        Dialogs,
        Authentication,
        LauncherUpdates);

    public void Dispose()
    {
        Authentication.Dispose();
        LauncherUpdates.Dispose();
        SingleInstance.Dispose();
    }
}
