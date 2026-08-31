using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TrackGenius.Core;
using TrackGenius.Model;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech;

/// <summary>Subscribes to standings snapshots and drives the announcement pipeline off-thread.</summary>
public sealed class RaceAnnouncer : IDisposable
{
    private readonly AnnouncementScheduler _scheduler;
    private readonly ISpeechTemplateRenderer _renderer;
    private readonly IAnnouncementPolicy _policy;
    private readonly SpeechQueue _queue;
    private readonly ITtsEngine _ttsEngine;
    private readonly IAudioPlayer _audioPlayer;
    private readonly ILogger<RaceAnnouncer> _logger;
    private readonly object _gate = new();
    private IReadOnlyList<RaceStandingsEntry>? _lastSnapshot;

    public bool Enabled { get; set; }

    public RaceAnnouncer(AnnouncementScheduler scheduler, ISpeechTemplateRenderer renderer,
        IAnnouncementPolicy policy, SpeechQueue queue, ITtsEngine ttsEngine, IAudioPlayer audioPlayer,
        ILogger<RaceAnnouncer> logger)
    {
        _scheduler = scheduler;
        _renderer = renderer;
        _policy = policy;
        _queue = queue;
        _ttsEngine = ttsEngine;
        _audioPlayer = audioPlayer;
        _logger = logger;
    }

    public void Attach(RaceEngine engine) => engine.RaceDataChanged += HandleStandings;

    public void Detach(RaceEngine engine) => engine.RaceDataChanged -= HandleStandings;

    /// <summary>Engine event handler: derive facts, render, gate, queue. Cheap and synchronous in v1.</summary>
    public void HandleStandings(object? sender, IReadOnlyList<RaceStandingsEntry> entries)
    {
        if (!Enabled)
            return;

        try
        {
            IReadOnlyList<RaceStandingsEntry>? previous;
            lock (_gate)
            {
                previous = _lastSnapshot;
                _lastSnapshot = entries;
            }

            var now = DateTimeOffset.Now;
            var facts = StandingsDiffer.Diff(previous, entries, now);
            if (facts.Count == 0)
                return;

            foreach (var intent in _scheduler.Schedule(facts, entries, now))
            {
                var content = _renderer.Render(intent);
                var message = new SpeechMessage(
                    Guid.NewGuid(), content.Text, content.Ssml, intent.Priority, intent.Type,
                    now,
                    intent.ExpiresAfter is { } expiry ? now + expiry : now + TimeSpan.FromSeconds(15),
                    Interruptible: false, content.Language, content.VoiceId);

                var decision = _policy.Evaluate(message, new PolicyContext(_queue.Count, now));
                if (!decision.Accept)
                {
                    _logger.LogInformation("SpeechMessageRejected Category={Category} Reason={Reason}", message.Category, decision.Reason);
                    continue;
                }

                _queue.EnqueueAsync(message).AsTask().GetAwaiter().GetResult();
                _logger.LogInformation("SpeechMessageAccepted MessageId={MessageId} Category={Category}", message.Id, message.Category);
            }
        }
        catch (Exception ex)
        {
            // Speech must never break the timing pipeline.
            _logger.LogError(ex, "SpeechPipelineFailed Stage={Stage}", "HandleStandings");
        }
    }

    /// <summary>Pumps the queue through TTS + player until empty (tests drive it directly; a worker will own it later).</summary>
    public async Task DrainForTestAsync()
    {
        while (true)
        {
            var message = await _queue.DequeueAsync(default);
            if (message is null)
                return;

            var clip = await _ttsEngine.SynthesizeAsync(new SpeechContent(message.Text, message.Ssml, message.Language, message.VoiceId));
            await _audioPlayer.PlayAsync(clip);
            _logger.LogInformation("SpeechMessageSpoken MessageId={MessageId} Category={Category}", message.Id, message.Category);
        }
    }

    public void Dispose()
    {
        // v1: nothing background to stop; the queue drains explicitly. Reserved for a future worker.
    }
}
