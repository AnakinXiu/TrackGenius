# Race Time Board Design

**Date:** 2026-08-27
**Status:** Approved in brainstorming
**Branch state:** builds on the user's staged (uncommitted) scaffolding — `Race.RaceName/CountDownTime`, `RaceEngine.RaceStart(IRace)` + `RaceTime`/`RemainTime`, `RacePageViewModel` display properties, `QuickRacePage` `RaceTimeBoard` grid.

## Problem

QuickRacePage has no race header. The user staged the plumbing (model fields,
engine timer properties, VM display strings, a placeholder board grid) but the
pieces are not connected: nothing starts the timer, nothing supplies the race
name (`ApplyCurrentRace` has no caller), the four board TextBlocks all sit in
Grid.Column 0, and the time properties are one-shot reads that never refresh.

## Goal

A `RaceTimeBoard` header on QuickRacePage showing, live: the race name, the
race's elapsed time (`RaceTime`), its remaining countdown (`RemainTime`), and
the current system time (`CurrentTime`).

## Decisions (from brainstorming)

| Decision | Choice |
|---|---|
| Refresh mechanism | Single `DispatcherTimer` (1 s) in `RacePageViewModel`, raises PropertyChanged for all three time properties per tick |
| Countdown at zero | RemainTime may go negative (`-0:05.000`); no clamping |
| RaceName source | Auto-generated at `StartRace()` — `$"Quick Race {DateTime.Now:yyyy-MM-dd HH:mm}"`; delivered via `ApplyCurrentRace` (single injection point, as staged) |
| Layout | Name left (20pt, primary); RaceTime + RemainTime center (each with muted label beneath); CurrentTime right, muted |
| Verification | Unit tests for model/engine; board visuals by user's manual check (standing policy) |
| Commits | None — implementation stays uncommitted for user review |

## Architecture

```
StartRace()                                  [RacePageViewModel]
  → new Race(Guid.NewGuid(), FreePractice, "World GT", 10, racers)
  → _raceEngine.RaceStart(race)              [staged overload: subscribes + creates & starts RaceTimer]
  → ApplyCurrentRace(race)                   → CurrentRace → RaceName updated

DispatcherTimer (1s, VM ctor)                [UI thread]
  → PropertyChanged: RaceTime, RemainTime, CurrentTime
  → getters pull RaceEngine.RaceTime/RemainTime (stopwatch pass-through) + DateTime.Now
```

### Model (`RaceTimer.cs`)
- Remove the unused `TimerElapsedEventHandler EverySecondElapsed` property,
  the `using Microsoft.Win32;`, and the unused `TimeSpan RaceTime { get; set; }`.
- `Elapsed` / `Remaining` stay as staged (`Remaining` goes negative naturally).

### Core (`RaceEngine.cs`)
- Staged `RaceStart(IRace race)` additionally calls `_raceTimer.Start()`.
- Staged `RaceStart(ICollection<RaceData> racers)` delegates: subscribes, then
  calls `RaceStart(new Race(...))` so both paths run a started timer.

### ViewModel (`RacePageViewModel.cs`)
- `StartRace()` builds the `IRace` (defaults identical to today's inline
  construction: `Guid.NewGuid()`, `RaceType.FreePractice`,
  `new RaceClass("World GT")`, countdown `10`) with
  `RaceName = $"Quick Race {DateTime.Now:yyyy-MM-dd HH:mm}"`, starts the
  engine via the `IRace` overload, then calls `ApplyCurrentRace(race)`.
- Add `private readonly DispatcherTimer _clockTimer` (1 s tick, started in
  ctor): tick handler raises `OnPropertyChanged` for `RaceTime`,
  `RemainTime`, `CurrentTime`.
- `CurrentTime` getter unchanged (`DateTime.Now.ToLongTimeString()`).

### View (`QuickRacePage.xaml`)
- `RaceTimeBoard` grid: columns `*` / `Auto` / `Auto` / `Auto`; height ~48.
  - Col 0: `RaceName` — 20pt SemiBold, `RaceTextPrimaryBrush`, trimmed.
  - Col 1: `RaceTime` (16pt) over muted 10pt label "RACE TIME".
  - Col 2: `RemainTime` (16pt) over muted 10pt label "REMAINING".
  - Col 3: `CurrentTime` — muted, right-aligned, vertically centered.
- All colors via existing theme tokens; no new tokens.

## Error handling

- `_raceEngine` null (never started / start failed): getters already return
  `string.Empty` — board shows blanks; `CurrentTime` always live.
- Timer tick on UI thread by construction (`DispatcherTimer`); serial-thread
  engine events unaffected (timer only reads).
- No dispose choreography: the 1 s tick is negligible and page-scoped.

## Testing

- `RaceTests`: ctor sets `RaceName`/`CountDownTime` (update existing factory to
  cover the 5-arg ctor).
- `RaceTimerTests` (new): `Start` sets `IsStarted` and `Elapsed` grows;
  `Remaining` = countdown − elapsed (negative after expiry, asserted by
  setting `CountDownTime` 0 and starting → `Remaining <= 0`).
- `RaceEngineTests`: `RaceStart(IRace)` subscribes (a detection still raises
  `RaceDataChanged`) and the engine's `RaceTime` grows after start (assert
  `>= 0` twice / `IsStarted` indirectly via `RaceTime` change).
- `RacePageViewModelTests`: after `StartRaceCommand`, `RaceName` is non-empty
  and matches the `Quick Race ...` pattern; `RaceTime`/`RemainTime` parse as
  non-empty after start.

## Out of scope

- User-editable race name; pause/resume; race-end behavior at 0 remaining.
- Calculators for the new `RaceOrderRule` members (staged stubs stay).
- Sub-second display animation (1 s tick only).
- Committing anything (user reviews the working tree).
