using System;

namespace TrackGenius.Speech.Abstractions;

public sealed record PolicyDecision(bool Accept, string? Reason);

public sealed record PolicyContext(int QueueLength, DateTimeOffset Now);
