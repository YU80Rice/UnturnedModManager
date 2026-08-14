using System.Windows;
using System.Windows.Controls;
using UnturnedModManager.ViewModels;
using Wpf.Ui.Controls;

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
    }

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
}
