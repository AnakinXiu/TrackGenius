# Race Data Notification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When `RaceEngine` accepts a car detection and updates its race data collection, it raises a standings-snapshot event that `RacePageViewModel` consumes to update `RaceDataItems` so the live race list refreshes in the UI.

**Architecture:** One new hop on the existing event pipeline: `MessageConsumer.CarDetected → RaceEngine.UpdateRaceStatus → Race.OrderCalculator.Calculate → RaceEngine.RaceDataChanged(snapshot) → RacePageViewModel (Dispatcher.BeginInvoke → find-or-create row by TransponderID → set fields)`. Model owns ranking and gap/interval display semantics; Core stays WPF-free; the ViewModel is a field mapper that owns thread marshaling.

**Tech Stack:** .NET 8 WPF (`net8.0` / `net8.0-windows`), NUnit 3.14 in the existing `TrackGenius.UITests` project (54 tests green baseline), hand-rolled `FakeSerialPortWrapper`. No new packages or projects.

**Spec:** `docs/superpowers/specs/2026-08-19-racedata-notification-design.md`

## Global Constraints

- Work from the **repo root** (`E:\source\repo\TrackGenius`); solution is `source/TrackGenius.sln`.
- Test gate: `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj` — baseline **54 passed / 0 failed**.
- **Pre-existing failures, out of scope:** `TrackGenius.ProtocolTests` has 2 failing `DetectedMessageTests.GivenCorrectMessageDataArray…` cases (stale expectations vs. the `DetectedMessage` parser). Do NOT fix them; do not treat them as regressions. Only UITests gates this plan.
- **Never commit `CLAUDE.md`. Stage explicit file paths only — never `git add -A`/`git add .`** (a prior commit on this branch accidentally swept in unrelated WIP).
- Commit messages end with: `Co-Authored-By: Claude <noreply@anthropic.com>`
- **Build lock:** if `dotnet build`/`test` fails with `MSB3021`/`MSB3027` (output locked — the app is running), report BLOCKED with that error; do not commit unverified code.
- Agents must not launch the GUI app; manual `dotnet run` verification is the user's.
- New Model files: file-scoped `namespace TrackGenius.Model;`. New test files: file-scoped `namespace TrackGenius.UITests.<Folder>;`, NUnit `[Test]`, names follow `Given…_When…_Then…`.
- No `<Nullable>` enable (CS8632 warnings are tolerated, matching existing code).
- Gap/Interval display (invariant culture): equal lap counts → time difference `m:ss.fff` (e.g. `"0:02.000"`); differing lap counts → `"+1 Lap"` / `"+3 Laps"` (signed `+`, singular at 1, plural otherwise; magnitude `Math.Abs`); leader → `"-"` for both.

## Spec clarifications discovered during planning (binding)

1. **Gap/Interval sign.** The spec prose says "front racer's race time − current racer's race time", but at equal lap counts the front racer crossed *earlier* (less race time), so that subtraction is negative. The display intent is the positive "behind by" value. Implement: **`current.RacedTime − reference.RacedTime`** (non-negative because the reference always ranks ahead; equal laps + equal time → `"0:00.000"`). Reference = racer in front for Gap, leader for Interval.
2. **`AnonymousDriverCreator` never sets `Car.Transponder`**, and no concrete `ITransponder` implementation exists in the repo. Consequence: `Race.GetRaceDataByTransponder` NREs on the *second* detection of any car, so the engine cannot work past one detection. Task 2 adds a `Transponder` class and sets it in the creator — required enabler, in scope.
3. **Test frame encoding.** `DetectedMessage` parses `TransponderID` from bytes 3–6 and `Milliseconds` from bytes 7–10, each as: bytes read in order, reversed, interpreted big-endian — i.e. the field's **little-endian** encoding. Tests build frames programmatically (Task 5's `MakeDetectedMessage`) so they never depend on the stale ProtocolTests fixtures.

## Pre-existing quirks this plan does NOT fix (work around in tests)

- `RaceEngine.UpdateRaceStatus` reads `raceData.GetLastDetectedTimeSpan().Milliseconds` — the **sub-second component** of the last lap time, not the absolute detection clock. Suppression therefore only triggers for small absolute timestamps. Keep this logic **verbatim**; tests use whole-second timestamps where the sub-second component is 0.
- `RaceData.RecordDetection` computes lap N's time as `raceTime − previousLapTime` (not `− previousRaceTime`), which drifts from lap 3 on. Calculator tests use at most 2 laps where the math is correct.
- `RacerNumber` / `Description` on `RaceDataItemViewModel` stay at defaults — nothing in the domain supplies them.

## Pre-flight (before Task 1)

- [ ] **Handle the user's uncommitted WIP.** `source/TrackGenius.Core/RaceEngine.cs` (subscriptions moved from ctor into `RaceStart`, `[NotNull]` annotations) and `source/TrackGenius/ViewModels/RacePageViewModel.cs` (`AddTestData()` call commented out) have uncommitted modifications. Tasks 5–6 edit these same files, so the WIP hunks would be swept into feature commits. Ask the user to commit the WIP first (suggested message: `Move engine subscriptions to RaceStart`), or get their explicit OK to fold it in. If folding in: keep every WIP hunk intact — do not revert any of it.
- [ ] **Confirm `AddTestData` restoration.** The WIP comments the call out; the spec decision is "keep always". Task 6 restores `AddTestData();` per spec. Flag this to the user at pre-flight; implement per spec unless they say otherwise.

---

## Task 1: Model — `RaceOrderRule`, `IRaceOrderCalculator`, `RaceStandingsEntry`

**Files:**
- Create: `source/TrackGenius.Model/Race/RaceOrderRule.cs`
- Create: `source/TrackGenius.Model/Race/IRaceOrderCalculator.cs`
- Create: `source/TrackGenius.Model/Race/RaceStandingsEntry.cs`
- Test: `source/TrackGenius.UITests/Model/RaceStandingsEntryTests.cs`

**Interfaces (produced; consumed by Tasks 3–6):**

