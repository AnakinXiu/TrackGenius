using System.Threading;
using System.Threading.Tasks;

namespace TrackGenius.Speech.Abstractions;

public interface IAudioPlayer
{
    Task PlayAsync(AudioClip clip, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
