using System.Windows;
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
    internal ScrollViewer ResultsScroll => ResultsScrollViewer;

    public static readonly DependencyProperty ColumnCountProperty = DependencyProperty.Register(
        nameof(ColumnCount), typeof(int), typeof(CommunityPage), new PropertyMetadata(3));

    public int ColumnCount
    {
        get => (int)GetValue(ColumnCountProperty);
        private set => SetValue(ColumnCountProperty, value);
    }

    internal static int GetColumnCount(double viewportWidth) => viewportWidth <= 820 ? 1 : viewportWidth <= 1100 ? 2 : 3;

    /// <summary>
    /// 自定义详情打开委托 (当挂载入工坊聚合外壳时由外壳接管，消除双重导航事件)
    /// </summary>
    public Action<int>? CustomOpenDetailAction { get; set; }

    public CommunityPage()
    {
        InitializeComponent(); DataContext = _viewModel;
        _viewModel.OpenModRequested += OnOpenModRequested;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        SizeChanged += OnPageSizeChanged;
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
    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ColumnCount = GetColumnCount(e.NewSize.Width);
        bool compact = e.NewSize.Width <= 650;
        Grid.SetRow(Filters, compact ? 1 : 0);
        Grid.SetColumn(Filters, compact ? 0 : 1);
        Grid.SetColumnSpan(SearchBox, compact ? 2 : 1);
        Grid.SetColumnSpan(Filters, compact ? 2 : 1);
        Grid.SetRow(StatusLine, compact ? 2 : 1);
        Filters.Margin = compact ? new Thickness(0, 8, 0, 0) : default;
    }

    private void OpenMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CommunityMod mod }) _viewModel.OpenSelected(mod);
    }

    private void Page_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        ScrollWheelRouter.RouteToNearestScrollViewer(sender, e);
}
