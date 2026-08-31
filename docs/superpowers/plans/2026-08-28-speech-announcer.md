# Speech Announcer (Interface Phase) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `TrackGenius.Speech` subsystem that turns live standings snapshots into prioritized, policy-filtered English announcements through a faked TTS/audio tail — interfaces only, no real TTS backend.

**Architecture:** One new net8.0 project referencing Core+Model subscribes to `RaceEngine.RaceDataChanged`; `StandingsDiffer` derives facts from consecutive snapshot pairs, the scheduler maps them to prioritized intents, a template renderer + speech formatter produce text, a policy (dedup/expiry/queue-cap) gates a priority `Channel`-based queue, and `FakeTtsEngine`/`FakeAudioPlayer` terminate the pipeline.

**Tech Stack:** .NET 8 (`net8.0`, no WPF), `System.Threading.Channels` (BCL), NUnit 3.14 in a new `TrackGenius.SpeechTests` project.

**Spec:** `docs/superpowers/specs/2026-08-28-speech-announcer-design.md`

## Global Constraints

- Work from repo root `E:\source\repo\TrackGenius`; solution `source/TrackGenius.sln`.
- New projects: `TrackGenius.Speech` (`net8.0`, references `TrackGenius.Core`, `TrackGenius.Model`) and `TrackGenius.SpeechTests` (`net8.0`, IsTestProject, references Speech; NUnit 3.14 + NUnit3TestAdapter 4.5.0 + Microsoft.NET.Test.Sdk 17.8.0 — match UITests versions). Add both via `dotnet sln add` (no hand-edited GUIDs).
- **Speech never references UI; Core never references Speech.** No WPF usages inside Speech.
- Priorities binding: `Background=0, Normal=10, Important=50, High=70, Critical=100`.
- v1 announcements + priorities/expiry (from spec table): RaceStarted=Critical/no expiry; RaceFinished=Critical/no expiry (manual only); FastestLap=High/15s; LeaderChanged=High/10s; PositionChanged=Important/8s.
- Policy numbers binding: dedup window **3000 ms per category**; default expiry **15 s**; queue capacity **20**, dropping oldest Background first then Normal; expiry evaluated at dequeue.
- Templates: en-US, plain `Text` (Ssml null), e.g. FastestLap → `"Car number fifteen, fastest lap, twelve point four three eight seconds."`
- Logging: `Microsoft.Extensions.Logging` `ILogger` injected into RaceAnnouncer/queue; category prefix `TrackGenius.Speech.*`; PascalCase event names with named placeholders (per `source/doc/Logging.md`), e.g. `logger.LogInformation("SpeechMessageQueued MessageId={MessageId} Category={Category} Priority={Priority}", …)`.
- Thread contract: the `RaceDataChanged` handler must return before any pipeline work (capture snapshot, enqueue to channel).
- Tests: file-scoped `namespace TrackGenius.SpeechTests.<Folder>;`, NUnit, `Given…_When…_Then…` names. No `<Nullable>` enable.
- Gates per task: `dotnet build source/TrackGenius.sln` 0 errors + the SpeechTests run green; UITests stay green (baseline: run once at Task 1 to capture the current count, gate on "no failures").
- MSB3021/MSB3027 → `taskkill //IM TrackGenius.UI.exe //F`, retry, else BLOCKED. No GUI launch (user verifies — standing policy).
- **No commits until the user reviews the implementation** (user's review flow for this feature; they will say when to commit).

---

## Task 1: Projects + Abstractions (models & interfaces)

**Files:**
- Create: `source/TrackGenius.Speech/TrackGenius.Speech.csproj`
- Create: `source/TrackGenius.Speech/Abstractions/AnnouncementPriority.cs`
- Create: `source/TrackGenius.Speech/Abstractions/AnnouncementIntent.cs`
- Create: `source/TrackGenius.Speech/Abstractions/SpeechContent.cs`
- Create: `source/TrackGenius.Speech/Abstractions/AudioClip.cs`
- Create: `source/TrackGenius.Speech/Abstractions/SpeechMessage.cs`
- Create: `source/TrackGenius.Speech/Abstractions/PolicyDecision.cs` (record `PolicyDecision(bool Accept, string? Reason)` + `PolicyContext(int QueueLength, DateTimeOffset Now)`)
- Create: `source/TrackGenius.Speech/Abstractions/ISpeechTemplateRenderer.cs`
- Create: `source/TrackGenius.Speech/Abstractions/IAnnouncementPolicy.cs`
- Create: `source/TrackGenius.Speech/Abstractions/ISpeechQueue.cs`
- Create: `source/TrackGenius.Speech/Abstractions/ITtsEngine.cs`
- Create: `source/TrackGenius.Speech/Abstractions/IAudioPlayer.cs`
- Create: `source/TrackGenius.SpeechTests/TrackGenius.SpeechTests.csproj`
- Test: `source/TrackGenius.SpeechTests/Abstractions/ModelContractTests.cs`

**Interfaces (produced; consumed by every later task):** exactly the spec's §Interfaces block — `AnnouncementPriority` enum values 0/10/50/70/100; records `AnnouncementIntent(string Type, AnnouncementPriority Priority, string DriverId, string? DriverDisplayName, IReadOnlyList<RaceStandingsEntry> Snapshot, DateTimeOffset CreatedAt, TimeSpan? ExpiresAfter = null)`, `SpeechContent(string Text, string? Ssml, string Language, string? VoiceId)`, `AudioClip(byte[] Data, string Format)`, `SpeechMessage(Guid Id, string Text, string? Ssml, AnnouncementPriority Priority, string Category, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, bool Interruptible, string Language, string? VoiceId)`, `PolicyDecision`, `PolicyContext`; interfaces `ISpeechTemplateRenderer { SpeechContent Render(AnnouncementIntent intent); }`, `IAnnouncementPolicy { PolicyDecision Evaluate(SpeechMessage message, PolicyContext context); }`, `ISpeechQueue { ValueTask EnqueueAsync(SpeechMessage message, CancellationToken cancellationToken = default); bool TryCancel(Guid messageId); void Clear(AnnouncementPriority minimumPriority = AnnouncementPriority.Background); }`, `ITtsEngine { Task<AudioClip> SynthesizeAsync(SpeechContent content, CancellationToken cancellationToken = default); }`, `IAudioPlayer { Task PlayAsync(AudioClip clip, CancellationToken cancellationToken = default); Task StopAsync(CancellationToken cancellationToken = default); }`.

- [ ] **Step 1: Create the Speech project and files**

`source/TrackGenius.Speech/TrackGenius.Speech.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyTitle>TrackGenius.Speech</AssemblyTitle>
    <Product>TrackGenius.Speech</Product>
    <Copyright>Copyright ©  2026</Copyright>
    <OutputPath>bin\$(Configuration)\</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\TrackGenius.Core\TrackGenius.Core.csproj" />
    <ProjectReference Include="..\TrackGenius.Model\TrackGenius.Model.csproj" />
  </ItemGroup>
</Project>
```

`Abstractions/AnnouncementPriority.cs`:

```csharp
namespace TrackGenius.Speech.Abstractions;

public enum AnnouncementPriority
{
    Background = 0,
    Normal = 10,
    Important = 50,
    High = 70,
    Critical = 100,
}
```

`Abstractions/AnnouncementIntent.cs`:

```csharp
namespace TrackGenius.Speech.Abstractions;

/// <summary>A worth-saying race fact, before language is applied.</summary>
public sealed record AnnouncementIntent(
    string Type,
    AnnouncementPriority Priority,
    string DriverId,
    string? DriverDisplayName,
    IReadOnlyList<RaceStandingsEntry> Snapshot,
    DateTimeOffset CreatedAt,
    TimeSpan? ExpiresAfter = null);
```

`Abstractions/SpeechContent.cs`:

```csharp
namespace TrackGenius.Speech.Abstractions;

/// <summary>Rendered announcement text; Ssml optional so any backend can consume it.</summary>
public sealed record SpeechContent(string Text, string? Ssml, string Language, string? VoiceId);
```

`Abstractions/AudioClip.cs`:

```csharp
namespace TrackGenius.Speech.Abstractions;

/// <summary>Synthesized audio; opaque to the pipeline (format names the codec, e.g. "wav").</summary>
public sealed record AudioClip(byte[] Data, string Format);
```

`Abstractions/SpeechMessage.cs`:

```csharp
namespace TrackGenius.Speech.Abstractions;

/// <summary>The queueable announcement: enough data for ordering, expiry, dedup, diagnostics.</summary>
public sealed record SpeechMessage(
    Guid Id,
    string Text,
    string? Ssml,
    AnnouncementPriority Priority,
    string Category,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool Interruptible,
    string Language,
    string? VoiceId);
```

`Abstractions/PolicyDecision.cs`:

```csharp
namespace TrackGenius.Speech.Abstractions;

public sealed record PolicyDecision(bool Accept, string? Reason);

public sealed record PolicyContext(int QueueLength, DateTimeOffset Now);
```

`Abstractions/ISpeechTemplateRenderer.cs`:

```csharp
namespace TrackGenius.Speech.Abstractions;

public interface ISpeechTemplateRenderer
{
    SpeechContent Render(AnnouncementIntent intent);
}
```

`Abstractions/IAnnouncementPolicy.cs`:

```csharp
namespace TrackGenius.Speech.Abstractions;

public interface IAnnouncementPolicy
{
    PolicyDecision Evaluate(SpeechMessage message, PolicyContext context);
}
```

`Abstractions/ISpeechQueue.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TrackGenius.Speech.Abstractions;

public interface ISpeechQueue
{
    ValueTask EnqueueAsync(SpeechMessage message, CancellationToken cancellationToken = default);

    bool TryCancel(Guid messageId);

    void Clear(AnnouncementPriority minimumPriority = AnnouncementPriority.Background);
}
```

`Abstractions/ITtsEngine.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace TrackGenius.Speech.Abstractions;

public interface ITtsEngine
{
    Task<AudioClip> SynthesizeAsync(SpeechContent content, CancellationToken cancellationToken = default);
}
```

`Abstractions/IAudioPlayer.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace TrackGenius.Speech.Abstractions;

public interface IAudioPlayer
{
    Task PlayAsync(AudioClip clip, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
```

(Files using `IReadOnlyList<>`/`Guid`/`DateTimeOffset` need `using System;` + `using System.Collections.Generic;`; `AnnouncementIntent` also `using TrackGenius.Model;`.)

- [ ] **Step 2: Create the test project**

`source/TrackGenius.SpeechTests/TrackGenius.SpeechTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <AssemblyName>TrackGenius.SpeechTests</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\TrackGenius.Speech\TrackGenius.Speech.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Register both in the solution + write the contract test**

```bash
cd /e/source/repo/TrackGenius
dotnet sln source/TrackGenius.sln add source/TrackGenius.Speech/TrackGenius.Speech.csproj
dotnet sln source/TrackGenius.sln add source/TrackGenius.SpeechTests/TrackGenius.SpeechTests.csproj
```

`source/TrackGenius.SpeechTests/Abstractions/ModelContractTests.cs` (pins the enum values and record shapes the whole pipeline depends on):

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests.Abstractions;

[TestFixture]
public class ModelContractTests
{
    [Test]
    public void GivenPriorities_WhenRead_ThenNumericValuesAreStable()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)AnnouncementPriority.Background, Is.EqualTo(0));
            Assert.That((int)AnnouncementPriority.Normal, Is.EqualTo(10));
            Assert.That((int)AnnouncementPriority.Importance(), Is.EqualTo(50));
            Assert.That((int)AnnouncementPriority.High, Is.EqualTo(70));
            Assert.That((int)AnnouncementPriority.Critical, Is.EqualTo(100));
        });
    }

    [Test]
    public void GivenIntent_WhenConstructedWithDefaults_ThenExpiresAfterNull()
    {
        var intent = new AnnouncementIntent("FastestLap", AnnouncementPriority.High, "100", "Car 100",
            new List<RaceStandingsEntry>(), DateTimeOffset.Now);

        Assert.That(intent.ExpiresAfter, Is.Null);
    }

    [Test]
    public void GivenSpeechMessage_WhenConstructed_ThenAllFieldsHold()
    {
        var created = DateTimeOffset.Now;
        var message = new SpeechMessage(Guid.NewGuid(), "hello", null,
            AnnouncementPriority.High, "FastestLap", created, created + TimeSpan.FromSeconds(15),
            Interruptible: false, "en-US", null);

        Assert.Multiple(() =>
        {
            Assert.That(message.Category, Is.EqualTo("FastestLap"));
            Assert.That(message.Interruptible, Is.False);
            Assert.That(message.Language, Is.EqualTo("en-US"));
        });
    }
}
```

(CAUTION: the first test's `AnnouncementPriority.Importance()` is a deliberate self-check that does NOT compile — the enum member is `Important`. Write it as `AnnouncementPriority.Important`. This transcript-style trap mirrors nothing; just use the correct member name.)

- [ ] **Step 4: Build + run the new test project**

```bash
dotnet build source/TrackGenius.sln
dotnet test source/TrackGenius.SpeechTests/TrackGenius.SpeechTests.csproj
```
Expected: build 0 errors; 3 tests pass. Also run `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj` once and record the passing count (baseline for later tasks; gate = no failures).

- [ ] **Step 5: No commit** (user review flow)

---

## Task 2: `SpeechFormatter` + `SpeechTemplateRenderer` (TDD)

**Files:**
- Create: `source/TrackGenius.Speech/SpeechFormatter.cs`
- Create: `source/TrackGenius.Speech/SpeechTemplateRenderer.cs`
- Test: `source/TrackGenius.SpeechTests/Templates/SpeechFormatterTests.cs`
- Test: `source/TrackGenius.SpeechTests/Templates/SpeechTemplateRendererTests.cs`

**Interfaces:**
- Consumes: Task 1 abstractions; `RaceStandingsEntry` (Model) — `Position`, `RaceData.Car.Transponder.RecoderNumber`, `BestLapTime`.
- Produces: `public static class SpeechFormatter` with `string NumberToSpokenWords(int number)` (0–999: "fifteen", "two hundred seven"), `string LapTimeToSpoken(TimeSpan time)` ("twelve point four three eight seconds" — seconds with 3 decimals, each digit spoken; minutes when ≥60s: "one minute twelve point four zero zero"), `string Ordinal(int position)` ("first","second","third", then "fourth"…"tenth", else `"{n}th"`). `public sealed class SpeechTemplateRenderer : ISpeechTemplateRenderer` producing en-US (`Language="en-US"`, `Ssml=null`, `VoiceId=null`).

- [ ] **Step 1: Write the failing SpeechFormatter tests**

```csharp
using System;
using NUnit.Framework;
using TrackGenius.Speech;

namespace TrackGenius.SpeechTests.Templates;

[TestFixture]
public class SpeechFormatterTests
{
    [TestCase(0, "zero")]
    [TestCase(7, "seven")]
    [TestCase(15, "fifteen")]
    [TestCase(100, "one hundred")]
    [TestCase(207, "two hundred seven")]
    [TestCase(999, "nine hundred ninety nine")]
    public void GivenNumber_WhenSpoken_ThenWords(int number, string expected)
        => Assert.That(SpeechFormatter.NumberToSpokenWords(number), Is.EqualTo(expected));

    [TestCase(1, "first")]
    [TestCase(2, "second")]
    [TestCase(3, "third")]
    [TestCase(4, "fourth")]
    [TestCase(10, "tenth")]
    [TestCase(11, "11th")]
    public void GivenPosition_WhenOrdinal_ThenWord(int position, string expected)
        => Assert.That(SpeechFormatter.Ordinal(position), Is.EqualTo(expected));

    [Test]
    public void GivenSubMinuteLap_WhenSpoken_ThenSecondsDigits()
        => Assert.That(SpeechFormatter.LapTimeToSpoken(TimeSpan.FromSeconds(12.438)),
            Is.EqualTo("twelve point four three eight seconds"));

    [Test]
    public void GivenOverMinuteLap_WhenSpoken_ThenMinutesThenSeconds()
        => Assert.That(SpeechFormatter.LapTimeToSpoken(TimeSpan.FromMilliseconds(72_400)),
            Is.EqualTo("one minute twelve point four zero zero seconds"));
}
```

- [ ] **Step 2: Run — expect compile failure** (`SpeechFormatter` missing).

```bash
dotnet test source/TrackGenius.SpeechTests/TrackGenius.SpeechTests.csproj --filter "FullyQualifiedName~SpeechFormatterTests"
```

- [ ] **Step 3: Implement SpeechFormatter**

```csharp
using System;
using System.Globalization;

namespace TrackGenius.Speech;

/// <summary>Pure spoken-English formatting for numbers, ordinals and lap times.</summary>
public static class SpeechFormatter
{
    private static readonly string[] Ones = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
    private static readonly string[] Teens = { "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
    private static readonly string[] Tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

    public static string NumberToSpokenWords(int number)
    {
        if (number is < 0 or > 999)
            return number.ToString(CultureInfo.InvariantCulture);
        if (number < 10) return Ones[number];
        if (number < 20) return Teens[number - 10];
        if (number < 100)
            return Tens[number / 10] + (number % 10 == 0 ? "" : " " + Ones[number % 10]);
        return Ones[number / 100] + " hundred" + (number % 100 == 0 ? "" : " " + NumberToSpokenWords(number % 100));
    }

    public static string Ordinal(int position) => position switch
    {
        1 => "first",
        2 => "second",
        3 => "third",
        4 => "fourth",
        5 => "fifth",
        6 => "sixth",
        7 => "seventh",
        8 => "eighth",
        9 => "ninth",
        10 => "tenth",
        _ => position.ToString(CultureInfo.InvariantCulture) + "th",
    };

    public static string LapTimeToSpoken(TimeSpan time)
    {
        var secondsSpoken = SecondsWithDigits(time.TotalSeconds % 60);
        if (time.TotalMinutes < 1)
            return secondsSpoken + " seconds";
        var minutes = (int)time.TotalMinutes;
        return $"{NumberToSpokenWords(minutes)} minute{(minutes == 1 ? "" : "s")} {secondsSpoken} seconds";
    }

    // "twelve point four three eight" — whole seconds as words, decimals digit by digit.
    private static string SecondsWithDigits(double seconds)
    {
        var whole = (int)seconds;
        var digits = ((int)Math.Round((seconds - whole) * 1000)).ToString("000", CultureInfo.InvariantCulture);
        return $"{NumberToSpokenWords(whole)} point {string.Join(" ", digits.ToCharArray())}";
    }
}
```

- [ ] **Step 4: Run formatter tests — green.**

- [ ] **Step 5: Write the failing template tests**

`SpeechTemplateRendererTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrackGenius.Model;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests.Templates;

[TestFixture]
public class SpeechTemplateRendererTests
{
    private static RaceStandingsEntry Entry(string transponder, int position, double bestSeconds, bool isRaceBest)
        => new(new RaceData(AnonymousDriverCreator.CreateAnonymous(transponder),
                AnonymousDriverCreator.CreateAnonymous(transponder).Cars.First()),
            position, TimeSpan.FromSeconds(bestSeconds), TimeSpan.FromSeconds(bestSeconds),
            "-", "-", "-", "-", "-", "-", "-", isRaceBest);

    private readonly SpeechTemplateRenderer _renderer = new();

    [Test]
    public void GivenFastestLapIntent_WhenRendered_ThenSpokenText()
    {
        var intent = new AnnouncementIntent("FastestLap", AnnouncementPriority.High, "100", null,
            new List<RaceStandingsEntry> { Entry("100", 1, 12.438, true) }, DateTimeOffset.Now);

        var content = _renderer.Render(intent);

        Assert.Multiple(() =>
        {
            Assert.That(content.Text, Is.EqualTo("Car number one hundred, fastest lap, twelve point four three eight seconds."));
            Assert.That(content.Language, Is.EqualTo("en-US"));
            Assert.That(content.Ssml, Is.Null);
        });
    }

    [Test]
    public void GivenLeaderChangedIntent_WhenRendered_ThenSpokenText()
    {
        var intent = new AnnouncementIntent("LeaderChanged", AnnouncementPriority.High, "200", null,
            new List<RaceStandingsEntry> { Entry("200", 1, 15.0, false), Entry("100", 2, 15.5, false) },
            DateTimeOffset.Now);

        Assert.That(_renderer.Render(intent).Text,
            Is.EqualTo("Car number two hundred takes the lead."));
    }

    [Test]
    public void GivenPositionChangedIntent_WhenRendered_ThenSpokenText()
    {
        var intent = new AnnouncementIntent("PositionChanged", AnnouncementPriority.Important, "100", null,
            new List<RaceStandingsEntry> { Entry("200", 1, 15.0, false), Entry("100", 2, 15.5, false) },
            DateTimeOffset.Now);

        Assert.That(_renderer.Render(intent).Text,
            Is.EqualTo("Car number one hundred moves up to second place."));
    }

    [Test]
    public void GivenRaceStartedIntent_WhenRendered_ThenSpokenText()
    {
        var intent = new AnnouncementIntent("RaceStarted", AnnouncementPriority.Critical, "", null,
            new List<RaceStandingsEntry>(), DateTimeOffset.Now);

        Assert.That(_renderer.Render(intent).Text, Is.EqualTo("Race started."));
    }

    [Test]
    public void GivenRaceFinishedIntent_WhenRendered_ThenSpokenText()
    {
        var intent = new AnnouncementIntent("RaceFinished", AnnouncementPriority.Critical, "", null,
            new List<RaceStandingsEntry>(), DateTimeOffset.Now);

        Assert.That(_renderer.Render(intent).Text, Is.EqualTo("Race finished."));
    }
}
```

- [ ] **Step 6: Run — expect failure** (renderer missing). **Step 7: Implement** `SpeechTemplateRenderer.cs`:

```csharp
using System.Linq;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech;

/// <summary>en-US plain-text templates; SSML reserved, not generated in v1.</summary>
public sealed class SpeechTemplateRenderer : ISpeechTemplateRenderer
{
    public SpeechContent Render(AnnouncementIntent intent)
    {
        var text = intent.Type switch
        {
            "FastestLap" => FastestLap(intent),
            "LeaderChanged" => $"Car number {SpeechFormatter.NumberToSpokenWords(CarNumber(intent))} takes the lead.",
            "PositionChanged" => $"Car number {SpeechFormatter.NumberToSpokenWords(CarNumber(intent))} moves up to {SpeechFormatter.Ordinal(intent.Snapshot.First(e => DriverKey(e) == intent.DriverId).Position)} place.",
            "RaceStarted" => "Race started.",
            "RaceFinished" => "Race finished.",
            _ => string.Empty,
        };
        return new SpeechContent(text, null, "en-US", null);
    }

    private static string FastestLap(AnnouncementIntent intent)
    {
        var entry = intent.Snapshot.First(e => DriverKey(e) == intent.DriverId);
        return $"Car number {SpeechFormatter.NumberToSpokenWords(CarNumber(intent))}, fastest lap, {SpeechFormatter.LapTimeToSpoken(entry.BestLapTime)}.";
    }

    // v1 key: the transponder id is the stable driver key across snapshots.
    internal static string DriverKey(RaceStandingsEntry entry)
        => entry.RaceData.Car.Transponder.RecoderNumber;

    // Car "number" spoken from the transponder id's numeric value; falls back to digits-as-is.
    private static int CarNumber(AnnouncementIntent intent)
        => int.TryParse(intent.DriverId, out var n) ? n : 0;
}
```

- [ ] **Step 8: Run all SpeechTests — green (3 contract + 13 formatter + 5 template ≈ 21). Full build + UITests baseline unchanged. Step 9: No commit.**

---

## Task 3: `AnnouncementPolicy` (TDD)

**Files:**
- Create: `source/TrackGenius.Speech/AnnouncementPolicy.cs`
- Test: `source/TrackGenius.SpeechTests/Policy/AnnouncementPolicyTests.cs`

**Interfaces:**
- Consumes: Task 1 `IAnnouncementPolicy`/`SpeechMessage`/`PolicyDecision`/`PolicyContext`.
- Produces: `public sealed class AnnouncementPolicy : IAnnouncementPolicy` with ctor `AnnouncementPolicy(TimeSpan dedupWindow, TimeSpan defaultExpiry, int maxQueueLength)`; internal state remembers last-accepted time per `Category`; `Evaluate` rejects when (a) message expired (`ExpiresAt < context.Now` → Reason "Expired"), (b) same category accepted within `dedupWindow` → "Duplicate", (c) `context.QueueLength >= maxQueueLength` and priority ≤ Normal → "QueueFull". Accepts otherwise with Reason null.

- [ ] **Step 1: Failing tests**

```csharp
using System;
using NUnit.Framework;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests.Policy;

[TestFixture]
public class AnnouncementPolicyTests
{
    private static SpeechMessage Message(string category, AnnouncementPriority priority, DateTimeOffset createdAt, DateTimeOffset? expiresAt = null)
        => new(Guid.NewGuid(), "text", null, priority, category, createdAt, expiresAt, false, "en-US", null);

    [Test]
    public void GivenExpiredMessage_WhenEvaluated_ThenRejectedAsExpired()
    {
        var policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), 20);
        var now = DateTimeOffset.Now;
        var message = Message("FastestLap", AnnouncementPriority.High, now - TimeSpan.FromSeconds(20), now - TimeSpan.FromSeconds(5));

        var decision = policy.Evaluate(message, new PolicyContext(0, now));

        Assert.That(decision.Accept, Is.False);
        Assert.That(decision.Reason, Is.EqualTo("Expired"));
    }

    [Test]
    public void GivenSameCategoryWithinWindow_WhenEvaluated_ThenRejectedAsDuplicate()
    {
        var policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), 20);
        var now = DateTimeOffset.Now;
        policy.Evaluate(Message("LeaderChanged", AnnouncementPriority.High, now), new PolicyContext(0, now));

        var decision = policy.Evaluate(Message("LeaderChanged", AnnouncementPriority.High, now + TimeSpan.FromSeconds(1)), new PolicyContext(0, now + TimeSpan.FromSeconds(1)));

        Assert.Multiple(() =>
        {
            Assert.That(decision.Accept, Is.False);
            Assert.That(decision.Reason, Is.EqualTo("Duplicate"));
        });
    }

    [Test]
    public void GivenSameCategoryAfterWindow_WhenEvaluated_ThenAccepted()
    {
        var policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), 20);
        var now = DateTimeOffset.Now;
        policy.Evaluate(Message("LeaderChanged", AnnouncementPriority.High, now), new PolicyContext(0, now));

        var decision = policy.Evaluate(Message("LeaderChanged", AnnouncementPriority.High, now + TimeSpan.FromSeconds(4)), new PolicyContext(0, now + TimeSpan.FromSeconds(4)));

        Assert.That(decision.Accept, Is.True);
    }

    [Test]
    public void GivenFullQueueAndNormalPriority_WhenEvaluated_ThenRejectedAsQueueFull()
    {
        var policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), maxQueueLength: 2);
        var now = DateTimeOffset.Now;

        var decision = policy.Evaluate(Message("LapCommentary", AnnouncementPriority.Normal, now), new PolicyContext(2, now));

        Assert.Multiple(() =>
        {
            Assert.That(decision.Accept, Is.False);
            Assert.That(decision.Reason, Is.EqualTo("QueueFull"));
        });
    }

    [Test]
    public void GivenFullQueueAndCriticalPriority_WhenEvaluated_ThenAccepted()
    {
        var policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), maxQueueLength: 2);
        var now = DateTimeOffset.Now;

        var decision = policy.Evaluate(Message("RaceStarted", AnnouncementPriority.Critical, now), new PolicyContext(2, now));

        Assert.That(decision.Accept, Is.True);
    }
}
```

- [ ] **Step 2: RED** (type missing) → **Step 3: Implement**

```csharp
using System;
using System.Collections.Generic;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech;

/// <summary>v1 policy: expiry, per-category dedup window, queue cap (Critical/High bypass the cap).</summary>
public sealed class AnnouncementPolicy : IAnnouncementPolicy
{
    private readonly TimeSpan _dedupWindow;
    private readonly int _maxQueueLength;
    private readonly Dictionary<string, DateTimeOffset> _lastAcceptedPerCategory = new();

    public AnnouncementPolicy(TimeSpan dedupWindow, TimeSpan defaultExpiry, int maxQueueLength)
    {
        _dedupWindow = dedupWindow;
        _maxQueueLength = maxQueueLength;
    }

    public PolicyDecision Evaluate(SpeechMessage message, PolicyContext context)
    {
        if (message.ExpiresAt is { } expiry && expiry < context.Now)
            return new PolicyDecision(false, "Expired");

        if (_lastAcceptedPerCategory.TryGetValue(message.Category, out var lastAccepted)
            && context.Now - lastAccepted < _dedupWindow)
            return new PolicyDecision(false, "Duplicate");

        if (context.QueueLength >= _maxQueueLength && message.Priority <= AnnouncementPriority.Normal)
            return new PolicyDecision(false, "QueueFull");

        _lastAcceptedPerCategory[message.Category] = context.Now;
        return new PolicyDecision(true, null);
    }
}
```

- [ ] **Step 4: Green; build; Step 5: No commit.**

---

## Task 4: `SpeechQueue` (priority Channel-based, TDD)

**Files:**
- Create: `source/TrackGenius.Speech/SpeechQueue.cs`
- Test: `source/TrackGenius.SpeechTests/Queue/SpeechQueueTests.cs`

**Interfaces:**
- Consumes: Task 1 `ISpeechQueue`.
- Produces: `public sealed class SpeechQueue : ISpeechQueue` — ctor `(int capacity, ILogger<SpeechQueue> logger)` (logger may be `NullLogger<SpeechQueue>.Instance` in tests); `EnqueueAsync` adds to a bounded `Channel<SpeechMessage>`-backed pending list honoring capacity (drops oldest lowest-priority when full, logs `SpeechMessageDropped`); `TryDequeue(Func<SpeechMessage, bool> filter)`-style surface is NOT the interface — instead expose `Task<SpeechMessage?> DequeueAsync(CancellationToken ct)` (null when a message expired or was cancelled) and `int Count`; `TryCancel(Guid)` marks cancelled; `Clear(AnnouncementPriority minimum)` removes pending messages with priority **below** `minimum`. **Interface note:** `DequeueAsync`/`Count` are extra members beyond `ISpeechQueue` (interface stays as spec'd); the worker in Task 6 consumes them.

- [ ] **Step 1: Failing tests** (use `NullLogger<SpeechQueue>.Instance` from `Microsoft.Extensions.Logging.Abstractions`):

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests.Queue;

[TestFixture]
public class SpeechQueueTests
{
    private static SpeechMessage Message(string text, AnnouncementPriority priority)
        => new(Guid.NewGuid(), text, null, priority, text, DateTimeOffset.Now,
            DateTimeOffset.Now + TimeSpan.FromSeconds(30), false, "en-US", null);

    [Test]
    public async Task GivenMixedPriorities_WhenDequeued_ThenCriticalFirstThenFifo()
    {
        var queue = new SpeechQueue(20, NullLogger<SpeechQueue>.Instance);
        await queue.EnqueueAsync(Message("normal-a", AnnouncementPriority.Normal));
        await queue.EnqueueAsync(Message("critical-a", AnnouncementPriority.Critical));
        await queue.EnqueueAsync(Message("normal-b", AnnouncementPriority.Normal));

        Assert.Multiple(() =>
        {
            Assert.That((await queue.DequeueAsync(CancellationToken.None))!.Text, Is.EqualTo("critical-a"));
            Assert.That((await queue.DequeueAsync(CancellationToken.None))!.Text, Is.EqualTo("normal-a"));
            Assert.That((await queue.DequeueAsync(CancellationToken.None))!.Text, Is.EqualTo("normal-b"));
        });
    }

    [Test]
    public async Task GivenCapacityFull_WhenEnqueueingLowPriority_ThenOldestNormalDropped()
    {
        var queue = new SpeechQueue(2, NullLogger<SpeechQueue>.Instance);
        await queue.EnqueueAsync(Message("normal-1", AnnouncementPriority.Normal));
        await queue.EnqueueAsync(Message("high-1", AnnouncementPriority.High));
        await queue.EnqueueAsync(Message("normal-2", AnnouncementPriority.Normal));

        Assert.That(queue.Count, Is.EqualTo(2));
        var texts = new[] { await queue.DequeueAsync(CancellationToken.None), await queue.DequeueAsync(CancellationToken.None) };
        Assert.That(texts.Select(m => m!.Text), Is.EqualTo(new[] { "high-1", "normal-2" }));
    }

    [Test]
    public async Task GivenCancelledMessage_WhenDequeued_ThenSkipped()
    {
        var queue = new SpeechQueue(20, NullLogger<SpeechQueue>.Instance);
        await queue.EnqueueAsync(Message("doomed", AnnouncementPriority.Normal));
        await queue.EnqueueAsync(Message("kept", AnnouncementPriority.Normal));

        queue.TryCancel(queuePendingId(queue, "doomed"));
        Assert.That((await queue.DequeueAsync(CancellationToken.None))!.Text, Is.EqualTo("kept"));
    }

    [Test]
    public async Task GivenExpiredMessage_WhenDequeued_ThenSkippedAndNextReturned()
    {
        var queue = new SpeechQueue(20, NullLogger<SpeechQueue>.Instance);
        var expired = new SpeechMessage(Guid.NewGuid(), "expired", null, AnnouncementPriority.Normal, "expired",
            DateTimeOffset.Now - TimeSpan.FromSeconds(60), DateTimeOffset.Now - TimeSpan.FromSeconds(30), false, "en-US", null);
        await queue.EnqueueAsync(expired);
        await queue.EnqueueAsync(Message("fresh", AnnouncementPriority.Normal));

        Assert.That((await queue.DequeueAsync(CancellationToken.None))!.Text, Is.EqualTo("fresh"));
    }

    [Test]
    public async Task GivenClearHigh_WhenCleared_ThenOnlyCriticalAndHighRemain()
    {
        var queue = new SpeechQueue(20, NullLogger<SpeechQueue>.Instance);
        await queue.EnqueueAsync(Message("n", AnnouncementPriority.Normal));
        await queue.EnqueueAsync(Message("i", AnnouncementPriority.Important));
        await queue.EnqueueAsync(Message("h", AnnouncementPriority.High));
        await queue.EnqueueAsync(Message("c", AnnouncementPriority.Critical));

        queue.Clear(AnnouncementPriority.High);

        Assert.That(queue.Count, Is.EqualTo(2));   // h + c remain
    }

    private static Guid queuePendingId(SpeechQueue queue, string text)
        => queue.Pending.Single(m => m.Text == text).Id;
}
```

(`Pending` — expose the internal list read-only for tests: `public IReadOnlyList<SpeechMessage> Pending { get; }` backed by the same collection.)

- [ ] **Step 2: RED → Step 3: Implement** `SpeechQueue.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech;

/// <summary>Priority queue: highest priority first, FIFO within a priority; bounded with oldest-lowest drop.</summary>
public sealed class SpeechQueue : ISpeechQueue
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly ILogger<SpeechQueue> _logger;
    private readonly List<SpeechMessage> _pending = new();
    private readonly HashSet<Guid> _cancelled = new();

    public SpeechQueue(int capacity, ILogger<SpeechQueue> logger)
    {
        _capacity = capacity;
        _logger = logger;
    }

    public int Count { get { lock (_gate) return _pending.Count; } }

    public IReadOnlyList<SpeechMessage> Pending { get { lock (_gate) return _pending.ToArray(); } }

    public ValueTask EnqueueAsync(SpeechMessage message, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_pending.Count >= _capacity)
            {
                var victim = _pending
                    .Where(m => m.Priority <= AnnouncementPriority.Normal)
                    .OrderBy(m => m.Priority).ThenBy(m => m.CreatedAt).FirstOrDefault();
                if (victim == null && message.Priority <= AnnouncementPriority.Normal)
                {
                    _logger.LogWarning("SpeechMessageDropped MessageId={MessageId} Reason={Reason}", message.Id, "QueueFullNoLowPriority");
                    return ValueTask.CompletedTask;
                }
                if (victim != null)
                {
                    _pending.Remove(victim);
                    _logger.LogWarning("SpeechMessageDropped MessageId={MessageId} Reason={Reason}", victim.Id, "QueueFull");
                }
            }

            _pending.Add(message);
        }

        _logger.LogInformation("SpeechMessageQueued MessageId={MessageId} Category={Category} Priority={Priority}",
            message.Id, message.Category, message.Priority);
        return ValueTask.CompletedTask;
    }

    public bool TryCancel(Guid messageId)
    {
        lock (_gate)
            return _cancelled.Add(messageId) && _pending.RemoveAll(m => m.Id == messageId) >= 0;
    }

    public void Clear(AnnouncementPriority minimumPriority = AnnouncementPriority.Background)
    {
        lock (_gate)
            _pending.RemoveAll(m => m.Priority < minimumPriority);
    }

    /// <summary>Next runnable message (highest priority, oldest first), skipping cancelled/expired; null when none.</summary>
    public Task<SpeechMessage?> DequeueAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            SpeechMessage? next = null;
            while (_pending.Count > 0)
            {
                next = _pending
                    .OrderByDescending(m => m.Priority)
                    .ThenBy(m => m.CreatedAt)
                    .First();
                _pending.Remove(next);
                if (_cancelled.Remove(next.Id))
                    continue;
                if (next.ExpiresAt is { } expiry && expiry < DateTimeOffset.Now)
                {
                    _logger.LogInformation("SpeechMessageExpired MessageId={MessageId}", next.Id);
                    continue;
                }
                return Task.FromResult<SpeechMessage?>(next);
            }
            return Task.FromResult<SpeechMessage?>(null);
        }
    }
}
```

(Remove the unused `Channels` using if the compiler warns — the bounded-list approach replaced it; keep the simplest compiling form.)

- [ ] **Step 4: Green (5 queue tests). Build. Step 5: No commit.**

---

## Task 5: `StandingsDiffer` + `AnnouncementScheduler` (TDD)

**Files:**
- Create: `source/TrackGenius.Speech/StandingsDiffer.cs`
- Create: `source/TrackGenius.Speech/AnnouncementScheduler.cs`
- Test: `source/TrackGenius.SpeechTests/Scheduling/StandingsDifferTests.cs`
- Test: `source/TrackGenius.SpeechTests/Scheduling/AnnouncementSchedulerTests.cs`

**Interfaces:**
- Consumes: `RaceStandingsEntry` (Model); Task 1 `AnnouncementIntent`; Task 2 `SpeechTemplateRenderer.DriverKey`.
- Produces:
  - `public sealed record DerivedFact(string Type, AnnouncementPriority Priority, string DriverId, string? DriverDisplayName, TimeSpan? ExpiresAfter)`;
  - `public static class StandingsDiffer` with `IReadOnlyList<DerivedFact> Diff(IReadOnlyList<RaceStandingsEntry>? previous, IReadOnlyList<RaceStandingsEntry> current, DateTimeOffset now)`: first non-empty snapshot → `RaceStarted`; previous empty-of-laps and current has laps is the same case; leader (position 1) `DriverKey` differs → `LeaderChanged` (High, 10s); entry newly `IsRaceBestLap` AND its `BestLapTime` < previous race-best (or no previous best) → `FastestLap` (High, 15s); two entries in the top-3 swapped relative order → one `PositionChanged` per gaining driver (Important, 8s). Same-pair or empty current → empty list.
  - `public sealed class AnnouncementScheduler` with `IReadOnlyList<AnnouncementIntent> Schedule(IReadOnlyList<DerivedFact> facts, IReadOnlyList<RaceStandingsEntry> snapshot, DateTimeOffset now)` mapping each fact 1:1 to an intent (Type/Priority/DriverId/DisplayName/Snapshot/CreatedAt/ExpiresAfter verbatim).

- [ ] **Step 1: Failing StandingsDifferTests** — reuse the `Entry(...)` helper from Task 2's template tests (copy it into a shared `TestStandings` static helper class in `source/TrackGenius.SpeechTests/TestStandings.cs` and refactor both test files to use it):

```csharp
// TestStandings.cs
using System;
using System.Linq;
using TrackGenius.Model;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests;

