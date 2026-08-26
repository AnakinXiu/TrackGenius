# Lap Analysis Metrics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add five lap-analysis metrics (Top 5 Average, Top 10 Average, Top 3 Consecutive, Std. Deviation, Consistency) to the race standings grid, computed in the Model layer and displayed as hideable columns hidden by default.

**Architecture:** A new static `LapAnalysisCalculator` in `TrackGenius.Model` provides pure math functions over lap durations; `LapsRaceTimeOrderCalculator` formats them into five new `string` fields on `RaceStandingsEntry`; the ViewModel maps them onto `RaceDataItemViewModel`; `RaceDataListControl` renders five new columns gated by five new `RaceDataColumnSettings` options (hidden by default, persistence-compatible).

**Tech Stack:** .NET 8 WPF, NUnit via existing `TrackGenius.UITests` (103 passing baseline). No new packages.

**Spec:** `docs/superpowers/specs/2026-08-26-lap-analysis-metrics-design.md`

## Global Constraints

- Branch `RaceUIImprove`; work from repo root `E:\source\repo\TrackGenius`; solution `source/TrackGenius.sln`.
- Test gate: `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj` — baseline **103 passed / 0 failed**; after each task the suite total grows as noted per task.
- Build gate: `dotnet build source/TrackGenius.sln` 0 errors (NU1900/MSB3243/MSB3245 warnings are pre-existing infra noise — ignore).
- **Metric definitions are binding (user-verified 2026-08-26):**
  - Top N Average = arithmetic mean of the N **fastest** lap durations; **null when fewer than N laps** (no partial averaging).
  - Top 3 Consecutive = fastest 3-lap **consecutive** (in lap order) window; null when fewer than 3 laps; display `"{sum:m:ss.fff} ({avg:ss.fff})"`.
  - Std. Deviation = **population** σ (divide by n), μ = mean of all laps; null when fewer than 2 laps; display seconds with 3 decimals (`0.158`).
  - Consistency = `(1 − σ/μ) × 100`, same σ/μ as the σ column; null when fewer than 2 laps; clamp at 0 (never negative); display `99.2` with `%` suffix, 1 decimal.
  - All display strings invariant culture; `"-"` when null (matches Gap/Interval leader convention).
  - `μ == 0` guard: σ/consistency return null rather than dividing by zero.
- Display formats exactly: Top averages `m:ss.fff` (e.g. `20.120s` laps render as `0:20.120`); Top3 sum `m:ss.fff`, Top3 avg `ss.fff` inside parentheses (e.g. `1:00.000 (20.000)`).
- New columns: keys `Top5Average`, `Top10Average`, `Top3Consecutive`, `StdDeviation`, `Consistency`; labels `Top 5`, `Top 10`, `Top 3 Consec`, `Std Dev`, `Consistency`; hideable, **`isVisible: false` default**; inserted after `BestLap` in `All` and after the Best Lap column in XAML.
- New Model file: file-scoped `namespace TrackGenius.Model;`. Tests: file-scoped `TrackGenius.UITests.…`, NUnit, `Given…_When…_Then…` names.
- Never commit `CLAUDE.md`; stage explicit paths only, never `git add -A`. Commit trailer `Co-Authored-By: Claude <noreply@anthropic.com>`.
- If `MSB3021`/`MSB3027` (app running locks output): `taskkill //IM TrackGenius.UI.exe //F`, retry, else BLOCKED.
- Tasks 1–3 must not launch the GUI. Task 4 (verification) launches it.

---

## Task 1: `LapAnalysisCalculator` math functions (TDD)

**Files:**
- Create: `source/TrackGenius.Model/Race/LapAnalysisCalculator.cs`
- Test: `source/TrackGenius.UITests/Model/LapAnalysisCalculatorTests.cs`

**Interfaces (produced; consumed by Tasks 2–3):**

```csharp
// TrackGenius.Model — static class, pure functions over lap durations in lap order.
public static TimeSpan? TopAverage(IReadOnlyList<TimeSpan> laps, int count);      // mean of `count` fastest; null if laps.Count < count
public static TimeSpan? Top3Consecutive(IReadOnlyList<TimeSpan> laps);            // fastest 3-consecutive-window SUM; null if < 3 laps
public static double? StdDeviation(IReadOnlyList<TimeSpan> laps);                 // population σ in SECONDS; null if < 2 laps or μ==0
public static double? Consistency(IReadOnlyList<TimeSpan> laps);                  // (1 − σ/μ)×100 percent; null if < 2 laps or μ==0; clamped ≥ 0
```

- [ ] **Step 1: Write the failing tests**

