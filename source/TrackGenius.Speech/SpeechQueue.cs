using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech;

/// <summary>Priority queue: highest priority first, FIFO within a priority; bounded with oldest-lowest drop.</summary>
public sealed class SpeechQueue : ISpeechQueue
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly ILogger<SpeechQueue> _logger;
    private readonly List<SpeechMessage> _pending = new();
    private readonly HashSet<Guid> _cancelled = new();

    public SpeechQueue(int capacity, ILogger<SpeechQueue> logger)
    {
        _capacity = capacity;
        _logger = logger;
    }

    public int Count
    {
        get { lock (_gate) return _pending.Count; }
    }

    /// <summary>Test/diagnostics view of pending messages in queue order.</summary>
    public IReadOnlyList<SpeechMessage> Pending
    {
        get { lock (_gate) return _pending.ToArray(); }
    }

    public ValueTask EnqueueAsync(SpeechMessage message, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_pending.Count >= _capacity)
            {
                var victim = _pending
                    .Where(m => m.Priority <= AnnouncementPriority.Normal)
                    .OrderBy(m => m.Priority).ThenBy(m => m.CreatedAt).FirstOrDefault();

                if (victim == null && message.Priority <= AnnouncementPriority.Normal)
                {
                    // Queue full of higher-priority messages: the low-priority newcomer is dropped.
                    _logger.LogWarning("SpeechMessageDropped MessageId={MessageId} Reason={Reason}", message.Id, "QueueFullNoLowPriority");
                    return ValueTask.CompletedTask;
                }

                if (victim != null)
                {
                    _pending.Remove(victim);
                    _logger.LogWarning("SpeechMessageDropped MessageId={MessageId} Reason={Reason}", victim.Id, "QueueFull");
                }
            }

            _pending.Add(message);
        }

        _logger.LogInformation("SpeechMessageQueued MessageId={MessageId} Category={Category} Priority={Priority}",
            message.Id, message.Category, message.Priority);
        return ValueTask.CompletedTask;
    }

    public bool TryCancel(Guid messageId)
    {
        lock (_gate)
            return _cancelled.Add(messageId) && _pending.RemoveAll(m => m.Id == messageId) >= 0;
    }

    public void Clear(AnnouncementPriority minimumPriority = AnnouncementPriority.Background)
    {
        lock (_gate)
            _pending.RemoveAll(m => m.Priority < minimumPriority);
    }

    /// <summary>Test helper: find a pending message's id by its text.</summary>
    public Guid PendingId(string text)
    {
        lock (_gate)
            return _pending.Single(m => m.Text == text).Id;
    }

    /// <summary>Next runnable message (highest priority, oldest first), skipping cancelled/expired; null when none.</summary>
    public Task<SpeechMessage?> DequeueAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            while (_pending.Count > 0)
            {
                var next = _pending
                    .OrderByDescending(m => m.Priority)
                    .ThenBy(m => m.CreatedAt)
                    .First();
                _pending.Remove(next);

                if (_cancelled.Remove(next.Id))
                    continue;

                if (next.ExpiresAt is { } expiry && expiry < DateTimeOffset.Now)
                {
                    _logger.LogInformation("SpeechMessageExpired MessageId={MessageId}", next.Id);
                    continue;
                }

                return Task.FromResult<SpeechMessage?>(next);
            }

            return Task.FromResult<SpeechMessage?>(null);
        }
    }
}