```csharp
// TrackGenius.Model
public enum RaceOrderRule { LapsThenRaceTime }

public sealed record RaceStandingsEntry(
    RaceData RaceData,
    int Position,
    TimeSpan BestLapTime,
    TimeSpan LastLapTime,
    string Gap,
    string Interval);

public interface IRaceOrderCalculator
{
    RaceOrderRule Rule { get; }
    IReadOnlyList<RaceStandingsEntry> Calculate(ICollection<RaceData> raceDataCollection);
}
```

- [ ] **Step 1: Write the failing test**

`source/TrackGenius.UITests/Model/RaceStandingsEntryTests.cs`:

```csharp
using System;
using NUnit.Framework;
using TrackGenius.Model;

namespace TrackGenius.UITests.Model;

[TestFixture]
public class RaceStandingsEntryTests
{
    [Test]
    public void GivenValues_WhenEntryConstructed_ThenPropertiesHold()
    {
        var raceData = new RaceData(null, null);

        var entry = new RaceStandingsEntry(raceData, Position: 2,
            BestLapTime: TimeSpan.FromSeconds(18.2), LastLapTime: TimeSpan.FromSeconds(19.1),
            Gap: "0:02.100", Interval: "0:04.200");

        Assert.Multiple(() =>
        {
            Assert.That(entry.RaceData, Is.SameAs(raceData));
            Assert.That(entry.Position, Is.EqualTo(2));
            Assert.That(entry.BestLapTime, Is.EqualTo(TimeSpan.FromSeconds(18.2)));
            Assert.That(entry.LastLapTime, Is.EqualTo(TimeSpan.FromSeconds(19.1)));
            Assert.That(entry.Gap, Is.EqualTo("0:02.100"));
            Assert.That(entry.Interval, Is.EqualTo("0:04.200"));
        });
    }
}
```

- [ ] **Step 2: Run to verify it fails**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RaceStandingsEntryTests"
```
Expected: compile error CS0246 — `RaceStandingsEntry` does not exist.

- [ ] **Step 3: Create the three type files**

`source/TrackGenius.Model/Race/RaceOrderRule.cs`:

```csharp
namespace TrackGenius.Model;

/// <summary>
/// How race standings are ordered. LapsThenRaceTime: most laps first;
/// among equal lap counts, less race time (clock time of the last accepted detection) first.
/// </summary>
public enum RaceOrderRule
{
    LapsThenRaceTime
}
```

`source/TrackGenius.Model/Race/IRaceOrderCalculator.cs`:

```csharp
using System.Collections.Generic;

namespace TrackGenius.Model;

public interface IRaceOrderCalculator
{
    RaceOrderRule Rule { get; }

    IReadOnlyList<RaceStandingsEntry> Calculate(ICollection<RaceData> raceDataCollection);
}
```

`source/TrackGenius.Model/Race/RaceStandingsEntry.cs`:

```csharp
using System;

namespace TrackGenius.Model;

/// <summary>
/// One racer's computed standing. Gap/Interval are pre-formatted display strings.
/// </summary>
public sealed record RaceStandingsEntry(
    RaceData RaceData,
    int Position,
    TimeSpan BestLapTime,
    TimeSpan LastLapTime,
    string Gap,
    string Interval);
```

- [ ] **Step 4: Run to verify it passes**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RaceStandingsEntryTests"
```
Expected: 1 passed (suite total 55).

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius.Model/Race/RaceOrderRule.cs source/TrackGenius.Model/Race/IRaceOrderCalculator.cs source/TrackGenius.Model/Race/RaceStandingsEntry.cs source/TrackGenius.UITests/Model/RaceStandingsEntryTests.cs
git commit -m "Add race order rule, calculator interface, and standings entry types

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 2: Model — `Transponder` class + fix `AnonymousDriverCreator` (NRE enabler)

Without this, `Race.GetRaceDataByTransponder` (`racer.Car.Transponder.RecoderNumber`) throws NRE on the second detection of any car, because the anonymous `Car` is created without a `Transponder` and no concrete `ITransponder` exists anywhere in the solution.

**Files:**
- Create: `source/TrackGenius.Model/Entity/Transponder.cs`
- Modify: `source/TrackGenius.Model/Entity/AnonymousDriverCreator.cs`
- Test: `source/TrackGenius.UITests/Model/AnonymousDriverCreatorTests.cs`

**Interfaces (produces; consumed by Tasks 5–6 indirectly via `Car.Transponder`):**

```csharp
// TrackGenius.Model
public class Transponder : ITransponder  // RecoderName, RecoderNumber: string; RecoderType: TransponderType
```

`AnonymousDriverCreator.CreateAnonymous(transponderID)` now sets `Car.Transponder = new Transponder { RecoderNumber = transponderID }`.

- [ ] **Step 1: Write the failing test**

`source/TrackGenius.UITests/Model/AnonymousDriverCreatorTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using TrackGenius.Model;

namespace TrackGenius.UITests.Model;

[TestFixture]
public class AnonymousDriverCreatorTests
{
    [Test]
    public void GivenTransponderID_WhenCreateAnonymous_ThenCarCarriesTransponderNumber()
    {
        var driver = AnonymousDriverCreator.CreateAnonymous("100");

        var car = driver.Cars.Single();
        Assert.Multiple(() =>
        {
            Assert.That(car.Transponder, Is.Not.Null);
            Assert.That(car.Transponder.RecoderNumber, Is.EqualTo("100"));
        });
    }

    [Test]
    public void GivenTransponderID_WhenCreateAnonymous_ThenDriverNamedAfterTransponder()
    {
        var driver = AnonymousDriverCreator.CreateAnonymous("200");

        Assert.Multiple(() =>
        {
            Assert.That(driver.DriverName, Is.EqualTo("200"));
            Assert.That(driver.Cars, Has.Count.EqualTo(1));
        });
    }
}
```

- [ ] **Step 2: Run to verify it fails**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~AnonymousDriverCreatorTests"
```
Expected: first test FAILS with `NullReferenceException` (or `Is.Not.Null` failure) on `car.Transponder`.

- [ ] **Step 3: Implement**

`source/TrackGenius.Model/Entity/Transponder.cs`:

```csharp
using TrackGenius.Const;

namespace TrackGenius.Model;

public class Transponder : ITransponder
{
    public string RecoderName { get; set; }

    public string RecoderNumber { get; set; }

