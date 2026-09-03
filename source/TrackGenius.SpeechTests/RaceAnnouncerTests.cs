using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Model;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;
using TrackGenius.Speech.Fakes;

namespace TrackGenius.SpeechTests;

[TestFixture]
public class RaceAnnouncerTests
{
    private AnnouncementScheduler _scheduler = null!;
    private AnnouncementPolicy _policy = null!;
    private SpeechQueue _queue = null!;
    private FakeTtsEngine _tts = null!;
    private FakeAudioPlayer _player = null!;
    private RaceAnnouncer _announcer = null!;

    [SetUp]
    public void SetUp()
    {
        _scheduler = new AnnouncementScheduler();
        _policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), 20);
        _queue = new SpeechQueue(20, NullLogger<SpeechQueue>.Instance);
        _tts = new FakeTtsEngine();
        _player = new FakeAudioPlayer();
        _announcer = new RaceAnnouncer(_scheduler, new SpeechTemplateRenderer(), _policy, _queue, _tts, _player,
            NullLogger<RaceAnnouncer>.Instance)
        {
            Enabled = true,
        };
    }

    [TearDown]
    public void TearDown() => _announcer.Dispose();

    [Test]
    public async Task GivenSnapshotSequence_WhenHandled_ThenAnnouncementsSpokenInOrder()
    {
        var first = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true) };
        var improved = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 19.438, true) };

        _announcer.HandleStandings(this, first);
        _announcer.HandleStandings(this, improved);
        await _announcer.DrainForTestAsync();

        Assert.That(_tts.SynthesizedTexts, Is.EqualTo(new[]
        {
            "Race started.",
            "Car number one hundred, fastest lap, nineteen point four three eight seconds.",
        }));
    }

    [Test]
    public async Task GivenDisabledAnnouncer_WhenHandled_ThenNothingSpoken()
    {
        _announcer.Enabled = false;

        _announcer.HandleStandings(this, new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true) });
        await _announcer.DrainForTestAsync();

        Assert.That(_tts.SynthesizedTexts, Is.Empty);
    }

    [Test]
    public async Task GivenDuplicateLeaderChanges_WhenHandled_ThenSecondSuppressed()
    {
        var a = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true), TestStandings.Entry("200", 2, 21.0, false) };
        var b = new List<RaceStandingsEntry> { TestStandings.Entry("200", 1, 20.5, true), TestStandings.Entry("100", 2, 20.0, false) };

        _announcer.HandleStandings(this, a);
        _announcer.HandleStandings(this, b);
        _announcer.HandleStandings(this, a);   // leader flips back within the dedup window
        await _announcer.DrainForTestAsync();

        Assert.That(_tts.SynthesizedTexts.Count(t => t.Contains("takes the lead")), Is.EqualTo(1));
    }
}
