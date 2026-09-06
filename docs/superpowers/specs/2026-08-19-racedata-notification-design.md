# Race Data Notification: RaceEngine → RacePageViewModel

**Date:** 2026-08-19
**Status:** Approved
**Branch:** RunRaceEngine

## Problem

When `RaceEngine` receives a car-detection message and updates its race data
collection, the race page UI never learns about it. `RacePageViewModel.RaceDataItems`
is seeded with hard-coded test data and has no relationship to the engine's
`Race.RaceDataCollection`. Live lap counting therefore happens in the domain
layer but never reaches the screen.

## Goal

Each accepted detection that changes race state must notify the
`RacePageViewModel`, which updates `RaceDataItems` so the UI refreshes. The
engine's race data collection drives the page's `RaceDataItems`.

## Decisions (from brainstorming)

| Decision | Choice |
|---|---|
| Test data in `AddTestData()` | **Revised 2026-08-19 (execution):** call stays disabled (commented out); race page starts empty until real detections arrive. Rows/initializers retained in source, updated to string `Gap`/`Interval`, so they can be re-enabled for layout work. |
| Notification mechanism | Event raised by `RaceEngine` |
| Thread marshaling (serial thread → UI thread) | In the ViewModel (Core stays thread-agnostic) |
| Position/gap/interval computation | Model layer (engine uses it; ViewModel only maps) |
| Event shape | Single event carrying a full standings snapshot |
| Order rule | `LapsThenRaceTime` default; rule property on `Race`; no rule-selector UI yet |
| Lap-difference display | `"+1 Lap"` / `"+3 Laps"` (signed) |

## Architecture

One new hop on the existing event-driven pipeline:

```
SerialPortWrapper.DataReceived → CommunicateService.MessageReceived
  → MessageConsumer.CarDetected → RaceEngine.OnCarDetected
  → UpdateRaceStatus (adds racer / records lap; suppresses too-short intervals)
  → Race.OrderCalculator.Calculate(RaceDataCollection)      [Model, new]
  → RaceEngine.RaceDataChanged(IReadOnlyList<RaceStandingsEntry>)  [new, serial thread]
  → RacePageViewModel.OnRaceDataChanged
      → Dispatcher.BeginInvoke (marshal to UI thread)
      → find-or-create RaceDataItemViewModel by TransponderID → set fields
      → PropertyChanged → UI refresh
```

Layering is preserved: Model knows nothing of UI, Core knows nothing of WPF,
and the ViewModel is a field mapper that owns thread marshaling.

## Model layer

New files in `TrackGenius.Model/Race/`:

### `RaceOrderRule` (enum)

Single member `LapsThenRaceTime` — the extension point for user-selectable
rules set before the race starts.

### `IRaceOrderCalculator`

```csharp
public interface IRaceOrderCalculator
{
    RaceOrderRule Rule { get; }
    IReadOnlyList<RaceStandingsEntry> Calculate(ICollection<RaceData> raceDataCollection);
}
```

### `LapsRaceTimeOrderCalculator` (the default)

Ordering: lap count descending; among equal lap counts, `RacedTime` ascending
(the racer with the most laps leads; at equal laps, whoever reached that count
sooner leads). Ties keep insertion order. Positions are 1-based.

`RacedTime` is `RaceData.RacedTime` — the race-clock time of the racer's last
accepted detection.

### `RaceStandingsEntry` (record)

| Property | Type | Meaning |
|---|---|---|
| `RaceData` | `RaceData` | The racer's domain object |
| `Position` | `int` | 1-based standing |
| `BestLapTime` | `TimeSpan` | Fastest lap (`LapRecords.Min(LapTime)`; zero when no laps) |
| `LastLapTime` | `TimeSpan` | Most recent lap time; zero when no laps |
| `Gap` | `string` | See semantics below |
| `Interval` | `string` | See semantics below |

### Gap / Interval semantics

Produced by the calculator as pre-formatted strings (invariant culture):

- **Gap** = leader's `RacedTime` − current racer's `RacedTime`.
- **Interval** = same definition, but against the **racer in front**.
  *(Corrected 2026-08-26: the original spec text had these two references
  swapped; standard timing convention is Gap-to-leader,
  Interval-to-car-ahead. Implementation and tests now follow the corrected
  definitions.)*
- **Equal lap counts** → time difference formatted `m:ss.fff` (e.g. `"0:02.100"`).
- **Different lap counts** → the lap-count difference, signed:
  `"+1 Lap"`, `"+3 Laps"` (singular/plural by count).
- **Leader** → both `Gap` and `Interval` are `"-"`.
- The rule is purely mechanical — no special cases: compare lap counts with
  the reference racer (leader for Gap, front racer for Interval); equal →
  time difference, different → lap difference. Two zero-lap racers compare
  as equal, so their time difference is `0:00.000` (both `RacedTime`s are
  zero); a zero-lap racer against anyone with laps shows laps.