    public TransponderType RecoderType { get; set; }
}
```

`source/TrackGenius.Model/Entity/AnonymousDriverCreator.cs` — full replacement (only change: the `Transponder` assignment; keep the existing TODO comment):

```csharp
using System.Drawing;

namespace TrackGenius.Model;

public static class AnonymousDriverCreator
{
    public static IDriver CreateAnonymous(string transponderID) =>
        new Driver
        {
            DriverName = transponderID,
            Cars =
            {
                new Car
                {
                    CarName = transponderID,
                    CarColor = Color.CadetBlue,   //TODO: Change to a random color
                    Transponder = new Transponder { RecoderNumber = transponderID }
                }
            }
        };
}
```

- [ ] **Step 4: Run to verify pass**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~AnonymousDriverCreatorTests"
```
Expected: 2 passed (suite total 57).

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius.Model/Entity/Transponder.cs source/TrackGenius.Model/Entity/AnonymousDriverCreator.cs source/TrackGenius.UITests/Model/AnonymousDriverCreatorTests.cs
git commit -m "Give anonymous cars a transponder so race data lookup works

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 3: Model — `LapsRaceTimeOrderCalculator` (all gap/interval semantics, TDD)

**Files:**
- Create: `source/TrackGenius.Model/Race/LapsRaceTimeOrderCalculator.cs`
- Test: `source/TrackGenius.UITests/Model/LapsRaceTimeOrderCalculatorTests.cs`

**Interfaces:**
- Consumes: `RaceData` (`LapsCount`, `RacedTime`, `LapRecords`), Task 1 types, `AnonymousDriverCreator` (test helper only).
- Produces: `LapsRaceTimeOrderCalculator : IRaceOrderCalculator`, `Rule => RaceOrderRule.LapsThenRaceTime`. Tasks 4–6 use it via `IRaceOrderCalculator`.

**Semantics:** order by `LapsCount` descending, then `RacedTime` ascending (stable for ties — LINQ `OrderBy`/`ThenBy` are stable). `Position` = index + 1. `BestLapTime` = min `LapRecords` lap time (zero if none). `LastLapTime` = last record's lap time (zero if none). Gap vs front racer, Interval vs leader: equal lap counts → `(current.RacedTime − reference.RacedTime)` formatted `m:ss.fff` (positive "behind by" — see Spec clarification 1); differing counts → `"+N Lap(s)"`; leader → `"-"` for both.

- [ ] **Step 1: Write the failing tests**

`source/TrackGenius.UITests/Model/LapsRaceTimeOrderCalculatorTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrackGenius.Model;

namespace TrackGenius.UITests.Model;

[TestFixture]
public class LapsRaceTimeOrderCalculatorTests
{
    private readonly LapsRaceTimeOrderCalculator _calculator = new();

    /// <summary>Whole-second lap timestamps keep clear of the GetLastDetectedTimeSpan quirk.</summary>
    private static RaceData Racer(string transponder, params int[] lapMilliseconds)
    {
        var driver = AnonymousDriverCreator.CreateAnonymous(transponder);
        var raceData = new RaceData(driver, driver.Cars.First());
        foreach (var milliseconds in lapMilliseconds)
            raceData.RecordDetection(TimeSpan.FromMilliseconds(milliseconds));
        return raceData;
    }

    private IReadOnlyList<RaceStandingsEntry> Calculate(params RaceData[] racers)
        => _calculator.Calculate(racers);

    [Test]
    public void GivenEmptyCollection_WhenCalculate_ThenEmptyList()
    {
        var result = _calculator.Calculate(new List<RaceData>());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GivenSingleRacerWithOneLap_WhenCalculate_ThenLeaderEntryWithDashesAndTimes()
    {
        var racer = Racer("100", 61_000);

        var result = Calculate(racer);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].RaceData, Is.SameAs(racer));
            Assert.That(result[0].Position, Is.EqualTo(1));
            Assert.That(result[0].Gap, Is.EqualTo("-"));
            Assert.That(result[0].Interval, Is.EqualTo("-"));
            Assert.That(result[0].BestLapTime, Is.EqualTo(TimeSpan.FromMilliseconds(61_000)));
            Assert.That(result[0].LastLapTime, Is.EqualTo(TimeSpan.FromMilliseconds(61_000)));
        });
    }

    [Test]
    public void GivenTwoRacersEqualLaps_WhenCalculate_ThenFasterRacerFirstWithTimeGap()
    {
        var fast = Racer("100", 60_000);
        var slow = Racer("200", 62_000);

        var result = Calculate(slow, fast);   // insertion order deliberately reversed

        Assert.Multiple(() =>
        {
            Assert.That(result[0].RaceData, Is.SameAs(fast));
            Assert.That(result[1].RaceData, Is.SameAs(slow));
            Assert.That(result[0].Position, Is.EqualTo(1));
            Assert.That(result[1].Position, Is.EqualTo(2));
            Assert.That(result[0].Gap, Is.EqualTo("-"));
            Assert.That(result[0].Interval, Is.EqualTo("-"));
            Assert.That(result[1].Gap, Is.EqualTo("0:02.000"));
            Assert.That(result[1].Interval, Is.EqualTo("0:02.000"));
        });
    }

    [Test]
    public void GivenRacerWithMoreLaps_WhenCalculate_ThenLeadsDespiteSlowerTimeAndLapDifferenceShown()
    {
        var lapped = Racer("100", 60_000, 115_000);   // 2 laps, race time 115s
        var quick = Racer("200", 62_000);             // 1 lap, race time 62s

        var result = Calculate(quick, lapped);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].RaceData, Is.SameAs(lapped));
            Assert.That(result[1].RaceData, Is.SameAs(quick));
            Assert.That(result[1].Gap, Is.EqualTo("+1 Lap"));
            Assert.That(result[1].Interval, Is.EqualTo("+1 Lap"));
        });
    }

    [Test]
    public void GivenThreeLapDifference_WhenCalculate_ThenPluralLapText()
    {
        var leader = Racer("100", 60_000, 120_000, 180_000, 240_000);   // 4 laps
        var trailing = Racer("200", 62_000);                            // 1 lap

        var result = Calculate(trailing, leader);

        Assert.Multiple(() =>
        {
            Assert.That(result[1].Gap, Is.EqualTo("+3 Laps"));
            Assert.That(result[1].Interval, Is.EqualTo("+3 Laps"));
        });
    }

    [Test]
    public void GivenZeroLapRacers_WhenCalculate_ThenLapsToLappedRacersAndZeroTimeToPeers()
    {
        var leader = Racer("100", 60_000);   // 1 lap
        var peer1 = Racer("200");            // 0 laps
        var peer2 = Racer("300");            // 0 laps

        var result = Calculate(leader, peer1, peer2);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].RaceData, Is.SameAs(leader));
            Assert.That(result[1].RaceData, Is.SameAs(peer1));   // zero-lap racers keep insertion order
            Assert.That(result[2].RaceData, Is.SameAs(peer2));
            Assert.That(result[1].Gap, Is.EqualTo("+1 Lap"));        // vs front racer (leader, 1 lap)
            Assert.That(result[1].Interval, Is.EqualTo("+1 Lap"));   // vs leader
            Assert.That(result[2].Gap, Is.EqualTo("0:00.000"));     // vs front racer (peer1, equal 0 laps)
            Assert.That(result[2].Interval, Is.EqualTo("+1 Lap"));   // vs leader
        });
    }

    [Test]
    public void GivenTwoLapRacer_WhenCalculate_ThenBestAndLastLapTimesFromRecords()
    {
        var racer = Racer("100", 60_000, 122_000);   // lap1 = 60s, lap2 = 62s

        var result = Calculate(racer);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].BestLapTime, Is.EqualTo(TimeSpan.FromMilliseconds(60_000)));
            Assert.That(result[0].LastLapTime, Is.EqualTo(TimeSpan.FromMilliseconds(62_000)));
        });
    }
}
```

