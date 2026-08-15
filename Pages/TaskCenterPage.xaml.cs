using System.Windows.Controls;
using System.Windows.Input;
using UnturnedModManager.Helpers;
using UnturnedModManager.Services;
using UnturnedModManager.ViewModels;

namespace UnturnedModManager.Pages;

public partial class TaskCenterPage : Page
{
    private readonly TaskCenterViewModel _viewModel = App.Services.CreateTaskCenterViewModel();

    public TaskCenterPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.OpenPluginRequested += OpenPlugin;
    }

    private void OpenPlugin(int id) => AppNavigationService.Current.OpenCommunityDetail(this, id);

    private void Page_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        ScrollWheelRouter.RouteToNearestScrollViewer(sender, e);
}
