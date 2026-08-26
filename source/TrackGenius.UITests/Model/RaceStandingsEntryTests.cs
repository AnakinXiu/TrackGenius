using System;
using NUnit.Framework;
using TrackGenius.Model;

namespace TrackGenius.UITests.Model;

[TestFixture]
public class RaceStandingsEntryTests
{
    [Test]
    public void GivenValues_WhenEntryConstructed_ThenPropertiesHold()
    {
        var raceData = new RaceData(null, null);

        var entry = new RaceStandingsEntry(raceData, Position: 2,
            BestLapTime: TimeSpan.FromSeconds(18.2), LastLapTime: TimeSpan.FromSeconds(19.1),
            Gap: "0:02.100", Interval: "0:04.200",
            Top5Average: "0:18.300", Top10Average: "0:18.600",
            Top3Consecutive: "0:55.200 (18.400)", StdDeviation: "0.150", Consistency: "99.2");

        Assert.Multiple(() =>
        {
            Assert.That(entry.RaceData, Is.SameAs(raceData));
            Assert.That(entry.Position, Is.EqualTo(2));
            Assert.That(entry.BestLapTime, Is.EqualTo(TimeSpan.FromSeconds(18.2)));
            Assert.That(entry.LastLapTime, Is.EqualTo(TimeSpan.FromSeconds(19.1)));
            Assert.That(entry.Gap, Is.EqualTo("0:02.100"));
            Assert.That(entry.Interval, Is.EqualTo("0:04.200"));
            Assert.That(entry.Top5Average, Is.EqualTo("0:18.300"));
            Assert.That(entry.Top10Average, Is.EqualTo("0:18.600"));
            Assert.That(entry.Top3Consecutive, Is.EqualTo("0:55.200 (18.400)"));
            Assert.That(entry.StdDeviation, Is.EqualTo("0.150"));
            Assert.That(entry.Consistency, Is.EqualTo("99.2"));
        });
    }
}