- [ ] **Step 2: Run to verify they fail**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~LapsRaceTimeOrderCalculatorTests"
```
Expected: compile error CS0246 — `LapsRaceTimeOrderCalculator` does not exist.

- [ ] **Step 3: Implement**

`source/TrackGenius.Model/Race/LapsRaceTimeOrderCalculator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrackGenius.Model;

/// <summary>
/// Default standings rule: most laps first; among equal lap counts, less race time first.
/// Gap/Interval are pre-formatted display strings (invariant culture).
/// </summary>
public sealed class LapsRaceTimeOrderCalculator : IRaceOrderCalculator
{
    private const string TimeFormat = @"m\:ss\.fff";

    public RaceOrderRule Rule => RaceOrderRule.LapsThenRaceTime;

    public IReadOnlyList<RaceStandingsEntry> Calculate(ICollection<RaceData> raceDataCollection)
    {
        if (raceDataCollection == null)
            throw new ArgumentNullException(nameof(raceDataCollection));

        var ordered = raceDataCollection
            .OrderByDescending(racer => racer.LapsCount)
            .ThenBy(racer => racer.RacedTime)
            .ToList();

        var entries = new List<RaceStandingsEntry>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            var racer = ordered[index];
            entries.Add(new RaceStandingsEntry(
                RaceData: racer,
                Position: index + 1,
                BestLapTime: BestLap(racer),
                LastLapTime: LastLap(racer),
                Gap: DescribeDifference(index == 0 ? null : ordered[index - 1], racer),
                Interval: DescribeDifference(index == 0 ? null : ordered[0], racer)));
        }

        return entries;
    }

    // Equal lap counts -> positive time difference (current is behind);
    // differing counts -> lap difference; no reference (leader) -> "-".
    private static string DescribeDifference(RaceData reference, RaceData current)
    {
        if (reference == null)
            return "-";

        var lapDifference = reference.LapsCount - current.LapsCount;
        if (lapDifference != 0)
            return $"+{Math.Abs(lapDifference)} {(Math.Abs(lapDifference) == 1 ? "Lap" : "Laps")}";

        return (current.RacedTime - reference.RacedTime).ToString(TimeFormat, CultureInfo.InvariantCulture);
    }

    private static TimeSpan BestLap(RaceData raceData)
        => raceData.LapRecords.Count == 0
            ? TimeSpan.Zero
            : raceData.LapRecords.Min(lap => lap.LapTime);

    private static TimeSpan LastLap(RaceData raceData)
        => raceData.LapRecords.Count == 0
            ? TimeSpan.Zero
            : raceData.LapRecords[^1].LapTime;
}
```

- [ ] **Step 4: Run to verify pass**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~LapsRaceTimeOrderCalculatorTests"
```
Expected: 7 passed (suite total 64).

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius.Model/Race/LapsRaceTimeOrderCalculator.cs source/TrackGenius.UITests/Model/LapsRaceTimeOrderCalculatorTests.cs
git commit -m "Add laps-then-race-time order calculator with gap/interval display semantics

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 4: Model — `Race`/`IRace` gain `OrderRule` + `OrderCalculator`

**Files:**
- Modify: `source/TrackGenius.Model/Race/IRace.cs`
- Modify: `source/TrackGenius.Model/Race/Race.cs`
- Test: `source/TrackGenius.UITests/Model/RaceTests.cs`

**Interfaces (produces; consumed by Task 5):**

```csharp
// added to IRace / Race:
RaceOrderRule OrderRule { get; set; }      // default LapsThenRaceTime; setter swaps the calculator
IRaceOrderCalculator OrderCalculator { get; }  // read-only, kept in sync with OrderRule
```

- [ ] **Step 1: Write the failing tests**

`source/TrackGenius.UITests/Model/RaceTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrackGenius.Model;

namespace TrackGenius.UITests.Model;

[TestFixture]
public class RaceTests
{
    private static Race CreateRace()
        => new(new Guid(), RaceType.FreePractice, new RaceClass("World GT"), new List<RaceData>());

    [Test]
    public void GivenNewRace_WhenPropertiesRead_ThenDefaultRuleAndCalculator()
    {
        var race = CreateRace();

        Assert.Multiple(() =>
        {
            Assert.That(race.OrderRule, Is.EqualTo(RaceOrderRule.LapsThenRaceTime));
            Assert.That(race.OrderCalculator, Is.InstanceOf<LapsRaceTimeOrderCalculator>());
        });
    }

    [Test]
    public void GivenRace_WhenOrderRuleSet_ThenCalculatorMatchesRule()
    {
        var race = CreateRace();

        race.OrderRule = RaceOrderRule.LapsThenRaceTime;   // only rule today

        Assert.That(race.OrderCalculator, Is.InstanceOf<LapsRaceTimeOrderCalculator>());
    }
}
```