`source/TrackGenius.UITests/Model/LapAnalysisCalculatorTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrackGenius.Model;

namespace TrackGenius.UITests.Model;

[TestFixture]
public class LapAnalysisCalculatorTests
{
    private static IReadOnlyList<TimeSpan> Laps(params double[] seconds)
        => seconds.Select(s => TimeSpan.FromSeconds(s)).ToList();

    [Test]
    public void GivenFivePlusLaps_WhenTopAverage5_ThenMeanOfFiveFastest()
    {
        var laps = Laps(20.1, 19.8, 20.5, 19.9, 20.3, 21.0);

        var result = LapAnalysisCalculator.TopAverage(laps, 5);

        Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(20.12)));
    }

    [Test]
    public void GivenFewerThanFiveLaps_WhenTopAverage5_ThenNull()
    {
        var laps = Laps(20.1, 19.8, 20.5, 19.9);

        Assert.That(LapAnalysisCalculator.TopAverage(laps, 5), Is.Null);
    }

    [Test]
    public void GivenTenPlusLaps_WhenTopAverage10_ThenMeanOfTenFastest()
    {
        var laps = Laps(20.0, 20.1, 19.9, 20.2, 20.0, 20.3, 19.8, 20.4, 19.9, 20.0, 21.5);

        var result = LapAnalysisCalculator.TopAverage(laps, 10);

        Assert.That(result!.Value.TotalSeconds, Is.EqualTo(20.06).Within(0.0001));
    }

    [Test]
    public void GivenFewerThanTenLaps_WhenTopAverage10_ThenNull()
    {
        Assert.That(LapAnalysisCalculator.TopAverage(Laps(20, 20, 20, 20, 20, 20, 20, 20, 20), 10), Is.Null);
    }

    [Test]
    public void GivenConsecutiveWindow_WhenTop3Consecutive_ThenFastestWindowSum()
    {
        // Windows: 60.2, 60.0, 61.2 → fastest = laps 2-4 = 60.0.
        var laps = Laps(20.5, 19.8, 19.9, 20.3, 21.0);

        var result = LapAnalysisCalculator.Top3Consecutive(laps);

        Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(60.0)));
    }

    [Test]
    public void GivenExactlyThreeLaps_WhenTop3Consecutive_ThenWholeRaceSum()
    {
        Assert.That(LapAnalysisCalculator.Top3Consecutive(Laps(20, 21, 22)), Is.EqualTo(TimeSpan.FromSeconds(63)));
    }

    [Test]
    public void GivenFewerThanThreeLaps_WhenTop3Consecutive_ThenNull()
    {
        Assert.That(LapAnalysisCalculator.Top3Consecutive(Laps(20, 21)), Is.Null);
    }

    [Test]
    public void GivenLaps_WhenStdDeviation_ThenPopulationSigmaInSeconds()
    {
        // μ = 20.1; deviations 0.1 each → σ = 0.1 (population: 0.04/4 = 0.01, √ = 0.1).
        var result = LapAnalysisCalculator.StdDeviation(Laps(20.0, 20.4, 19.8, 20.2));

        Assert.That(result, Is.EqualTo(0.1).Within(0.0001));
    }

    [Test]
    public void GivenIdenticalLaps_WhenStdDeviation_ThenZero()
    {
        Assert.That(LapAnalysisCalculator.StdDeviation(Laps(20, 20, 20, 20)), Is.EqualTo(0).Within(0.0001));
    }

    [Test]
    public void GivenSingleLap_WhenStdDeviation_ThenNull()
    {
        Assert.That(LapAnalysisCalculator.StdDeviation(Laps(20.0)), Is.Null);
    }

    [Test]
    public void GivenLaps_WhenConsistency_ThenOneMinusSigmaOverMuPercent()
    {
        // μ = 20.1, σ = 0.1 → (1 − 0.1/20.1) × 100 = 99.5025.
        var result = LapAnalysisCalculator.Consistency(Laps(20.0, 20.4, 19.8, 20.2));

        Assert.That(result, Is.EqualTo(99.5025).Within(0.0001));
    }

    [Test]
    public void GivenIdenticalLaps_WhenConsistency_Then100()
    {
        Assert.That(LapAnalysisCalculator.Consistency(Laps(20, 20, 20)), Is.EqualTo(100).Within(0.0001));
    }

    [Test]
    public void GivenExtremeSpread_WhenConsistency_ThenClampedAtZero()
    {
        // μ = 10, σ = 30 → 1 − 3 = −2 → clamped to 0.
        Assert.That(LapAnalysisCalculator.Consistency(Laps(40, 40, -20, 0)), Is.EqualTo(0).Within(0.0001));
    }
}
```

