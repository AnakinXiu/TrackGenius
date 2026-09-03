using System;
using System.Collections.Generic;
using System.Threading;
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

    private readonly object _workerGate = new();
    private CancellationTokenSource? _workerCts;
    private Task? _workerLoop;
    private volatile ITtsEngine _activeTtsEngine;   // swap target of SwapBackend; starts as the ctor value
    private volatile IAudioPlayer _activeAudioPlayer;

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
        _activeTtsEngine = ttsEngine;
        _activeAudioPlayer = audioPlayer;
    }

    public void Attach(RaceEngine engine)
    {
        engine.RaceDataChanged += HandleStandings;
        if (Enabled)
            _ = StartWorkerAsync();
    }

    public void Detach(RaceEngine engine)
    {
        _ = StopWorkerAsync();
        engine.RaceDataChanged -= HandleStandings;
    }

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

    /// <summary>Arms the background worker loop; idempotent. Attach starts it when Enabled.</summary>
    internal async Task StartWorkerAsync()
    {
        lock (_workerGate)
        {
            if (_workerLoop is not null)
                return;
            _workerCts = new CancellationTokenSource();
            var token = _workerCts.Token;
            _workerLoop = Task.Run(() => WorkerLoopAsync(token));
        }
        // Give the loop a beat to arm; it polls every 200ms so no startup signal is needed.
        await Task.CompletedTask;
    }

    /// <summary>Cancels the worker and awaits loop exit; idempotent and no-throw on cancellation.</summary>
    internal async Task StopWorkerAsync()
    {
        Task? loop;
        lock (_workerGate)
        {
            _workerCts?.Cancel();
            loop = _workerLoop;
            _workerLoop = null;
            // The CTS is intentionally not disposed: the loop may still be racing a Task.Delay(token)
            // against its token, and a dispose here surfaces as ObjectDisposedException noise.
        }
        if (loop is not null)
        {
            try { await loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// Hot-swaps the synthesis/playback backend; takes effect for subsequent messages.
    /// When <paramref name="enabled"/> is true the worker loop is also armed (idempotent), so a
    /// backend swapped in before any race attached still speaks once messages are queued.
    /// </summary>
    public void SwapBackend(ITtsEngine ttsEngine, IAudioPlayer audioPlayer, bool enabled)
    {
        _activeTtsEngine = ttsEngine ?? throw new ArgumentNullException(nameof(ttsEngine));
        _activeAudioPlayer = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));
        Enabled = enabled;
        if (enabled)
            _ = StartWorkerAsync();
    }

    private async Task WorkerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var message = await _queue.DequeueAsync(token).ConfigureAwait(false);
                if (message is null)
                {
                    await Task.Delay(200, token).ConfigureAwait(false);
                    continue;
                }

                var clip = await _activeTtsEngine.SynthesizeAsync(
                    new SpeechContent(message.Text, message.Ssml, message.Language, message.VoiceId),
                    token).ConfigureAwait(false);
                await _activeAudioPlayer.PlayAsync(clip, token).ConfigureAwait(false);
                _logger.LogInformation("SpeechMessageSpoken MessageId={MessageId} Category={Category}", message.Id, message.Category);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // One bad message must never kill the loop.
                _logger.LogError(ex, "SpeechMessageFailed Stage={Stage}", "WorkerLoop");
            }
        }
    }

    public void Dispose()
    {
        StopWorkerAsync().GetAwaiter().GetResult();
    }
}
