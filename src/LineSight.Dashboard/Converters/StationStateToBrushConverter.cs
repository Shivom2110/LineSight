using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LineSight.Core.Models;

namespace LineSight.Dashboard.Converters;

/// <summary>Running = green, Idle = yellow, Down = red, no data = grey.</summary>
public sealed class StationStateToBrushConverter : IValueConverter
{
    private static readonly Brush Running = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0x9E, 0x5B)));
    private static readonly Brush Idle = Freeze(new SolidColorBrush(Color.FromRgb(0xE0, 0xA8, 0x00)));
    private static readonly Brush Down = Freeze(new SolidColorBrush(Color.FromRgb(0xD6, 0x45, 0x45)));
    private static readonly Brush Unknown = Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98)));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        StationState.Running => Running,
        StationState.Idle => Idle,
        StationState.Down => Down,
        _ => Unknown
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    // Frozen brushes are immutable, so one instance can be shared safely by every card.
    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
