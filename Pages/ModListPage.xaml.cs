using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UnturnedModManager.Helpers;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using UnturnedModManager.ViewModels;
using Wpf.Ui.Controls;

namespace UnturnedModManager.Pages;

public partial class ModListPage : Page
{
    private readonly LocalModsViewModel _viewModel = App.Services.CreateLocalModsViewModel();

    public ModListPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.NoticeRaised += OnNoticeRaised;
        _viewModel.OpenCommunityRequested += OnOpenCommunityRequested;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await _viewModel.ActivateAsync();
    private void OnUnloaded(object sender, RoutedEventArgs e) => _viewModel.Deactivate();

    private void OnOpenCommunityRequested(int id) =>
        AppNavigationService.Current.OpenCommunityDetail(this, id);

    private void OnNoticeRaised(UserNotice notice)
        => App.Services.Notifications.Publish(notice);

    private void ModsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.ListView list
            || HasInteractiveAncestor(e.OriginalSource as DependencyObject))
            return;

        var container = ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) as System.Windows.Controls.ListViewItem;
        if (container?.DataContext is not ModItem item)
            return;

        if (_viewModel.OpenCommunityCommand.CanExecute(item))
        {
            e.Handled = true;
            _viewModel.OpenCommunityCommand.Execute(item);
        }
    }

    private void ModsList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter
            || sender is not System.Windows.Controls.ListView list
            || list.SelectedItem is not ModItem item
            || !_viewModel.OpenCommunityCommand.CanExecute(item))
            return;

        _viewModel.OpenCommunityCommand.Execute(item);
        e.Handled = true;
    }

    public Task RefreshAfterExternalImportAsync() => _viewModel.RefreshAfterExternalImportAsync();

    private static bool HasInteractiveAncestor(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is System.Windows.Controls.Button or ToggleSwitch) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void Panel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        => ScrollWheelRouter.RouteToNearestScrollViewer(sender, e);
}
