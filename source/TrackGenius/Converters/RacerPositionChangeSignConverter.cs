using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TrackGenius.UI.Converters;

public sealed class RacerPositionChangeSignConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == DependencyProperty.UnsetValue)
            return PositionChangeState.Unchanged;
        if (value == null)
            return PositionChangeState.Unchanged;

        try
        {
            var delta = System.Convert.ToInt32(value, culture);
            if (delta < 0) return PositionChangeState.Improved;
            if (delta > 0) return PositionChangeState.Worsened;
            return PositionChangeState.Unchanged;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            return PositionChangeState.Unchanged;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
