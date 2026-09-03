using System;
using NUnit.Framework;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests.Policy;

[TestFixture]
public class AnnouncementPolicyTests
{
    private static SpeechMessage Message(string category, AnnouncementPriority priority, DateTimeOffset createdAt, DateTimeOffset? expiresAt = null)
        => new(Guid.NewGuid(), "text", null, priority, category, createdAt, expiresAt, false, "en-US", null);

    [Test]
    public void GivenExpiredMessage_WhenEvaluated_ThenRejectedAsExpired()
    {
        var policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), 20);
        var now = DateTimeOffset.Now;
        var message = Message("FastestLap", AnnouncementPriority.High, now - TimeSpan.FromSeconds(20), now - TimeSpan.FromSeconds(5));

        var decision = policy.Evaluate(message, new PolicyContext(0, now));

        Assert.That(decision.Accept, Is.False);
        Assert.That(decision.Reason, Is.EqualTo("Expired"));
    }

    [Test]
    public void GivenSameCategoryWithinWindow_WhenEvaluated_ThenRejectedAsDuplicate()
    {
        var policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), 20);
        var now = DateTimeOffset.Now;
        policy.Evaluate(Message("LeaderChanged", AnnouncementPriority.High, now), new PolicyContext(0, now));

        var decision = policy.Evaluate(Message("LeaderChanged", AnnouncementPriority.High, now + TimeSpan.FromSeconds(1)), new PolicyContext(0, now + TimeSpan.FromSeconds(1)));

        Assert.Multiple(() =>
        {
            Assert.That(decision.Accept, Is.False);
            Assert.That(decision.Reason, Is.EqualTo("Duplicate"));
        });
    }

    [Test]
    public void GivenSameCategoryAfterWindow_WhenEvaluated_ThenAccepted()
    {
        var policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), 20);
        var now = DateTimeOffset.Now;
        policy.Evaluate(Message("LeaderChanged", AnnouncementPriority.High, now), new PolicyContext(0, now));

        var decision = policy.Evaluate(Message("LeaderChanged", AnnouncementPriority.High, now + TimeSpan.FromSeconds(4)), new PolicyContext(0, now + TimeSpan.FromSeconds(4)));

        Assert.That(decision.Accept, Is.True);
    }

    [Test]
    public void GivenFullQueueAndNormalPriority_WhenEvaluated_ThenRejectedAsQueueFull()
    {
        var policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), maxQueueLength: 2);
        var now = DateTimeOffset.Now;

        var decision = policy.Evaluate(Message("LapCommentary", AnnouncementPriority.Normal, now), new PolicyContext(2, now));

        Assert.Multiple(() =>
        {
            Assert.That(decision.Accept, Is.False);
            Assert.That(decision.Reason, Is.EqualTo("QueueFull"));
        });
    }

    [Test]
    public void GivenFullQueueAndCriticalPriority_WhenEvaluated_ThenAccepted()
    {
        var policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), maxQueueLength: 2);
        var now = DateTimeOffset.Now;

        var decision = policy.Evaluate(Message("RaceStarted", AnnouncementPriority.Critical, now), new PolicyContext(2, now));

        Assert.That(decision.Accept, Is.True);
    }
}
