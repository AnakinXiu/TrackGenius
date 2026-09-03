using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech.Fakes;

/// <summary>Records synthesized texts; emits silent clips. No audio device.</summary>
public sealed class FakeTtsEngine : ITtsEngine
{
    private readonly object _gate = new();
    private readonly List<string> _texts = new();

    public IReadOnlyList<string> SynthesizedTexts
    {
        get { lock (_gate) return _texts.ToArray(); }
    }

    public Task<AudioClip> SynthesizeAsync(SpeechContent content, CancellationToken cancellationToken = default)
    {
        lock (_gate) _texts.Add(content.Text);
        return Task.FromResult(new AudioClip(Array.Empty<byte>(), "none"));
    }
}
