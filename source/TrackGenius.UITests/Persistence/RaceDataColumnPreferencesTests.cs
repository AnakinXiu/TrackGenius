using System;
using NUnit.Framework;
using TrackGenius.UI.Persistence;

namespace TrackGenius.UITests.Persistence;

[TestFixture]
public class RaceDataColumnPreferencesTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not json")]
    [TestCase("{")]
    public void GivenInvalidJson_WhenParsed_ThenEmptyListReturned(string json)
        => Assert.That(RaceDataColumnPreferences.ParseHiddenColumns(json), Is.Empty);

    [Test]
    public void GivenValidJson_WhenParsed_ThenKeysReturned()
    {
        var json = "{\"hiddenColumns\":[\"Laps\",\"Notes\"]}";
        CollectionAssert.AreEqual(new[] { "Laps", "Notes" }, RaceDataColumnPreferences.ParseHiddenColumns(json));
    }

    [Test]
    public void GivenJsonWithoutHiddenColumns_WhenParsed_ThenEmptyListReturned()
    {
        var json = "{\"somethingElse\":42}";
        Assert.That(RaceDataColumnPreferences.ParseHiddenColumns(json), Is.Empty);
    }

    [Test]
    public void GivenKeys_WhenSerializedThenParsed_ThenRoundTrips()
    {
        var json = RaceDataColumnPreferences.SerializeHiddenColumns(new[] { "Laps", "Notes" });
        CollectionAssert.AreEqual(new[] { "Laps", "Notes" }, RaceDataColumnPreferences.ParseHiddenColumns(json));
    }

    [Test]
    public void GivenEmptyKeys_WhenSerializedThenParsed_ThenEmpty()
    {
        var json = RaceDataColumnPreferences.SerializeHiddenColumns(Array.Empty<string>());
        Assert.That(RaceDataColumnPreferences.ParseHiddenColumns(json), Is.Empty);
    }
}
