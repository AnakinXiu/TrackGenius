using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PiperSharp;

namespace TrackGenius.Speech.Piper;

public sealed record PiperSetup(string ExecutablePath, string WorkingDirectory, string ModelDirectory);

/// <summary>Idempotent installer for piper.exe + the default voice model under a local root.</summary>
public sealed class PiperBootstrap
{
    public const string DefaultModelKey = "en_US-amy-medium";

    private readonly string _modelKey;
    private readonly ILogger<PiperBootstrap> _logger;

    public string RootDirectory { get; }
    public string WorkingDirectory { get; }
    public string ModelDirectory { get; }
    public string ExecutablePath { get; }

    public PiperBootstrap(string rootDirectory, ILogger<PiperBootstrap> logger, string modelKey = DefaultModelKey)
    {
        RootDirectory = rootDirectory;
        WorkingDirectory = Path.Combine(rootDirectory, "piper");
        ExecutablePath = Path.Combine(WorkingDirectory, PiperDownloader.PiperExecutable);
        ModelDirectory = Path.Combine(WorkingDirectory, "voices", modelKey);
        _modelKey = modelKey;
        _logger = logger;
    }

    public bool IsReady =>
        File.Exists(ExecutablePath)
        && File.Exists(Path.Combine(ModelDirectory, "model.json"))
        && Directory.EnumerateFiles(ModelDirectory, "*.onnx").FirstOrDefault() is not null;

    public async Task<PiperSetup> EnsureReadyAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (IsReady)
            return new PiperSetup(ExecutablePath, WorkingDirectory, ModelDirectory);

        Report(progress, $"Preparing piper under {WorkingDirectory}");
        Directory.CreateDirectory(WorkingDirectory);

        if (!File.Exists(ExecutablePath))
        {
            Report(progress, "Downloading piper executable");
            _logger.LogInformation("SpeechBootstrap Stage={Stage}", "DownloadExecutable");
            // The piper release archive contains a top-level "piper/" folder, so it is
            // extracted into the root and lands at <root>/piper/piper.exe. ExtractPiper is
            // the synchronous Stream overload; the archive is already materialized here.
            using var stream = await PiperDownloader.DownloadPiper();
            stream.ExtractPiper(RootDirectory);
        }

        if (!Directory.Exists(ModelDirectory) || !File.Exists(Path.Combine(ModelDirectory, "model.json")))
        {
            Report(progress, $"Downloading voice model {_modelKey}");
            _logger.LogInformation("SpeechBootstrap Stage={Stage} ModelKey={ModelKey}", "DownloadModel", _modelKey);
            // DownloadModel(saveModelTo) appends the model key itself, so target the
            // "voices" root and let it create <root>/piper/voices/<modelKey>.
            var model = await PiperDownloader.GetModelByKey(_modelKey)
                        ?? throw new InvalidOperationException($"Voice model {_modelKey} not found.");
            await model.DownloadModel(Path.Combine(WorkingDirectory, "voices"));
        }

        if (!IsReady)
            throw new InvalidOperationException("Piper bootstrap finished but the installation is incomplete.");

        Report(progress, "Piper ready");
        return new PiperSetup(ExecutablePath, WorkingDirectory, ModelDirectory);
    }

    private void Report(IProgress<string>? progress, string message)
    {
        _logger.LogInformation("SpeechBootstrap Progress={Progress}", message);
        progress?.Report(message);
    }
}
