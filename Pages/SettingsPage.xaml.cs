using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace UnturnedModManager.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => GamePathTextBox.Text = AppSettings.UnturnedInstallPath;
    }

    private void BrowsePathButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择 Unturned 安装目录",
            SelectedPath = GamePathTextBox.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            GamePathTextBox.Text = dialog.SelectedPath;
        }
    }

    /// <summary>
    /// "自动寻找"按钮：后台扫描 Steam 注册表，命中则填入输入框并展示 Informational 提示，
    /// 未命中则展示 Warning 提示引导用户手动浏览。
    /// </summary>
    private async void AutoDetectButton_Click(object sender, RoutedEventArgs e)
    {
        AutoDetectButton.IsEnabled = false;
        try
        {
            var detectedPath = await Task.Run(AppSettings.DetectSteamUnturnedPath);

            if (!string.IsNullOrEmpty(detectedPath))
            {
                GamePathTextBox.Text = detectedPath;
                ShowStatusInfoBar(
                    "已成功自动寻找到游戏路径！请点击下方的'保存设置'以应用。",
                    InfoBarSeverity.Informational);
            }
            else
            {
                ShowStatusInfoBar(
                    "未能自动寻找到游戏路径，请点击'浏览...'手动选择您的游戏文件夹。",
                    InfoBarSeverity.Warning);
            }
        }
        finally
        {
            AutoDetectButton.IsEnabled = true;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var path = GamePathTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            ShowStatusInfoBar("请选择有效的游戏安装路径", InfoBarSeverity.Warning);
            return;
        }

        if (!Directory.Exists(path))
        {
            ShowStatusInfoBar("所选路径不存在，请检查后重新选择", InfoBarSeverity.Error);
            return;
        }

        AppSettings.UnturnedInstallPath = path;
        ShowStatusInfoBar("设置已保存", InfoBarSeverity.Success);
    }

    /// <summary>
    /// 在页面顶部预声明的 StatusInfoBar 上展示反馈消息。
    /// </summary>
    private void ShowStatusInfoBar(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}
