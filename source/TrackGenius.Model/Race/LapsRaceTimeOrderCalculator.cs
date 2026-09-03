using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrackGenius.Model;

/// <summary>
/// Default standings rule: most laps first; among equal lap counts, less race time first.
/// Gap/Interval and lap-analysis metrics are pre-formatted display strings (invariant culture).
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

        // The race's fastest best lap among drivers who have completed one; null when nobody has a lap yet.
        TimeSpan? raceBestLap = null;
        foreach (var racer in ordered)
        {
            if (racer.LapRecords.Count == 0)
                continue;

            var best = BestLap(racer);
            if (raceBestLap is null || best < raceBestLap)
                raceBestLap = best;
        }

        var entries = new List<RaceStandingsEntry>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            var racer = ordered[index];
            var laps = racer.LapRecords.Select(record => record.LapTime).ToList();
            var bestLap = BestLap(racer);
            entries.Add(new RaceStandingsEntry(
                RaceData: racer,
                Position: index + 1,
                BestLapTime: bestLap,
                LastLapTime: LastLap(racer),
                Gap: DescribeDifference(index == 0 ? null : ordered[0], racer),
                Interval: DescribeDifference(index == 0 ? null : ordered[index - 1], racer),
                Top5Average: FormatTime(LapAnalysisCalculator.TopAverage(laps, 5)),
                Top10Average: FormatTime(LapAnalysisCalculator.TopAverage(laps, 10)),
                Top3Consecutive: FormatTop3(LapAnalysisCalculator.Top3Consecutive(laps)),
                StdDeviation: FormatSigma(LapAnalysisCalculator.StdDeviation(laps)),
                Consistency: FormatConsistency(LapAnalysisCalculator.Consistency(laps)),
                IsRaceBestLap: raceBestLap is not null && bestLap == raceBestLap));
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

    private static string FormatTime(TimeSpan? value)
        => value?.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? "-";

    // "{window sum m:ss.fff} ({average ss.fff})"
    private static string FormatTop3(TimeSpan? windowSum)
        => windowSum is null
            ? "-"
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0} ({1})",
                windowSum.Value.ToString(TimeFormat, CultureInfo.InvariantCulture),
                (windowSum.Value / 3).ToString(@"ss\.fff", CultureInfo.InvariantCulture));

    private static string FormatSigma(double? sigma)
        => sigma?.ToString("0.000", CultureInfo.InvariantCulture) ?? "-";

    // Literal "%" — the "0.0%" format specifier would multiply by 100 on top of the
    // already-percent value LapAnalysisCalculator.Consistency returns.
    private static string FormatConsistency(double? consistency)
        => consistency is null ? "-" : consistency.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
}