*(Note on the clamp test: `Laps(40, 40, -20, 0)` contains negative lap durations, which real races cannot produce — it exists purely to exercise the clamp branch. If you prefer realism, replace with `Laps(1, 1, 28, 30)`: μ = 15, σ ≈ 13.96 → 1 − 0.9306 = 6.9% — that does NOT trip the clamp, so keep the synthetic negative test for the clamp and this realistic one as an extra assertion if desired. Keep the synthetic test as written.)*

- [ ] **Step 2: Run to verify they fail**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~LapAnalysisCalculatorTests"
```
Expected: compile error CS0103/CS0246 — `LapAnalysisCalculator` does not exist.

- [ ] **Step 3: Implement**

`source/TrackGenius.Model/Race/LapAnalysisCalculator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TrackGenius.Model;

/// <summary>
/// Pure lap-analysis math over completed lap durations (in lap order).
/// Null return = not enough data for the metric (displayed as "-").
/// </summary>
public static class LapAnalysisCalculator
{
    /// <summary>Mean of the <paramref name="count"/> fastest laps; null when fewer than count laps.</summary>
    public static TimeSpan? TopAverage(IReadOnlyList<TimeSpan> laps, int count)
    {
        if (laps is null || laps.Count < count)
            return null;

        var fastest = laps.OrderBy(lap => lap).Take(count);
        return new TimeSpan((long)fastest.Average(lap => lap.Ticks));
    }

    /// <summary>Fastest 3-consecutive-lap window sum; null when fewer than 3 laps.</summary>
    public static TimeSpan? Top3Consecutive(IReadOnlyList<TimeSpan> laps)
    {
        if (laps is null || laps.Count < 3)
            return null;

        TimeSpan? best = null;
        for (var i = 0; i + 2 < laps.Count; i++)
        {
            var window = laps[i] + laps[i + 1] + laps[i + 2];
            if (best is null || window < best)
                best = window;
        }

        return best;
    }

    /// <summary>Population standard deviation in seconds; null when fewer than 2 laps or zero mean.</summary>
    public static double? StdDeviation(IReadOnlyList<TimeSpan> laps)
    {
        if (laps is null || laps.Count < 2)
            return null;

        var meanSeconds = laps.Average(lap => lap.TotalSeconds);
        if (meanSeconds == 0)
            return null;

        var variance = laps.Average(lap =>
        {
            var delta = lap.TotalSeconds - meanSeconds;
            return delta * delta;
        });

        return Math.Sqrt(variance);
    }

    /// <summary>Consistency as (1 − σ/μ) × 100 percent, clamped at 0; null when σ is null.</summary>
    public static double? Consistency(IReadOnlyList<TimeSpan> laps)
    {
        var sigma = StdDeviation(laps);
        if (sigma is null)
            return null;

        var meanSeconds = laps.Average(lap => lap.TotalSeconds);
        var consistency = (1 - sigma.Value / meanSeconds) * 100;
        return Math.Max(0, consistency);
    }
}
```

- [ ] **Step 4: Run to verify pass**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~LapAnalysisCalculatorTests"
```
Expected: 12 passed (suite total 115).

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius.Model/Race/LapAnalysisCalculator.cs source/TrackGenius.UITests/Model/LapAnalysisCalculatorTests.cs
git commit -m "Add lap analysis calculator with top-N, consecutive, deviation and consistency math

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 2: `RaceStandingsEntry` fields + calculator formatting (TDD)

**Files:**
- Modify: `source/TrackGenius.Model/Race/RaceStandingsEntry.cs`
- Modify: `source/TrackGenius.Model/Race/LapsRaceTimeOrderCalculator.cs`
- Test: `source/TrackGenius.UITests/Model/LapsRaceTimeOrderCalculatorTests.cs` (append)

**Interfaces:**
- Consumes: Task 1's `LapAnalysisCalculator` functions.
- Produces: `RaceStandingsEntry` record gains five `string` fields (after `Interval`): `Top5Average`, `Top10Average`, `Top3Consecutive`, `StdDeviation`, `Consistency` — pre-formatted, `"-"` when the math returns null. Task 3's VM mapping consumes these exact names.

- [ ] **Step 1: Write the failing tests** — append to `LapsRaceTimeOrderCalculatorTests`:

