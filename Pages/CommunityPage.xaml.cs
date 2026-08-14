using System.Windows.Controls;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using UnturnedModManager.ViewModels;

namespace UnturnedModManager.Pages;

public partial class CommunityPage : Page
{
    private readonly CommunityViewModel _viewModel = App.Services.CreateCommunityViewModel();
    public CommunityPage()
    {
        InitializeComponent(); DataContext = _viewModel;
        _viewModel.OpenModRequested += id => AppNavigationService.Current.OpenCommunityDetail(this, id);
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }
    private void ModsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox list && list.SelectedItem is CommunityMod mod) { list.SelectedItem = null; _viewModel.OpenSelected(mod); }
    }
}
