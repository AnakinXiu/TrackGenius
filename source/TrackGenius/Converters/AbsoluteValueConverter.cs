using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TrackGenius.UI.Converters;

public sealed class AbsoluteValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == DependencyProperty.UnsetValue)
            return 0;
        if (value == null)
            return 0;

        try
        {
            return Math.Abs(System.Convert.ToInt32(value, culture));
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            return 0;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
