using System.Threading;
using System.Threading.Tasks;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech.Fakes;

/// <summary>Counts plays/stops; touches no audio device.</summary>
public sealed class FakeAudioPlayer : IAudioPlayer
{
    private int _plays;
    private int _stops;

    public int PlayCount => _plays;

    public int StopCount => _stops;

    public Task PlayAsync(AudioClip clip, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _plays);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _stops);
        return Task.CompletedTask;
    }
}