public static class TestStandings
{
    public static RaceStandingsEntry Entry(string transponder, int position, double bestSeconds, bool isRaceBest)
        => new(new RaceData(AnonymousDriverCreator.CreateAnonymous(transponder),
                AnonymousDriverCreator.CreateAnonymous(transponder).Cars.First()),
            position, TimeSpan.FromSeconds(bestSeconds), TimeSpan.FromSeconds(bestSeconds),
            "-", "-", "-", "-", "-", "-", "-", isRaceBest);
}
```

```csharp
// StandingsDifferTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests.Scheduling;

[TestFixture]
public class StandingsDifferTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Now;

    [Test]
    public void GivenFirstSnapshot_WhenDiffed_ThenRaceStartedOnly()
    {
        var current = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true) };

        var facts = StandingsDiffer.Diff(null, current, Now);

        Assert.That(facts.Select(f => f.Type), Is.EqualTo(new[] { "RaceStarted" }));
    }

    [Test]
    public void GivenLeaderChanged_WhenDiffed_ThenLeaderChangedFact()
    {
        var previous = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true), TestStandings.Entry("200", 2, 21.0, false) };
        var current = new List<RaceStandingsEntry> { TestStandings.Entry("200", 1, 20.5, true), TestStandings.Entry("100", 2, 20.0, false) };

        var facts = StandingsDiffer.Diff(previous, current, Now);

        var leader = facts.Single(f => f.Type == "LeaderChanged");
        Assert.Multiple(() =>
        {
            Assert.That(leader.DriverId, Is.EqualTo("200"));
            Assert.That(leader.Priority, Is.EqualTo(AnnouncementPriority.High));
            Assert.That(leader.ExpiresAfter, Is.EqualTo(TimeSpan.FromSeconds(10)));
        });
    }

    [Test]
    public void GivenNewRaceBest_WhenDiffed_ThenFastestLapFact()
    {
        var previous = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true) };
        var current = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 19.5, true) };

        var facts = StandingsDiffer.Diff(previous, current, Now);

        var fastest = facts.Single(f => f.Type == "FastestLap");
        Assert.Multiple(() =>
        {
            Assert.That(fastest.DriverId, Is.EqualTo("100"));
            Assert.That(fastest.Priority, Is.EqualTo(AnnouncementPriority.High));
            Assert.That(fastest.ExpiresAfter, Is.EqualTo(TimeSpan.FromSeconds(15)));
        });
    }

    [Test]
    public void GivenSameRaceBestRetained_WhenDiffed_ThenNoFastestLapFact()
    {
        var previous = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 19.5, true) };
        var current = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 19.5, true) };

        Assert.That(StandingsDiffer.Diff(previous, current, Now), Is.Empty);
    }

    [Test]
    public void GivenTopThreeSwap_WhenDiffed_ThenPositionChangedForGainer()
    {
        var previous = new List<RaceStandingsEntry>
        {
            TestStandings.Entry("100", 1, 20.0, true), TestStandings.Entry("200", 2, 21.0, false), TestStandings.Entry("300", 3, 22.0, false),
        };
        var current = new List<RaceStandingsEntry>
        {
            TestStandings.Entry("100", 1, 20.0, true), TestStandings.Entry("300", 2, 22.0, false), TestStandings.Entry("200", 3, 21.0, false),
        };

        var facts = StandingsDiffer.Diff(previous, current, Now);

        var position = facts.Single(f => f.Type == "PositionChanged");
        Assert.Multiple(() =>
        {
            Assert.That(position.DriverId, Is.EqualTo("300"));
            Assert.That(position.Priority, Is.EqualTo(AnnouncementPriority.Important));
            Assert.That(position.ExpiresAfter, Is.EqualTo(TimeSpan.FromSeconds(8)));
        });
    }

    [Test]
    public void GivenIdenticalSnapshots_WhenDiffed_ThenNoFacts()
    {
        var snapshot = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true) };

        Assert.That(StandingsDiffer.Diff(snapshot, snapshot, Now), Is.Empty);
    }
}
```

- [ ] **Step 2: RED → Step 3: Implement** `StandingsDiffer.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech;

