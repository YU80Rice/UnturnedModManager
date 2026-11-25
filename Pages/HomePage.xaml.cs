using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UnturnedModManager.Helpers;
using UnturnedModManager.ViewModels;

namespace UnturnedModManager.Pages;

public partial class HomePage : Page
{
    private readonly HomeViewModel _viewModel = App.Services.CreateHomeViewModel();

    public HomePage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.NoticeRaised += OnNoticeRaised;
        Loaded += async (_, _) => await _viewModel.ActivateAsync();
        RootScrollViewer.SizeChanged += (_, e) => ArrangeBento(e.NewSize.Width > 1100);
    }

    private void ArrangeBento(bool twoColumns)
    {
        BentoSecondaryColumn.Width = twoColumns ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(WelcomeCard, twoColumns ? 1 : 0);
        Grid.SetRow(WelcomeCard, twoColumns ? 0 : 1);
        Grid.SetRow(EnvironmentCard, twoColumns ? 1 : 2);
        Grid.SetColumn(DiagnosticsCard, twoColumns ? 1 : 0);
        Grid.SetRow(DiagnosticsCard, twoColumns ? 1 : 3);
        SummaryCard.Margin = new Thickness(0, 0, twoColumns ? 14 : 0, 14);
        EnvironmentCard.Margin = twoColumns ? new Thickness(0, 0, 14, 0) : new Thickness(0, 0, 0, 14);
    }

    private void ShowChangeLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = $"UMM · {_viewModel.HomeAnnouncementVersion} 更新日志",
            Owner = Window.GetWindow(this),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 560,
            Height = 430,
            MinWidth = 410,
            MinHeight = 300,
            ShowInTaskbar = false
        };
        dialog.SetResourceReference(BackgroundProperty, "ApplicationBackgroundBrush");
        dialog.SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        var layout = new DockPanel { Margin = new Thickness(22) };
        var footer = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        footer.Children.Add(new Wpf.Ui.Controls.Button { Content = "打开官方发布页", Command = _viewModel.OpenReleaseNotesCommand, Margin = new Thickness(0, 0, 8, 0) });
        footer.Children.Add(new Wpf.Ui.Controls.Button { Content = "关闭", IsCancel = true });
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);
        layout.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock { Text = _viewModel.ReleaseNotesText, TextWrapping = TextWrapping.Wrap, LineHeight = 22 }
        });
        dialog.Content = layout;
        dialog.ShowDialog();
    }

    private void OnNoticeRaised(UserNotice notice)
        => App.Services.Notifications.Publish(notice);

    private void CardPanel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        => ScrollWheelRouter.RouteToNearestScrollViewer(sender, e);
}
