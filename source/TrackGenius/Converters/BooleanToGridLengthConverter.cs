using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TrackGenius.UI.Converters;

public sealed class BooleanToGridLengthConverter : IValueConverter
{
    public GridLength VisibleLength { get; set; } = new GridLength(1, GridUnitType.Star);

    public GridLength HiddenLength { get; set; } = new GridLength(0);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool visible && visible ? VisibleLength : HiddenLength;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