```csharp
    [Test]
    public void GivenEnoughLaps_WhenCalculate_ThenAnalysisFieldsFormatted()
    {
        // 10 laps: 20.0..20.9 → fastest 5 = 20.0,20.1,20.2,20.3,20.4 → avg 20.2;
        // fastest 10 avg = 20.45; windows all ≈ 60.x, fastest = laps 1-3 = 60.3;
        // μ = 20.45, population σ of 0.1..0.9 spread; consistency per formula.
        var driver = AnonymousDriverCreator.CreateAnonymous("100");
        var raceData = new RaceData(driver, driver.Cars.First());
        for (var tenth = 0; tenth < 10; tenth++)
            raceData.RecordDetection(TimeSpan.FromSeconds(20 + tenth / 10.0));

        var entry = new LapsRaceTimeOrderCalculator()
            .Calculate(new List<RaceData> { raceData })[0];

        Assert.Multiple(() =>
        {
            Assert.That(entry.Top5Average, Is.EqualTo("0:20.200"));
            Assert.That(entry.Top10Average, Is.EqualTo("0:20.450"));
            Assert.That(entry.Top3Consecutive, Is.EqualTo("1:00.300 (20.100)"));
            Assert.That(entry.StdDeviation, Is.EqualTo("0.287"));
            Assert.That(entry.Consistency, Is.EqualTo("98.6"));
        });
    }

    [Test]
    public void GivenTwoLaps_WhenCalculate_ThenTopNAveragesDashButSigmaPresent()
    {
        var driver = AnonymousDriverCreator.CreateAnonymous("100");
        var raceData = new RaceData(driver, driver.Cars.First());
        raceData.RecordDetection(TimeSpan.FromSeconds(20.0));
        raceData.RecordDetection(TimeSpan.FromSeconds(20.4));

        var entry = new LapsRaceTimeOrderCalculator()
            .Calculate(new List<RaceData> { raceData })[0];

        Assert.Multiple(() =>
        {
            Assert.That(entry.Top5Average, Is.EqualTo("-"));
            Assert.That(entry.Top10Average, Is.EqualTo("-"));
            Assert.That(entry.Top3Consecutive, Is.EqualTo("-"));
            Assert.That(entry.StdDeviation, Is.EqualTo("0.200"));
            Assert.That(entry.Consistency, Is.EqualTo("99.0"));
        });
    }

    [Test]
    public void GivenSingleLap_WhenCalculate_ThenAllAnalysisFieldsDash()
    {
        var driver = AnonymousDriverCreator.CreateAnonymous("100");
        var raceData = new RaceData(driver, driver.Cars.First());
        raceData.RecordDetection(TimeSpan.FromSeconds(20.0));

        var entry = new LapsRaceTimeOrderCalculator()
            .Calculate(new List<RaceData> { raceData })[0];

        Assert.Multiple(() =>
        {
            Assert.That(entry.Top5Average, Is.EqualTo("-"));
            Assert.That(entry.Top10Average, Is.EqualTo("-"));
            Assert.That(entry.Top3Consecutive, Is.EqualTo("-"));
            Assert.That(entry.StdDeviation, Is.EqualTo("-"));
            Assert.That(entry.Consistency, Is.EqualTo("-"));
        });
    }
```

*Expected-value derivation (verify by hand before trusting): laps 20.0…20.9 — Top5 = avg(20.0,20.1,20.2,20.3,20.4) = 20.20; Top10 = avg(20.0..20.9) = 20.45; 3-windows: (20.0+20.1+20.2)=60.3, (20.1+20.2+20.3)=60.6, … strictly increasing → best = 60.3, avg 20.1; μ = 20.45, deviations ±{0.45,0.35,0.25,0.15,0.05,0.05,0.15,0.25,0.35,0.45} → Σd² = 2×(0.2025+0.1225+0.0625+0.0225+0.0025) = 2×0.4125 = 0.825 → σ² = 0.0825 → σ = 0.28723 → "0.287"; consistency = (1 − 0.28723/20.45)×100 = 98.5957 → "98.6". The test's `TimeSpan.FromSeconds(20 + tenth/10.0)` construction introduces float rounding; if `0:20.200`/`1:00.300` fail by one millisecond tick, switch the loop to `TimeSpan.FromMilliseconds(20000 + tenth * 100)` — use that form from the start if you prefer exactness.*

- [ ] **Step 2: Run to verify they fail**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~LapsRaceTimeOrderCalculatorTests"
```
Expected: compile error — `RaceStandingsEntry` has no `Top5Average` etc.

- [ ] **Step 3: Implement**

`RaceStandingsEntry.cs` — add after `Interval` in the positional record:

```csharp
    string Top5Average,
    string Top10Average,
    string Top3Consecutive,
    string StdDeviation,
    string Consistency);
