using System;
using System.Globalization;
using NUnit.Framework;
using TrackGenius.UI.Converters;

namespace TrackGenius.UITests.Converters;

[TestFixture]
public class TimeSpanFormatConverterTests
{
    private readonly TimeSpanFormatConverter _converter = new();

    [Test]
    public void GivenTimeSpan_WhenConvertedWithDefaultFormat_ThenFormattedStringReturned()
    {
        var oneMinTwoSec345Ms = new TimeSpan(0, 0, 1, 2, 345); // exact, avoids double-rounding
        var text = (string)_converter.Convert(oneMinTwoSec345Ms, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.That(text, Is.EqualTo("1:02.345"));
    }

    [Test]
    public void GivenTimeSpan_WhenConvertedWithParameter_ThenParameterFormatUsed()
    {
        var text = (string)_converter.Convert(TimeSpan.Zero, typeof(string), @"ss\.ff", CultureInfo.InvariantCulture);
        Assert.That(text, Is.EqualTo("00.00"));
    }

    [Test]
    public void GivenNonTimeSpan_WhenConverted_ThenEmptyStringReturned()
        => Assert.That(_converter.Convert("not a timespan", typeof(string), null, CultureInfo.InvariantCulture),
                       Is.EqualTo(string.Empty));
}
