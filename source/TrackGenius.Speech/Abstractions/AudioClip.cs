namespace TrackGenius.Speech.Abstractions;

/// <summary>Synthesized audio; opaque to the pipeline (format names the codec, e.g. "wav").</summary>
public sealed record AudioClip(byte[] Data, string Format);
