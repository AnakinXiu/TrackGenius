# Piper TTS Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fake TTS tail with a real Piper backend — announcements synthesize via PiperSharp and play audibly through `System.Media.SoundPlayer`, driven by a background worker in `RaceAnnouncer`.

**Architecture:** Three new classes inside `TrackGenius.Speech` (`Piper/PiperTtsEngine`, `Piper/PiperBootstrap`, `Audio/SoundPlayerAudioPlayer`) implement the existing `ITtsEngine`/`IAudioPlayer` contracts; `RaceAnnouncer` gains a background drain loop; `App.xaml.cs` bootstraps piper.exe + `en_US-amy-medium` into `%LOCALAPPDATA%` and swaps the backend on success.

**Tech Stack:** PiperSharp 1.0.7 (net8.0; pulls NAudio/Newtonsoft/SharpCompress transitively), `System.Media.SoundPlayer` (BCL), NUnit.

**Spec:** `docs/superpowers/specs/2026-09-01-piper-tts-backend-design.md`

## Global Constraints

- Work from repo root `E:\source\repo\TrackGenius`; solution `source/TrackGenius.sln`. Branch: `RaceSpeechEngine`.
- **PiperSharp reference goes ONLY in `TrackGenius.Speech.csproj`** (UI/Core must stay Piper-free; they see only the abstractions).
- PiperSharp API facts (verified against v1.0.7 source — do not deviate):
  - `new PiperProvider(PiperConfiguration)`; `Task<byte[]> InferAsync(string text, AudioOutputType outputType = AudioOutputType.Wav, CancellationToken token = default)` — **no per-call CancellationToken parameter name `cancellationToken`; it is `token`**.
  - `PiperConfiguration { string ExecutableLocation; string WorkingDirectory; VoiceModel Model; float SpeakingRate = 1f; ... }`.
  - `PiperDownloader.DownloadPiper()` → `Task<Stream>`; `.ExtractPiper(string extractTo)` extension → `Task<string>`.
  - `PiperDownloader.DownloadModelByKey(string modelKey)` → `Task<VoiceModel>`; `model.DownloadModel(string saveModelTo)` → `Task<VoiceModel>`; `VoiceModel.LoadModel(string directory)` → `Task<VoiceModel>` (reads `model.json`, sets `ModelLocation`).
  - `PiperDownloader.PiperExecutable` → `"piper.exe"` on Windows.
  - Namespaces: `PiperSharp`, `PiperSharp.Models` (VoiceModel/PiperConfiguration), `AudioOutputType` is in `PiperSharp.Models`.
