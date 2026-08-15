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
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnNoticeRaised(notice));
            return;
        }

        StatusInfoBar.Message = notice.Message;
        StatusInfoBar.Severity = notice.Severity switch
        {
            UserNoticeSeverity.Success => InfoBarSeverity.Success,
            UserNoticeSeverity.Warning => InfoBarSeverity.Warning,
            UserNoticeSeverity.Error => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational
        };
        StatusInfoBar.IsOpen = true;
    }

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

    private void Page_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void Page_Drop(object sender, System.Windows.DragEventArgs e)
    {
        e.Handled = true;
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            || e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files)
            return;
        await _viewModel.ImportAsync(files);
    }

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
