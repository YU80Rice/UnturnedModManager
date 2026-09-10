using System.Windows;
using System.Windows.Controls;

namespace UnturnedModManager.Prototypes;

public partial class PrototypePlayPage : Page
{
    public PrototypePlayPage()
    {
        InitializeComponent();
    }

    private void MascotToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        if (MascotContainer.Visibility == Visibility.Visible)
        {
            MascotContainer.Visibility = Visibility.Collapsed;
            MascotToggleBtn.Content = "🦊 吉祥物挂件已隐藏 (点击恢复)";
        }
        else
        {
            MascotContainer.Visibility = Visibility.Visible;
            MascotToggleBtn.Content = "🦊 切换吉祥物挂件显隐";
        }
    }
}