- [ ] **Step 2: Run to verify they fail**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RaceTests"
```
Expected: compile error CS1061 — `IRace` has no definition for `OrderRule` / `OrderCalculator`.

- [ ] **Step 3: Implement**

`source/TrackGenius.Model/Race/IRace.cs` — add to the interface (e.g. after `MinLapIntervalMilliseconds`):

```csharp
    RaceOrderRule OrderRule { get; set; }

    IRaceOrderCalculator OrderCalculator { get; }
```

`source/TrackGenius.Model/Race/Race.cs` — add members:

```csharp
        private RaceOrderRule _orderRule = RaceOrderRule.LapsThenRaceTime;

        public RaceOrderRule OrderRule
        {
            get => _orderRule;
            set
            {
                _orderRule = value;
                OrderCalculator = CreateCalculatorFor(value);
            }
        }

        public IRaceOrderCalculator OrderCalculator { get; private set; }

        // Future rules switch here; today the default calculator serves the only rule.
        private static IRaceOrderCalculator CreateCalculatorFor(RaceOrderRule rule)
            => new LapsRaceTimeOrderCalculator();
```

And at the end of the 5-argument constructor body (after `RaceTimer = raceTimer ?? throw new ArgumentNullException(nameof(raceTimer));`):

```csharp
            OrderCalculator = CreateCalculatorFor(_orderRule);
```

- [ ] **Step 4: Run to verify pass**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RaceTests"
```
Expected: 2 passed (suite total 66).

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius.Model/Race/IRace.cs source/TrackGenius.Model/Race/Race.cs source/TrackGenius.UITests/Model/RaceTests.cs
git commit -m "Carry order rule and calculator on the race with laps-then-race-time default

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 5: Core — `RaceEngine.RaceDataChanged` event

**Files:**
- Modify: `source/TrackGenius.Core/RaceEngine.cs`
- Test: `source/TrackGenius.UITests/Core/RaceEngineTests.cs`

**Interfaces:**
- Consumes: `Race.OrderCalculator` (Task 4), `RobitronicMessageConsumer.ConsumeMessage(object, ICommonMessage)` (public — lets tests drive the consumer directly), `DetectedMessage` (Protocol type; constructible from a crafted byte frame).
- Produces (consumed by Task 6): `public event EventHandler<IReadOnlyList<RaceStandingsEntry>> RaceDataChanged;` — raised on the calling (serial) thread after each actual mutation (racer added, or lap accepted); silent when a detection is suppressed and nothing was added.

**IMPORTANT — WIP file:** `RaceEngine.cs` has the user's uncommitted WIP (subscriptions in `RaceStart`, `[NotNull]` attributes). Keep every WIP hunk; the edits below are additive fragments around them.

- [ ] **Step 1: Write the failing tests**

`source/TrackGenius.UITests/Core/RaceEngineTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Communication;
using TrackGenius.Core;
using TrackGenius.Model;
using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.UITests.Core;

[TestFixture]
public class RaceEngineTests
{
    private FakeSerialPortWrapper _wrapper;
    private CommunicateService _communicateService;
    private RobitronicMessageConsumer _consumer;
    private RaceEngine _engine;
    private List<IReadOnlyList<RaceStandingsEntry>> _received;

    [SetUp]
    public void SetUp()
    {
        _wrapper = new FakeSerialPortWrapper();
        _communicateService = new CommunicateService(_wrapper, NullLogger<CommunicateService>.Instance);
        _consumer = new RobitronicMessageConsumer();
        _engine = new RaceEngine(_consumer, _communicateService);
        _received = new List<IReadOnlyList<RaceStandingsEntry>>();
        _engine.RaceDataChanged += (_, entries) => _received.Add(entries);
    }

    [TearDown]
    public void TearDown() => _engine.Dispose();

    /// <summary>
    /// DetectedMessage reads TransponderID from bytes 3-6 and Milliseconds from bytes 7-10,
    /// each little-endian (Cut -> Reverse -> big-endian ToInt32 of the reversed slice).
    /// </summary>
    private static DetectedMessage MakeDetectedMessage(long transponder, int milliseconds)
    {
        var data = new byte[13];
        data[0] = 13;   // packet length
        data[2] = 0x84; // CarDetect packet type (ctor requirement)
        data[3] = (byte)(transponder & 0xFF);
        data[4] = (byte)((transponder >> 8) & 0xFF);
        data[5] = (byte)((transponder >> 16) & 0xFF);
        data[6] = (byte)((transponder >> 24) & 0xFF);
        data[7] = (byte)(milliseconds & 0xFF);
        data[8] = (byte)((milliseconds >> 8) & 0xFF);
        data[9] = (byte)((milliseconds >> 16) & 0xFF);
        data[10] = (byte)((milliseconds >> 24) & 0xFF);
        return new DetectedMessage(data);
    }

    private void SendDetection(long transponder, int milliseconds)
        => _consumer.ConsumeMessage(this, MakeDetectedMessage(transponder, milliseconds));

    [Test]
    public void GivenEncodedFrame_WhenParsed_ThenTransponderAndMillisecondsMatch()
    {
        var message = MakeDetectedMessage(100, 3_600_000);

        Assert.Multiple(() =>
        {
            Assert.That(message.TransponderID, Is.EqualTo("100"));
            Assert.That(message.Milliseconds, Is.EqualTo(3_600_000));
        });
    }

    [Test]
    public void GivenStartedRace_WhenFirstDetectionArrives_ThenRaceDataChangedFiresWithNewRacer()
    {
        _engine.RaceStart(new List<RaceData>());

        SendDetection(transponder: 100, milliseconds: 3_600_000);

        Assert.That(_received.Count, Is.EqualTo(1));
        Assert.That(_received[0].Count, Is.EqualTo(1));
        var entry = _received[0][0];
        Assert.Multiple(() =>
        {
            Assert.That(entry.Position, Is.EqualTo(1));
            Assert.That(entry.RaceData.Car.Transponder.RecoderNumber, Is.EqualTo("100"));
            Assert.That(entry.RaceData.LapsCount, Is.EqualTo(1));
            Assert.That(entry.Gap, Is.EqualTo("-"));
            Assert.That(entry.Interval, Is.EqualTo("-"));
        });
    }

    [Test]
    public void GivenFirstLapRecorded_WhenSecondDetectionWithinMinInterval_ThenNoEvent()
    {
        _engine.RaceStart(new List<RaceData>());
        SendDetection(transponder: 100, milliseconds: 3_600_000);

        // Whole-second timestamps make the last lap's sub-second component 0, so the
        // interval check compares against 0 and 1_000 < 1500 (MinLapIntervalMilliseconds) → suppressed.
        SendDetection(transponder: 100, milliseconds: 1_000);

        Assert.That(_received.Count, Is.EqualTo(1));   // only the first detection fired
    }

    [Test]
    public void GivenFirstLapRecorded_WhenSecondDetectionAfterMinInterval_ThenLapCountedAndEventFires()
    {
        _engine.RaceStart(new List<RaceData>());
        SendDetection(transponder: 100, milliseconds: 3_600_000);

        SendDetection(transponder: 100, milliseconds: 3_601_000);

        Assert.That(_received.Count, Is.EqualTo(2));
        var entry = _received[1].Single();
        Assert.Multiple(() =>
        {
            Assert.That(entry.RaceData.LapsCount, Is.EqualTo(2));
            Assert.That(entry.LastLapTime, Is.EqualTo(TimeSpan.FromMilliseconds(1_000)));
        });
    }

    [Test]
    public void GivenTwoRacersDetected_WhenStandingsRaised_ThenOrderAndGapComputed()
    {
        _engine.RaceStart(new List<RaceData>());

        SendDetection(transponder: 100, milliseconds: 3_600_000);
        SendDetection(transponder: 200, milliseconds: 3_601_000);

        var standings = _received[1];
        Assert.Multiple(() =>
        {
            Assert.That(standings[0].RaceData.Car.Transponder.RecoderNumber, Is.EqualTo("100"));
            Assert.That(standings[1].RaceData.Car.Transponder.RecoderNumber, Is.EqualTo("200"));
            Assert.That(standings[1].Gap, Is.EqualTo("0:01.000"));
            Assert.That(standings[1].Interval, Is.EqualTo("0:01.000"));
        });
    }
}
```

