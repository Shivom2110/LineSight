using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LineSight.Dashboard.Converters;

/// <summary>Hides an element when the bound value is null or an empty string.</summary>
public sealed class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || value is string { Length: 0 } ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