public sealed record DerivedFact(
    string Type, AnnouncementPriority Priority, string DriverId, string? DriverDisplayName, TimeSpan? ExpiresAfter);

/// <summary>Pure diff of consecutive standings snapshots → announcable facts.</summary>
public static class StandingsDiffer
{
    private static readonly TimeSpan LeaderExpiry = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FastestLapExpiry = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PositionExpiry = TimeSpan.FromSeconds(8);

    public static IReadOnlyList<DerivedFact> Diff(
        IReadOnlyList<RaceStandingsEntry>? previous,
        IReadOnlyList<RaceStandingsEntry> current,
        DateTimeOffset now)
    {
        var facts = new List<DerivedFact>();

        if (previous is null || previous.Count == 0)
        {
            if (current.Count > 0)
                facts.Add(new DerivedFact("RaceStarted", AnnouncementPriority.Critical, null!, null, null));
            return facts;
        }

        var previousLeader = previous.FirstOrDefault(e => e.Position == 1);
        var currentLeader = current.FirstOrDefault(e => e.Position == 1);
        if (previousLeader != null && currentLeader != null && Key(previousLeader) != Key(currentLeader))
            facts.Add(new DerivedFact("LeaderChanged", AnnouncementPriority.High, Key(currentLeader), Name(currentLeader), LeaderExpiry));

        var previousBest = previous.Where(e => e.IsRaceBestLap).Select(e => e.BestLapTime).DefaultIfEmpty().Min();
        var currentBestEntry = current.FirstOrDefault(e => e.IsRaceBestLap);
        if (currentBestEntry != null && currentBestEntry.BestLapTime < previousBest)
            facts.Add(new DerivedFact("FastestLap", AnnouncementPriority.High, Key(currentBestEntry), Name(currentBestEntry), FastestLapExpiry));

        foreach (var gain in GainersInTopThree(previous, current))
            facts.Add(new DerivedFact("PositionChanged", AnnouncementPriority.Important, gain.Key, gain.Name, PositionExpiry));

        return facts;
    }