(`_received[1].Single()` needs `using System.Linq;` — add it to the usings.)

- [ ] **Step 2: Run to verify they fail**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RaceEngineTests"
```
Expected: compile error CS1061 — `RaceEngine` has no definition for `RaceDataChanged`. (The encoding-sanity test may already pass — that is fine; it guards the helper, not the feature.)

- [ ] **Step 3: Implement**

`source/TrackGenius.Core/RaceEngine.cs` — add the event declaration next to the fields:

```csharp
        public event EventHandler<IReadOnlyList<RaceStandingsEntry>> RaceDataChanged;
```

Replace `UpdateRaceStatus` with (only changes: the `changed` flag, the guarded raise, and the raise after `RecordDetection`; the suppression logic and its TODO comment stay **verbatim**):

```csharp
        private void UpdateRaceStatus(CarDetectMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var changed = false;
            var raceData = _race.GetRaceDataByTransponder(message.TransponderID);
            if (raceData == null)
            {
                var anonymousDriver = AnonymousDriverCreator.CreateAnonymous(message.TransponderID);
                raceData = new RaceData(anonymousDriver, anonymousDriver.Cars.First());
                _race.RaceDataCollection.Add(raceData);
                changed = true;
            }

            var lastDetectedMilliseconds = raceData.GetLastDetectedTimeSpan().Milliseconds;
            var interval = message.Milliseconds - lastDetectedMilliseconds;
            if (interval <= 0 || interval < _race.MinLapIntervalMilliseconds)
            {
                // TODO: Should add log and show a message in the UI to indicate that the detection is ignored due to too short interval.
                if (changed)
                    RaiseRaceDataChanged();   // the racer was added even though this pass was suppressed
                return;
            }

            raceData.RecordDetection(TimeSpan.FromMilliseconds(message.Milliseconds));
            RaiseRaceDataChanged();
        }

        private void RaiseRaceDataChanged()
            => RaceDataChanged?.Invoke(this, _race.OrderCalculator.Calculate(_race.RaceDataCollection));
```

- [ ] **Step 4: Run to verify pass**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RaceEngineTests"
```
Expected: 5 passed (suite total 71).

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius.Core/RaceEngine.cs source/TrackGenius.UITests/Core/RaceEngineTests.cs
git commit -m "Raise race data changed event from the engine after accepted detections

Co-Authored-By: Claude <noreply@anthropic.com>"
```
(If the pre-flight decision was to fold the user's WIP in rather than have them commit it first, note that in the commit message body: one line, `Includes pending RaceStart subscription hardening.`)

---

## Task 6: UI — item VM rename + `RacePageViewModel` wiring

**Files:**
- Modify: `source/TrackGenius/ViewModels/RaceDataItemViewModel.cs`
- Modify: `source/TrackGenius/ViewModels/RacePageViewModel.cs`
- Modify: `source/TrackGenius/Views/Controls/RaceDataListControl.xaml` (lines 178–181)
- Test: `source/TrackGenius.UITests/ViewModels/RacePageViewModelTests.cs`

**Interfaces:**
- Consumes: `RaceEngine.RaceDataChanged` (Task 5), `RaceStandingsEntry` (Task 1), `RaceDataItemViewModel` existing `(IDriver, ICar, int)` ctor and `RaiseIfChanged` setters, `CommunicateService.MessageReceived` (public field-like delegate — test drives the full pipeline through it).
- Produces: `RaceDataItemViewModel.Gap: string` and `.Interval: string` (replacing `GapTime`/`IntervalTime`); a `RacePageViewModel` that subscribes to each engine before `RaceStart`, marshals via the ctor-captured `Dispatcher`, find-or-creates rows by `TransponderID`, and maps fields.

**IMPORTANT — WIP file:** `RacePageViewModel.cs` has the user's WIP (`AddTestData()` commented out). Per the spec decision ("keep always"), Task 6 **restores** `AddTestData();` — see Pre-flight.

- [ ] **Step 1: Write the failing tests**

`source/TrackGenius.UITests/ViewModels/RacePageViewModelTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Communication;
using TrackGenius.Core;
using TrackGenius.Protocol.Robitronic;
using TrackGenius.UI.ViewModels;

