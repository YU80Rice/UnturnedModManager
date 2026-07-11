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
        // 关键修复：用 AddHandler + handledEventsToo:true 订阅冒泡 MouseWheel 事件
        // 即使内部 Border / ui:Card 子控件捕获并标记 Handled=true，本处理器仍会触发
        // 这比 XAML 中的 PreviewMouseWheel 隧道事件更可靠（后者可能被子控件拦截）
        RootScrollViewer?.AddHandler(
            UIElement.MouseWheelEvent,
            (MouseWheelEventHandler)ScrollViewer_MouseWheelForceScroll,
            true);
    }

    /// <summary>
    /// 强制滚动处理器：无论子控件是否处理 MouseWheel，都手动滚动 ScrollViewer。
    /// </summary>
    private void ScrollViewer_MouseWheelForceScroll(object sender, MouseWheelEventArgs e)
    {
        if (RootScrollViewer != null)
        {
            // e.Delta 标准 +120/-120，除以 2.0 给出 60 DIPs/档（比默认更明显）
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
