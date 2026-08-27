namespace TrackGenius.Model;

/// <summary>
/// How race standings are ordered. LapsThenRaceTime: most laps first;
/// among equal lap counts, less race time (clock time of the last accepted detection) first.
/// </summary>
public enum RaceOrderRule
{
    LapsThenRaceTime,
    FastestLap,
    Top3Consecutive,
    Custom
}
