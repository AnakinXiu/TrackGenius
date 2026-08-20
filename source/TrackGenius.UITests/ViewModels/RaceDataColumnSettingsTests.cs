using System.Linq;
using NUnit.Framework;
using TrackGenius.UI.ViewModels;

namespace TrackGenius.UITests.ViewModels;

[TestFixture]
public class RaceDataColumnSettingsTests
{
    [Test]
    public void GivenDefaultSettings_WhenCreated_ThenAllTenColumnsVisibleAndPositionNonHideable()
    {
        var settings = new RaceDataColumnSettings();

        Assert.That(settings.All, Has.Count.EqualTo(10));
        Assert.That(settings.All[0], Is.SameAs(settings.Position));
        Assert.That(settings.Position.CanHide, Is.False);
        Assert.That(settings.All.Skip(1), Has.All.Property("CanHide").EqualTo(true));
        foreach (var option in settings.All)
            Assert.That(option.IsVisible, Is.True);
    }

    [Test]
    public void GivenNonHideableOption_WhenIsVisibleSetFalse_ThenStaysVisible()
    {
        var option = new RaceDataColumnOption("Position", "Position", canHide: false);

        option.IsVisible = false;

        Assert.That(option.IsVisible, Is.True);
    }

    [Test]
    public void GivenHiddenKeys_WhenApplied_ThenMatchingColumnsHiddenAndPositionStaysVisible()
    {
        var settings = new RaceDataColumnSettings();

        settings.ApplyHiddenKeys(new[] { "Laps", "Notes", "Position" });

        Assert.Multiple(() =>
        {
            Assert.That(settings.Laps.IsVisible, Is.False);
            Assert.That(settings.Notes.IsVisible, Is.False);
            Assert.That(settings.Position.IsVisible, Is.True);
            Assert.That(settings.Driver.IsVisible, Is.True);
        });
    }

    [Test]
    public void GivenHiddenColumns_WhenGetHiddenKeysCalled_ThenReturnsOnlyHiddenKeys()
    {
        var settings = new RaceDataColumnSettings();
        settings.Laps.IsVisible = false;
        settings.Notes.IsVisible = false;

        CollectionAssert.AreEquivalent(new[] { "Laps", "Notes" }, settings.GetHiddenKeys());
    }

    [Test]
    public void GivenAppliedKeys_WhenRoundTrippedThroughGetThenApply_ThenStateIsStable()
    {
        var settings = new RaceDataColumnSettings();
        settings.Laps.IsVisible = false;
        settings.Transponder.IsVisible = false;
        var hidden = settings.GetHiddenKeys().ToArray();

        var roundTripped = new RaceDataColumnSettings();
        roundTripped.ApplyHiddenKeys(hidden);

        CollectionAssert.AreEquivalent(hidden, roundTripped.GetHiddenKeys());
    }
}
