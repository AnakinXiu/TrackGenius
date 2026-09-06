using System;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech.Audio;

/// <summary>Plays WAV AudioClips from memory. Blocking play is offloaded to the thread pool — never call from the UI thread path directly.</summary>
public sealed class SoundPlayerAudioPlayer : IAudioPlayer
{
    private readonly object _gate = new();
    private SoundPlayer? _current;

    public Task PlayAsync(AudioClip clip, CancellationToken cancellationToken = default)
    {
        if (clip is null)
            throw new ArgumentNullException(nameof(clip));
        if (!string.Equals(clip.Format, "wav", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Only WAV clips are supported (got '{clip.Format}').");

        return Task.Run(() =>
        {
            var player = new SoundPlayer(new MemoryStream(clip.Data));
            lock (_gate)
                _current = player;
            try
            {
                player.PlaySync();
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_current, player))
                        _current = null;
                }
                player.Dispose();
            }
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _current?.Stop();
            _current = null;
        }
        return Task.CompletedTask;
    }
}
