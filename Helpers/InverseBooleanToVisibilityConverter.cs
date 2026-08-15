using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UnturnedModManager.Helpers;

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool visible && !visible ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility visibility && visibility != Visibility.Visible;
}