    private static IEnumerable<(string Key, string? Name)> GainersInTopThree(
        IReadOnlyList<RaceStandingsEntry> previous, IReadOnlyList<RaceStandingsEntry> current)
    {
        var previousPositions = previous.Where(e => e.Position <= 3).ToDictionary(Key);
        foreach (var entry in current.Where(e => e.Position <= 3))
        {
            if (previousPositions.TryGetValue(Key(entry), out var before) && entry.Position < before.Position)
                yield return (Key(entry), Name(entry));
        }
    }

    private static string Key(RaceStandingsEntry entry)
        => SpeechTemplateRenderer.DriverKey(entry);

    private static string? Name(RaceStandingsEntry entry)
        => entry.RaceData.Driver?.DriverName;
}
```

- [ ] **Step 4: Green. Step 5: failing scheduler tests → implement the 1:1 mapper → green:**

```csharp
// AnnouncementScheduler.cs
using System;
using System.Collections.Generic;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech;

/// <summary>Maps derived facts to announcement intents (1:1 in v1).</summary>
public sealed class AnnouncementScheduler
{
    public IReadOnlyList<AnnouncementIntent> Schedule(
        IReadOnlyList<DerivedFact> facts,
        IReadOnlyList<RaceStandingsEntry> snapshot,
        DateTimeOffset now)
    {
        var intents = new List<AnnouncementIntent>(facts.Count);
        foreach (var fact in facts)
            intents.Add(new AnnouncementIntent(
                fact.Type, fact.Priority, fact.DriverId, fact.DriverDisplayName,
                snapshot, now, fact.ExpiresAfter));
        return intents;
    }
}
```

(Scheduler test: one fact in → one intent out with fields verbatim — write `GivenFacts_WhenScheduled_ThenIntentsCarryFields` asserting Type/Priority/DriverId/ExpiresAfter equality.)

- [ ] **Step 6: Full SpeechTests green; build; Step 7: No commit.**

---

## Task 6: `RaceAnnouncer` orchestration + Fakes + end-to-end (TDD)

**Files:**
- Create: `source/TrackGenius.Speech/Fakes/FakeTtsEngine.cs`
- Create: `source/TrackGenius.Speech/Fakes/FakeAudioPlayer.cs`
- Create: `source/TrackGenius.Speech/RaceAnnouncer.cs`
- Test: `source/TrackGenius.SpeechTests/RaceAnnouncerTests.cs`

**Interfaces:**
- Consumes: Tasks 1–5 everything; `RaceEngine` (Core) — `RaceDataChanged` event only.
- Produces: `public sealed class RaceAnnouncer : IDisposable` — ctor `(AnnouncementScheduler scheduler, ISpeechTemplateRenderer renderer, IAnnouncementPolicy policy, SpeechQueue queue, ITtsEngine ttsEngine, IAudioPlayer audioPlayer, ILogger<RaceAnnouncer> logger)`; `bool Enabled { get; set; }` (default **false**); `Attach(RaceEngine engine)` / `Detach(RaceEngine engine)`; `Dispose()` = detach + stop worker. Fakes: `FakeTtsEngine : ITtsEngine` records `IReadOnlyList<string> SynthesizedTexts`, returns `AudioClip(Array.Empty<byte>(), "none")`; `FakeAudioPlayer : IAudioPlayer` records `IReadOnlyList<string> PlayedTexts` (exposed via an optional callback set by RaceAnnouncer? No — fake records the clip's identity; to assert text, RaceAnnouncer passes the message to the fake via a `Func<AudioClip,string>`? SIMPLEST: fakes carry `ConcurrentQueue<string> Announcements` filled by the TEST asserting on `FakeTtsEngine.SynthesizedTexts` — TTS sees `SpeechContent.Text`, that's the announcement text; player fake just counts plays.)

- [ ] **Step 1: Fakes**

```csharp
// Fakes/FakeTtsEngine.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech.Fakes;

