# Speech Announcement System Design (Interface Phase)

**Date:** 2026-08-28
**Status:** For user review
**Source material:** `G:\Downloads\TrackGenius_Speech_RaceAnnouncer_Architecture.md` (AI-generated blueprint)
**Scope:** Full pipeline with minimal implementations; NO real TTS backend this phase (interface design only, per user direction)

## Problem

TrackGenius has no voice output. The AI architecture doc proposes an 8-stage,
cross-platform announcer subsystem, but it assumes infrastructure this codebase
does not have: a DI container, a generic event dispatcher, explicit domain
events, and six new projects. The design below adapts the doc's pipeline to the
codebase's actual shape (manual composition root, plain CLR events, snapshot
standings, UI → Core layering).

## Decisions (from brainstorming, user-confirmed)

| Decision | Choice |
|---|---|
| v1 scope | Full 8-stage pipeline with minimal implementations; FakeTtsEngine/FakeAudioPlayer only; no SSML generation, no multi-language, no replay harness |
| Project structure | One new project `TrackGenius.Speech` (net8.0, no WPF) |
| Event source | Subscribe to existing `RaceEngine.RaceDataChanged` snapshots; DIFF consecutive snapshots to derive facts — zero changes to Core |
| Dependency direction | Speech → Core (Speech references Core, like UI does; Core never references Speech) |
| Announcements (v1) | Race started/finished (Critical), overall fastest lap (High), leader change (High), top-3 position changes (Important) |
| Voice state | Disabled by default until a Settings toggle exists |
| Docs/commits | Spec reviewed before plan; per user's flow, code lands uncommitted for review |

## Adaptation from the AI doc — what changed and why

