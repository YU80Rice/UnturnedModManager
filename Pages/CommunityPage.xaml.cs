using System.Windows.Controls;
using System.Windows.Input;
using UnturnedModManager.Helpers;
using UnturnedModManager.Models;
using UnturnedModManager.Navigation;
using UnturnedModManager.Services;
using UnturnedModManager.ViewModels;

namespace UnturnedModManager.Pages;

public partial class CommunityPage : Page
{
    private readonly CommunityViewModel _viewModel = App.Services?.CreateCommunityViewModel() ?? new CommunityViewModel();
    public CommunityViewModel ViewModel => _viewModel;

    /// <summary>
    /// 自定义详情打开委托 (当挂载入工坊聚合外壳时由外壳接管，消除双重导航事件)
    /// </summary>
    public Action<int>? CustomOpenDetailAction { get; set; }

    public CommunityPage()
    {
        InitializeComponent(); DataContext = _viewModel;
        _viewModel.OpenModRequested += OnOpenModRequested;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }

    private void OnOpenModRequested(int id)
    {
        if (CustomOpenDetailAction is not null)
        {
            CustomOpenDetailAction.Invoke(id);
            return;
        }

        AppNavigationService.Current.OpenCommunityDetail(new CommunityDetailRequest(id, WorkshopSubDomain.CommunityDiscovery));
    }
    private void ModsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox list && list.SelectedItem is CommunityMod mod) { list.SelectedItem = null; _viewModel.OpenSelected(mod); }
    }

    private void Page_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        ScrollWheelRouter.RouteToNearestScrollViewer(sender, e);
}
