# Piper TTS Backend Design

**Date:** 2026-09-01
**Status:** For user review
**Builds on:** speech announcer subsystem (commit `b2c5848`) — `ITtsEngine`/`IAudioPlayer`/fakes/RaceAnnouncer
**Source research:** rhasspy/piper (GitHub), PiperSharp 1.0.7 (NuGet, net8.0), System.Media.SoundPlayer docs

## Problem

The announcer pipeline terminates in `FakeTtsEngine`/`FakeAudioPlayer` — announcements are
recorded, never spoken. A real Piper backend is needed to actually hear race announcements.

## Decisions (user-confirmed)

| Decision | Choice |
|---|---|
| Integration | **PiperSharp** NuGet package (net8.0; wraps piper.exe; built-in downloader) |
| Bootstrap | Auto-download piper.exe + voice model to `%LocalAppData%\TrackGenius\piper` on first use (idempotent) |
| Default voice | `en_US-amy-medium` (~63 MB, female, medium quality/size, CPU-fast) |
| Live audio | `Enabled = true` after successful bootstrap; announcements play audibly this phase |
| Playback | `System.Media.SoundPlayer` (WAV bytes from memory; zero audio-specific dependencies) |
| Worker | Background drain loop inside `RaceAnnouncer` (required for audible speech) |

## Architecture

```
RaceAnnouncer (orchestration unchanged)
  └─ background worker loop (NEW): started on Attach, stopped on Detach/Dispose
       DequeueAsync → PiperTtsEngine.SynthesizeAsync → SoundPlayerAudioPlayer.PlayAsync → next
            → PiperTtsEngine : ITtsEngine        [Speech/Piper/]
                 PiperSharp PiperProvider.InferAsync(text, Wav) → AudioClip(bytes, "wav")
            → SoundPlayerAudioPlayer : IAudioPlayer [Speech/Audio/]
                 SoundPlayer(MemoryStream).PlaySync() on the worker thread
            → PiperBootstrap                    [Speech/Piper/]
                 %LocalAppData%\TrackGenius\piper\{piper.exe, voices\en_US-amy-medium.onnx}
                 idempotent EnsureReadyAsync: PiperDownloader.DownloadPiper + model download
```

No new projects: PiperSharp reference stays inside `TrackGenius.Speech` (the "split into
backend projects later" path from the announcer spec remains open). Core/UI layers see
only `ITtsEngine`/`IAudioPlayer`.

## Components

### `PiperTtsEngine : ITtsEngine` (`Speech/Piper/PiperTtsEngine.cs`)

- Ctor: `(string executablePath, string workingDirectory, VoiceModel model, ILogger<PiperTtsEngine> logger)`
  — builds the internal `PiperProvider(new PiperConfiguration { ... })`.
- `SynthesizeAsync`: `SpeechContent.Text` → `provider.InferAsync(text, AudioOutputType.Wav)`
  → `AudioClip(bytes, "wav")`. `Ssml` ignored (v1 plain text). Logs
  `SpeechSynthesized Text={TextTruncated} ElapsedMs={ElapsedMs}` per Logging.md.
- Exceptions propagate to the caller (the worker isolates them).

### `SoundPlayerAudioPlayer : IAudioPlayer` (`Speech/Audio/SoundPlayerAudioPlayer.cs`)

- `PlayAsync`: `Task.Run(() => new SoundPlayer(new MemoryStream(clip.Data)).PlaySync())` —
  blocking play belongs on the worker thread, never the UI thread. WAV-only (matches Piper
  output); non-WAV data throws `NotSupportedException` (isolated by the worker).
- `StopAsync`: disposes the current player (best-effort stop of in-flight audio).
- Thread-safe for sequential single-consumer use (the worker); no device selection in v1.

### `PiperBootstrap` (`Speech/Piper/PiperBootstrap.cs`)

- `Task EnsureReadyAsync(IProgress<string>? progress, CancellationToken ct)` — idempotent:
  - Target dir: `%LOCALAPPDATA%\TrackGenius\piper` (exe) and `...\piper\voices` (model).
  - If `piper.exe` missing → `PiperDownloader.DownloadPiper().ExtractPiper(dir)`.
  - If model files missing → `VoiceModel` via `PiperDownloader.DownloadModelByKey(DefaultModelKey, voicesDir)`
    (key `en_US-amy-medium`; returns the loaded model either way).
  - Progress strings logged (`SpeechBootstrap Progress={Progress}`) and surfaced to the
    caller for the future settings UI.
- Returns the provider inputs (paths + loaded model). All failures throw — the composition
  caller decides to disable speech.

### `RaceAnnouncer` worker (modify `Speech/RaceAnnouncer.cs`)

- `StartWorker()` / `StopWorker()` (internal idempotent): a long-running `Task` loop:
  `while (!stopped) { var m = await queue.DequeueAsync(ct); if (m is null) { await Task.Delay(200, ct); continue; }`
  `try { synthesize; play; } catch (log SpeechMessageFailed) }` — one message at a time,
  queue order = priority order. 200 ms idle poll (v1; no events needed yet).
- `Attach` starts the worker (when `Enabled`), `Detach`/`Dispose` stops it.
- `DrainForTestAsync` unchanged — tests drain deterministically without the worker.

### Composition (`App.xaml.cs`)

- Build the pipeline with fakes as today (safe default), fire-and-forget
  `Task.Run(PiperBootstrap.EnsureReadyAsync)` at startup:
  - Success → build `PiperTtsEngine` + `SoundPlayerAudioPlayer`, call a new
    `RaceAnnouncer.SwapBackend(tts, player, enabled: true)` (also stops the worker mid-drain
    safely: lock-swap references under a gate, worker picks up new instances on next message).
  - Failure → log `SpeechBootstrapFailed`, stay on fakes with `Enabled=false` (app behavior
    unchanged from today).

## Error handling

- Worker isolates every message: synthesis or playback failure logs
  `SpeechMessageFailed MessageId={MessageId} Reason={Reason}` and drops that message; the
  loop continues. The timing pipeline is never touched (handler unchanged).
- Bootstrap failure = speech silently disabled (log only) — the app runs exactly as the
  interface phase did.
- Concurrent syntheses impossible by construction (single worker).

## Testing

`TrackGenius.SpeechTests`:

- `SoundPlayerAudioPlayerTests`: valid tiny WAV bytes (44-byte silent header) → `PlayAsync`
  completes without throwing, `StopAsync` no-throw on fresh/disposed player; non-WAV format
  string is rejected (`AudioClip(data, "mp3")` → `NotSupportedException`).
- `PiperBootstrapTests`: temp-directory idempotency — `EnsureReadyAsync` twice; second call
  performs no downloads (assert via a counting progress sink: zero progress reports on the
  cached path). No network access in tests (skip download-path tests; manual run covers).
- `RaceAnnouncerWorkerTests`: fake TTS that signals a `TaskCompletionSource` on first
  synthesis; start worker via `Attach` on a stub engine event → await signal with timeout →
  `StopWorker`; assert exactly the enqueued message was spoken.
- Real-audio end-to-end (Piper download → synthesis → audible playback) is the user's
  manual verification (standing policy).

## Out of scope

- Settings UI / persistence for voice choice, enable toggle, volume (fifth field on the
  settings page later).
- Runtime voice switching, SSML pass-through, non-WAV output, device selection, interrupts.
- Migrating to dedicated `TrackGenius.Speech.Piper` project (only when a second backend
  arrives).
- The 2 stale UITest timer tests (separate fix, unrelated).
