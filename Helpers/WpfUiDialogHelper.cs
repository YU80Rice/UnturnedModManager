using System.Threading.Tasks;
using System.Windows;

namespace UnturnedModManager.Helpers;

/// <summary>
/// 提供确认弹窗帮助方法。
///
/// 说明：WPF-UI 3.0.5 中 ContentDialog 在 NavigationView + Page 场景下缺少公开宿主控件，
/// 调用 ShowAsync 会抛出 "DialogHost was not set" 并闪退。因此 Helper 改用系统原生 MessageBox，
/// 稳定可靠，且在 Windows 11 下自动采用系统 Fluent 风格。
/// </summary>
public static class WpfUiDialogHelper
{
    private static Task<bool> ConfirmAsync(string title, string content)
    {
        var result = System.Windows.MessageBox.Show(
            content,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    /// <summary>
    /// 第一步确认：询问是否由启动器自动下载并部署 BepInEx。
    /// </summary>
    public static Task<bool> ConfirmAutoInstallAsync()
    {
        return ConfirmAsync(
            "未检测到 BepInEx",
            "未检测到 BepInEx 模组环境，是否由启动器自动下载并部署到您的 Unturned 目录？");
    }

    /// <summary>
    /// 第二步确认：提示即将下载官方稳定版，请确保网络通畅且 Steam 关闭。
    /// </summary>
    public static Task<bool> ConfirmStartDownloadAsync()
    {
        return ConfirmAsync(
            "准备下载",
            "即将下载官方 BepInEx 5.4.22 x64 稳定版，请确保您的网络通畅且 Steam 处于关闭状态，是否开始？");
    }

    /// <summary>
    /// 修复确认：询问是否重新下载并完整覆盖 BepInEx 环境。
    /// </summary>
    public static Task<bool> ConfirmRepairAsync()
    {
        return ConfirmAsync(
            "修复 BepInEx 环境",
            "将重新下载并完整覆盖 BepInEx 环境以修复损坏或缺失的 dll。是否继续？");
    }

    /// <summary>
    /// 启动时检测到未安装 BepInEx 且全局模组开关开启，询问是否立即安装。
    /// </summary>
    public static Task<bool> ConfirmInstallBeforeLaunchAsync()
    {
        return ConfirmAsync(
            "模组环境未安装",
            "没有安装 BepInEx 模组环境，是否立即下载并安装？");
    }

    /// <summary>
    /// 二次确认：用户拒绝安装后，询问是否以原版启动游戏。
    /// </summary>
    public static Task<bool> ConfirmLaunchVanillaAsync()
    {
        return ConfirmAsync(
            "以原版启动",
            "将以原版启动游戏，是否继续？");
    }

    /// <summary>
    /// 首次启动主动探测：发现 Steam 注册的 Unturned 安装路径后，询问用户是否采用。
    /// </summary>
    public static Task<bool> ConfirmDetectedGamePathAsync(string detectedPath)
    {
        return ConfirmAsync(
            "发现游戏路径",
            $"检测到您的《未转变者（Unturned）》安装路径为：\n\n{detectedPath}\n\n是否将其设为默认启动路径？");
    }

    /// <summary>
    /// 路径配置成功后的 OK 提示。
    /// </summary>
    public static Task ShowPathConfiguredSuccessAsync()
    {
        System.Windows.MessageBox.Show(
            "路径配置成功！",
            "完成",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
        return Task.CompletedTask;
    }
}
