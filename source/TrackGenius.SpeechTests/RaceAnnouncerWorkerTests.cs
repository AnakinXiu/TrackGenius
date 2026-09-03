using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Model;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests;

[TestFixture]
public class RaceAnnouncerWorkerTests
{
    /// <summary>TTS fake that signals when a synthesis happens and records texts.</summary>
    private sealed class SignalingTts : ITtsEngine
    {
        private readonly List<string> _texts = new();
        public TaskCompletionSource<bool> FirstSynthesis { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<string> Texts { get { lock (_texts) return _texts.ToArray(); } }

        public Task<AudioClip> SynthesizeAsync(SpeechContent content, CancellationToken ct = default)
        {
            lock (_texts) _texts.Add(content.Text);
            FirstSynthesis.TrySetResult(true);
            return Task.FromResult(new AudioClip(Array.Empty<byte>(), "wav"));
        }
    }

    private AnnouncementScheduler _scheduler = null!;
    private AnnouncementPolicy _policy = null!;
    private SpeechQueue _queue = null!;
    private SignalingTts _tts = null!;
    private RaceAnnouncer _announcer = null!;

    [SetUp]
    public void SetUp()
    {
        _scheduler = new AnnouncementScheduler();
        _policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), 20);
        _queue = new SpeechQueue(20, NullLogger<SpeechQueue>.Instance);
        _tts = new SignalingTts();
        _announcer = new RaceAnnouncer(_scheduler, new SpeechTemplateRenderer(), _policy, _queue,
            _tts, new TrackGenius.Speech.Fakes.FakeAudioPlayer(), NullLogger<RaceAnnouncer>.Instance)
        {
            Enabled = true,
        };
    }

    [TearDown]
    public async Task TearDown()
    {
        await _announcer.StopWorkerAsync();
        _announcer.Dispose();
    }

    [Test]
    public async Task GivenEnqueuedMessageAndStartedWorker_WhenDrained_ThenSynthesizedOnce()
    {
        await _announcer.StartWorkerAsync();
        _announcer.HandleStandings(this,
            new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true) });   // "Race started."

        var synthesized = await Task.WhenAny(_tts.FirstSynthesis.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await _announcer.StopWorkerAsync();

        Assert.That(synthesized, Is.SameAs(_tts.FirstSynthesis.Task), "worker did not synthesize within 5s");
        Assert.That(_tts.Texts, Is.EqualTo(new[] { "Race started." }));
    }

    [Test]
    public async Task GivenFailingSynthesis_WhenWorkerProcesses_ThenLoopSurvivesForNextMessage()
    {
        var failures = 0;
        var tts = new SignalingTts();
        var failingThenWorking = new DelegateTts(content =>
        {
            var attempt = Interlocked.Increment(ref failures);
            if (attempt == 1)
                throw new InvalidOperationException("piper crashed");
            tts.SynthesizeAsync(content).Wait();   // record + signal via inner fake
            return Task.FromResult(new AudioClip(Array.Empty<byte>(), "wav"));
        });
        var announcer = new RaceAnnouncer(_scheduler, new SpeechTemplateRenderer(), _policy, _queue,
            failingThenWorking, new TrackGenius.Speech.Fakes.FakeAudioPlayer(), NullLogger<RaceAnnouncer>.Instance)
        {
            Enabled = true,
        };

        await announcer.StartWorkerAsync();
        announcer.HandleStandings(this, new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true) });
        announcer.HandleStandings(this, new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 19.0, true) });   // fastest lap: second message

        var synthesized = await Task.WhenAny(tts.FirstSynthesis.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await announcer.StopWorkerAsync();
        announcer.Dispose();

        Assert.That(synthesized, Is.SameAs(tts.FirstSynthesis.Task), "worker died on first failure instead of continuing");
    }

    [Test]
    public async Task GivenDisabledAnnouncerWithQueuedMessage_WhenBackendSwappedEnabled_ThenWorkerSpeaks()
    {
        // The startup composition path: announcer built disabled (worker never armed),
        // a message may already be queued, then SwapBackend(enabled: true) must arm
        // the worker so the message is spoken without any Attach having run.
        var disabledAnnouncer = new RaceAnnouncer(_scheduler, new SpeechTemplateRenderer(), _policy, _queue,
            new TrackGenius.Speech.Fakes.FakeTtsEngine(), new TrackGenius.Speech.Fakes.FakeAudioPlayer(),
            NullLogger<RaceAnnouncer>.Instance)
        {
            Enabled = false,
        };
        try
        {
            // While disabled the handler drops everything (early return), so the
            // pre-swap message goes straight onto the queue — the state a race that
            // started during the bootstrap download is in when the swap lands.
            await _queue.EnqueueAsync(new SpeechMessage(Guid.NewGuid(), "Race started.", null,
                AnnouncementPriority.Critical, "RaceStarted", DateTimeOffset.Now,
                DateTimeOffset.Now + TimeSpan.FromSeconds(30), false, "en-US", null));

            disabledAnnouncer.SwapBackend(_tts, new TrackGenius.Speech.Fakes.FakeAudioPlayer(), enabled: true);

            var synthesized = await Task.WhenAny(_tts.FirstSynthesis.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.That(synthesized, Is.SameAs(_tts.FirstSynthesis.Task), "SwapBackend(enabled) did not arm the worker");
            Assert.That(_tts.Texts, Is.EqualTo(new[] { "Race started." }));
        }
        finally
        {
            await disabledAnnouncer.StopWorkerAsync();
            disabledAnnouncer.Dispose();
        }
    }

    private sealed class DelegateTts : ITtsEngine
    {
        private readonly Func<SpeechContent, Task<AudioClip>> _synthesize;
        public DelegateTts(Func<SpeechContent, Task<AudioClip>> synthesize) => _synthesize = synthesize;
        public Task<AudioClip> SynthesizeAsync(SpeechContent content, CancellationToken ct = default)
            => _synthesize(content);
    }
}