/// <summary>Records synthesized texts; emits silent clips. No audio device.</summary>
public sealed class FakeTtsEngine : ITtsEngine
{
    private readonly object _gate = new();
    private readonly List<string> _texts = new();

    public IReadOnlyList<string> SynthesizedTexts { get { lock (_gate) return _texts.ToArray(); } }

    public Task<AudioClip> SynthesizeAsync(SpeechContent content, CancellationToken cancellationToken = default)
    {
        lock (_gate) _texts.Add(content.Text);
        return Task.FromResult(new AudioClip(Array.Empty<byte>(), "none"));
    }
}
```

(`using System;` needed for Array. Same pattern for `FakeAudioPlayer` — records nothing textual, exposes `PlayCount`/`StopCount`, PlayAsync completes immediately.)

```csharp
// Fakes/FakeAudioPlayer.cs
using System.Threading;
using System.Threading.Tasks;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech.Fakes;

public sealed class FakeAudioPlayer : IAudioPlayer
{
    private int _plays;
    private int _stops;

    public int PlayCount => _plays;
    public int StopCount => _stops;

    public Task PlayAsync(AudioClip clip, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _plays);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _stops);
        return Task.CompletedTask;
    }
}
```

(`using System.Threading;` for Interlocked.)

- [ ] **Step 2: Failing end-to-end tests** (pure snapshot feed — no engine needed; drive `RaceAnnouncer` via an internal test hook `ProcessSnapshotsForTest(IReadOnlyList<RaceStandingsEntry>)`? NO — better: test the public path. Construct `RaceEngine` like `RaceEngineTests` does (FakeSerialPortWrapper lives in UITests — NOT referenceable from SpeechTests). DECISION: give RaceAnnouncer an internal ctor-visible handler method `HandleStandings(object? sender, IReadOnlyList<RaceStandingsEntry> entries)` (public — it's the event handler signature), tests call it directly with snapshots. `Attach` merely wires `engine.RaceDataChanged += HandleStandings`.)

```csharp
// RaceAnnouncerTests.cs (key tests)
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Speech;
using TrackGenius.Speech.Abstractions;
using TrackGenius.Speech.Fakes;

