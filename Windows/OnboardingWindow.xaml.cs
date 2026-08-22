using System.Windows;
using UnturnedModManager.ViewModels;
using Wpf.Ui.Controls;

namespace UnturnedModManager;

public partial class OnboardingWindow : FluentWindow
{
    private readonly OnboardingViewModel _viewModel;

    public OnboardingWindow()
    {
        InitializeComponent();
        _viewModel = App.Services.CreateOnboardingViewModel();
        DataContext = _viewModel;
        _viewModel.Completed += OnCompleted;
    }

    private void OnCompleted() => DialogResult = true;

    protected override void OnClosed(EventArgs e)
    {
        // Closing the wizard is treated as “later” so an existing user is not
        // trapped in the same dialog on every launch.
        if (!AppSettings.IsOnboardingCompleted)
            AppSettings.IsOnboardingCompleted = true;
        _viewModel.Completed -= OnCompleted;
        base.OnClosed(e);
    }
}
