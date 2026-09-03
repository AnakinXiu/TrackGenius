using System;
using System.Collections.Generic;
using System.Linq;
using TrackGenius.Model;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech;

public sealed record DerivedFact(
    string Type, AnnouncementPriority Priority, string DriverId, string? DriverDisplayName, TimeSpan? ExpiresAfter);

/// <summary>Pure diff of consecutive standings snapshots → announcable facts.</summary>
public static class StandingsDiffer
{
    private static readonly TimeSpan LeaderExpiry = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FastestLapExpiry = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PositionExpiry = TimeSpan.FromSeconds(8);

    public static IReadOnlyList<DerivedFact> Diff(
        IReadOnlyList<RaceStandingsEntry>? previous,
        IReadOnlyList<RaceStandingsEntry> current,
        DateTimeOffset now)
    {
        var facts = new List<DerivedFact>();

        if (previous is null || previous.Count == 0)
        {
            if (current.Count > 0)
                facts.Add(new DerivedFact("RaceStarted", AnnouncementPriority.Critical, string.Empty, null, null));
            return facts;
        }

        var previousLeader = previous.FirstOrDefault(e => e.Position == 1);
        var currentLeader = current.FirstOrDefault(e => e.Position == 1);
        if (previousLeader != null && currentLeader != null && Key(previousLeader) != Key(currentLeader))
            facts.Add(new DerivedFact("LeaderChanged", AnnouncementPriority.High, Key(currentLeader), Name(currentLeader), LeaderExpiry));

        var previousBest = previous.Where(e => e.IsRaceBestLap).Select(e => e.BestLapTime).DefaultIfEmpty().Min();
        var currentBestEntry = current.FirstOrDefault(e => e.IsRaceBestLap);
        if (currentBestEntry != null && currentBestEntry.BestLapTime < previousBest)
            facts.Add(new DerivedFact("FastestLap", AnnouncementPriority.High, Key(currentBestEntry), Name(currentBestEntry), FastestLapExpiry));

        foreach (var gain in GainersInTopThree(previous, current))
            facts.Add(new DerivedFact("PositionChanged", AnnouncementPriority.Important, gain.Key, gain.Name, PositionExpiry));

        return facts;
    }

    private static IEnumerable<(string Key, string? Name)> GainersInTopThree(
        IReadOnlyList<RaceStandingsEntry> previous, IReadOnlyList<RaceStandingsEntry> current)
    {
        var previousPositions = previous.Where(e => e.Position <= 3).ToDictionary(Key);
        foreach (var entry in current.Where(e => e.Position <= 3))
        {
            if (previousPositions.TryGetValue(Key(entry), out var before) && entry.Position < before.Position)
                yield return (Key(entry), Name(entry));
        }
    }

    private static string Key(RaceStandingsEntry entry)
        => SpeechTemplateRenderer.DriverKey(entry);

    private static string? Name(RaceStandingsEntry entry)
        => entry.RaceData.Driver?.DriverName;
}