## Model layer — `Race` / `IRace` changes

- `RaceOrderRule OrderRule { get; set; }` — default `LapsThenRaceTime`;
  settable before start; setting it swaps the calculator to the matching
  instance.
- `IRaceOrderCalculator OrderCalculator { get; }` — read-only, kept in sync
  with `OrderRule`. Today both values map to the single default calculator.

The existing `IRaceRanker` / `BestLapRanker` stubs are untouched (see
Out of scope).

## Core layer — `RaceEngine`

New member:

```csharp
public event EventHandler<IReadOnlyList<RaceStandingsEntry>> RaceDataChanged;
```

Raised in `UpdateRaceStatus` **only when state actually changed** — a new
racer was added to `RaceDataCollection`, or a lap passed the
`MinLapIntervalMilliseconds` suppression check. Suppressed (ignored)
detections raise nothing. Raise order: mutate → calculate → invoke. Raised on
the serial-port thread. `RaceStart` and `Dispose` never raise.

The engine does not choose the rule; it uses whatever its `Race` carries.

## UI layer

### `RaceDataItemViewModel`

- `GapTime: TimeSpan` → `Gap: string`
- `IntervalTime: TimeSpan` → `Interval: string`
- `LastLapTime` / `BestLapTime` stay `TimeSpan` (the existing
  `TimeSpanFormatConverter` keeps formatting those columns).
- `Gap` / `Interval` are set verbatim by the mapper — all display semantics
  live in the calculator.

### `RaceDataListControl.xaml`

The two bindings rename (`GapTime` → `Gap`, `IntervalTime` → `Interval`) and
drop `TimeSpanFormatConverter` (values arrive pre-formatted).

### `RacePageViewModel`

- Captures `Dispatcher.CurrentDispatcher` in its constructor (constructed on
  the UI thread in `MainForm`).
- `StartRace()`: unsubscribe the previous engine's `RaceDataChanged`, then
  subscribe the new engine **before** calling `RaceStart` (safe ordering).
- `OnRaceDataChanged`: if not on the dispatcher thread → `BeginInvoke` and
  return; otherwise apply directly. (On test threads `CheckAccess()` is true,
  so tests apply without pumping.)
- Apply logic: for each entry, find an existing `RaceDataItemViewModel` whose
  `TransponderID` matches; if absent, create via the existing
  `(IDriver, ICar, int startPosition)` ctor with start position
  `RaceDataItems.Count + 1` and add to the collection; then set
  `RacerPosition`, `LapsCount`, `LastLapTime`, `BestLapTime`, `Gap`, `Interval`.
  `RaiseIfChanged` in the item VM suppresses no-op UI refreshes.
- `RacerNumber` and `Description` stay at defaults — nothing in the domain
  supplies them.
- `AddTestData()` remains, its five rows' `GapTime`/`IntervalTime`
  initializers updated to string `Gap`/`Interval` values. Fake transponder
  IDs ("88156" etc.) cannot collide with real ones, so find-or-create stays
  safe across race restarts.

## Error handling

- The serial-thread path in the ViewModel handler only performs
  `CheckAccess`/`BeginInvoke` — no user code that can throw there. Mapping
  runs on the UI thread under WPF's normal exception handling.
- `RaceDataChanged` handlers run after race state is durably updated, so a
  UI-side failure cannot corrupt lap data.

## Testing

New tests in `TrackGenius.UITests` (NUnit, `Given…_When…_Then…` naming):

- **`Model/LapsRaceTimeOrderCalculatorTests`** — empty collection; single
  racer (leader dashes, `"+0"`-style never appears); laps-desc-then-time
  ordering; equal-lap time gap vs racer-in-front and vs leader;
  different-lap-count display (`"+1 Lap"`, `"+3 Laps"`, pluralization);
  zero-lap racer (laps vs lapped racers, `0:00.000` vs a fellow zero-lap
  racer); time format `m:ss.fff`.
- **`Core/RaceEngineTests`** — engine wired as in `RaceEngineFactoryTests`
  (`FakeSerialPortWrapper` + `NullLogger`); drive a `RobitronicMessageConsumer`'s
  `CarDetected`; assert `RaceDataChanged` fires with the expected entry (new
  racer, lap count, position) and stays silent for a suppressed too-short
  interval.
- **`ViewModels/RacePageViewModelTests`** — start a race via the factory with
  the fake port open, feed standings through the engine, assert
  `RaceDataItems` gained a row with mapped fields, including string
  `Gap`/`Interval`.

## Out of scope

- Order-rule selector UI (domain plumbing only; `Race.OrderRule` is the
  future hook).
- Removing or rewiring `IRaceRanker` / `BestLapRanker` stubs.
- The pre-existing interval-suppression quirk
  (`GetLastDetectedTimeSpan().Milliseconds` reads the sub-second component of
  the last lap time rather than the absolute detection time) — this change
  surfaces its output but does not alter it.
- `RacerNumber` / `Description` population.
