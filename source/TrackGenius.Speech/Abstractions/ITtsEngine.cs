using System.Threading;
using System.Threading.Tasks;

namespace TrackGenius.Speech.Abstractions;

public interface ITtsEngine
{
    Task<AudioClip> SynthesizeAsync(SpeechContent content, CancellationToken cancellationToken = default);
}
