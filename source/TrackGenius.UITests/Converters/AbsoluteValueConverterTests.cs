using System.Globalization;
using NUnit.Framework;
using TrackGenius.UI.Converters;

namespace TrackGenius.UITests.Converters;

[TestFixture]
public class AbsoluteValueConverterTests
{
    private readonly AbsoluteValueConverter _converter = new();

    [TestCase(0, ExpectedResult = 0)]
    [TestCase(5, ExpectedResult = 5)]
    [TestCase(-3, ExpectedResult = 3)]
    [TestCase(-99, ExpectedResult = 99)]
    public int GivenInteger_WhenConverted_ThenAbsoluteValueReturned(int value)
        => (int)_converter.Convert(value, typeof(int), null, CultureInfo.InvariantCulture);

    [Test]
    public void GivenNull_WhenConverted_ThenZeroReturned()
        => Assert.That(_converter.Convert(null, typeof(int), null, CultureInfo.InvariantCulture), Is.EqualTo(0));
}
