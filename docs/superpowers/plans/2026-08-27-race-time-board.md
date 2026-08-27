# Race Time Board Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A live race header (`RaceTimeBoard`) on QuickRacePage showing the race name, elapsed time, remaining countdown, and the system clock, wired through the user's staged model/engine/VM scaffolding.

**Architecture:** `RaceStart(IRace)` starts a `RaceTimer`; `StartRace()` builds the `IRace` (auto name) and injects it via `ApplyCurrentRace`; a 1 s `DispatcherTimer` in the VM raises `PropertyChanged` for the three time properties; the board lays out name | race time + label | remaining + label | clock, themed.

**Tech Stack:** .NET 8 WPF, NUnit via `TrackGenius.UITests` (137 passing baseline).

**Spec:** `docs/superpowers/specs/2026-08-27-race-time-board-design.md`

## Global Constraints

- **DO NOT COMMIT.** The user's staged scaffolding plus this implementation stay uncommitted in the working tree for user review. (The spec/plan docs also stay uncommitted with them.)
- Work from repo root `E:\source\repo\TrackGenius`; solution `source/TrackGenius.sln`.
- Gates per task: `dotnet build source/TrackGenius.sln` 0 errors AND `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj` all pass (baseline 137; grows with new tests).
- If `MSB3021/MSB3027`: `taskkill //IM TrackGenius.UI.exe //F`, retry, else BLOCKED.
- No GUI launch (user verifies visually — standing policy).
- TDD where a unit-testable seam exists (Model/Core/VM); XAML is verified by build only.
- New/changed test methods: `Given…_When…_Then…`, NUnit, file-scoped namespaces.

---

## Task 1: `RaceTimer` cleanup + tests

**Files:**
- Modify: `source/TrackGenius.Model/Race/RaceTimer.cs`
- Test: `source/TrackGenius.UITests/Model/RaceTimerTests.cs` (new)

**Interfaces (produced; consumed by Task 2):** `RaceTimer(int countDownTime)`; `int CountDownTime { get; set; }`; `bool IsStarted { get; }`; `TimeSpan Elapsed { get; }`; `TimeSpan Remaining { get; }` (may be negative); `void Start()`; `void Stop()`.

- [ ] **Step 1: Write the failing tests**

`source/TrackGenius.UITests/Model/RaceTimerTests.cs`:

```csharp
using System;
using System.Threading;
using NUnit.Framework;
using TrackGenius.Model;

namespace TrackGenius.UITests.Model;

[TestFixture]
public class RaceTimerTests
{
    [Test]
    public void GivenNewTimer_WhenStartCalled_ThenIsStartedAndElapsedGrows()
    {
        var timer = new RaceTimer(10);

        timer.Start();
        Thread.Sleep(30);

        Assert.Multiple(() =>
        {
            Assert.That(timer.IsStarted, Is.True);
            Assert.That(timer.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(25)));
        });
    }

    [Test]
    public void GivenUnstartedTimer_WhenPropertiesRead_ThenNotStartedAndZeroElapsed()
    {
        var timer = new RaceTimer(10);

        Assert.Multiple(() =>
        {
            Assert.That(timer.IsStarted, Is.False);
            Assert.That(timer.Elapsed, Is.EqualTo(TimeSpan.Zero));
            Assert.That(timer.Remaining, Is.EqualTo(TimeSpan.FromSeconds(10)));
        });
    }

    [Test]
    public void GivenExpiredCountdown_WhenRemainingRead_ThenNegativeAllowed()
    {
        var timer = new RaceTimer(0);
        timer.Start();

        Assert.That(timer.Remaining, Is.LessThanOrEqualTo(TimeSpan.Zero));
    }

    [Test]
    public void GivenStartedTimer_WhenStopped_ThenElapsedFreezesAndIsStartedFalse()
    {
        var timer = new RaceTimer(10);
        timer.Start();
        Thread.Sleep(20);

        timer.Stop();
        var frozen = timer.Elapsed;
        Thread.Sleep(20);

        Assert.Multiple(() =>
        {
            Assert.That(timer.IsStarted, Is.False);
            Assert.That(timer.Elapsed, Is.EqualTo(frozen));
        });
    }
}
```

- [ ] **Step 2: Run to verify they pass already or fail**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RaceTimerTests"
```
The staged `Elapsed`/`Remaining` already satisfy most assertions; `Start`/`Stop` exist. Expect PASS — these tests pin behavior before cleanup. If any fail, fix `RaceTimer` semantics first.

- [ ] **Step 3: Cleanup RaceTimer**

Remove from `RaceTimer.cs`: `using Microsoft.Win32;`, `public TimeSpan RaceTime { get; set; }`, `public TimerElapsedEventHandler EverySecondElapsed { get; set; }`. No behavior changes.

- [ ] **Step 4: Build + full suite**

```bash
dotnet build source/TrackGenius.sln
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: 0 errors; 141 passed (137 + 4).

