using System.Globalization;
using NUnit.Framework;
using TrackGenius.UI.Converters;

namespace TrackGenius.UITests.Converters;

[TestFixture]
public class RacerPositionChangeSignConverterTests
{
    private readonly RacerPositionChangeSignConverter _converter = new();

    [TestCase(-5, ExpectedResult = PositionChangeState.Improved)]
    [TestCase(-1, ExpectedResult = PositionChangeState.Improved)]
    [TestCase(0, ExpectedResult = PositionChangeState.Unchanged)]
    [TestCase(1, ExpectedResult = PositionChangeState.Worsened)]
    [TestCase(7, ExpectedResult = PositionChangeState.Worsened)]
    public PositionChangeState GivenDelta_WhenConverted_ThenExpectedStateReturned(int delta)
        => (PositionChangeState)_converter.Convert(delta, typeof(PositionChangeState), null, CultureInfo.InvariantCulture);

    [Test]
    public void GivenNull_WhenConverted_ThenUnchangedReturned()
        => Assert.That(_converter.Convert(null, typeof(PositionChangeState), null, CultureInfo.InvariantCulture),
                       Is.EqualTo(PositionChangeState.Unchanged));
}
