using System;

namespace TrackGenius.Model;

/// <summary>
/// One racer's computed standing. Gap/Interval are pre-formatted display strings.
/// </summary>
public sealed record RaceStandingsEntry(
    RaceData RaceData,
    int Position,
    TimeSpan BestLapTime,
    TimeSpan LastLapTime,
    string Gap,
    string Interval);
