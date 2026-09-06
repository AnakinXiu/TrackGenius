using System.Linq;
using NUnit.Framework;
using TrackGenius.Model;

namespace TrackGenius.UITests.Model;

[TestFixture]
public class AnonymousDriverCreatorTests
{
    [Test]
    public void GivenTransponderID_WhenCreateAnonymous_ThenCarCarriesTransponderNumber()
    {
        var driver = AnonymousDriverCreator.CreateAnonymous("100");

        var car = driver.Cars.Single();
        Assert.Multiple(() =>
        {
            Assert.That(car.Transponder, Is.Not.Null);
            Assert.That(car.Transponder.RecoderNumber, Is.EqualTo("100"));
        });
    }

    [Test]
    public void GivenTransponderID_WhenCreateAnonymous_ThenDriverNamedAfterTransponder()
    {
        var driver = AnonymousDriverCreator.CreateAnonymous("200");

        Assert.Multiple(() =>
        {
            Assert.That(driver.DriverName, Is.EqualTo("200"));
            Assert.That(driver.Cars, Has.Count.EqualTo(1));
        });
    }
}
