using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace UnturnedModManager.Pages;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 拦截鼠标滚轮事件并手动滚动 ScrollViewer。
    /// 修复：内部 Border / ui:Card 有 Background 命中测试时，滚轮事件可能被吞噬而不冒泡到 ScrollViewer。
    /// PreviewMouseWheel 是隧道事件，在子控件处理前先到达此处。
    /// </summary>
    private void RootScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (RootScrollViewer != null)
        {
            RootScrollViewer.ScrollToVerticalOffset(RootScrollViewer.VerticalOffset - e.Delta / 3.0);
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
