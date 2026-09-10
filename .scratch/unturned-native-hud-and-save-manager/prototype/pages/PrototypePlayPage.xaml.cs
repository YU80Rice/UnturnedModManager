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
            MascotToggleBtn.Content = "🦊 隐藏";
        }
        else
        {
            MascotContainer.Visibility = Visibility.Visible;
            MascotToggleBtn.Content = "🦊 挂件";
        }
    }

    private void RepairBepInExBtn_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "【原型验证】触发 BepInEx 5.4.23.5 核心注入完整性校验与文件修复。",
            "环境与诊断闭环",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ExportDiagnosticsBtn_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "【原型验证】已打包脱敏日志与系统环境信息，生成诊断压缩包：\nUMM-诊断包_20260910_XXXXXX.zip",
            "环境与诊断闭环",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void BepInExToggle_Checked(object sender, RoutedEventArgs e)
    {
        // 模组加载器激活状态
    }

    private void BepInExToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        // 模组加载器禁用状态 (纯净原版)
    }

    private void DxvkToggle_Checked(object sender, RoutedEventArgs e)
    {
        // 开启 DXVK (Vulkan)
    }

    private void DxvkToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        // 关闭 DXVK (DirectX 原生)
    }
}
