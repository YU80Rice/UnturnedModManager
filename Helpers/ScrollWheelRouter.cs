using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace UnturnedModManager.Helpers;

/// <summary>
/// Routes wheel input from cards and other hit-test surfaces to the nearest scrollable ancestor.
/// Starting from the actual event source is important: WPF-UI pages contain layered panels, and
/// starting at the page root can skip the list or detail ScrollViewer the user is interacting with.
/// </summary>
public static class ScrollWheelRouter
{
    public static void RouteToNearestScrollViewer(object sender, MouseWheelEventArgs e)
    {
        DependencyObject? current = e.OriginalSource as DependencyObject ?? sender as DependencyObject;
        while (current is not null)
        {
            if (current is ScrollViewer viewer && viewer.ScrollableHeight > 0)
            {
                viewer.ScrollToVerticalOffset(viewer.VerticalOffset - e.Delta);
                e.Handled = true;
                return;
            }

            current = GetParent(current);
        }
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        try { return VisualTreeHelper.GetParent(current); }
        catch (InvalidOperationException) { return LogicalTreeHelper.GetParent(current); }
    }
}