namespace TrackGenius.SpeechTests;

[TestFixture]
public class RaceAnnouncerTests
{
    private AnnouncementScheduler _scheduler = null!;
    private AnnouncementPolicy _policy = null!;
    private SpeechQueue _queue = null!;
    private FakeTtsEngine _tts = null!;
    private FakeAudioPlayer _player = null!;
    private RaceAnnouncer _announcer = null!;

    [SetUp]
    public void SetUp()
    {
        _scheduler = new AnnouncementScheduler();
        _policy = new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), 20);
        _queue = new SpeechQueue(20, NullLogger<SpeechQueue>.Instance);
        _tts = new FakeTtsEngine();
        _player = new FakeAudioPlayer();
        _announcer = new RaceAnnouncer(_scheduler, new SpeechTemplateRenderer(), _policy, _queue, _tts, _player,
            NullLogger<RaceAnnouncer>.Instance)
        { Enabled = true };
    }

    [TearDown]
    public void TearDown() => _announcer.Dispose();

    [Test]
    public async Task GivenSnapshotSequence_WhenHandled_ThenAnnouncementsSpokenInOrder()
    {
        var first = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true) };
        var improved = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 19.438, true) };

        _announcer.HandleStandings(this, first);
        _announcer.HandleStandings(this, improved);
        await _announcer.DrainForTestAsync();

        Assert.That(_tts.SynthesizedTexts, Is.EqualTo(new[]
        {
            "Race started.",
            "Car number one hundred, fastest lap, nineteen point four three eight seconds.",
        }));
    }

    [Test]
    public async Task GivenDisabledAnnouncer_WhenHandled_ThenNothingSpoken()
    {
        _announcer.Enabled = false;

        _announcer.HandleStandings(this, new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true) });
        await _announcer.DrainForTestAsync();

        Assert.That(_tts.SynthesizedTexts, Is.Empty);
    }

    [Test]
    public async Task GivenDuplicateLeaderChanges_WhenHandled_ThenSecondSuppressed()
    {
        var a = new List<RaceStandingsEntry> { TestStandings.Entry("100", 1, 20.0, true), TestStandings.Entry("200", 2, 21.0, false) };
        var b = new List<RaceStandingsEntry> { TestStandings.Entry("200", 1, 20.5, true), TestStandings.Entry("100", 2, 20.0, false) };

        _announcer.HandleStandings(this, a);
        _announcer.HandleStandings(this, b);
        _announcer.HandleStandings(this, a);   // leader flips back within the dedup window
        await _announcer.DrainForTestAsync();

        Assert.That(_tts.SynthesizedTexts.Count(t => t.Contains("takes the lead")), Is.EqualTo(1));
    }
}
```

- [ ] **Step 3: RED → implement** `RaceAnnouncer.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TrackGenius.Core;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.Speech;

