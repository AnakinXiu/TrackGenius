using System.Globalization;
using System.Windows;
using NUnit.Framework;
using TrackGenius.UI.Converters;

namespace TrackGenius.UITests.Converters;

[TestFixture]
public class BooleanToGridLengthConverterTests
{
    private readonly BooleanToGridLengthConverter _converter = new();

    [Test]
    public void GivenTrue_WhenConverted_ThenStarGridLengthReturned()
    {
        var result = (GridLength)_converter.Convert(true, typeof(GridLength), null, CultureInfo.InvariantCulture);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsStar, Is.True);
            Assert.That(result.Value, Is.EqualTo(1d));
        });
    }

    [Test]
    public void GivenFalse_WhenConverted_ThenZeroAbsoluteGridLengthReturned()
    {
        var result = (GridLength)_converter.Convert(false, typeof(GridLength), null, CultureInfo.InvariantCulture);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsAbsolute, Is.True);
            Assert.That(result.Value, Is.EqualTo(0d));
        });
    }

    [Test]
    public void GivenTrueWithNumericParameter_WhenConverted_ThenParameterWeightedStarGridLengthReturned()
    {
        var result = (GridLength)_converter.Convert(true, typeof(GridLength), 8, CultureInfo.InvariantCulture);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsStar, Is.True);
            Assert.That(result.Value, Is.EqualTo(8d));
        });
    }

    [TestCase("11", ExpectedResult = 11d)]
    [TestCase("3.5", ExpectedResult = 3.5d)]
    public double GivenTrueWithStringParameter_WhenConverted_ThenParsedStarWeightReturned(string parameter)
        => ((GridLength)_converter.Convert(true, typeof(GridLength), parameter, CultureInfo.InvariantCulture)).Value;

    [Test]
    public void GivenNonBool_WhenConverted_ThenHiddenLengthReturned()
        => Assert.That(
            ((GridLength)_converter.Convert("not a bool", typeof(GridLength), null, CultureInfo.InvariantCulture)).Value,
            Is.EqualTo(0d));
}
