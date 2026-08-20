using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TrackGenius.UI.Converters;

public sealed class BooleanToGridLengthConverter : IValueConverter
{
    /// <summary>
    /// Converts a boolean to a <see cref="GridLength"/>: visible (true) yields a star
    /// length whose weight comes from <paramref name="parameter"/> (default 1); hidden
    /// (false/null) yields a zero length so the column collapses cleanly.
    /// The star weight lets each column claim a share of the available width
    /// proportional to its header width.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool visible && visible)
        {
            var weight = TryParseWeight(parameter, out var w) ? w : 1.0;
            return new GridLength(weight, GridUnitType.Star);
        }

        return new GridLength(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static bool TryParseWeight(object parameter, out double weight)
    {
        weight = 0;
        switch (parameter)
        {
            case double d:
                weight = d;
                return true;
            case int i:
                weight = i;
                return true;
            case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                weight = parsed;
                return true;
            default:
                return false;
        }
    }
}
