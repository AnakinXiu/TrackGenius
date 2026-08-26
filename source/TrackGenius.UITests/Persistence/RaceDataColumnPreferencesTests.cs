using System;
using System.IO;
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

    [Test]
    public void GivenWrongTypedHiddenColumns_WhenParsed_ThenEmptyListReturned()
    {
        var json = "{\"hiddenColumns\":5}";
        Assert.That(RaceDataColumnPreferences.ParseHiddenColumns(json), Is.Empty);
    }

    [Test]
    public void GivenMixedTypeHiddenColumns_WhenParsed_ThenOnlyStringsReturned()
    {
        var json = "{\"hiddenColumns\":[\"a\",5]}";
        CollectionAssert.AreEqual(new[] { "a" }, RaceDataColumnPreferences.ParseHiddenColumns(json));
    }

    [Test]
    public void GivenHiddenAndShownKeys_WhenSerialized_ThenJsonCarriesBothArrays()
    {
        var json = RaceDataColumnPreferences.SerializeColumns(new[] { "Laps" }, new[] { "Consistency" });

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"hiddenColumns\""));
            Assert.That(json, Does.Contain("\"shownColumns\""));
            CollectionAssert.AreEqual(new[] { "Laps" }, RaceDataColumnPreferences.ParseHiddenColumns(json));
            CollectionAssert.AreEqual(new[] { "Consistency" }, RaceDataColumnPreferences.ParseShownColumns(json));
        });
    }

    [Test]
    public void GivenShownColumns_WhenParsed_ThenKeysReturned()
    {
        var json = "{\"shownColumns\":[\"Consistency\",\"Top5Average\"]}";
        CollectionAssert.AreEqual(new[] { "Consistency", "Top5Average" }, RaceDataColumnPreferences.ParseShownColumns(json));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not json")]
    [TestCase("{")]
    [TestCase("{\"shownColumns\":5}")]
    [TestCase("{\"somethingElse\":42}")]
    public void GivenInvalidOrMissingShownColumns_WhenParsed_ThenEmptyListReturned(string json)
        => Assert.That(RaceDataColumnPreferences.ParseShownColumns(json), Is.Empty);

    [Test]
    public void GivenMixedTypeShownColumns_WhenParsed_ThenOnlyStringsReturned()
    {
        var json = "{\"shownColumns\":[\"Consistency\",5]}";
        CollectionAssert.AreEqual(new[] { "Consistency" }, RaceDataColumnPreferences.ParseShownColumns(json));
    }

    [Test]
    public void GivenNoPreferenceFile_WhenLoaded_ThenLoadReturnsNullSoDefaultsSurvive()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tg-missing-{Guid.NewGuid():N}.json");
        var store = new RaceDataColumnPreferencesStore(path);

        Assert.Multiple(() =>
        {
            Assert.That(store.Load(), Is.Null);
            Assert.That(store.LoadShown(), Is.Null);
        });
    }

    [Test]
    public void GivenSavedShownAndHiddenKeys_WhenLoaded_ThenBothListsRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tg-roundtrip-{Guid.NewGuid():N}.json");
        try
        {
            var store = new RaceDataColumnPreferencesStore(path);
            store.Save(new[] { "Laps" }, new[] { "Consistency" });

            Assert.Multiple(() =>
            {
                CollectionAssert.AreEqual(new[] { "Laps" }, store.Load());
                CollectionAssert.AreEqual(new[] { "Consistency" }, store.LoadShown());
            });
        }
        finally
        {
            File.Delete(path);
        }
    }
}