/// <summary>Subscribes to standings snapshots and drives the announcement pipeline off-thread.</summary>
public sealed class RaceAnnouncer : IDisposable
{
    private readonly AnnouncementScheduler _scheduler;
    private readonly ISpeechTemplateRenderer _renderer;
    private readonly IAnnouncementPolicy _policy;
    private readonly SpeechQueue _queue;
    private readonly ITtsEngine _ttsEngine;
    private readonly IAudioPlayer _audioPlayer;
    private readonly ILogger<RaceAnnouncer> _logger;
    private readonly object _gate = new();
    private IReadOnlyList<RaceStandingsEntry>? _lastSnapshot;

    public bool Enabled { get; set; }

    public RaceAnnouncer(AnnouncementScheduler scheduler, ISpeechTemplateRenderer renderer,
        IAnnouncementPolicy policy, SpeechQueue queue, ITtsEngine ttsEngine, IAudioPlayer audioPlayer,
        ILogger<RaceAnnouncer> logger)
    {
        _scheduler = scheduler;
        _renderer = renderer;
        _policy = policy;
        _queue = queue;
        _ttsEngine = ttsEngine;
        _audioPlayer = audioPlayer;
        _logger = logger;
    }

    public void Attach(RaceEngine engine) => engine.RaceDataChanged += HandleStandings;

