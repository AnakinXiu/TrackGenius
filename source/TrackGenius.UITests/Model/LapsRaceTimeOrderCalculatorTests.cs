using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrackGenius.Model;

namespace TrackGenius.UITests.Model;

[TestFixture]
public class LapsRaceTimeOrderCalculatorTests
{
    private readonly LapsRaceTimeOrderCalculator _calculator = new();

    /// <summary>Whole-second lap timestamps keep clear of the GetLastDetectedTimeSpan quirk.</summary>
    private static RaceData Racer(string transponder, params int[] lapMilliseconds)
    {
        var driver = AnonymousDriverCreator.CreateAnonymous(transponder);
        var raceData = new RaceData(driver, driver.Cars.First());
        foreach (var milliseconds in lapMilliseconds)
            raceData.RecordDetection(TimeSpan.FromMilliseconds(milliseconds));
        return raceData;
    }

    private IReadOnlyList<RaceStandingsEntry> Calculate(params RaceData[] racers)
        => _calculator.Calculate(racers);

    [Test]
    public void GivenEmptyCollection_WhenCalculate_ThenEmptyList()
    {
        var result = _calculator.Calculate(new List<RaceData>());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GivenSingleRacerWithOneLap_WhenCalculate_ThenLeaderEntryWithDashesAndTimes()
    {
        var racer = Racer("100", 61_000);

        var result = Calculate(racer);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].RaceData, Is.SameAs(racer));
            Assert.That(result[0].Position, Is.EqualTo(1));
            Assert.That(result[0].Gap, Is.EqualTo("-"));
            Assert.That(result[0].Interval, Is.EqualTo("-"));
            Assert.That(result[0].BestLapTime, Is.EqualTo(TimeSpan.FromMilliseconds(61_000)));
            Assert.That(result[0].LastLapTime, Is.EqualTo(TimeSpan.FromMilliseconds(61_000)));
        });
    }

    [Test]
    public void GivenTwoRacersEqualLaps_WhenCalculate_ThenFasterRacerFirstWithTimeGap()
    {
        var fast = Racer("100", 60_000);
        var slow = Racer("200", 62_000);

        var result = Calculate(slow, fast);   // insertion order deliberately reversed

        Assert.Multiple(() =>
        {
            Assert.That(result[0].RaceData, Is.SameAs(fast));
            Assert.That(result[1].RaceData, Is.SameAs(slow));
            Assert.That(result[0].Position, Is.EqualTo(1));
            Assert.That(result[1].Position, Is.EqualTo(2));
            Assert.That(result[0].Gap, Is.EqualTo("-"));
            Assert.That(result[0].Interval, Is.EqualTo("-"));
            Assert.That(result[1].Gap, Is.EqualTo("0:02.000"));
            Assert.That(result[1].Interval, Is.EqualTo("0:02.000"));
        });
    }

    [Test]
    public void GivenRacerWithMoreLaps_WhenCalculate_ThenLeadsDespiteSlowerTimeAndLapDifferenceShown()
    {
        var lapped = Racer("100", 60_000, 115_000);   // 2 laps, race time 115s
        var quick = Racer("200", 62_000);             // 1 lap, race time 62s

        var result = Calculate(quick, lapped);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].RaceData, Is.SameAs(lapped));
            Assert.That(result[1].RaceData, Is.SameAs(quick));
            Assert.That(result[1].Gap, Is.EqualTo("+1 Lap"));
            Assert.That(result[1].Interval, Is.EqualTo("+1 Lap"));
        });
    }

    [Test]
    public void GivenThreeLapDifference_WhenCalculate_ThenPluralLapText()
    {
        var leader = Racer("100", 60_000, 120_000, 180_000, 240_000);   // 4 laps
        var trailing = Racer("200", 62_000);                            // 1 lap

        var result = Calculate(trailing, leader);

        Assert.Multiple(() =>
        {
            Assert.That(result[1].Gap, Is.EqualTo("+3 Laps"));
            Assert.That(result[1].Interval, Is.EqualTo("+3 Laps"));
        });
    }

    [Test]
    public void GivenZeroLapRacers_WhenCalculate_ThenLapsToLappedRacersAndZeroTimeToPeers()
    {
        var leader = Racer("100", 60_000);   // 1 lap
        var peer1 = Racer("200");            // 0 laps
        var peer2 = Racer("300");            // 0 laps

        var result = Calculate(leader, peer1, peer2);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].RaceData, Is.SameAs(leader));
            Assert.That(result[1].RaceData, Is.SameAs(peer1));   // zero-lap racers keep insertion order
            Assert.That(result[2].RaceData, Is.SameAs(peer2));
            Assert.That(result[1].Gap, Is.EqualTo("+1 Lap"));        // to leader (1 lap)
            Assert.That(result[1].Interval, Is.EqualTo("+1 Lap"));   // to car ahead (the leader)
            Assert.That(result[2].Gap, Is.EqualTo("+1 Lap"));   // to leader (1 lap)
            Assert.That(result[2].Interval, Is.EqualTo("0:00.000"));   // to car ahead (peer1, equal 0 laps)
        });
    }

    [Test]
    public void GivenThreeRacersDifferentTimes_WhenCalculate_ThenGapIsToLeaderAndIntervalIsToCarAhead()
    {
        // Leader 60s, P2 62s, P3 65s — one lap each.
        // Gap (to leader):     P2 = 2s, P3 = 5s.
        // Interval (to ahead): P2 = 2s, P3 = 3s.
        var leader = Racer("100", 60_000);
        var second = Racer("200", 62_000);
        var third = Racer("300", 65_000);

        var result = Calculate(third, second, leader);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Gap, Is.EqualTo("-"));
            Assert.That(result[0].Interval, Is.EqualTo("-"));
            Assert.That(result[1].Gap, Is.EqualTo("0:02.000"));
            Assert.That(result[1].Interval, Is.EqualTo("0:02.000"));
            Assert.That(result[2].Gap, Is.EqualTo("0:05.000"));   // vs leader, not vs P2
            Assert.That(result[2].Interval, Is.EqualTo("0:03.000"));   // vs P2, not vs leader
        });
    }

    [Test]
    public void GivenZeroLapRacers_WhenCalculate_ThenGapToLeaderAndIntervalToCarAhead()
    {
        // Leader 1 lap; two zero-lap peers.
        // Gap (to leader): both "+1 Lap".
        // Interval: P2 vs leader (ahead is the leader) = "+1 Lap"; P3 vs P2 (equal 0 laps) = "0:00.000".
        var leader = Racer("100", 60_000);
        var peer1 = Racer("200");
        var peer2 = Racer("300");

        var result = Calculate(leader, peer1, peer2);

        Assert.Multiple(() =>
        {
            Assert.That(result[1].Gap, Is.EqualTo("+1 Lap"));
            Assert.That(result[1].Interval, Is.EqualTo("+1 Lap"));   // ahead of P2 is the leader
            Assert.That(result[2].Gap, Is.EqualTo("+1 Lap"));   // to leader
            Assert.That(result[2].Interval, Is.EqualTo("0:00.000"));   // to P2, equal laps
        });
    }

    [Test]
    public void GivenTwoLapRacer_WhenCalculate_ThenBestAndLastLapTimesFromRecords()
    {
        var racer = Racer("100", 60_000, 122_000);   // lap1 = 60s, lap2 = 62s

        var result = Calculate(racer);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].BestLapTime, Is.EqualTo(TimeSpan.FromMilliseconds(60_000)));
            Assert.That(result[0].LastLapTime, Is.EqualTo(TimeSpan.FromMilliseconds(62_000)));
        });
    }
}