- Default voice key binding: `"en_US-amy-medium"`. Install root binding: `%LOCALAPPDATA%\TrackGenius\piper` (exe) and `...\piper\voices\<modelKey>` (model dir).
- Logging per `source/doc/Logging.md`: PascalCase events, named placeholders, injected `ILogger<T>`; e.g. `SpeechSynthesized`, `SpeechBootstrap`, `SpeechBootstrapFailed`, `SpeechMessageFailed`, `SpeechPlaybackStopped`.
- Thread contract: `SoundPlayerAudioPlayer.PlayAsync` must offload blocking play via `Task.Run` (worker thread, never UI). Worker: sequential single consumer, 200 ms idle poll, honors CancellationToken.
- Tests: file-scoped namespaces, NUnit, `Given…_When…_Then…`; **no network access in tests** (no PiperDownloader calls from tests).
- Gates per task: `dotnet build source/TrackGenius.sln` 0 errors; `dotnet test source/TrackGenius.SpeechTests/TrackGenius.SpeechTests.csproj` all pass; UITests unchanged from current state (141 pass / 2 known pre-existing timer failures).
- MSB3021/MSB3027 → `taskkill //IM TrackGenius.UI.exe //F`, retry, else BLOCKED. No GUI launch (manual audible verification is the user's).
- **No commits until user review** (user's flow for this feature).

---

## Task 1: PiperSharp package + `SoundPlayerAudioPlayer` (TDD)

**Files:**
- Modify: `source/TrackGenius.Speech/TrackGenius.Speech.csproj`
- Create: `source/TrackGenius.Speech/Audio/SoundPlayerAudioPlayer.cs`
- Test: `source/TrackGenius.SpeechTests/Audio/SoundPlayerAudioPlayerTests.cs`

**Interfaces (produced; consumed by Task 4):** `public sealed class SoundPlayerAudioPlayer : IAudioPlayer` — parameterless ctor; `Task PlayAsync(AudioClip clip, CancellationToken ct = default)` (WAV only: `clip.Format != "wav"` → `NotSupportedException`); `Task StopAsync(CancellationToken ct = default)` (disposes current player; no-throw when nothing playing).

- [ ] **Step 1: Add the package**

```bash
cd /e/source/repo/TrackGenius
dotnet add source/TrackGenius.Speech/TrackGenius.Speech.csproj package PiperSharp --version 1.0.7
```

(If restore fails on the private feed: retry with `--ignore-failed-sources`.)

- [ ] **Step 2: Write the failing tests**

`source/TrackGenius.SpeechTests/Audio/SoundPlayerAudioPlayerTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;

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
```

- [ ] **Step 3: Run — RED** (`SoundPlayerAudioPlayer` missing).

```bash
dotnet test source/TrackGenius.SpeechTests/TrackGenius.SpeechTests.csproj --filter "FullyQualifiedName~SoundPlayerAudioPlayerTests"
```

- [ ] **Step 4: Implement**

`source/TrackGenius.Speech/Audio/SoundPlayerAudioPlayer.cs`:

```csharp
using System;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech.Audio;

/// <summary>Plays WAV AudioClips from memory. Blocking play is offloaded to the thread pool — never call from the UI thread path directly.</summary>
public sealed class SoundPlayerAudioPlayer : IAudioPlayer
{
    private readonly object _gate = new();
    private SoundPlayer? _current;

    public Task PlayAsync(AudioClip clip, CancellationToken cancellationToken = default)
    {
        if (clip is null)
            throw new ArgumentNullException(nameof(clip));
        if (!string.Equals(clip.Format, "wav", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Only WAV clips are supported (got '{clip.Format}').");

        return Task.Run(() =>
        {
            var player = new SoundPlayer(new MemoryStream(clip.Data));
            lock (_gate)
                _current = player;
            try
            {
                player.PlaySync();
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_current, player))
                        _current = null;
                }
                player.Dispose();
            }
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _current?.Stop();
            _current = null;
        }
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 5: Green + build.** Expected: 3 new tests pass (suite 45).

---

## Task 2: `PiperBootstrap` (TDD, no-network paths)

**Files:**
- Create: `source/TrackGenius.Speech/Piper/PiperBootstrap.cs`
- Test: `source/TrackGenius.SpeechTests/Piper/PiperBootstrapTests.cs`

**Interfaces (produced; consumed by Task 4):**
- `public sealed record PiperSetup(string ExecutablePath, string WorkingDirectory, string ModelDirectory);`
- `public sealed class PiperBootstrap` — ctor `(string rootDirectory, ILogger<PiperBootstrap> logger, string modelKey = "en_US-amy-medium")`; properties `string ExecutablePath`, `string WorkingDirectory`, `string ModelDirectory`; `bool IsReady { get; }` (exe + model.json + .onnx all exist); `Task<PiperSetup> EnsureReadyAsync(IProgress<string>? progress = null, CancellationToken ct = default)` — if `IsReady`, returns immediately WITHOUT progress reports; otherwise downloads piper (unless exe exists) and model (unless model dir complete) via PiperDownloader, reporting progress, then returns the setup.

- [ ] **Step 1: Failing tests**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Speech.Piper;

namespace TrackGenius.SpeechTests.Piper;

[TestFixture]
public class PiperBootstrapTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"piper-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private static void WriteCompleteModel(string modelDir)
    {
        Directory.CreateDirectory(modelDir);
        File.WriteAllText(Path.Combine(modelDir, "model.json"), "{}");
        File.WriteAllBytes(Path.Combine(modelDir, "voice.onnx"), new byte[] { 1 });
    }

    [Test]
    public void GivenNothingInstalled_WhenIsReadyRead_ThenFalse()
    {
        var bootstrap = new PiperBootstrap(_root, NullLogger<PiperBootstrap>.Instance);

        Assert.That(bootstrap.IsReady, Is.False);
    }

    [Test]
    public async Task GivenExeAndModelPresent_WhenEnsureReadyCalled_ThenImmediateNoProgress()
    {
        Directory.CreateDirectory(Path.Combine(_root, "piper"));
        File.WriteAllText(bootstrap_exe(_root), "stub");
        WriteCompleteModel(Path.Combine(_root, "piper", "voices", "en_US-amy-medium"));
        var bootstrap = new PiperBootstrap(_root, NullLogger<PiperBootstrap>.Instance);
        var progressReports = new List<string>();

        var setup = await bootstrap.EnsureReadyAsync(new Progress<string>(progressReports.Add));

        Assert.Multiple(() =>
        {
            Assert.That(bootstrap.IsReady, Is.True);
            Assert.That(progressReports, Is.Empty);   // cached path: zero downloads, zero progress
            Assert.That(setup.ExecutablePath, Is.EqualTo(bootstrap_exe(_root)));
            Assert.That(setup.ModelDirectory, Is.EqualTo(Path.Combine(_root, "piper", "voices", "en_US-amy-medium")));
        });
    }

    [Test]
    public async Task GivenExeOnly_WhenIsReadyRead_ThenFalseUntilModelExists()
    {
        Directory.CreateDirectory(Path.Combine(_root, "piper"));
        File.WriteAllText(bootstrap_exe(_root), "stub");
        var bootstrap = new PiperBootstrap(_root, NullLogger<PiperBootstrap>.Instance);

        Assert.That(bootstrap.IsReady, Is.False);   // exe without model is not ready
        WriteCompleteModel(Path.Combine(_root, "piper", "voices", "en_US-amy-medium"));

        Assert.That(bootstrap.IsReady, Is.True);
        var setup = await bootstrap.EnsureReadyAsync();
        Assert.That(setup.WorkingDirectory, Is.EqualTo(Path.Combine(_root, "piper")));
    }

    private static string bootstrap_exe(string root)
        => Path.Combine(root, "piper", "piper.exe");
}
```

- [ ] **Step 2: RED → Step 3: Implement** (network code paths are NOT unit-tested — they are behind the `IsReady` early return):

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PiperSharp;
using PiperSharp.Models;

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
            var stream = await PiperDownloader.DownloadPiper();
            await stream.ExtractPiper(WorkingDirectory);
        }

        if (!Directory.Exists(ModelDirectory) || !File.Exists(Path.Combine(ModelDirectory, "model.json")))
        {
            Report(progress, $"Downloading voice model {_modelKey}");
            _logger.LogInformation("SpeechBootstrap Stage={Stage} ModelKey={ModelKey}", "DownloadModel", _modelKey);
            await PiperDownloader.DownloadModelByKey(_modelKey)
                .ContinueWith(t => t.Result.DownloadModel(ModelDirectory), ct, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default)
                .Unwrap();
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
```

**Simplification directive:** if the `ContinueWith` download chain is awkward, replace the model-download block with the straightforward sequence:

```csharp
        if (!Directory.Exists(ModelDirectory) || !File.Exists(Path.Combine(ModelDirectory, "model.json")))
        {
            Report(progress, $"Downloading voice model {_modelKey}");
            var model = await PiperDownloader.GetModelByKey(_modelKey)
                         ?? throw new InvalidOperationException($"Voice model {_modelKey} not found.");
            await model.DownloadModel(ModelDirectory);
        }
```

— `GetModelByKey` returns `Task<VoiceModel?>` per source; this form is preferred. Use it.

- [ ] **Step 4: Green (3 tests) + build. Step 5: No commit.**

---

## Task 3: `PiperTtsEngine` (TDD with fake provider seam)

**Files:**
- Create: `source/TrackGenius.Speech/Piper/PiperTtsEngine.cs`
- Test: `source/TrackGenius.SpeechTests/Piper/PiperTtsEngineTests.cs`

**Interfaces:**
- Consumes: `SpeechContent`/`AudioClip`/`ITtsEngine`; PiperSharp types.
- Produces: `public sealed class PiperTtsEngine : ITtsEngine` — ctor `(string executablePath, string workingDirectory, VoiceModel model, ILogger<PiperTtsEngine> logger)`; also an internal ctor seam for tests: `internal PiperTtsEngine(Func<string, CancellationToken, Task<byte[]>> infer, ILogger<PiperTtsEngine> logger)`. `SynthesizeAsync` maps text → `AudioClip(bytes, "wav")`, logs `SpeechSynthesized ElapsedMs={ElapsedMs} Length={Length}`; Ssml ignored (plain text only in v1).

- [ ] **Step 1: Failing tests** (no network — the internal seam injects synthesis):

```csharp
using System;
using System.Linq;
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
```

- [ ] **Step 2: RED → Step 3: Implement**:

```csharp
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
        : this(CreateProvider(executablePath, workingDirectory, model).InferAsync, logger)
    {
    }

    internal PiperTtsEngine(Func<string, CancellationToken, Task<byte[]>> infer, ILogger<PiperTtsEngine> logger)
    {
        _infer = infer;
        _logger = logger;
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
```

(Adapter note: `InferAsync(text, AudioOutputType.Wav, token)` has signature `Task<byte[]> InferAsync(string, AudioOutputType, CancellationToken)`; the method-group conversion to `Func<string, CancellationToken, Task<byte[]>>` will NOT match — wrap it instead:)

```csharp
        : this((text, ct) => CreateProvider(executablePath, workingDirectory, model)
                .InferAsync(text, AudioOutputType.Wav, ct), logger)
```

(Use this wrapped form in the public ctor.)

- [ ] **Step 4: Green (2 tests) + build. Step 5: No commit.**

---

## Task 4: `RaceAnnouncer` background worker (TDD)

**Files:**
- Modify: `source/TrackGenius.Speech/RaceAnnouncer.cs`
- Test: `source/TrackGenius.SpeechTests/RaceAnnouncerWorkerTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3 types; existing queue/policy/renderer.
- Produces (new members on `RaceAnnouncer`): `internal Task StartWorkerAsync()` (idempotent; returns after the loop is armed); `internal Task StopWorkerAsync()` (cancels + awaits drain-exit; idempotent); `public void SwapBackend(ITtsEngine ttsEngine, IAudioPlayer audioPlayer, bool enabled)` (thread-safe swap under the existing `_gate`; takes effect for subsequent messages). `Attach` starts the worker only when `Enabled`; `Detach`/`Dispose` stop it. `DrainForTestAsync` unchanged.

- [ ] **Step 1: Failing worker tests**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
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

    private sealed class DelegateTts : ITtsEngine
    {
        private readonly Func<SpeechContent, Task<AudioClip>> _synthesize;
        public DelegateTts(Func<SpeechContent, Task<AudioClip>> synthesize) => _synthesize = synthesize;
        public Task<AudioClip> SynthesizeAsync(SpeechContent content, CancellationToken ct = default)
            => _synthesize(content);
    }
}
```

- [ ] **Step 2: RED → Step 3: Implement** — add to `RaceAnnouncer`:

```csharp
    private readonly object _workerGate = new();
    private CancellationTokenSource? _workerCts;
    private Task? _workerLoop;
    private volatile ITtsEngine _activeTtsEngine;   // initialize both to ctor values
    private volatile IAudioPlayer _activeAudioPlayer;

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

    internal async Task StopWorkerAsync()
    {
        Task? loop;
        lock (_workerGate)
        {
            _workerCts?.Cancel();
            loop = _workerLoop;
            _workerLoop = null;
        }
        if (loop is not null)
        {
            try { await loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    public void SwapBackend(ITtsEngine ttsEngine, IAudioPlayer audioPlayer, bool enabled)
    {
        if (ttsEngine is null) throw new ArgumentNullException(nameof(ttsEngine));
        if (audioPlayer is null) throw new ArgumentNullException(nameof(audioPlayer));
        _activeTtsEngine = ttsEngine;
        _activeAudioPlayer = audioPlayer;
        Enabled = enabled;
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
```

Wire-up changes: `Attach` → after subscribing, `if (Enabled) _ = StartWorkerAsync();`. `Detach` → `_ = StopWorkerAsync();` before unsubscribing. `Dispose` → `StopWorkerAsync().GetAwaiter().GetResult();` (acceptable: loop exits ≤200ms). Ctor assigns `_activeTtsEngine = ttsEngine; _activeAudioPlayer = audioPlayer;` and `DrainForTestAsync` keeps using the ctor fields (unchanged behavior for existing tests).

(If `DequeueAsync(CancellationToken)` signature mismatch blocks (it takes a plain `CancellationToken` — fine), adjust naturally; the queue interface method is `Task<SpeechMessage?> DequeueAsync(CancellationToken)`.)

- [ ] **Step 4: Green (2 worker tests + all 47 prior) + build + UITests state unchanged. Step 5: No commit.**

---

## Task 5: Composition — bootstrap + backend swap in `App.xaml.cs`

**Files:**
- Modify: `source/TrackGenius/App.xaml.cs`

**Interfaces:**
- Consumes: Tasks 1–4 all types; `_loggingContext` loggers.
- Produces: startup pipeline — fakes first, background bootstrap, swap-to-Piper + enable on success, log-only disable on failure.

- [ ] **Step 1: Implement** (replace the announcer construction block inside `CreateMainWindow`):

```csharp
            // Speech pipeline: start on fakes (silent), swap to Piper once bootstrapped.
            var speechLogger = _loggingContext.LoggerFactory.CreateLogger<RaceAnnouncer>();
            var speechQueue = new SpeechQueue(20, _loggingContext.LoggerFactory.CreateLogger<SpeechQueue>());
            var announcer = new RaceAnnouncer(
                new AnnouncementScheduler(),
                new SpeechTemplateRenderer(),
                new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), 20),
                speechQueue,
                new TrackGenius.Speech.Fakes.FakeTtsEngine(),
                new TrackGenius.Speech.Fakes.FakeAudioPlayer(),
                speechLogger)
            {
                Enabled = false,
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    var piperRoot = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "TrackGenius");
                    var bootstrap = new PiperBootstrap(piperRoot,
                        _loggingContext.LoggerFactory.CreateLogger<PiperBootstrap>());
                    var setup = await bootstrap.EnsureReadyAsync();

                    var model = await VoiceModel.LoadModel(setup.ModelDirectory);
                    var piperTts = new PiperTtsEngine(setup.ExecutablePath, setup.WorkingDirectory, model,
                        _loggingContext.LoggerFactory.CreateLogger<PiperTtsEngine>());
                    var player = new SoundPlayerAudioPlayer();

                    announcer.SwapBackend(piperTts, player, enabled: true);
                }
                catch (Exception ex)
                {
                    speechLogger.LogError(ex, "SpeechBootstrapFailed");
                    // Stay on fakes, disabled — racing continues without voice.
                }
            });

            return new MainForm(communicateService, connectionService, userBehaviorLogger, announcer);
```

Add usings: `System.IO`, `TrackGenius.Speech.Piper`, `TrackGenius.Speech.Audio`, `PiperSharp.Models`.

- [ ] **Step 2: Gates**

```bash
dotnet build source/TrackGenius.sln
dotnet test source/TrackGenius.SpeechTests/TrackGenius.SpeechTests.csproj
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: build 0 errors; SpeechTests green (~49); UITests unchanged (141 + 2 pre-existing).

- [ ] **Step 3: No commit — report changed files; user runs the app to hear Piper (first launch downloads ~63 MB model; subsequent launches are instant), per standing manual-verification policy.**

---

## Final verification

- [ ] Build 0 errors; SpeechTests all green; UITests state unchanged.
- [ ] `grep`: no `PiperSharp` using outside `source/TrackGenius.Speech/`.
- [ ] Working tree uncommitted; file list reported for user review.
- [ ] User's manual audible check: launch app → port + Robitronic → Start → drive detection → announcements speak in sequence (first run downloads piper + amy model into `%LOCALAPPDATA%\TrackGenius\piper`).

## Out of scope (do not do)

- Settings UI/persistence for enable/voice/volume; runtime voice switching; SSML; non-WAV; device selection.
- Stale UITest timer tests fix (separate).
- Second-backend project split.
