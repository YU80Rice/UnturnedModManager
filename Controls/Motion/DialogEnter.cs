using System.Windows;
using MotionHelper = UnturnedModManager.Helpers.Motion;

namespace UnturnedModManager.Controls.Motion;

public static class DialogEnter
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(DialogEnter), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement root) return;
        if (e.NewValue is true)
        {
            root.Loaded += OnLoaded;
            if (root.IsLoaded) MotionHelper.ScaleFadeIn(root);
        }
        else
        {
            root.Loaded -= OnLoaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => MotionHelper.ScaleFadeIn((FrameworkElement)sender);
}
