using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrackGenius.Model;

/// <summary>
/// Default standings rule: most laps first; among equal lap counts, less race time first.
/// Gap/Interval are pre-formatted display strings (invariant culture).
/// </summary>
public sealed class LapsRaceTimeOrderCalculator : IRaceOrderCalculator
{
    private const string TimeFormat = @"m\:ss\.fff";

    public RaceOrderRule Rule => RaceOrderRule.LapsThenRaceTime;

    public IReadOnlyList<RaceStandingsEntry> Calculate(ICollection<RaceData> raceDataCollection)
    {
        if (raceDataCollection == null)
            throw new ArgumentNullException(nameof(raceDataCollection));

        var ordered = raceDataCollection
            .OrderByDescending(racer => racer.LapsCount)
            .ThenBy(racer => racer.RacedTime)
            .ToList();

        var entries = new List<RaceStandingsEntry>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            var racer = ordered[index];
            entries.Add(new RaceStandingsEntry(
                RaceData: racer,
                Position: index + 1,
                BestLapTime: BestLap(racer),
                LastLapTime: LastLap(racer),
                Gap: DescribeDifference(index == 0 ? null : ordered[0], racer),
                Interval: DescribeDifference(index == 0 ? null : ordered[index - 1], racer)));
        }

        return entries;
    }

    // Equal lap counts -> positive time difference (current is behind);
    // differing counts -> lap difference; no reference (leader) -> "-".
    private static string DescribeDifference(RaceData reference, RaceData current)
    {
        if (reference == null)
            return "-";

        var lapDifference = reference.LapsCount - current.LapsCount;
        if (lapDifference != 0)
            return $"+{Math.Abs(lapDifference)} {(Math.Abs(lapDifference) == 1 ? "Lap" : "Laps")}";

        return (current.RacedTime - reference.RacedTime).ToString(TimeFormat, CultureInfo.InvariantCulture);
    }

    private static TimeSpan BestLap(RaceData raceData)
        => raceData.LapRecords.Count == 0
            ? TimeSpan.Zero
            : raceData.LapRecords.Min(lap => lap.LapTime);

    private static TimeSpan LastLap(RaceData raceData)
        => raceData.LapRecords.Count == 0
            ? TimeSpan.Zero
            : raceData.LapRecords[^1].LapTime;
}
