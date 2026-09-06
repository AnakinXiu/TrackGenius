using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrackGenius.Model;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests.Scheduling;

[TestFixture]
public class StandingsDifferTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Now;

    [Test]
    public void GivenFirstSnapshot_WhenDiffed_ThenRaceStartedOnly()
    {
        var current = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true) };

        var facts = StandingsDiffer.Diff(null, current, Now);

        Assert.That(facts.Select(f => f.Type), Is.EqualTo(new[] { "RaceStarted" }));
    }

    [Test]
    public void GivenLeaderChanged_WhenDiffed_ThenLeaderChangedFact()
    {
        var previous = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true), TestStandings.Entry("200", 2, 21.0, false) };
        var current = new List<RaceStandingsEntry> { TestStandings.Entry("200", 1, 20.5, true), TestStandings.Entry("100", 2, 20.0, false) };

        var facts = StandingsDiffer.Diff(previous, current, Now);

        var leader = facts.Single(f => f.Type == "LeaderChanged");
        Assert.Multiple(() =>
        {
            Assert.That(leader.DriverId, Is.EqualTo("200"));
            Assert.That(leader.Priority, Is.EqualTo(AnnouncementPriority.High));
            Assert.That(leader.ExpiresAfter, Is.EqualTo(TimeSpan.FromSeconds(10)));
        });
    }

    [Test]
    public void GivenNewRaceBest_WhenDiffed_ThenFastestLapFact()
    {
        var previous = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true) };
        var current = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 19.5, true) };

        var facts = StandingsDiffer.Diff(previous, current, Now);

        var fastest = facts.Single(f => f.Type == "FastestLap");
        Assert.Multiple(() =>
        {
            Assert.That(fastest.DriverId, Is.EqualTo("100"));
            Assert.That(fastest.Priority, Is.EqualTo(AnnouncementPriority.High));
            Assert.That(fastest.ExpiresAfter, Is.EqualTo(TimeSpan.FromSeconds(15)));
        });
    }

    [Test]
    public void GivenSameRaceBestRetained_WhenDiffed_ThenNoFastestLapFact()
    {
        var previous = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 19.5, true) };
        var current = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 19.5, true) };

        Assert.That(StandingsDiffer.Diff(previous, current, Now), Is.Empty);
    }

    [Test]
    public void GivenTopThreeSwap_WhenDiffed_ThenPositionChangedForGainer()
    {
        var previous = new List<RaceStandingsEntry>
        {
            TestStandings.Entry("100", 1, 20.0, true), TestStandings.Entry("200", 2, 21.0, false), TestStandings.Entry("300", 3, 22.0, false),
        };
        var current = new List<RaceStandingsEntry>
        {
            TestStandings.Entry("100", 1, 20.0, true), TestStandings.Entry("300", 2, 22.0, false), TestStandings.Entry("200", 3, 21.0, false),
        };

        var facts = StandingsDiffer.Diff(previous, current, Now);

        var position = facts.Single(f => f.Type == "PositionChanged");
        Assert.Multiple(() =>
        {
            Assert.That(position.DriverId, Is.EqualTo("300"));
            Assert.That(position.Priority, Is.EqualTo(AnnouncementPriority.Important));
            Assert.That(position.ExpiresAfter, Is.EqualTo(TimeSpan.FromSeconds(8)));
        });
    }

    [Test]
    public void GivenIdenticalSnapshots_WhenDiffed_ThenNoFacts()
    {
        var snapshot = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true) };

        Assert.That(StandingsDiffer.Diff(snapshot, snapshot, Now), Is.Empty);
    }
}
