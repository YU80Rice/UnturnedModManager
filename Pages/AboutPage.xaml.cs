using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace UnturnedModManager.Pages;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        // 关键修复：订阅隧道事件 PreviewMouseWheelEvent + handledEventsToo=true
        // 隧道事件从 Window 根向下传递，经过 ScrollViewer 时本处理器触发
        // 即使祖先（NavigationView 等）标记 Handled=true，handledEventsToo=true 仍会触发
        // 这比订阅冒泡 MouseWheelEvent 更可靠，因为后者可能被祖先在隧道阶段抑制而根本不触发
        RootScrollViewer?.AddHandler(
            UIElement.PreviewMouseWheelEvent,
            (MouseWheelEventHandler)ScrollViewer_ForceScroll,
            true);
    }

    /// <summary>
    /// 强制滚动处理器：在隧道阶段拦截滚轮事件并手动滚动 ScrollViewer。
    /// </summary>
    private void ScrollViewer_ForceScroll(object sender, MouseWheelEventArgs e)
    {
        if (RootScrollViewer != null)
        {
            // e.Delta 标准 +120/-120，除以 2.0 给出 60 DIPs/档
            RootScrollViewer.ScrollToVerticalOffset(RootScrollViewer.VerticalOffset - e.Delta / 2.0);
            e.Handled = true;
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
