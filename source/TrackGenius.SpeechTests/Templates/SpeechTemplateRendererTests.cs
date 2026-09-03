using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrackGenius.Model;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests.Templates;

[TestFixture]
public class SpeechTemplateRendererTests
{
    private readonly SpeechTemplateRenderer _renderer = new();

    [Test]
    public void GivenFastestLapIntent_WhenRendered_ThenSpokenText()
    {
        var intent = new AnnouncementIntent("FastestLap", AnnouncementPriority.High, "100", null,
            new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 12.438, true) }, DateTimeOffset.Now);

        var content = _renderer.Render(intent);

        Assert.Multiple(() =>
        {
            Assert.That(content.Text, Is.EqualTo("Car number one hundred, fastest lap, twelve point four three eight seconds."));
            Assert.That(content.Language, Is.EqualTo("en-US"));
            Assert.That(content.Ssml, Is.Null);
        });
    }

    [Test]
    public void GivenLeaderChangedIntent_WhenRendered_ThenSpokenText()
    {
        var intent = new AnnouncementIntent("LeaderChanged", AnnouncementPriority.High, "200", null,
            new List<RaceStandingsEntry> { TestStandings.Entry("200", 1, 15.0, false), TestStandings.Entry("100", 2, 15.5, false) },
            DateTimeOffset.Now);

        Assert.That(_renderer.Render(intent).Text,
            Is.EqualTo("Car number two hundred takes the lead."));
    }

    [Test]
    public void GivenPositionChangedIntent_WhenRendered_ThenSpokenText()
    {
        var intent = new AnnouncementIntent("PositionChanged", AnnouncementPriority.Important, "100", null,
            new List<RaceStandingsEntry> { TestStandings.Entry("200", 1, 15.0, false), TestStandings.Entry("100", 2, 15.5, false) },
            DateTimeOffset.Now);

        Assert.That(_renderer.Render(intent).Text,
            Is.EqualTo("Car number one hundred moves up to second place."));
    }

    [Test]
    public void GivenRaceStartedIntent_WhenRendered_ThenSpokenText()
    {
        var intent = new AnnouncementIntent("RaceStarted", AnnouncementPriority.Critical, "", null,
            new List<RaceStandingsEntry>(), DateTimeOffset.Now);

        Assert.That(_renderer.Render(intent).Text, Is.EqualTo("Race started."));
    }

    [Test]
    public void GivenRaceFinishedIntent_WhenRendered_ThenSpokenText()
    {
        var intent = new AnnouncementIntent("RaceFinished", AnnouncementPriority.Critical, "", null,
            new List<RaceStandingsEntry>(), DateTimeOffset.Now);

        Assert.That(_renderer.Render(intent).Text, Is.EqualTo("Race finished."));
    }
}
