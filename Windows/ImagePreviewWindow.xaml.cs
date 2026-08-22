using System.Windows;
using System.Windows.Input;

namespace UnturnedModManager;

/// <summary>仅预览远程图片，不执行图片页面中的任何内容或脚本。</summary>
public partial class ImagePreviewWindow : Window
{
    public string ImageUrl { get; }

    public ImagePreviewWindow(string imageUrl)
    {
        ImageUrl = imageUrl;
        InitializeComponent();
        DataContext = this;
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PreviewScale is null) return;
        PreviewScale.ScaleX = e.NewValue;
        PreviewScale.ScaleY = e.NewValue;
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e) =>
        ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 0.25);

    private void ResetZoom_Click(object sender, RoutedEventArgs e) => ZoomSlider.Value = 1;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        ZoomSlider.Value = Math.Clamp(
            ZoomSlider.Value + (e.Delta > 0 ? 0.1 : -0.1),
            ZoomSlider.Minimum,
            ZoomSlider.Maximum);
        e.Handled = true;
    }
}
