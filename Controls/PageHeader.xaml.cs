using System.Windows;
using System.Windows.Controls;

namespace UnturnedModManager.Controls;

public partial class PageHeader : System.Windows.Controls.UserControl
{
    public PageHeader() => InitializeComponent();

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(""));
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(PageHeader), new PropertyMetadata(""));
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
}
