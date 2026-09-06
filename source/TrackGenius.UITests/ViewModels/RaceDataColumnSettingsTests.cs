using System.Linq;
using NUnit.Framework;
using TrackGenius.UI.ViewModels;

namespace TrackGenius.UITests.ViewModels;

[TestFixture]
public class RaceDataColumnSettingsTests
{
    [Test]
    public void GivenDefaultSettings_WhenCreated_ThenExistingColumnsVisibleAndAnalysisHidden()
    {
        var settings = new RaceDataColumnSettings();

        Assert.That(settings.All, Has.Count.EqualTo(14));
        Assert.That(settings.All[0], Is.SameAs(settings.Position));
        Assert.That(settings.Position.CanHide, Is.False);
        Assert.That(settings.All.Skip(1), Has.All.Property("CanHide").EqualTo(true));
        foreach (var option in settings.All.Where(o => !IsAnalysisColumn(o)))
            Assert.That(option.IsVisible, Is.True, $"{option.Key} should default visible");

        Assert.Multiple(() =>
        {
            Assert.That(settings.Top5Average.IsVisible, Is.False);
            Assert.That(settings.Top10Average.IsVisible, Is.False);
            Assert.That(settings.Top3Consecutive.IsVisible, Is.False);
            Assert.That(settings.StdDeviation.IsVisible, Is.False);
            Assert.That(settings.Consistency.IsVisible, Is.False);
        });
    }

    private static bool IsAnalysisColumn(RaceDataColumnOption option)
        => option.Key is "Top5Average" or "Top10Average" or "Top3Consecutive" or "StdDeviation" or "Consistency";

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

        // The analysis columns default hidden, so show them to keep this test about Laps/Notes only.
        settings.Top5Average.IsVisible = true;
        settings.Top10Average.IsVisible = true;
        settings.Top3Consecutive.IsVisible = true;
        settings.StdDeviation.IsVisible = true;
        settings.Consistency.IsVisible = true;

        CollectionAssert.AreEquivalent(new[] { "Laps", "Notes" }, settings.GetHiddenKeys());
    }

    [Test]
    public void GivenAppliedKeys_WhenRoundTrippedThroughGetThenApply_ThenStateIsStable()
    {
        var settings = new RaceDataColumnSettings();
        settings.Laps.IsVisible = false;
        settings.Notes.IsVisible = false;
        var hidden = settings.GetHiddenKeys().ToArray();

        var roundTripped = new RaceDataColumnSettings();
        roundTripped.ApplyHiddenKeys(hidden);

        CollectionAssert.AreEquivalent(hidden, roundTripped.GetHiddenKeys());
    }

    [Test]
    public void GivenAnalysisColumnShown_WhenRoundTripped_ThenStaysShown()
    {
        var settings = new RaceDataColumnSettings();
        settings.Top5Average.IsVisible = true;
        settings.Consistency.IsVisible = true;

        var roundTripped = new RaceDataColumnSettings();
        roundTripped.ApplyColumnOverrides(settings.GetHiddenKeys(), settings.GetShownKeys());

        Assert.Multiple(() =>
        {
            Assert.That(roundTripped.Top5Average.IsVisible, Is.True);
            Assert.That(roundTripped.Consistency.IsVisible, Is.True);
            Assert.That(roundTripped.Top10Average.IsVisible, Is.False);
        });
    }

    [Test]
    public void GivenOverrides_WhenApplied_ThenShownWinsOverHiddenAndDefaultsKept()
    {
        var settings = new RaceDataColumnSettings();

        settings.ApplyColumnOverrides(new[] { "Laps", "Consistency" }, new[] { "Consistency", "Top5Average" });

        Assert.Multiple(() =>
        {
            // Hidden override wins over the visible default.
            Assert.That(settings.Laps.IsVisible, Is.False);
            // Shown override wins over both the hidden list and the hidden default.
            Assert.That(settings.Consistency.IsVisible, Is.True);
            Assert.That(settings.Top5Average.IsVisible, Is.True);
            // No override → constructor default kept (hidden analysis column stays hidden,
            // visible column stays visible).
            Assert.That(settings.Top10Average.IsVisible, Is.False);
            Assert.That(settings.Position.IsVisible, Is.True);
        });
    }

    [Test]
    public void GivenVisibleByDefaultColumns_WhenGetShownKeysCalled_ThenOnlyDeviationsFromDefaultReturned()
    {
        var settings = new RaceDataColumnSettings();
        settings.Consistency.IsVisible = true;
        settings.Laps.IsVisible = false;

        Assert.Multiple(() =>
        {
            // Only keys shown against their default belong in the shown list.
            CollectionAssert.AreEquivalent(new[] { "Consistency" }, settings.GetShownKeys());
            // Hidden semantics are unchanged: every invisible column, whether the user hid it
            // or it defaults hidden (the remaining analysis columns).
            CollectionAssert.AreEquivalent(
                new[] { "Laps", "Top5Average", "Top10Average", "Top3Consecutive", "StdDeviation" },
                settings.GetHiddenKeys());
        });
    }
}
