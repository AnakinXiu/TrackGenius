using System;
using System.Collections.Generic;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech;

/// <summary>v1 policy: expiry, per-category dedup window, queue cap (Critical/High bypass the cap).</summary>
public sealed class AnnouncementPolicy : IAnnouncementPolicy
{
    private readonly TimeSpan _dedupWindow;
    private readonly int _maxQueueLength;
    private readonly Dictionary<string, DateTimeOffset> _lastAcceptedPerCategory = new();

    public AnnouncementPolicy(TimeSpan dedupWindow, TimeSpan defaultExpiry, int maxQueueLength)
    {
        _dedupWindow = dedupWindow;
        _maxQueueLength = maxQueueLength;
    }

    public PolicyDecision Evaluate(SpeechMessage message, PolicyContext context)
    {
        if (message.ExpiresAt is { } expiry && expiry < context.Now)
            return new PolicyDecision(false, "Expired");

        if (_lastAcceptedPerCategory.TryGetValue(message.Category, out var lastAccepted)
            && context.Now - lastAccepted < _dedupWindow)
            return new PolicyDecision(false, "Duplicate");

        if (context.QueueLength >= _maxQueueLength && message.Priority <= AnnouncementPriority.Normal)
            return new PolicyDecision(false, "QueueFull");

        _lastAcceptedPerCategory[message.Category] = context.Now;
        return new PolicyDecision(true, null);
    }
}