namespace TrackGenius.UITests.ViewModels;

[TestFixture]
public class RacePageViewModelTests
{
    private static readonly string[] FakeIds = { "88156", "48825", "44401", "99812", "35890" };

    private CommunicateService _communicateService;
    private RaceConnectionService _connection;
    private RacePageViewModel _viewModel;

    [SetUp]
    public void SetUp()
    {
        _communicateService = new CommunicateService(
            new FakeSerialPortWrapper(), NullLogger<CommunicateService>.Instance);
        _connection = new RaceConnectionService(_communicateService);
        _viewModel = new RacePageViewModel(
            new RaceEngineFactory(_connection, _communicateService), _connection);
    }

    [TearDown]
    public void TearDown() => _connection.Close();

    // Threading: the VM is constructed on the test thread, where Dispatcher.CurrentDispatcher
    // creates a dispatcher and CheckAccess() is true — the handler applies synchronously, no pumping.

    [Test]
    public void GivenOpenPortAndStartedRace_WhenDetectionArrives_ThenRaceDataItemsGainsRowWithMappedFields()
    {
        _connection.Open("COM3", new RobitronicProtocol());
        _viewModel.StartRaceCommand.Execute(null);

        // Full pipeline: RaceStart subscribed the consumer to MessageReceived.
        _communicateService.MessageReceived?.Invoke(
            _communicateService, MakeDetectedMessage(transponder: 100, milliseconds: 3_600_000));

        var row = _viewModel.RaceDataItems.Single(item => !FakeIds.Contains(item.TransponderID));
        Assert.Multiple(() =>
        {
            Assert.That(row.TransponderID, Is.EqualTo("100"));
            Assert.That(row.RacerPosition, Is.EqualTo(1));
            Assert.That(row.LapsCount, Is.EqualTo(1));
            Assert.That(row.Gap, Is.EqualTo("-"));
            Assert.That(row.Interval, Is.EqualTo("-"));
        });
    }

    [Test]
    public void GivenStartedRace_WhenSecondRacerDetected_ThenBothRowsPresentWithComputedGap()
    {
        _connection.Open("COM3", new RobitronicProtocol());
        _viewModel.StartRaceCommand.Execute(null);

        _communicateService.MessageReceived?.Invoke(
            _communicateService, MakeDetectedMessage(transponder: 100, milliseconds: 3_600_000));
        _communicateService.MessageReceived?.Invoke(
            _communicateService, MakeDetectedMessage(transponder: 200, milliseconds: 3_601_000));

        var first = _viewModel.RaceDataItems.Single(item => item.TransponderID == "100");
        var second = _viewModel.RaceDataItems.Single(item => item.TransponderID == "200");
        Assert.Multiple(() =>
        {
            Assert.That(first.RacerPosition, Is.EqualTo(1));
            Assert.That(second.RacerPosition, Is.EqualTo(2));
            Assert.That(second.Gap, Is.EqualTo("0:01.000"));
        });
    }

    private static DetectedMessage MakeDetectedMessage(long transponder, int milliseconds)
    {
        var data = new byte[13];
        data[0] = 13;   // packet length
        data[2] = 0x84; // CarDetect packet type
        data[3] = (byte)(transponder & 0xFF);
        data[4] = (byte)((transponder >> 8) & 0xFF);
        data[5] = (byte)((transponder >> 16) & 0xFF);
        data[6] = (byte)((transponder >> 24) & 0xFF);
        data[7] = (byte)(milliseconds & 0xFF);
        data[8] = (byte)((milliseconds >> 8) & 0xFF);
        data[9] = (byte)((milliseconds >> 16) & 0xFF);
        data[10] = (byte)((milliseconds >> 24) & 0xFF);
        return new DetectedMessage(data);
    }
}
```

- [ ] **Step 2: Run to verify they fail**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RacePageViewModelTests"
```
Expected: compile error CS1061 — `RaceDataItemViewModel` has no `Gap`/`Interval`, and `RaceEngine` event not yet consumed (if Task 5 landed, only the `Gap`/`Interval` errors remain).

- [ ] **Step 3: Rename item VM properties**

`source/TrackGenius/ViewModels/RaceDataItemViewModel.cs` — replace the backing fields:

```csharp
    private string _gap;
    private string _interval;
```
(for `_gapTime` / `_intervalTime`, near line 29–30), and the properties:

```csharp
    public string Gap
    {
        get => _gap;
        set => PropertyChanged.RaiseIfChanged(this, ref _gap, value, nameof(Gap));
    }

    public string Interval
    {
        get => _interval;
        set => PropertyChanged.RaiseIfChanged(this, ref _interval, value, nameof(Interval));
    }
```

- [ ] **Step 4: Update the XAML bindings**

`source/TrackGenius/Views/Controls/RaceDataListControl.xaml`, lines 178–181 — drop the converter (values are pre-formatted strings) and rename the bindings:

```xml
                                    <TextBlock Grid.Row="0" Grid.Column="4" FontSize="16" VerticalAlignment="Center" Margin="12,0,0,0" Text="{Binding Gap}"
                                               Visibility="{Binding ColumnSettings.Gap.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                                    <TextBlock Grid.Row="0" Grid.Column="5" FontSize="16" VerticalAlignment="Center" Margin="12,0,0,0" Text="{Binding Interval}"
                                               Visibility="{Binding ColumnSettings.Interval.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
```

- [ ] **Step 5: Wire the page VM**

`source/TrackGenius/ViewModels/RacePageViewModel.cs`:

Add usings:

```csharp
using System.Linq;
using System.Windows.Threading;
```

Add a field next to `_raceEngine`:

```csharp
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
```

Replace `StartRace()` with (keeps the existing comments and the `NotSupportedException` handling; adds unsubscribe-old/subscribe-new around engine creation):

