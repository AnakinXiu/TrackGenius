using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Speech.Abstractions;
using TrackGenius.Speech.Piper;

namespace TrackGenius.SpeechTests.Piper;

[TestFixture]
public class PiperTtsEngineTests
{
    [Test]
    public async Task GivenText_WhenSynthesized_ThenWavAudioClipReturned()
    {
        var engine = new PiperTtsEngine(
            (text, ct) => Task.FromResult(new byte[] { 1, 2, 3, 4 }),
            NullLogger<PiperTtsEngine>.Instance);

        var clip = await engine.SynthesizeAsync(
            new SpeechContent("Race started.", null, "en-US", null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(clip.Format, Is.EqualTo("wav"));
            Assert.That(clip.Data, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        });
    }

    [Test]
    public void GivenFailingInference_WhenSynthesized_ThenExceptionPropagates()
    {
        var engine = new PiperTtsEngine(
            (text, ct) => Task.FromException<byte[]>(new InvalidOperationException("piper crashed")),
            NullLogger<PiperTtsEngine>.Instance);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.SynthesizeAsync(new SpeechContent("boom", null, "en-US", null), CancellationToken.None));
    }
}
