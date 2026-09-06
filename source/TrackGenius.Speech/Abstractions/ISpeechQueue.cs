using System;
using System.Threading;
using System.Threading.Tasks;

namespace TrackGenius.Speech.Abstractions;

public interface ISpeechQueue
{
    ValueTask EnqueueAsync(SpeechMessage message, CancellationToken cancellationToken = default);

    bool TryCancel(Guid messageId);

    void Clear(AnnouncementPriority minimumPriority = AnnouncementPriority.Background);
}