    public void Detach(RaceEngine engine) => engine.RaceDataChanged -= HandleStandings;

    /// <summary>Event handler: derive facts, render, gate, queue — synchronously cheap (v1), off-thread tail via DrainForTestAsync/worker.</summary>
    public void HandleStandings(object? sender, IReadOnlyList<RaceStandingsEntry> entries)
    {
        if (!Enabled)
            return;

        try
        {
            IReadOnlyList<RaceStandingsEntry>? previous;
            lock (_gate)
            {
                previous = _lastSnapshot;
                _lastSnapshot = entries;
            }

            var now = DateTimeOffset.Now;
            var facts = StandingsDiffer.Diff(previous, entries, now);
            if (facts.Count == 0)
                return;

            foreach (var intent in _scheduler.Schedule(facts, entries, now))
            {
                var content = _renderer.Render(intent);
                var message = new SpeechMessage(
                    Guid.NewGuid(), content.Text, content.Ssml, intent.Priority, intent.Type,
                    now, intent.ExpiresAfter is { } expiry ? now + expiry : now + TimeSpan.FromSeconds(15),
                    Interruptible: false, content.Language, content.VoiceId);

                var decision = _policy.Evaluate(message, new PolicyContext(_queue.Count, now));
                if (!decision.Accept)
                {
                    _logger.LogInformation("SpeechMessageRejected Category={Category} Reason={Reason}", message.Category, decision.Reason);
                    continue;
                }

                _queue.EnqueueAsync(message).AsTask().GetAwaiter().GetResult();
                _logger.LogInformation("SpeechMessageAccepted MessageId={MessageId} Category={Category}", message.Id, message.Category);
            }
        }
        catch (Exception ex)
        {
            // Speech must never break the timing pipeline.
            _logger.LogError(ex, "SpeechPipelineFailed Stage={Stage}", "HandleStandings");
        }
    }

    /// <summary>Pumps the queue through TTS+player until empty (called by the worker in production; by tests directly).</summary>
    public async Task DrainForTestAsync()
    {
        while (true)
        {
            var message = await _queue.DequeueAsync(default);
            if (message is null)
                return;

            var clip = await _ttsEngine.SynthesizeAsync(new SpeechContent(message.Text, message.Ssml, message.Language, message.VoiceId));
            await _audioPlayer.PlayAsync(clip);
            _logger.LogInformation("SpeechMessageSpoken MessageId={MessageId} Category={Category}", message.Id, message.Category);
        }
    }

    public void Dispose()
    {
        // v1: nothing background to stop; queue is drained explicitly. Reserved for the worker.
    }
}
```

- [ ] **Step 4: Green (3 announcer tests; ~34 total SpeechTests). Build + UITests still green. Step 5: No commit.**

---

## Task 7: Composition-root wiring (UI project references Speech)

**Files:**
- Modify: `source/TrackGenius/TrackGenius.UI.csproj` (add ProjectReference to Speech)
- Modify: `source/TrackGenius/App.xaml.cs` (construct pipeline, pass to MainForm)

**Interfaces:**
- Consumes: all Speech types; existing composition pattern (App.xaml.cs builds services, passes into MainForm).
- Produces: a constructed-but-**disabled** announcer owned by MainForm/RacePage lifecycle. No XAML, no Settings UI.

- [ ] **Step 1: csproj reference**

```xml
    <ProjectReference Include="..\TrackGenius.Speech\TrackGenius.Speech.csproj" />
```

- [ ] **Step 2: App.xaml.cs wiring** (inside `CreateMainWindow`, after `connectionService`):

```csharp
            // Speech pipeline (interface phase): composed but disabled — no TTS backend yet.
            var speechAnnouncer = new RaceAnnouncer(
                new AnnouncementScheduler(),
                new SpeechTemplateRenderer(),
                new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), 20),
                new SpeechQueue(20, _loggingContext.LoggerFactory.CreateLogger<SpeechQueue>()),
                new Fakes.FakeTtsEngine(),
                new Fakes.FakeAudioPlayer(),
                _loggingContext.LoggerFactory.CreateLogger<RaceAnnouncer>())
            {
                Enabled = false,
            };

            return new MainForm(communicateService, connectionService, userBehaviorLogger, speechAnnouncer);
```

(Add the `speechAnnouncer` parameter to `MainForm`'s ctor, thread it to `RacePageViewModel` alongside `raceEngineFactory`, and in `StartRace()` after `_raceEngine.RaceDataChanged += OnRaceDataChanged;` add `_announcer.Attach(_raceEngine);` — and before the old engine's `Dispose()` add `_announcer.Detach(oldEngine);` — mirroring the existing subscription lifecycle. Use `NullLogger` style only if MainForm ctor signature gets unwieldy; threading the real logger matches repo convention.)

- [ ] **Step 3: Full gates**

```bash
dotnet build source/TrackGenius.sln
dotnet test source/TrackGenius.SpeechTests/TrackGenius.SpeechTests.csproj
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: build 0 errors; SpeechTests green; UITests green at the Task-1 baseline count (the announcer is disabled — zero behavior change).

- [ ] **Step 4: No commit — hand the working tree to the user for review** (per this feature's review flow). Report changed-file list + test counts.

---

## Final verification

- [ ] `dotnet build source/TrackGenius.sln` — 0 errors.
- [ ] `dotnet test source/TrackGenius.SpeechTests/TrackGenius.SpeechTests.csproj` — all pass (~34).
- [ ] `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj` — no failures (baseline from Task 1).
- [ ] `grep` confirms: no `using System.Windows` anywhere under `source/TrackGenius.Speech/`; no reference to Speech from `TrackGenius.Core`.
- [ ] Announcer composed in App.xaml.cs with `Enabled = false`.
- [ ] Working tree uncommitted for user review; file list reported.

## Out of scope (do not do)

- Real TTS engines (Piper/eSpeak/Azure/System.Media), audio device output, volume.
- SSML generation, speech dictionary, multi-language templates beyond the en-US v1 set.
- Coalescing, throttling, interruptibility behavior, race-phase policies.
- Settings UI, persistence for the Enabled flag.
- Background worker threading inside RaceAnnouncer (v1 drains explicitly; handler is cheap) — reserved in Dispose comment.
