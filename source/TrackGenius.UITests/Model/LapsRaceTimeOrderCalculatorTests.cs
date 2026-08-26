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

    [Test]
    public void GivenConstantLapPace_WhenDetectionsRecorded_ThenEveryLapTimeEqualsThePace()
    {
        // Crossings every 20s: every lap time must be exactly 20s at any lap count.
        var raceData = new RaceData(AnonymousDriverCreator.CreateAnonymous("100"),
            AnonymousDriverCreator.CreateAnonymous("100").Cars.First());
        for (var lap = 1; lap <= 6; lap++)
            raceData.RecordDetection(TimeSpan.FromSeconds(20 * lap));

        Assert.That(raceData.LapRecords.Select(l => l.LapTime),
            Is.All.EqualTo(TimeSpan.FromSeconds(20)));
    }

    [Test]
    public void GivenVaryingLapPace_WhenDetectionsRecorded_ThenLapTimesAreCrossingDeltas()
    {
        // Crossings at 20s, 45s, 70s, 75s → laps 20s, 25s, 25s, 5s.
        var raceData = new RaceData(AnonymousDriverCreator.CreateAnonymous("100"),
            AnonymousDriverCreator.CreateAnonymous("100").Cars.First());
        foreach (var crossing in new[] { 20, 45, 70, 75 })
            raceData.RecordDetection(TimeSpan.FromSeconds(crossing));

        var expected = new[] { 20, 25, 25, 5 }.Select(s => TimeSpan.FromSeconds(s)).ToList();
        Assert.That(raceData.LapRecords.Select(l => l.LapTime).ToList(), Is.EqualTo(expected));
    }

    [Test]
    public void GivenRecordedDetections_WhenGetLastDetectedTimeSpanCalled_ThenReturnsAbsoluteTimeOfLastDetection()
    {
        // Crossings at 20s, 45s: the last detection happened at absolute 45s.
        var raceData = new RaceData(AnonymousDriverCreator.CreateAnonymous("100"),
            AnonymousDriverCreator.CreateAnonymous("100").Cars.First());
        raceData.RecordDetection(TimeSpan.FromSeconds(20));
        raceData.RecordDetection(TimeSpan.FromSeconds(45));

        Assert.That(raceData.GetLastDetectedTimeSpan(), Is.EqualTo(TimeSpan.FromSeconds(45)));
    }

    [Test]
    public void GivenEnoughLaps_WhenCalculate_ThenAnalysisFieldsFormatted()
    {
        // Laps 20.0..20.9s (cumulative crossings, whole-millisecond ticks):
        // fastest 5 = 20.0..20.4 -> avg 20.2; all 10 -> avg 20.45;
        // 3-lap windows grow strictly, best = laps 1-3 = 60.3s (avg 20.1);
        // mu = 20.45, population sigma of the 0.1..0.9 spread; consistency per formula.
        var raceData = new RaceData(AnonymousDriverCreator.CreateAnonymous("100"),
            AnonymousDriverCreator.CreateAnonymous("100").Cars.First());
        var crossing = TimeSpan.Zero;
        for (var tenth = 0; tenth < 10; tenth++)
        {
            crossing += TimeSpan.FromMilliseconds(20000 + tenth * 100);
            raceData.RecordDetection(crossing);
        }

        var entry = _calculator.Calculate(new List<RaceData> { raceData })[0];

        Assert.Multiple(() =>
        {
            Assert.That(entry.Top5Average, Is.EqualTo("0:20.200"));
            Assert.That(entry.Top10Average, Is.EqualTo("0:20.450"));
            Assert.That(entry.Top3Consecutive, Is.EqualTo("1:00.300 (20.100)"));
            Assert.That(entry.StdDeviation, Is.EqualTo("0.287"));
            Assert.That(entry.Consistency, Is.EqualTo("98.6"));
        });
    }

    [Test]
    public void GivenTwoLaps_WhenCalculate_ThenTopNAveragesDashButSigmaPresent()
    {
        // Laps 20.0s and 20.4s (crossings at 20.0s / 40.4s).
        var raceData = new RaceData(AnonymousDriverCreator.CreateAnonymous("100"),
            AnonymousDriverCreator.CreateAnonymous("100").Cars.First());
        raceData.RecordDetection(TimeSpan.FromMilliseconds(20000));
        raceData.RecordDetection(TimeSpan.FromMilliseconds(40400));

        var entry = _calculator.Calculate(new List<RaceData> { raceData })[0];

        Assert.Multiple(() =>
        {
            Assert.That(entry.Top5Average, Is.EqualTo("-"));
            Assert.That(entry.Top10Average, Is.EqualTo("-"));
            Assert.That(entry.Top3Consecutive, Is.EqualTo("-"));
            Assert.That(entry.StdDeviation, Is.EqualTo("0.200"));
            Assert.That(entry.Consistency, Is.EqualTo("99.0"));
        });
    }

    [Test]
    public void GivenSingleLap_WhenCalculate_ThenAllAnalysisFieldsDash()
    {
        var raceData = new RaceData(AnonymousDriverCreator.CreateAnonymous("100"),
            AnonymousDriverCreator.CreateAnonymous("100").Cars.First());
        raceData.RecordDetection(TimeSpan.FromMilliseconds(20000));

        var entry = _calculator.Calculate(new List<RaceData> { raceData })[0];

        Assert.Multiple(() =>
        {
            Assert.That(entry.Top5Average, Is.EqualTo("-"));
            Assert.That(entry.Top10Average, Is.EqualTo("-"));
            Assert.That(entry.Top3Consecutive, Is.EqualTo("-"));
            Assert.That(entry.StdDeviation, Is.EqualTo("-"));
            Assert.That(entry.Consistency, Is.EqualTo("-"));
        });
    }
}
