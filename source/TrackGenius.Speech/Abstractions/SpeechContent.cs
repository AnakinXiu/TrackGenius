namespace TrackGenius.Speech.Abstractions;

/// <summary>Rendered announcement text; Ssml optional so any backend can consume it.</summary>
public sealed record SpeechContent(string Text, string? Ssml, string Language, string? VoiceId);
