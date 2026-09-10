using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UnturnedModManager.Prototypes;

/// <summary>
/// UnturnedNativeShellPrototype 技术原型代码后置
/// 验证 5 槽位药丸导航互斥切换、Page + Frame 容器挂载契约、以及滚轮隧道路由与次级入口行为
/// </summary>
public partial class UnturnedNativeShellPrototype : UserControl
{
    private PrototypePlayPage? _playPage;
    private PrototypeDataPage? _dataPage;

    public UnturnedNativeShellPrototype()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 默认初始化并挂载首页 (开始游戏 PlayPage)
        NavigateToSlot(NavSlotPlay);
    }

    /// <summary>
    /// 5 槽位主业务按钮点击事件：切换高亮 Tag 并导航至对应 Page
    /// </summary>
    private void NavSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button clickedButton)
        {
            NavigateToSlot(clickedButton);
        }
    }

    public void NavigateToSlot(Button targetButton)
    {
        // 重置所有业务槽位的选中状态
        NavSlotPlay.Tag = null;
        NavSlotData.Tag = null;
        NavSlotMods.Tag = null;
        NavSlotSettings.Tag = null;

        // 激活当前选中的槽位
        targetButton.Tag = "Active";

        // 驱动 Frame 挂载对应的 Page
        if (targetButton == NavSlotPlay)
        {
            if (_playPage == null)
            {
                _playPage = new PrototypePlayPage();
                _playPage.EnvironmentStateChanged += UpdateGlobalStatus;
            }
            MainContentFrame.Navigate(_playPage);
        }
        else if (targetButton == NavSlotData)
        {
            _dataPage ??= new PrototypeDataPage();
            MainContentFrame.Navigate(_dataPage);
        }
        else if (targetButton == NavSlotMods)
        {
            // 模拟插件工坊与社区插件列表页挂载
            MainContentFrame.Navigate(CreatePlaceholderPage(
                "🧩 插件工坊 (Plugins & Workshop)",
                "聚合本地已安装插件与 unmod.online 社区发现，兼容现有 ModListPage / OnlineMarketPage，零修改挂载进 Frame。"
            ));
        }
        else if (targetButton == NavSlotSettings)
        {
            // 模拟设置页挂载
            MainContentFrame.Navigate(CreatePlaceholderPage(
                "⚙ 启动器设置 (Settings)",
                "兼容现有 SettingsPage，包括路径扫描、参数配置、网络诊断、全屏壁纸与外观设置。"
            ));
        }
    }

    private static Page CreatePlaceholderPage(string title, string description)
    {
        var page = new Page();
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xB8, 0x1A, 0x20, 0x27)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x2A, 0x34, 0x41)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24),
            Margin = new Thickness(0, 0, 0, 0)
        };

        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        });
        sp.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 13,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE0)),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        border.Child = sp;
        page.Content = border;
        return page;
    }

    /// <summary>
    /// 退出 UMM 槽位响应 (原型阶段提示)
    /// </summary>
    private void NavSlotExit_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "【原型验证】点击了第 5 槽位「退出 UMM」。在生产中将触发启动器优雅关闭流程（不影响正在运行的游戏）。",
            "Unturned Native Shell 原型",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>
    /// 任务中心次级入口
    /// </summary>
    private void SecondaryTaskBtn_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("【原型验证】触发任务中心浮层或导航。", "Unturned Native Shell 原型");
    }

    /// <summary>
    /// 账户中心次级入口
    /// </summary>
    private void SecondaryAccountBtn_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("【原型验证】触发社区账户面板。", "Unturned Native Shell 原型");
    }

    /// <summary>
    /// 关于次级入口
    /// </summary>
    private void SecondaryAboutBtn_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("【原型验证】关于 Unturned Mod Manager v2.2.1", "Unturned Native Shell 原型");
    }

    /// <summary>
    /// 隧道路由鼠标滚轮事件，防止在嵌套容器中外层吞掉内部 ScrollViewer 的滚轮输入
    /// </summary>
    private void Shell_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;

        // 如果鼠标当前停留在 ScrollViewer 内部，优先允许子控件处理，不强制截获
        // 这一隧道路由机制确保全屏 HUD 与内嵌 Page 的滚动互不冲突
    }

    /// <summary>
    /// 响应内嵌 Page 的环境状态变更，联动更新顶部全域状态药丸
    /// </summary>
    public void UpdateGlobalStatus(bool bepInExEnabled, bool dxvkEnabled)
    {
        if (bepInExEnabled)
        {
            GlobalStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0xA1, 0x69)); // Green
            GlobalStatusModeText.Text = dxvkEnabled ? "模组模式 (-NoBattlEye +DXVK)" : "模组模式 (-NoBattlEye)";
            GlobalStatusBepText.Text = "BepInEx 5.4.23.5 就绪";
            GlobalStatusModsText.Text = "已挂载 12 个插件";
            if (FindResource("UnturnedAccentGoldBrush") is System.Windows.Media.Brush goldBrush)
            {
                GlobalStatusModsText.Foreground = goldBrush;
            }
        }
        else
        {
            GlobalStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3B, 0x82, 0xF6)); // Blue
            GlobalStatusModeText.Text = "官方纯净 (+BattlEye)";
            GlobalStatusBepText.Text = dxvkEnabled ? "dxgi被BE阻止(回退DX11)" : "BepInEx 已停用";
            GlobalStatusModsText.Text = "原生游戏环境";
            GlobalStatusModsText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x71, 0x80, 0x96));
        }
    }
}