- [ ] **Step 5: No commit (user review gate)**

---

## Task 2: `RaceEngine` timer start + `IRace` overload delegation

**Files:**
- Modify: `source/TrackGenius.Core/RaceEngine.cs`
- Test: `source/TrackGenius.UITests/Core/RaceEngineTests.cs` (append)

**Interfaces (produced; consumed by Task 3):**
- `RaceStart(IRace race)` — subscribes `MessageReceived`/`CarDetected`, assigns `_race`, creates `_raceTimer = new RaceTimer(race.CountDownTime)` and calls `_raceTimer.Start()`.
- `RaceStart(ICollection<RaceData> racers)` — subscribes identically, then builds the default `Race` and delegates to the `IRace` overload.
- `RaceTime` / `RemainTime` unchanged pass-throughs.

- [ ] **Step 1: Append the failing tests**

In `RaceEngineTests.cs` (existing fixture — reuse `_wrapper`/`_communicateService`/`_consumer`/`_engine`/`_received` setup):

```csharp
    [Test]
    public void GivenRaceStartWithRace_WhenCalled_ThenTimerRunsAndDetectionStillRaises()
    {
        var race = new TrackGenius.Model.Race(Guid.Empty, TrackGenius.Model.RaceType.FreePractice,
            new TrackGenius.Model.RaceClass("World GT"), 10, new List<RaceData>());

        _engine.RaceStart(race);

        Assert.Multiple(() =>
        {
            Assert.That(_engine.RaceTime, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
            Assert.That(_engine.RemainTime, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
        });

        SendDetection(transponder: 100, milliseconds: 3_600_000);
        Assert.That(_received.Count, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void GivenLegacyRaceStart_WhenCalled_ThenTimerAlsoRuns()
    {
        _engine.RaceStart(new List<RaceData>());

        // Countdown default is 10s; timer started means Remaining <= 10s and RaceTime >= 0.
        Assert.Multiple(() =>
        {
            Assert.That(_engine.RaceTime, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
            Assert.That(_engine.RemainTime, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(10)));
        });
    }
```

(Add `using System;` if the file lacks it; `List<RaceData>`/NUnit usings already exist.)

- [ ] **Step 2: Run to verify RED**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~RaceEngineTests"
```
Expected: the two new tests FAIL — staged `RaceStart(IRace)` never calls `Start()`, so `RaceTime` stays 0 while... (both assertions may pass trivially with 0; the discriminating assertion is `RemainTime <= 10s` only when the stopwatch runs — with a never-started timer `Elapsed`=0, `Remaining`=10s exactly, `Is.LessThanOrEqualTo(10s)` passes. **Strengthen:** in the first test also assert `(_engine.RemainTime - TimeSpan.FromSeconds(10)) < TimeSpan.Zero` after `Thread.Sleep(30)` — a running stopwatch makes `Remaining` strictly below 10s. Include that assertion; drop the weak ones if desired.)

- [ ] **Step 3: Implement**

`RaceEngine.cs`:

```csharp
        public void RaceStart(IRace race)
        {
            _communicateService.MessageReceived += _messageConsumer.ConsumeMessage;
            _messageConsumer.CarDetected += OnCarDetected;

            _race = race;
            _raceTimer = new RaceTimer(_race.CountDownTime);
            _raceTimer.Start();
        }

        public void RaceStart(ICollection<RaceData> racers)
        {
            _communicateService.MessageReceived += _messageConsumer.ConsumeMessage;
            _messageConsumer.CarDetected += OnCarDetected;

            RaceStart(new Race(Guid.NewGuid(), RaceType.FreePractice, new RaceClass("World GT"), 10, racers));
        }
```

(Note: the delegation re-subscribes in the overload — WPF delegate `+=` on the same handler twice would double-fire. **Avoid:** keep subscription in ONE place. Final shape: `RaceStart(ICollection<RaceData>)` delegates subscription-free — move both `+=` lines into the `IRace` overload only, and the legacy overload simply calls `RaceStart(new Race(...))`.)

- [ ] **Step 4: Run to verify GREEN + full suite**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: all pass (143).

- [ ] **Step 5: No commit**

---

## Task 3: VM wiring — auto race name + 1 s refresh clock

**Files:**
- Modify: `source/TrackGenius/ViewModels/RacePageViewModel.cs`
- Test: `source/TrackGenius.UITests/ViewModels/RacePageViewModelTests.cs` (append)

**Interfaces:**
- Consumes: Task 2's started-timer engine; staged `ApplyCurrentRace`.
- Produces: `StartRace()` builds + injects the race; `DispatcherTimer _clockTimer` (1 s) raises `RaceTime`/`RemainTime`/`CurrentTime`.

- [ ] **Step 1: Append the failing test**

```csharp
    [Test]
    public void GivenStartedRace_WhenRaceNameRead_ThenAutoGenerated()
    {
        _connection.Open("COM3", new RobitronicProtocol());
        _viewModel.StartRaceCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.RaceName, Does.StartWith("Quick Race "));
            Assert.That(_viewModel.RaceName, Is.Not.Empty);
        });
    }