```csharp
    private void StartRace()
    {
        if (!CanStartRace)
            return;

        try
        {
            if (_raceEngine != null)
            {
                // Keep the engine alive for the race: disposing it immediately would
                // unsubscribe its handlers before any detection could arrive.
                _raceEngine.RaceDataChanged -= OnRaceDataChanged;
                _raceEngine.Dispose();
            }

            _raceEngine = _raceEngineFactory.CreateRaceEngine();
            _raceEngine.RaceDataChanged += OnRaceDataChanged;
            _raceEngine.RaceStart(new List<RaceData>());
            LastError = string.Empty;
        }
        catch (NotSupportedException ex)
        {
            // e.g. a protocol without a message consumer is selected.
            _raceEngine = null;
            LastError = ex.Message;
        }
    }
```

Add the handlers (near `StartRace`):

```csharp
    private void OnRaceDataChanged(object sender, IReadOnlyList<RaceStandingsEntry> entries)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke((Action)(() => ApplyStandings(entries)));
            return;
        }

        ApplyStandings(entries);
    }

    private void ApplyStandings(IReadOnlyList<RaceStandingsEntry> entries)
    {
        foreach (var entry in entries)
        {
            var item = RaceDataItems.FirstOrDefault(
                existing => existing.TransponderID == entry.RaceData.Car.Transponder.RecoderNumber);
            if (item == null)
            {
                item = new RaceDataItemViewModel(entry.RaceData.Driver, entry.RaceData.Car, RaceDataItems.Count + 1);
                RaceDataItems.Add(item);
            }

            item.RacerPosition = entry.Position;
            item.LapsCount = entry.RaceData.LapsCount;
            item.LastLapTime = entry.LastLapTime;
            item.BestLapTime = entry.BestLapTime;
            item.Gap = entry.Gap;
            item.Interval = entry.Interval;
        }
    }
```

- [ ] **Step 6: Restore `AddTestData()` with string Gap/Interval values**

Restore the `AddTestData();` call in the constructor (undo the WIP `// AddTestData();`), and replace each `GapTime = TimeSpan.FromSeconds(x), IntervalTime = TimeSpan.FromSeconds(y)` initializer with string values:

```csharp
    private void AddTestData()
    {
        var items = new[]
        {
            new RaceDataItemViewModel("88156", 1)
            {
                RacerNumber = 88, RacerPosition = 1, LapsCount = 12,
                BestLapTime = TimeSpan.FromSeconds(18.234), LastLapTime = TimeSpan.FromSeconds(19.012),
                Gap = "-", Interval = "-", Description = "Leader"
            },
            new RaceDataItemViewModel("48825", 2)
            {
                RacerNumber = 7, RacerPosition = 2, LapsCount = 12,
                BestLapTime = TimeSpan.FromSeconds(18.401), LastLapTime = TimeSpan.FromSeconds(18.890),
                Gap = "0:00.800", Interval = "0:00.800"
            },
            new RaceDataItemViewModel("44401", 4)
            {
                RacerNumber = 44, RacerPosition = 3, LapsCount = 11,
                BestLapTime = TimeSpan.FromSeconds(18.567), LastLapTime = TimeSpan.FromSeconds(18.945),
                Gap = "0:02.100", Interval = "0:00.700"
            },
            new RaceDataItemViewModel("99812", 5)
            {
                RacerNumber = 99, RacerPosition = 4, LapsCount = 10,
                BestLapTime = TimeSpan.FromSeconds(18.900), LastLapTime = TimeSpan.FromSeconds(19.300),
                Gap = "0:05.000", Interval = "0:02.900"
            },
            new RaceDataItemViewModel("35890", 3)
            {
                RacerNumber = 23, RacerPosition = 5, LapsCount = 9,
                BestLapTime = TimeSpan.FromSeconds(19.123), LastLapTime = TimeSpan.FromSeconds(20.000),
                Gap = "0:08.400", Interval = "0:03.400"
            },
        };

        foreach (var item in items)
            RaceDataItems.Add(item);
    }
```

- [ ] **Step 7: Run tests to verify pass**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RacePageViewModelTests"
```
Expected: 2 passed (suite total 73).

- [ ] **Step 8: Run the full suite**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: 73 passed, 0 failed.

- [ ] **Step 9: Commit**

```bash
git add source/TrackGenius/ViewModels/RaceDataItemViewModel.cs source/TrackGenius/ViewModels/RacePageViewModel.cs source/TrackGenius/Views/Controls/RaceDataListControl.xaml source/TrackGenius.UITests/ViewModels/RacePageViewModelTests.cs
git commit -m "Wire race page view model to engine standings and rename gap/interval display

Co-Authored-By: Claude <noreply@anthropic.com>"
```
(If folding the user's WIP in per the pre-flight decision, add one line to the body: `Includes pending AddTestData disablement reversal per spec.`)

---

## Final verification (after all tasks)

- [ ] **Full build + UITests**

```bash
dotnet build source/TrackGenius.sln
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: build 0 errors; 73 passed / 0 failed.

- [ ] **ProtocolTests unchanged**

```bash
dotnet test source/TrackGenius.ProtocolTests/TrackGenius.ProtocolTests.csproj
```
Expected: exactly 2 failures (the pre-existing `DetectedMessageTests` ones), 14 passed. Any NEW failure is a regression this plan introduced — investigate before proceeding.

- [ ] **Diff summary for the user**

```bash
git log --oneline develop..HEAD
git diff develop --stat
```

- [ ] **Hand to the user for manual GUI verification** (agents must not launch the app): open a port with the Robitronic protocol, start a race on the Quick Race page, drive a detection (or real transponder pass), and confirm the row appears/updates with position, laps, last/best lap, Gap and Interval.

## Out of scope (do not do)

- Order-rule selector UI; `IRaceRanker` / `BestLapRanker` cleanup.
- Fixing the `DetectedMessage` transponder parsing (stale ProtocolTests expectations).
- Fixing the `GetLastDetectedTimeSpan().Milliseconds` interval-suppression quirk or the `RecordDetection` lap-time drift from lap 3 on.
- `RacerNumber` / `Description` population.
