using System;

namespace TrackGenius.Speech.Abstractions;

/// <summary>The queueable announcement: enough data for ordering, expiry, dedup, diagnostics.</summary>
public sealed record SpeechMessage(
    Guid Id,
    string Text,
    string? Ssml,
    AnnouncementPriority Priority,
    string Category,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool Interruptible,
    string Language,
    string? VoiceId);
