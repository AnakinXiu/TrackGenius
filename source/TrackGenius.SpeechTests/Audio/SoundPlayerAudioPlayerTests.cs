using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;
using TrackGenius.Speech.Audio;

namespace TrackGenius.SpeechTests.Audio;

[TestFixture]
public class SoundPlayerAudioPlayerTests
{
    /// <summary>Minimal valid WAV: 44-byte RIFF header, zero data. SoundPlayer accepts it; plays silence instantly.</summary>
    private static byte[] SilentWav()
    {
        var wav = new byte[44];
        BitConverter.GetBytes(0x46464952).CopyTo(wav, 0);   // "RIFF"
        BitConverter.GetBytes(36).CopyTo(wav, 4);           // chunk size
        BitConverter.GetBytes(0x45564157).CopyTo(wav, 8);   // "WAVE"
        BitConverter.GetBytes(0x20746d66).CopyTo(wav, 12);  // "fmt "
        BitConverter.GetBytes(16).CopyTo(wav, 16);          // fmt chunk size
        BitConverter.GetBytes((short)1).CopyTo(wav, 20);    // PCM
        BitConverter.GetBytes((short)1).CopyTo(wav, 22);    // mono
        BitConverter.GetBytes(16000).CopyTo(wav, 24);       // sample rate
        BitConverter.GetBytes(32000).CopyTo(wav, 28);       // byte rate
        BitConverter.GetBytes((short)2).CopyTo(wav, 32);    // block align
        BitConverter.GetBytes((short)16).CopyTo(wav, 34);   // bits per sample
        BitConverter.GetBytes(0x61746164).CopyTo(wav, 36);  // "data"
        BitConverter.GetBytes(0).CopyTo(wav, 40);           // data size
        return wav;
    }

    [Test]
    public async Task GivenSilentWav_WhenPlayed_ThenCompletesWithoutThrowing()
    {
        var player = new SoundPlayerAudioPlayer();

        await player.PlayAsync(new AudioClip(SilentWav(), "wav"), CancellationToken.None);
        await player.StopAsync(CancellationToken.None);

        Assert.Pass();
    }

    [Test]
    public void GivenNonWavClip_WhenPlayed_ThenNotSupportedException()
    {
        var player = new SoundPlayerAudioPlayer();

        Assert.ThrowsAsync<NotSupportedException>(
            () => player.PlayAsync(new AudioClip(new byte[] { 1, 2, 3 }, "mp3"), CancellationToken.None));
    }

    [Test]
    public async Task GivenFreshPlayer_WhenStopped_ThenNoThrow()
    {
        var player = new SoundPlayerAudioPlayer();

        await player.StopAsync(CancellationToken.None);

        Assert.Pass();
    }
}
