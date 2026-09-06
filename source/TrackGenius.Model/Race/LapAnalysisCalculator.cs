using System;
using System.Collections.Generic;
using System.Linq;

namespace TrackGenius.Model;

/// <summary>
/// Pure lap-analysis math over completed lap durations (in lap order).
/// Null return = not enough data for the metric (displayed as "-").
/// </summary>
public static class LapAnalysisCalculator
{
    /// <summary>Mean of the <paramref name="count"/> fastest laps; null when fewer than count laps.</summary>
    public static TimeSpan? TopAverage(IReadOnlyList<TimeSpan> laps, int count)
    {
        if (laps is null || laps.Count < count)
            return null;

        var fastest = laps.OrderBy(lap => lap).Take(count);
        return new TimeSpan((long)fastest.Average(lap => lap.Ticks));
    }

    /// <summary>Fastest 3-consecutive-lap window sum; null when fewer than 3 laps.</summary>
    public static TimeSpan? Top3Consecutive(IReadOnlyList<TimeSpan> laps)
    {
        if (laps is null || laps.Count < 3)
            return null;

        TimeSpan? best = null;
        for (var i = 0; i + 2 < laps.Count; i++)
        {
            var window = laps[i] + laps[i + 1] + laps[i + 2];
            if (best is null || window < best)
                best = window;
        }

        return best;
    }

    /// <summary>Population standard deviation in seconds; null when fewer than 2 laps or zero mean.</summary>
    public static double? StdDeviation(IReadOnlyList<TimeSpan> laps)
    {
        if (laps is null || laps.Count < 2)
            return null;

        var meanSeconds = laps.Average(lap => lap.TotalSeconds);
        if (meanSeconds == 0)
            return null;

        var variance = laps.Average(lap =>
        {
            var delta = lap.TotalSeconds - meanSeconds;
            return delta * delta;
        });

        return Math.Sqrt(variance);
    }

    /// <summary>Consistency as (1 − σ/μ) × 100 percent, clamped at 0; null when σ is null.</summary>
    public static double? Consistency(IReadOnlyList<TimeSpan> laps)
    {
        var sigma = StdDeviation(laps);
        if (sigma is null)
            return null;

        var meanSeconds = laps.Average(lap => lap.TotalSeconds);
        var consistency = (1 - sigma.Value / meanSeconds) * 100;
        return Math.Max(0, consistency);
    }
}
