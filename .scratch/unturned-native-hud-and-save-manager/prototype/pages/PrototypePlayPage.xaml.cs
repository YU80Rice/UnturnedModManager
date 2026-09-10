using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace UnturnedModManager.Prototypes;

public partial class PrototypePlayPage : Page
{
    /// <summary>
    /// 当启动环境状态 (BepInEx / DXVK) 发生变化时通知 Shell 联动更新顶部全域药丸
    /// </summary>
    public event Action<bool, bool>? EnvironmentStateChanged;

    public bool IsBepInExEnabled => BepInExToggle?.IsChecked == true;
    public bool IsDxvkEnabled => DxvkToggle?.IsChecked == true;

    public PrototypePlayPage()
    {
        InitializeComponent();
    }

    private void MascotToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        if (MascotContainer.Visibility == Visibility.Visible)
        {
            MascotContainer.Visibility = Visibility.Collapsed;
            MascotToggleBtn.Content = "🦊 挂件(已藏)";
        }
        else
        {
            MascotContainer.Visibility = Visibility.Visible;
            MascotToggleBtn.Content = "🦊 挂件";
        }
    }

    private void BepInExToggle_Checked(object sender, RoutedEventArgs e)
    {
        ApplyEnvironmentReactivity();
    }

    private void BepInExToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        ApplyEnvironmentReactivity();
    }

    private void DxvkToggle_Checked(object sender, RoutedEventArgs e)
    {
        ApplyEnvironmentReactivity();
    }

    private void DxvkToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        ApplyEnvironmentReactivity();
    }

    /// <summary>
    /// 核心状态机循环：当 BepInEx / DXVK 状态变化时，完整联动更新启动按钮、状态横幅、Profile药丸与提示文案
    /// </summary>
    private void ApplyEnvironmentReactivity()
    {
        if (HeroLaunchBtn == null || StatusBanner == null) return;

        bool bepOn = IsBepInExEnabled;
        bool dxvkOn = IsDxvkEnabled;

        if (bepOn)
        {
            // 1. 模组模式 (绿色基调)
            HeroLaunchBtn.Background = new SolidColorBrush(Color.FromArgb(0xD8, 0x23, 0x6B, 0x3D));
            HeroLaunchBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69));
            HeroLaunchBtnText.Text = "启动游戏 (模组模式)";
            HeroSubtitleText.Text = "模组就绪 · 将加载 12 个本地插件与 BUE";

            StatusBanner.Background = new SolidColorBrush(Color.FromArgb(0xD0, 0x15, 0x24, 0x1B));
            StatusBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69));
            StatusBannerIcon.Text = "🛡️";
            StatusBannerTitle.Text = "运行状态就绪：";
            StatusBannerDesc.Text = dxvkOn
                ? "已开启纯净模组环境 (winhttp / -NoBattlEye) + DXVK Vulkan 渲染，无冲突。"
                : "已开启纯净模组环境 (winhttp / -NoBattlEye)，无崩溃与冲突。";
            StatusBannerTag.Text = "BE 5.4.23.5";
            StatusBannerTag.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69));

            ProfileTitleText.Text = "默认联机优化 (12 Mods)";
            if (FindResource("UnturnedAccentGoldBrush") is Brush goldBrush)
            {
                ProfileTitleText.Foreground = goldBrush;
            }

            BepInExStateDot.Fill = new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69));
            BepInExStateText.Text = "BepInEx 5.4.23.5 (x64) 运行就绪";
        }
        else
        {
            // 2. 官方纯净模式 (沉稳蓝灰调)
            HeroLaunchBtn.Background = new SolidColorBrush(Color.FromArgb(0xD8, 0x1E, 0x3A, 0x5F));
            HeroLaunchBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            HeroLaunchBtnText.Text = "启动游戏 (官方纯净模式)";
            HeroSubtitleText.Text = "纯净原版 · 直连官方 BattlEye 服务器";

            StatusBanner.Background = new SolidColorBrush(Color.FromArgb(0xD0, 0x14, 0x22, 0x32));
            StatusBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            StatusBannerIcon.Text = "🌐";
            StatusBannerTitle.Text = "官方纯净模式：";
            StatusBannerDesc.Text = dxvkOn
                ? "BepInEx 已停用，已开启 BattlEye 保护 + DXVK Vulkan 渲染，支持加入全球官方服。"
                : "BepInEx 已停用，已开启 BattlEye 反作弊守护，支持安全直连全球官方服务器。";
            StatusBannerTag.Text = "官方安全模式";
            StatusBannerTag.Foreground = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));

            ProfileTitleText.Text = "默认联机优化 (已停用加载)";
            ProfileTitleText.Foreground = new SolidColorBrush(Color.FromRgb(0x71, 0x80, 0x96));

            BepInExStateDot.Fill = new SolidColorBrush(Color.FromRgb(0x71, 0x80, 0x96));
            BepInExStateText.Text = "BepInEx 5.4.23.5 已停用 (启动原生游戏)";
        }

        // DXVK 标签与提示联动
        if (dxvkOn)
        {
            HeroLaunchTagText.Text = "[+DXVK]";
            HeroLaunchTagText.Visibility = Visibility.Visible;
            DxvkGpuHintText.Text = "✔ 已激活 DXVK：游戏将使用 Vulkan 渲染器启动 (建议在 RTX 显卡上配合全屏无边框)";
            DxvkGpuHintText.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69));
        }
        else
        {
            HeroLaunchTagText.Visibility = Visibility.Collapsed;
            DxvkGpuHintText.Text = "ℹ️ 检测到 NVIDIA GeForce RTX 4070 (支持 Vulkan 1.3，可按需开启提升帧率稳定性)";
            if (FindResource("UnturnedAccentGoldBrush") is Brush goldBrush)
            {
                DxvkGpuHintText.Foreground = goldBrush;
            }
        }

        // 触发 Shell 顶部药丸联动
        EnvironmentStateChanged?.Invoke(bepOn, dxvkOn);
    }

    private void HeroLaunchBtn_Click(object sender, RoutedEventArgs e)
    {
        string modeStr = IsBepInExEnabled ? "【模组模式】Unturned.exe -NoBattlEye" : "【官方纯净模式】Unturned.exe (+BattlEye)";
        if (IsDxvkEnabled) modeStr += " (DXVK Vulkan)";

        MessageBox.Show(
            $"【启动模拟循环演示】\n\n已通过 LaunchCoordinator 调起目标进程：\n{modeStr}\n\n当前方案: {ProfileTitleText.Text}\n工作目录: E:\\Steam\\steamapps\\common\\Unturned",
            "游戏启动循环验证",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void RepairBepInExBtn_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "【修复检查演示】\n已完成 BepInEx 5.4.23.5 核心注入完整性校验：\n✔ winhttp.dll 哈希校验匹配\n✔ doorstop_config.ini 引导配置正确\n✔ 核心插件目录可读写\n环境无损！",
            "环境与诊断闭环",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ExportDiagnosticsBtn_Click(object sender, RoutedEventArgs e)
    {
        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        MessageBox.Show(
            $"【导出诊断包演示】\n已完成运行日志脱敏并打包：\n• Client.log (已剔除 Steam 令牌与私钥)\n• 模组加载清单与冲突报告\n• 硬件环境与 Vulkan 驱动信息\n\n保存路径: /UMM-诊断包_{ts}.zip",
            "环境与诊断闭环",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
