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
            _playPage ??= new PrototypePlayPage();
            MainContentFrame.Navigate(_playPage);
        }
        else if (targetButton == NavSlotData)
        {
            _dataPage ??= new PrototypeDataPage();
            MainContentFrame.Navigate(_dataPage);
        }
        else if (targetButton == NavSlotMods)
        {
            // 模拟创意工坊与社区插件列表页挂载
            MainContentFrame.Navigate(CreatePlaceholderPage(
                "🧩 创意工坊与模组市场 (Mods & Workshop)",
                "兼容现有 ModListPage / OnlineMarketPage，零修改直接挂载进 Frame。"
            ));
        }
        else if (targetButton == NavSlotSettings)
        {
            // 模拟设置页挂载
            MainContentFrame.Navigate(CreatePlaceholderPage(
                "⚙ 启动器与游戏设置 (Settings)",
                "兼容现有 SettingsPage，包括路径扫描、参数配置、网络诊断与外观设置。"
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
    /// 退出游戏槽位响应 (原型阶段提示)
    /// </summary>
    private void NavSlotExit_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "【原型验证】点击了第 5 槽位「退出游戏/关闭启动器」。在生产中将触发优雅关闭流程。",
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
}
