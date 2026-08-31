using System;
using System.Collections.Generic;
using TrackGenius.Model;

namespace TrackGenius.Speech.Abstractions;

/// <summary>A worth-saying race fact, before language is applied.</summary>
public sealed record AnnouncementIntent(
    string Type,
    AnnouncementPriority Priority,
    string DriverId,
    string? DriverDisplayName,
    IReadOnlyList<RaceStandingsEntry> Snapshot,
    DateTimeOffset CreatedAt,
    TimeSpan? ExpiresAfter = null);
