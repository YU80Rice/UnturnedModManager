using System.Windows;
using UnturnedModManager.Services;

namespace UnturnedModManager.Windows;

public partial class SandboxAlertWindow : Window
{
    private readonly SandboxInspectionResult _result;

    public SandboxAlertWindow(SandboxInspectionResult result)
    {
        InitializeComponent();
        _result = result;
        PackagePathText.Text = result.PackagePath;
        ViolationsListView.ItemsSource = result.Violations;
    }

    private void CopyReportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(_result.FormatDiagnosticReport());
            CopyStatusText.Visibility = Visibility.Visible;
        }
        catch
        {
            // Clipboard may be temporarily locked
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