```

- [ ] **Step 2: Run to verify RED** (currently `RaceName` is empty — `ApplyCurrentRace` never called).

- [ ] **Step 3: Implement**

In `RacePageViewModel.cs`:

```csharp
    private readonly DispatcherTimer _clockTimer;
```

Ctor (after `StartRaceCommand = ...`):

```csharp
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _clockTimer.Tick += (_, _) =>
        {
            OnPropertyChanged(nameof(RaceTime));
            OnPropertyChanged(nameof(RemainTime));
            OnPropertyChanged(nameof(CurrentTime));
        };
        _clockTimer.Start();
```

In `StartRace()`, replace the engine-start block:

```csharp
            _raceEngine = _raceEngineFactory.CreateRaceEngine();
            _raceEngine.RaceDataChanged += OnRaceDataChanged;

            var race = new Race(Guid.NewGuid(), RaceType.FreePractice, new RaceClass("World GT"), 10, new List<RaceData>())
            {
                RaceName = $"Quick Race {DateTime.Now:yyyy-MM-dd HH:mm}",
            };
            _raceEngine.RaceStart(race);
            ApplyCurrentRace(race);
            LastError = string.Empty;
```

(`using TrackGenius.Model;` already present; `RaceName` needs a settable init on `Race` — the staged model exposes `get;` only: change to `public string RaceName { get; init; }` and keep ctor assignment pattern, OR add a 6th ctor param `raceName`. Choose: `get; init;` with object-initializer set as above requires the ctor to set it — simplest: `public string RaceName { get; set; } = string.Empty;` and assign via initializer. Use settable property.)

- [ ] **Step 4: Build + suite**

```bash
dotnet build source/TrackGenius.sln
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: 0 errors; 144 passed. Note: the DispatcherTimer starts in the VM ctor on the test thread — `DispatcherTimer` requires a running Dispatcher to tick; on the NUnit thread it simply never ticks (no failures, no pumps). Existing tests unaffected.

- [ ] **Step 5: No commit**

---

## Task 4: `RaceTimeBoard` XAML

**Files:**
- Modify: `source/TrackGenius/Views/Pages/QuickRacePage.xaml`

- [ ] **Step 1: Replace the staged board grid**

```xml
        <Grid Grid.Row="0" Name="RaceTimeBoard" MinHeight="48">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <TextBlock Grid.Column="0"
                       Text="{Binding RaceName}"
                       FontSize="20" FontWeight="SemiBold"
                       Foreground="{DynamicResource RaceTextPrimaryBrush}"
                       VerticalAlignment="Center"
                       TextTrimming="CharacterEllipsis"/>

            <StackPanel Grid.Column="1" Margin="24,0,24,0" VerticalAlignment="Center">
                <TextBlock Text="{Binding RaceTime}" FontSize="16"
                           Foreground="{DynamicResource RaceTextPrimaryBrush}"/>
                <TextBlock Text="RACE TIME" FontSize="10"
                           Foreground="{DynamicResource RaceTextMutedBrush}"/>
            </StackPanel>

            <StackPanel Grid.Column="2" Margin="0,0,24,0" VerticalAlignment="Center">
                <TextBlock Text="{Binding RemainTime}" FontSize="16"
                           Foreground="{DynamicResource RaceTextPrimaryBrush}"/>
                <TextBlock Text="REMAINING" FontSize="10"
                           Foreground="{DynamicResource RaceTextMutedBrush}"/>
            </StackPanel>

            <TextBlock Grid.Column="3"
                       Text="{Binding CurrentTime}"
                       FontSize="14"
                       Foreground="{DynamicResource RaceTextMutedBrush}"
                       VerticalAlignment="Center"
                       HorizontalAlignment="Right"/>
        </Grid>
```

- [ ] **Step 2: Build (XAML compile is the gate)**

```bash
dotnet build source/TrackGenius.sln
```
Expected: 0 errors.

- [ ] **Step 3: Full suite + STOP for user review**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: 144 passed, 0 failed. **No commit.** Report the changed files and hand the working tree to the user for visual review.

---

## Final verification

- [ ] Build 0 errors; UITests all pass (144 expected).
- [ ] `git status` shows staged scaffolding + this implementation uncommitted, as the user requires.
- [ ] Hand off for manual visual check (board layout, live ticking, negative remaining display).

## Out of scope (do not do)

- User-editable race name, pause/resume, race-end behavior.
- `RaceOrderRule` new calculators (staged `NotImplementedException` stubs remain).
- Committing anything.
