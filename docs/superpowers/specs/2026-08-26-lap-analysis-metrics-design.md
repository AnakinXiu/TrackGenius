# Lap Analysis Metrics Design

**Date:** 2026-08-26
**Status:** Approved (definitions verified by user 2026-08-26; layout/model-home defaults accepted)
**Depends on:** crossing-time lap computation (`f2068ff`) — lap times are true deltas.

## Problem

The standings grid shows position/laps/gap/interval/last/best only. Lap-analysis
metrics racers expect (and the design images show, e.g. "98.7%") are missing.

## Verified metric definitions (user-confirmed)

Data basis: a racer's completed laps `L = {L₁…Lₙ}`, each lap's **duration**
(`LapRecord.LapTime`, crossing-time delta), in lap order.

| Metric | Definition | Formula | Fewer than N laps |
|---|---|---|---|
| **Top 5 Average** | Mean of the 5 fastest laps (pace ceiling) | `avg(min 5 of L)` | empty (`"-"`) until 5 laps |
| **Top 10 Average** | Mean of the 10 fastest laps (sustainable pace) | `avg(min 10 of L)` | empty (`"-"`) until 10 laps |
| **Top 3 Consecutive** | Fastest 3-lap **consecutive** window (race pace) | `min over i of (Lᵢ+Lᵢ₊₁+Lᵢ₊₂)`; display `"{sum:mm:ss.fff} ({avg:ss.fff})"` | empty (`"-"`) until 3 laps |
| **Std. Deviation** | Spread of all completed laps (population) | `σ = √(Σ(Lᵢ−μ)²/n)`, μ = mean of all laps; display `0.000` (seconds, 3 decimals) | empty until 2 laps |
| **Consistency** | Stability as a percentage | `(1 − σ/μ) × 100`, same σ and μ as the Std. Deviation column; display `99.2%` (1 decimal) | empty until 2 laps |

User decisions: Top-N averages are **empty until N laps** (no partial
averaging); Top 3 Consecutive displays **sum (avg) combined**; std dev is
**population** (divide by n); consistency is **1 − σ/μ**.

Formatting: sums/averages in `mm:ss.fff`-family formats via the existing
`TimeSpanFormatConverter` pattern where the value is a TimeSpan; σ in seconds
with 3 decimals; consistency with `%` suffix, 1 decimal. All strings produced
in the Model layer (invariant culture), consistent with Gap/Interval.

## Architecture

Same pipeline as Gap/Interval — the metrics are computed in the Model layer,
carried on `RaceStandingsEntry`, mapped onto the item VM, rendered as hideable
columns:

```
LapsRaceTimeOrderCalculator.Calculate
  → LapAnalysisCalculator (new, static, TrackGenius.Model/Race/)
      .TopAverage(laps, 5) / .TopAverage(laps, 10) / .Top3Consecutive(laps)
      / .StdDev(laps) / .Consistency(laps)
  → RaceStandingsEntry gains 5 pre-formatted string fields
  → RacePageViewModel.ApplyStandings maps them onto RaceDataItemViewModel
  → RaceDataListControl: 5 new columns after Best Lap
  → RaceDataColumnSettings: 5 new hideable options (hidden by default)
```

- **LapAnalysisCalculator** (new static class): pure functions over
  `IReadOnlyList<TimeSpan>`. Returns `TimeSpan?` (null when not enough laps)
  for time metrics; `double?` for σ and consistency. Formatting to display
  strings happens at the `RaceStandingsEntry` construction site (calculator's
  `Format*` helpers), keeping the math functions unit-testable.
- **RaceStandingsEntry** (record): adds `Top5Average`, `Top10Average`,
  `Top3Consecutive`, `StdDeviation`, `Consistency` — all `string` (pre-
  formatted; `"-"` when null), mirroring Gap/Interval.
- **RaceDataItemViewModel**: adds the same five string properties with
  `RaiseIfChanged` setters.
- **RaceDataListControl**: five new grid columns (header + cell per column,
  each gated by its `ColumnSettings.*.IsVisible` via the existing
  BoolToGridLength/BoolToVis pattern), inserted after Best Lap.
- **RaceDataColumnSettings**: five new options — keys `Top5Average`,
  `Top10Average`, `Top3Consecutive`, `StdDeviation`, `Consistency`; labels
  "Top 5", "Top 10", "Top 3 Consec", "Std Dev", "Consistency"; hideable,
  **hidden by default** (`isVisible: false`). Persistence unchanged
  (`ApplyHiddenKeys` round-trip works because defaults are applied at
  construction and hidden-keys override).

## Error handling

- Null/insufficient laps → `"-"` (same convention as Gap/Interval leader dash).
- `μ == 0` (impossible with real laps; single lap of 0 ms) → consistency/σ
  guarded: return null rather than divide-by-zero.
- `σ/μ > 1` (theoretical) → consistency clamped at 0 (never negative).

## Testing

- `LapAnalysisCalculatorTests`: each metric — exact-value cases (worked
  examples from the definitions table), insufficient-laps → null, single-lap
  σ, equal-laps σ=0 → consistency 100.0%, formatting of Top3Consecutive
  combined string.
- `RaceDataColumnSettingsTests`: default-hidden for the five; round-trip
  through GetHiddenKeys/ApplyHiddenKeys keeps them hidden when shown-flag
  flips; existing 10-column test updated to 15.
- `RaceEngineTests`/`RacePageViewModelTests`: one standings snapshot test
  asserting the five fields flow end-to-end (`"-"` early race, values after
  enough laps).

## Out of scope

- Driver-details panel variants of these metrics (design images' right panel).
- Configurable N for Top-N averages; fastest-half consistency variant.
- Sorting rows by any new metric.
