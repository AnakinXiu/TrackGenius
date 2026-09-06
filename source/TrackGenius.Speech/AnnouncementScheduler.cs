using System;
using System.Collections.Generic;
using TrackGenius.Model;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech;

/// <summary>Maps derived facts to announcement intents (1:1 in v1).</summary>
public sealed class AnnouncementScheduler
{
    public IReadOnlyList<AnnouncementIntent> Schedule(
        IReadOnlyList<DerivedFact> facts,
        IReadOnlyList<RaceStandingsEntry> snapshot,
        DateTimeOffset now)
    {
        var intents = new List<AnnouncementIntent>(facts.Count);
        foreach (var fact in facts)
            intents.Add(new AnnouncementIntent(
                fact.Type, fact.Priority, fact.DriverId, fact.DriverDisplayName,
                snapshot, now, fact.ExpiresAfter));
        return intents;
    }
}