```

`LapsRaceTimeOrderCalculator.cs` — extend the entry construction in `Calculate`:

```csharp
            var laps = racer.LapRecords.Select(record => record.LapTime).ToList();
            entries.Add(new RaceStandingsEntry(
                RaceData: racer,
                Position: index + 1,
                BestLapTime: BestLap(racer),
                LastLapTime: LastLap(racer),
                Gap: DescribeDifference(index == 0 ? null : ordered[0], racer),
                Interval: DescribeDifference(index == 0 ? null : ordered[index - 1], racer),
                Top5Average: FormatTime(LapAnalysisCalculator.TopAverage(laps, 5)),
                Top10Average: FormatTime(LapAnalysisCalculator.TopAverage(laps, 10)),
                Top3Consecutive: FormatTop3(LapAnalysisCalculator.Top3Consecutive(laps)),
                StdDeviation: FormatSigma(LapAnalysisCalculator.StdDeviation(laps)),
                Consistency: FormatConsistency(LapAnalysisCalculator.Consistency(laps))));
```

And add these helpers:

```csharp
    private const string LapTimeFormat = @"m\:ss\.fff";

    private static string FormatTime(TimeSpan? value)
        => value?.ToString(LapTimeFormat, CultureInfo.InvariantCulture) ?? "-";

    private static string FormatTop3(TimeSpan? windowSum)
        => windowSum is null
            ? "-"
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0} ({1})",
                windowSum.Value.ToString(LapTimeFormat, CultureInfo.InvariantCulture),
                (windowSum.Value / 3).ToString(@"ss\.fff", CultureInfo.InvariantCulture));

    private static string FormatSigma(double? sigma)
        => sigma?.ToString("0.000", CultureInfo.InvariantCulture) ?? "-";

    private static string FormatConsistency(double? consistency)
        => consistency?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-";
```

- [ ] **Step 4: Run to verify pass**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~LapsRaceTimeOrderCalculatorTests"
```
Expected: all calculator tests pass (suite total 118).

