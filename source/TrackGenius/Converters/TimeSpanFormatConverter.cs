using System;
using System.Globalization;
using System.Windows.Data;

namespace TrackGenius.UI.Converters;

public sealed class TimeSpanFormatConverter : IValueConverter
{
    public string DefaultFormat { get; set; } = @"m\:ss\.fff";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
            return timeSpan.ToString(parameter as string ?? DefaultFormat, culture);

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
