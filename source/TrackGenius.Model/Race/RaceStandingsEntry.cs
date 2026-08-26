using System;

namespace TrackGenius.Model;

/// <summary>
/// One racer's computed standing. Gap/Interval and the lap-analysis metrics
/// are pre-formatted display strings ("-" when not enough data).
/// </summary>
public sealed record RaceStandingsEntry(
    RaceData RaceData,
    int Position,
    TimeSpan BestLapTime,
    TimeSpan LastLapTime,
    string Gap,
    string Interval,
    string Top5Average,
    string Top10Average,
    string Top3Consecutive,
    string StdDeviation,
    string Consistency);
