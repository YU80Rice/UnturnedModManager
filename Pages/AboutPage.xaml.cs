using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace UnturnedModManager.Pages;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 拦截鼠标滚轮事件并手动路由到最近的 ScrollViewer 祖先。
    /// 修复：内部 Border/ui:Card 有 Background 命中测试时，滚轮事件会被吞噬而不冒泡到 ScrollViewer。
    /// PreviewMouseWheel 是隧道事件，在子控件处理前先到达此处。
    /// 通过 VisualTreeHelper 向上递归寻找最近的 ScrollViewer 并手动滚动。
    /// （套用 HomePage/SettingsPage 已验证的修复模式）
    /// </summary>
    private void CardPanel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject element) return;

        DependencyObject? current = element;
        while (current != null)
        {
            if (current is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                e.Handled = true;
                return;
            }
            current = VisualTreeHelper.GetParent(current);
        }
    }

    /// <summary>
    /// 点击超链接时用默认浏览器打开 URL。
    /// </summary>
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
        }
        catch
        {
            // 忽略打开失败
        }
        e.Handled = true;
    }
}
