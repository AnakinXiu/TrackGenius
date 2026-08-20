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
            Gap: "0:02.100", Interval: "0:04.200");

        Assert.Multiple(() =>
        {
            Assert.That(entry.RaceData, Is.SameAs(raceData));
            Assert.That(entry.Position, Is.EqualTo(2));
            Assert.That(entry.BestLapTime, Is.EqualTo(TimeSpan.FromSeconds(18.2)));
            Assert.That(entry.LastLapTime, Is.EqualTo(TimeSpan.FromSeconds(19.1)));
            Assert.That(entry.Gap, Is.EqualTo("0:02.100"));
            Assert.That(entry.Interval, Is.EqualTo("0:04.200"));
        });
    }
}
