using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PiperSharp;
using PiperSharp.Models;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech.Piper;

/// <summary>Piper neural TTS via PiperSharp: text → in-memory WAV.</summary>
public sealed class PiperTtsEngine : ITtsEngine
{
    private readonly Func<string, CancellationToken, Task<byte[]>> _infer;
    private readonly ILogger<PiperTtsEngine> _logger;

    public PiperTtsEngine(string executablePath, string workingDirectory, VoiceModel model,
        ILogger<PiperTtsEngine> logger)
        : this((text, ct) => CreateProvider(executablePath, workingDirectory, model)
                .InferAsync(text, AudioOutputType.Wav, ct), logger)
    {
    }

    internal PiperTtsEngine(Func<string, CancellationToken, Task<byte[]>> infer, ILogger<PiperTtsEngine> logger)
    {
        _infer = infer;
        _logger = logger;
    }

    /// <summary>
    /// Factory for the composition root: loads the voice model and builds the engine in one step,
    /// keeping PiperSharp types out of the caller (App.xaml.cs stays PiperSharp-free).
    /// </summary>
    public static Task<PiperTtsEngine> FromSetupAsync(PiperSetup setup, ILogger<PiperTtsEngine> logger)
    {
        if (setup is null) throw new ArgumentNullException(nameof(setup));
        if (logger is null) throw new ArgumentNullException(nameof(logger));
        return CreateAsync(setup, logger);

        static async Task<PiperTtsEngine> CreateAsync(PiperSetup setup, ILogger<PiperTtsEngine> logger)
        {
            var model = await VoiceModel.LoadModel(setup.ModelDirectory).ConfigureAwait(false);
            return new PiperTtsEngine(setup.ExecutablePath, setup.WorkingDirectory, model, logger);
        }
    }

    private static PiperProvider CreateProvider(string executablePath, string workingDirectory, VoiceModel model)
        => new(new PiperConfiguration
        {
            ExecutableLocation = executablePath,
            WorkingDirectory = workingDirectory,
            Model = model,
        });

    public async Task<AudioClip> SynthesizeAsync(SpeechContent content, CancellationToken cancellationToken = default)
    {
        if (content is null)
            throw new ArgumentNullException(nameof(content));

        var stopwatch = Stopwatch.StartNew();
        // Plain text only in v1 — PiperSharp takes the raw string; SSML is not forwarded.
        var wav = await _infer(content.Text, cancellationToken);
        stopwatch.Stop();

        _logger.LogInformation("SpeechSynthesized ElapsedMs={ElapsedMs} ByteLength={ByteLength}",
            stopwatch.ElapsedMilliseconds, wav.Length);
        return new AudioClip(wav, "wav");
    }
}
