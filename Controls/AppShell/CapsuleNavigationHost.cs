using UnturnedModManager.Navigation;

namespace UnturnedModManager.Controls.AppShell;

/// <summary>主窗只管理顶层槽位。工坊的子域、详情和来源快照留在同一个工坊实例。</summary>
internal sealed class CapsuleNavigationHost : INavigationShellHost
{
    private readonly NavigationShellHost _shell = new();
    private readonly Func<IPluginWorkshopNavigation> _workshop;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;

    public CapsuleNavigationHost(Func<IPluginWorkshopNavigation> workshop, System.Windows.Threading.Dispatcher dispatcher)
    {
        _workshop = workshop;
        _dispatcher = dispatcher;
        _shell.StateChanged += (_, args) => StateChanged?.Invoke(this, args);
    }

    public PrimarySlot ActiveSlot => _shell.ActiveSlot;
    public SecondaryNavigationTarget? ActiveSecondary => _shell.ActiveSecondary;
    public bool CanNavigateBack => _shell.CanNavigateBack || (ActiveSlot == PrimarySlot.Mods && _workshop().IsDetailActive);
    public event EventHandler<NavigationStateChangedEventArgs>? StateChanged;

    public NavigationResult NavigateTo(PageNavigationRequest request) => OnUi(() =>
    {
        if (request is null) return NavigationResult.Rejected;
        if (!NavigationCacheKey.IsValid(request.TargetSlot, request.SubDomain, request.Context, out var failure, out _))
            return failure;
        if (request.TargetSlot == PrimarySlot.Data) return NavigationResult.PendingInitialization;
        if (request.TargetSlot == PrimarySlot.Mods)
        {
            if (request.Context is PluginDetailContext detail)
            {
                if (!int.TryParse(detail.ModId, out var id) || id <= 0) return NavigationResult.InvalidTarget;
                var workshop = _workshop();
                if (!(workshop.IsDetailActive && workshop.ActiveDetailModId == id)
                    && !workshop.ShowDetail(id, request.SubDomain)) return NavigationResult.Rejected;
            }
            else if (request.SubDomain is { } subDomain)
            {
                var change = _workshop().NavigateTo(subDomain, request.Context);
                if (change == NavigationChangeResult.InvalidTarget) return NavigationResult.InvalidTarget;
                if (change is NavigationChangeResult.Rejected or NavigationChangeResult.Unavailable) return NavigationResult.Rejected;
            }
            // 次级入口返回的是同一个工坊，不把已变化的子域重新套用到工坊。
            return _shell.NavigateTo(new PageNavigationRequest(PrimarySlot.Mods));
        }
        return _shell.NavigateTo(request);
    });

    public NavigationResult NavigateToSecondary(SecondaryNavigationTarget target) =>
        OnUi(() => _shell.NavigateToSecondary(target));

    public NavigationResult NavigateBack() => OnUi(() =>
    {
        if (_shell.CanNavigateBack) return _shell.NavigateBack();
        return ActiveSlot == PrimarySlot.Mods && _workshop().CloseDetail()
            ? NavigationResult.Success : NavigationResult.Rejected;
    });

    private NavigationResult OnUi(Func<NavigationResult> action) =>
        _dispatcher.CheckAccess() ? action() : _dispatcher.Invoke(action);
}
