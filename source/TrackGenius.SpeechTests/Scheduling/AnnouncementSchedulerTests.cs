using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrackGenius.Model;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests.Scheduling;

[TestFixture]
public class AnnouncementSchedulerTests
{
    [Test]
    public void GivenFacts_WhenScheduled_ThenIntentsCarryFields()
    {
        var scheduler = new AnnouncementScheduler();
        var now = DateTimeOffset.Now;
        var facts = new[]
        {
            new DerivedFact("FastestLap", AnnouncementPriority.High, "100", "Car 100", TimeSpan.FromSeconds(15)),
        };
        var snapshot = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 19.5, true) };

        var intents = scheduler.Schedule(facts, snapshot, now);

        var intent = intents.Single();
        Assert.Multiple(() =>
        {
            Assert.That(intent.Type, Is.EqualTo("FastestLap"));
            Assert.That(intent.Priority, Is.EqualTo(AnnouncementPriority.High));
            Assert.That(intent.DriverId, Is.EqualTo("100"));
            Assert.That(intent.DriverDisplayName, Is.EqualTo("Car 100"));
            Assert.That(intent.Snapshot, Is.SameAs(snapshot));
            Assert.That(intent.CreatedAt, Is.EqualTo(now));
            Assert.That(intent.ExpiresAfter, Is.EqualTo(TimeSpan.FromSeconds(15)));
        });
    }
}
