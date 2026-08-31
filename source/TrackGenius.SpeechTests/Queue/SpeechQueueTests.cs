using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests.Queue;

[TestFixture]
public class SpeechQueueTests
{
    private static SpeechMessage Message(string text, AnnouncementPriority priority)
        => new(Guid.NewGuid(), text, null, priority, text, DateTimeOffset.Now,
            DateTimeOffset.Now + TimeSpan.FromSeconds(30), false, "en-US", null);

    [Test]
    public async Task GivenMixedPriorities_WhenDequeued_ThenCriticalFirstThenFifo()
    {
        var queue = new SpeechQueue(20, NullLogger<SpeechQueue>.Instance);
        await queue.EnqueueAsync(Message("normal-a", AnnouncementPriority.Normal));
        await queue.EnqueueAsync(Message("critical-a", AnnouncementPriority.Critical));
        await queue.EnqueueAsync(Message("normal-b", AnnouncementPriority.Normal));

        Assert.That((await queue.DequeueAsync(CancellationToken.None))!.Text, Is.EqualTo("critical-a"));
        Assert.That((await queue.DequeueAsync(CancellationToken.None))!.Text, Is.EqualTo("normal-a"));
        Assert.That((await queue.DequeueAsync(CancellationToken.None))!.Text, Is.EqualTo("normal-b"));
    }

    [Test]
    public async Task GivenCapacityFull_WhenEnqueueingLowPriority_ThenOldestNormalDropped()
    {
        var queue = new SpeechQueue(2, NullLogger<SpeechQueue>.Instance);
        await queue.EnqueueAsync(Message("normal-1", AnnouncementPriority.Normal));
        await queue.EnqueueAsync(Message("high-1", AnnouncementPriority.High));
        await queue.EnqueueAsync(Message("normal-2", AnnouncementPriority.Normal));

        Assert.That(queue.Count, Is.EqualTo(2));
        var texts = new[] { await queue.DequeueAsync(CancellationToken.None), await queue.DequeueAsync(CancellationToken.None) };
        Assert.That(texts.Select(m => m!.Text), Is.EqualTo(new[] { "high-1", "normal-2" }));
    }

    [Test]
    public async Task GivenCancelledMessage_WhenDequeued_ThenSkipped()
    {
        var queue = new SpeechQueue(20, NullLogger<SpeechQueue>.Instance);
        await queue.EnqueueAsync(Message("doomed", AnnouncementPriority.Normal));
        await queue.EnqueueAsync(Message("kept", AnnouncementPriority.Normal));

        queue.TryCancel(queue.PendingId("doomed"));
        Assert.That((await queue.DequeueAsync(CancellationToken.None))!.Text, Is.EqualTo("kept"));
    }

    [Test]
    public async Task GivenExpiredMessage_WhenDequeued_ThenSkippedAndNextReturned()
    {
        var queue = new SpeechQueue(20, NullLogger<SpeechQueue>.Instance);
        var expired = new SpeechMessage(Guid.NewGuid(), "expired", null, AnnouncementPriority.Normal, "expired",
            DateTimeOffset.Now - TimeSpan.FromSeconds(60), DateTimeOffset.Now - TimeSpan.FromSeconds(30), false, "en-US", null);
        await queue.EnqueueAsync(expired);
        await queue.EnqueueAsync(Message("fresh", AnnouncementPriority.Normal));

        Assert.That((await queue.DequeueAsync(CancellationToken.None))!.Text, Is.EqualTo("fresh"));
    }

    [Test]
    public async Task GivenClearHigh_WhenCleared_ThenOnlyCriticalAndHighRemain()
    {
        var queue = new SpeechQueue(20, NullLogger<SpeechQueue>.Instance);
        await queue.EnqueueAsync(Message("n", AnnouncementPriority.Normal));
        await queue.EnqueueAsync(Message("i", AnnouncementPriority.Important));
        await queue.EnqueueAsync(Message("h", AnnouncementPriority.High));
        await queue.EnqueueAsync(Message("c", AnnouncementPriority.Critical));

        queue.Clear(AnnouncementPriority.High);

        Assert.That(queue.Count, Is.EqualTo(2));   // h + c remain
    }
}