- [ ] **Step 5: Run the full suite (engine/VM tests construct entries too — the record gained required params)**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: 118 passed. If `RaceEngineTests`/`RacePageViewModelTests` fail to compile (they don't construct `RaceStandingsEntry` directly — they consume it — no edits should be needed; if any test DOES construct it positionally, add the five new values in order).

- [ ] **Step 6: Commit**

```bash
git add source/TrackGenius.Model/Race/RaceStandingsEntry.cs source/TrackGenius.Model/Race/LapsRaceTimeOrderCalculator.cs source/TrackGenius.UITests/Model/LapsRaceTimeOrderCalculatorTests.cs
git commit -m "Carry formatted lap analysis metrics on standings entries

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 3: UI — item VM properties, column settings, grid columns

**Files:**
- Modify: `source/TrackGenius/ViewModels/RaceDataItemViewModel.cs`
- Modify: `source/TrackGenius/ViewModels/RacePageViewModel.cs` (ApplyStandings)
- Modify: `source/TrackGenius/ViewModels/RaceDataColumnSettings.cs`
- Modify: `source/TrackGenius/Views/Controls/RaceDataListControl.xaml`
- Test: `source/TrackGenius.UITests/ViewModels/RaceDataColumnSettingsTests.cs` (update + append)
- Test: `source/TrackGenius.UITests/ViewModels/RacePageViewModelTests.cs` (append)

**Interfaces:**
- Consumes: Task 2's `RaceStandingsEntry.Top5Average/Top10Average/Top3Consecutive/StdDeviation/Consistency`.
- Produces: five `string` properties on `RaceDataItemViewModel` (same names as entry fields); five column options on `RaceDataColumnSettings` (properties `Top5Average`, `Top10Average`, `Top3Consecutive`, `StdDeviation`, `Consistency`); XAML columns at grid columns 8–12 (Transponder→13, Notes→14), all visibility-gated.

- [ ] **Step 1: Update the column-settings tests (RED)**

In `RaceDataColumnSettingsTests.cs`:

Replace `GivenDefaultSettings_WhenCreated_ThenAllTenColumnsVisibleAndPositionNonHideable` with:

```csharp
    [Test]
    public void GivenDefaultSettings_WhenCreated_ThenExistingColumnsVisibleAndAnalysisHidden()
    {
        var settings = new RaceDataColumnSettings();

        Assert.That(settings.All, Has.Count.EqualTo(15));
        Assert.That(settings.All[0], Is.SameAs(settings.Position));
        Assert.That(settings.Position.CanHide, Is.False);
        Assert.That(settings.All.Skip(1), Has.All.Property("CanHide").EqualTo(true));
        foreach (var option in settings.All.Where(o => !IsAnalysisColumn(o)))
            Assert.That(option.IsVisible, Is.True, $"{o.Key} should default visible");

        Assert.Multiple(() =>
        {
            Assert.That(settings.Top5Average.IsVisible, Is.False);
            Assert.That(settings.Top10Average.IsVisible, Is.False);
            Assert.That(settings.Top3Consecutive.IsVisible, Is.False);
            Assert.That(settings.StdDeviation.IsVisible, Is.False);
            Assert.That(settings.Consistency.IsVisible, Is.False);
        });
    }

    private static bool IsAnalysisColumn(RaceDataColumnOption option)
        => option.Key is "Top5Average" or "Top10Average" or "Top3Consecutive" or "StdDeviation" or "Consistency";
```

Append:

```csharp
    [Test]
    public void GivenAnalysisColumnShown_WhenRoundTripped_ThenStaysShown()
    {
        var settings = new RaceDataColumnSettings();
        settings.Top5Average.IsVisible = true;
        settings.Consistency.IsVisible = true;

        var roundTripped = new RaceDataColumnSettings();
        roundTripped.ApplyHiddenKeys(settings.GetHiddenKeys());

        Assert.Multiple(() =>
        {
            Assert.That(roundTripped.Top5Average.IsVisible, Is.True);
            Assert.That(roundTripped.Consistency.IsVisible, Is.True);
            Assert.That(roundTripped.Top10Average.IsVisible, Is.False);
        });
    }
```

- [ ] **Step 2: Run to verify they fail**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RaceDataColumnSettingsTests"
```
Expected: compile error — no `Top5Average` on `RaceDataColumnSettings`; count 15 vs actual 10.

- [ ] **Step 3: Implement column settings**

`RaceDataColumnSettings.cs` — add properties after `BestLap`:

```csharp
    public RaceDataColumnOption Top5Average { get; }
    public RaceDataColumnOption Top10Average { get; }
    public RaceDataColumnOption Top3Consecutive { get; }
    public RaceDataColumnOption StdDeviation { get; }
    public RaceDataColumnOption Consistency { get; }
```

Constructor additions after the `BestLap` line:

```csharp
        Top5Average = new RaceDataColumnOption("Top5Average", "Top 5", canHide: true, isVisible: false);
        Top10Average = new RaceDataColumnOption("Top10Average", "Top 10", canHide: true, isVisible: false);
        Top3Consecutive = new RaceDataColumnOption("Top3Consecutive", "Top 3 Consec", canHide: true, isVisible: false);
        StdDeviation = new RaceDataColumnOption("StdDeviation", "Std Dev", canHide: true, isVisible: false);
        Consistency = new RaceDataColumnOption("Consistency", "Consistency", canHide: true, isVisible: false);
```

And `All` becomes (order binds XAML defaults and settings UI):

```csharp
        All = new List<RaceDataColumnOption>
        {
            Position, CarNumber, Driver, Laps, Gap, Interval, LastLap, BestLap,
            Top5Average, Top10Average, Top3Consecutive, StdDeviation, Consistency,
            Transponder, Notes
        };
```

- [ ] **Step 4: Run column-settings tests (GREEN)**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RaceDataColumnSettingsTests"
```
Expected: all pass.

- [ ] **Step 5: Item VM + ApplyStandings mapping + VM test (RED)**

`RaceDataItemViewModel.cs` — after the `Interval` property:

```csharp
    private string _top5Average;
    private string _top10Average;
    private string _top3Consecutive;
    private string _stdDeviation;
    private string _consistency;

    public string Top5Average
    {
        get => _top5Average;
        set => PropertyChanged.RaiseIfChanged(this, ref _top5Average, value, nameof(Top5Average));
    }

    public string Top10Average
    {
        get => _top10Average;
        set => PropertyChanged.RaiseIfChanged(this, ref _top10Average, value, nameof(Top10Average));
    }

    public string Top3Consecutive
    {
        get => _top3Consecutive;
        set => PropertyChanged.RaiseIfChanged(this, ref _top3Consecutive, value, nameof(Top3Consecutive));
    }

    public string StdDeviation
    {
        get => _stdDeviation;
        set => PropertyChanged.RaiseIfChanged(this, ref _stdDeviation, value, nameof(StdDeviation));
    }

    public string Consistency
    {
        get => _consistency;
        set => PropertyChanged.RaiseIfChanged(this, ref _consistency, value, nameof(Consistency));
    }
```

(Backing fields go with the other fields near `_gap`/`_interval`.)

`RacePageViewModel.ApplyStandings` — after `item.Interval = entry.Interval;`:

```csharp
            item.Top5Average = entry.Top5Average;
            item.Top10Average = entry.Top10Average;
            item.Top3Consecutive = entry.Top3Consecutive;
            item.StdDeviation = entry.StdDeviation;
            item.Consistency = entry.Consistency;
```

Append to `RacePageViewModelTests.cs`:

```csharp
    [Test]
    public void GivenStartedRace_WhenEnoughLapsDetected_ThenAnalysisFieldsDisplayed()
    {
        _connection.Open("COM3", new RobitronicProtocol());
        _viewModel.StartRaceCommand.Execute(null);

        // 5 laps of one transponder, 20.0s..20.4s crossings (whole-second-free to dodge the suppression quirk):
        // laps: 20.0, 20.1, 20.2, 20.3, 20.4 → Top5 avg = 20.2; window sums 60.3, 60.6, 60.9 → best (20.100).
        int[] crossingsMs = { 20_000, 40_100, 60_300, 80_600, 101_000 };
        foreach (var ms in crossingsMs)
            _communicateService.MessageReceived?.Invoke(
                _communicateService, MakeDetectedMessage(transponder: 100, milliseconds: ms));

        var row = _viewModel.RaceDataItems.Single(item => item.TransponderID == "100");
        Assert.Multiple(() =>
        {
            Assert.That(row.Top5Average, Is.EqualTo("0:20.200"));
            Assert.That(row.Top10Average, Is.EqualTo("-"));
            Assert.That(row.Top3Consecutive, Is.EqualTo("1:00.300 (20.100)"));
            Assert.That(row.StdDeviation, Is.Not.EqualTo("-"));
            Assert.That(row.Consistency, Is.Not.EqualTo("-"));
        });
    }
```

*(Crossing choice: gaps between crossings are 20.0s, 20.2s, 20.3s, 20.4s — wait: 20 000→40 100 = 20.1s; 40 100→60 300 = 20.2s; 60 300→80 600 = 20.3s; 80 600→101 000 = 20.4s. Laps = {20.1, 20.2, 20.3, 20.4}s — that's only 4 laps after the first crossing forms lap 1 = 20.1s… no: the FIRST crossing creates lap 1 with duration = crossing − 0 = 20.0s only if the race clock starts at 0 — RecordDetection treats raceTime as absolute and lap 1 = raceTime − 0. So laps = {20.0, 20.1, 20.2, 20.3, 20.4}. Top5 = avg = 20.2 ✓. Best 3-window = 20.0+20.1+20.2 = 60.3, avg 20.1 ✓. All crossings differ by ≥ 20 000 ms ≫ 1500 ms MinLapInterval ✓.)*

- [ ] **Step 6: Run to verify RED for the VM test, then GREEN after mapping**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RacePageViewModelTests"
```
Order: write test → run (fails: no `Top5Average` on item VM / values not mapped) → apply the Step 5 implementation → run again (passes). If you implemented Step 5's code before running RED, that is a process deviation — note it in the report.

- [ ] **Step 7: XAML columns**

`RaceDataListControl.xaml` — the grid currently has 10 columns (indices 0–9; Transponder=8, Notes=9). Add 5 `<ColumnDefinition>` entries between BestLap (7) and Transponder, in BOTH the header grid and the row grid:

```xml
<ColumnDefinition Width="{Binding ColumnSettings.Top5Average.IsVisible, Converter={StaticResource BoolToGridLength}, ConverterParameter=7, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
<ColumnDefinition Width="{Binding ColumnSettings.Top10Average.IsVisible, Converter={StaticResource BoolToGridLength}, ConverterParameter=7, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
<ColumnDefinition Width="{Binding ColumnSettings.Top3Consecutive.IsVisible, Converter={StaticResource BoolToGridLength}, ConverterParameter=12, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
<ColumnDefinition Width="{Binding ColumnSettings.StdDeviation.IsVisible, Converter={StaticResource BoolToGridLength}, ConverterParameter=6, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
<ColumnDefinition Width="{Binding ColumnSettings.Consistency.IsVisible, Converter={StaticResource BoolToGridLength}, ConverterParameter=8, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
```

Header TextBlocks after "Best Lap" (Grid.Column 8–12; renumber Transponder→13, Notes→14):

```xml
<TextBlock Grid.Column="8" FontSize="16" FontWeight="SemiBold" Style="{StaticResource RaceHeaderTextStyle}" Text="Top 5" Margin="12,0,0,0"
           Visibility="{Binding ColumnSettings.Top5Average.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
<TextBlock Grid.Column="9" FontSize="16" FontWeight="SemiBold" Style="{StaticResource RaceHeaderTextStyle}" Text="Top 10" Margin="12,0,0,0"
           Visibility="{Binding ColumnSettings.Top10Average.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
<TextBlock Grid.Column="10" FontSize="16" FontWeight="SemiBold" Style="{StaticResource RaceHeaderTextStyle}" Text="Top 3 Consec" Margin="12,0,0,0"
           Visibility="{Binding ColumnSettings.Top3Consecutive.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
<TextBlock Grid.Column="11" FontSize="16" FontWeight="SemiBold" Style="{StaticResource RaceHeaderTextStyle}" Text="Std Dev" Margin="12,0,0,0"
           Visibility="{Binding ColumnSettings.StdDeviation.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
<TextBlock Grid.Column="12" FontSize="16" FontWeight="SemiBold" Style="{StaticResource RaceHeaderTextStyle}" Text="Consistency" Margin="12,0,0,0"
           Visibility="{Binding ColumnSettings.Consistency.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
```

Row cells in the DataTemplate (same pattern, `FontSize="16" VerticalAlignment="Center" Margin="12,0,0,0"`, bindings `Top5Average`/`Top10Average`/`Top3Consecutive`/`StdDeviation`/`Consistency`, columns 8–12), and update the two existing cells: Transponder `Grid.Column="13"`, Notes `Grid.Column="14"`. Also update the ProgressBar `Grid.ColumnSpan` from 10 to 15.

- [ ] **Step 8: Build + full suite**

```bash
dotnet build source/TrackGenius.sln
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: 0 errors; 120 passed (118 + 2 new column-settings tests; the VM analysis test replaced nothing — recount: Task 1 +12, Task 2 +3, Task 3 +2 settings +1 VM = 121 total. Gate on "all pass", the exact count per your run).

- [ ] **Step 9: Commit**

```bash
git add source/TrackGenius/ViewModels/RaceDataItemViewModel.cs source/TrackGenius/ViewModels/RacePageViewModel.cs source/TrackGenius/ViewModels/RaceDataColumnSettings.cs source/TrackGenius/Views/Controls/RaceDataListControl.xaml source/TrackGenius.UITests/ViewModels/RaceDataColumnSettingsTests.cs source/TrackGenius.UITests/ViewModels/RacePageViewModelTests.cs
git commit -m "Add lap analysis columns to standings grid, hidden by default

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 4: Visual verification

**Files:** (temp only, not committed) `%TEMP%\tg-shots\metrics.png`, reuse `shot-window.ps1` if present (else recreate from Task 5 of the 2026-08-20 theme plan).

- [ ] **Step 1: Launch with analysis columns shown**

Temporarily flip the defaults is NOT wanted — instead set the persisted hidden-keys file so the new columns show: delete/rename `%LOCALAPPDATA%\TrackGenius\raceDataColumns.json`, then edit is unnecessary — simplest: run the app, enable the five columns via the column-settings UI if reachable; if no UI toggle exists for columns (check `QuickRacePage`/settings — the current app may not expose a column picker), then temporarily change the five `isVisible: false` to `true` in a scratch edit, launch, screenshot, and REVERT the scratch edit without committing.

- [ ] **Step 2: Screenshot + verify**

Launch (`dotnet run --project source/TrackGenius/TrackGenius.UI.csproj`), start a race with the Robitronic loopback or leave empty (empty grid still shows headers when columns enabled — headers alone verify column presence/labels; values need laps, so if no hardware/simulator available, verify values via the Task 3 VM test instead and screenshot only for header layout). Capture via `shot-window.ps1 -ProcId <pid>`. Check: five headers visible with correct labels, sensible widths, no layout breakage (Transponder/Notes shifted right correctly), Progress bar spans full width.

- [ ] **Step 3: Revert any scratch edits, restore `raceDataColumns.json` backup if renamed, final gates**

```bash
dotnet build source/TrackGenius.sln
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
git status --short   # must be clean (no scratch edits left)
```

- [ ] **Step 4: Report with screenshot path**

Return status + screenshot path for the controller/user review. Do not commit scratch artifacts.

---

## Final verification (after all tasks)

- [ ] `dotnet build source/TrackGenius.sln` — 0 errors.
- [ ] `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj` — all pass (121 expected; gate on green, not the count).
- [ ] `dotnet test source/TrackGenius.ProtocolTests/TrackGenius.ProtocolTests.csproj` — still exactly the 2 pre-existing failures.
- [ ] `git log --oneline` — three feature commits since `bdb9555`, clean tree.
- [ ] Screenshot reviewed; headers present; scratch edits reverted.

## Out of scope (do not do)

- Column-picker UI for the new columns (persistence file format already supports them).
- Driver-details panel, configurable N, fastest-half consistency, metric-based sorting.