| AI doc element | This design | Reason |
|---|---|---|
| `IRaceEventDispatcher` generic pub/sub | **Dropped.** Snapshot diffing in `StandingsDiffer` derives the facts | No second eventing abstraction beside the engine's existing event; diffing is pure and unit-testable on synthetic snapshots |
| Explicit events (`LeaderChanged`, `FastestLapChanged`, …) in Core | Derived speech-side from snapshot pairs | Core stays untouched this phase (doc's own rule: "Race Core 只负责产生事实" — snapshots ARE the facts) |
| 6 projects (Abstractions/Speech/Piper/Espeak/Azure/Audio) | One `TrackGenius.Speech` project with `Abstractions/` folder | Zero real TTS backends exist yet; split when backends arrive |
| Coalescing, throttling, interruptibility, race-phase policy, speech dictionary, multi-language, SSML | Deferred (YAGNI) — fields/hooks reserved in models | Nothing consumes them until a real TTS + settings UI exist |
| `Microsoft.Extensions.DependencyInjection` registration | Manual composition in `App.xaml.cs` | Repo has no DI container (CLAUDE.md: construct and inject manually) |
| Replay test harness | Synthetic snapshot pairs in unit tests | Same capability, no dedicated harness |
| Cross-platform audio | `IAudioPlayer` is platform-neutral; only Fake exists | Windows-only app today; contract preserves neutrality for free |

## Architecture

```
RaceEngine.RaceDataChanged (snapshot, serial thread)      [Core — unchanged]
   → RaceAnnouncer (subscribes; returns immediately, work continues on background channel)
      → StandingsDiffer       derive: RaceStarted, RaceFinished, FastestLap,
                              LeaderChanged, PositionChanged (v1 set)
      → AnnouncementScheduler worth-saying? → AnnouncementIntent (type, priority,
                              driver context, snapshot ref, expiry)
      → ISpeechTemplateRenderer  intent → SpeechContent (Text; SSML nullable)
          └─ SpeechFormatter (numbers/times → spoken English, v1: en)
      → IAnnouncementPolicy    dedup + expiry + max-queue → accept/reject
      → ISpeechQueue           priority + FIFO-within-priority, cancel, clear,
                              bounded (System.Threading.Channels worker)
      → ITtsEngine             SpeechContent → AudioClip   [FakeTtsEngine this phase]
      → IAudioPlayer           AudioClip → device          [FakeAudioPlayer this phase]
```

**Threading contract:** the `RaceDataChanged` handler captures the snapshot and
returns; all downstream stages run on a background consumer. No work on the
serial thread, none on the UI thread. Engine timing can never block on speech
(doc constraint 11), and speech failures never propagate into the timing
pipeline (doc constraint 12).

## Project layout

```
source/TrackGenius.Speech/                 (net8.0, refs Core + Model)
├── Abstractions/
│   ├── AnnouncementPriority.cs
│   ├── AnnouncementIntent.cs
│   ├── SpeechContent.cs / SpeechMessage.cs / AudioClip.cs
│   ├── ISpeechTemplateRenderer.cs
│   ├── IAnnouncementPolicy.cs (PolicyDecision, PolicyContext)
│   ├── ISpeechQueue.cs
│   ├── ITtsEngine.cs
│   └── IAudioPlayer.cs
├── RaceAnnouncer.cs                       (attach/subscribe/orchestrate; Enabled flag)
├── StandingsDiffer.cs                     (pure: snapshot pair → derived facts)
├── AnnouncementScheduler.cs               (facts → intents, priority mapping)
├── SpeechTemplateRenderer.cs              (en templates)
├── SpeechFormatter.cs                     (pure: numbers/times → spoken words)
├── AnnouncementPolicy.cs                  (dedup/expiry/queue-cap)
├── SpeechQueue.cs                         (Channels + priority + cancellation)
├── Fakes/
│   ├── FakeTtsEngine.cs                   (returns silent AudioClip; records calls)
│   └── FakeAudioPlayer.cs                 (records plays; no device)
└── SpeechDiagnostics.cs                   (stage-timestamped logging hooks)
```

## Interfaces (binding for this phase)

```csharp
namespace TrackGenius.Speech.Abstractions;

public enum AnnouncementPriority { Background = 0, Normal = 10, Important = 50, High = 70, Critical = 100 }

public sealed record AnnouncementIntent(
    string Type,                                   // "FastestLap"|"LeaderChanged"|"PositionChanged"|"RaceStarted"|"RaceFinished"
    AnnouncementPriority Priority,
    string DriverId,                               // transponder id — stable key
    string? DriverDisplayName,
    IReadOnlyList<RaceStandingsEntry> Snapshot,    // facts at decision time
    DateTimeOffset CreatedAt,
    TimeSpan? ExpiresAfter = null);                // default per-type (fast facts expire fast)

public sealed record SpeechContent(string Text, string? Ssml, string Language, string? VoiceId);

public sealed record AudioClip(byte[] Data, string Format);   // opaque to pipeline

public sealed record SpeechMessage(
    Guid Id, string Text, string? Ssml, AnnouncementPriority Priority, string Category,
    DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, bool Interruptible,
    string Language, string? VoiceId);

public interface ISpeechTemplateRenderer
{
    SpeechContent Render(AnnouncementIntent intent);
}

public interface IAnnouncementPolicy
{
    PolicyDecision Evaluate(SpeechMessage message, PolicyContext context);
}

public sealed record PolicyDecision(bool Accept, string? Reason);   // reason for diagnostics
public sealed record PolicyContext(int QueueLength, DateTimeOffset Now);

public interface ISpeechQueue
{
    ValueTask EnqueueAsync(SpeechMessage message, CancellationToken cancellationToken = default);
    bool TryCancel(Guid messageId);
    void Clear(AnnouncementPriority minimumPriority = AnnouncementPriority.Background);
}

public interface ITtsEngine
{
    Task<AudioClip> SynthesizeAsync(SpeechContent content, CancellationToken cancellationToken = default);
}

public interface IAudioPlayer
{
    Task PlayAsync(AudioClip clip, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
```

## RaceAnnouncer (composition surface)

```csharp
public sealed class RaceAnnouncer : IDisposable
{
    public bool Enabled { get; set; }                       // default false — silent until a settings toggle exists
    public void Attach(RaceEngine engine);                  // subscribe RaceDataChanged
    public void Detach(RaceEngine engine);
    // ctor(RaceEngine engine? no — Attach/Detach keeps engine lifetime owned by the race page)
}
```

## Announcement set & priorities (v1)

| Derived fact | Trigger (diff of consecutive snapshots) | Priority | Expiry |
|---|---|---|---|
| `RaceStarted` | `Attach` + first snapshot after a cleared grid (lap counts reset) | Critical | none |
| `RaceFinished` | v1: not auto-detected — the current FreePractice mode has no finish concept; hook reserved (manual emission in tests) | Critical | none |
| `FastestLap` | entry newly `IsRaceBestLap` with a faster time than the previous race-best | High | 15 s |
| `LeaderChanged` | position-1 transponder differs from previous snapshot | High | 10 s |
| `PositionChanged` | top-3 entries swap relative order | Important | 8 s |

v1 policy: priority ordering; per-category dedup window 3 s; queue cap 20 — when full, oldest
Background/Normal messages drop first; expiry checked at dequeue.
`Interruptible` is reserved as a model field; no interrupt behavior in v1.

## Composition & wiring

`App.xaml.cs` (manual, per repo convention):

```csharp
var speechQueue = new SpeechQueue(capacity: 20);
var announcer = new RaceAnnouncer(
    scheduler, renderer, policy, speechQueue, fakeTts, fakePlayer, logger)
    { Enabled = false };
// MainForm/RacePageViewModel calls announcer.Attach(engine) on race start,
// Detach on restart/dispose — mirrors RaceDataChanged subscription lifecycle.
```

## Error handling & diagnostics

- Every pipeline stage wrapped: exceptions logged (`TrackGenius.Speech.*`
  categories per `source/doc/Logging.md`), message dropped, pipeline continues.
- `SpeechDiagnostics` records per-message stage timestamps (created/queued/
  synthesized/played + failure reason) per doc §19 — written to the app log,
  no new infrastructure.
- FakeTtsEngine/FakeAudioPlayer cannot fail; real backends later must honor
  the same isolation.

## Testing

New NUnit project `TrackGenius.Speech.Tests` (mirrors TrackGenius.ConstTests
scale):

- **StandingsDiffer:** snapshot pairs → expected derived facts (leader change,
  top-3 swaps, fastest lap via `IsRaceBestLap`, first-snapshot race start);
  no-change pair → nothing.
- **Scheduler:** fact → intent with correct Type/Priority/expiry.
- **Policy:** dedup window hit/miss, expired reject, queue-cap drop order.
- **SpeechQueue:** priority order, FIFO within priority, TryCancel, Clear(min).
- **SpeechFormatter:** "12.438" → "twelve point four three eight"; positions
  "P1/P2/P3" → "first/second/third".
- **Templates:** each intent type → expected en-US text (exact strings).
- **End-to-end:** synthetic snapshot sequence → FakeTtsEngine recorded
  announcements in order (this is the doc's replay test, achieved via diff
  inputs).

## Out of scope (this phase)

- Any real TTS backend (Piper/eSpeak/Azure/System) — interfaces + fakes only.
- SSML generation, speech dictionary, driver SpeechName field, multi-language
  resource loading (Language field reserved).
- Coalescing, throttling, interruptibility behavior, race-phase policies.
- Settings UI / persistence for voice on-off & engine choice.
- Audio output device selection, volume control.
