# RaceDataListControl Columnar Table — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn `RaceDataListControl` into a columnar race-standings table where every `RaceDataItem` property is a column, columns are toggleable via a header context menu, the list stays live-sorted by Position, and a colored position-change indicator renders inside the Position cell — all while preserving the existing per-row composed layout (full-width `ProgressBar`).

**Architecture:** Keep the control as a composed `ListView` with a free-form row `DataTemplate` (not a `DataGrid`). Generalize the existing `Grid.IsSharedSizeScope` + `SharedSizeGroup` header↔row alignment from two columns to N, and drive per-column `Visibility` from a single `RaceDataColumnSettings` model. Collapsing a cell collapses its `Auto` column to zero width, so hiding a column removes it from header + every row at once. Persistence is a tiny JSON file in `%LocalAppData%` via `System.Text.Json`.

**Tech Stack:** .NET 8 (`net8.0-windows`), WPF + WPF-UI 4.3.0, `System.Text.Json` (BCL), NUnit 3.14.0 + NSubstitute. Local SDK is .NET 9 (9.0.314) but target frameworks stay `net8.0*`.

## Global Constraints

- **Target frameworks unchanged:** `net8.0-windows` for `TrackGenius.UI` and the new test project; `net8.0` for libraries. Do not add a `global.json`.
- **Namespaces:** UI project root namespace is `TrackGenius.UI`. New files live in `TrackGenius.UI.Converters`, `TrackGenius.UI.ViewModels`, `TrackGenius.UI.Persistence`, `TrackGenius.UI.Views.Controls`, and tests in `TrackGenius.UITests`.
- **INPC helper:** Use the existing `PropertyChanged.RaiseIfChanged(...)` extension in `TrackGenius.Const.TypeBinding` (namespace `TrackGenius.Const`) — call it directly on the `PropertyChanged` event field (no `?.`, the extension tolerates null). Requires `using System.ComponentModel;` and `using TrackGenius.Const;`.
- **Code style:** file-scoped namespaces are fine; do not enable `<Nullable>` (the project uses nullable annotations loosely, producing tolerated CS8632 warnings — match that).
- **Test style:** NUnit, `Given…_When…_Then…` method names (match `TrackGenius.ProtocolTests`).
- **Commit policy:** every commit message ends with a trailer line `Co-Authored-By: Claude <noreply@anthropic.com>`. **Do NOT commit `CLAUDE.md`** in any of these commits (user instruction — it stays uncommitted/separate).
- **Build/test commands:** build `dotnet build source/TrackGenius.sln`; test `dotnet test source/TrackGenius.sln`; run app `dotnet run --project source/TrackGenius/TrackGenius.UI.csproj`.

---

## File Structure

**New — `TrackGenius.UI` (production):**
- `Converters/PositionChangeState.cs` — enum `{ Improved, Worsened, Unchanged }`.
- `Converters/RacerPositionChangeSignConverter.cs` — `int → PositionChangeState`.
- `Converters/AbsoluteValueConverter.cs` — `int → |int|`.
- `Converters/TimeSpanFormatConverter.cs` — `TimeSpan → "m:ss.fff"` (format via parameter).
- `ViewModels/RaceDataColumnOption.cs` — INPC; `Key`, `Label`, `CanHide`, `IsVisible` (non-hideable ignores `false`).
- `ViewModels/RaceDataColumnSettings.cs` — named options + `All` aggregate; `ApplyHiddenKeys`, `GetHiddenKeys`.
- `Persistence/RaceDataColumnPreferences.cs` — pure JSON helpers (`ParseHiddenColumns`, `SerializeHiddenColumns`).
- `Persistence/RaceDataColumnPreferencesStore.cs` — file I/O at `%LocalAppData%\TrackGenius\raceDataColumns.json`.

**New — test project `TrackGenius.UITests`:**
- `TrackGenius.UITests.csproj`, `SanityTests.cs`, plus per-unit test files.

**Modified — `TrackGenius.UI`:**
- `Views/Controls/RaceDataListControl.xaml` — full rewrite: header + row templates, shared-size columns, indicator, resources.
- `Views/Controls/RaceDataListControl.xaml.cs` — `ColumnSettings`, live sort on `ItemsSource`, load/save, header context menu.
- `ViewModels/RacePageViewModel.cs` — realistic demo data (positions/laps/times).

---

## Task 1: TrackGenius.UITests project scaffold

**Files:**
- Create: `source/TrackGenius.UITests/TrackGenius.UITests.csproj`
- Create: `source/TrackGenius.UITests/SanityTests.cs`
- Modify: `source/TrackGenius.sln` (add project)

**Interfaces:**
- Produces: a building, test-running project `TrackGenius.UITests` referencing `TrackGenius.UI`, providing the home for all subsequent unit tests.

- [ ] **Step 1: Create the test project file**

`source/TrackGenius.UITests/TrackGenius.UITests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <IsTestProject>true</IsTestProject>
    <AssemblyName>TrackGenius.UITests</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\TrackGenius\TrackGenius.UI.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add a sanity test**

`source/TrackGenius.UITests/SanityTests.cs`:

```csharp
using NUnit.Framework;

namespace TrackGenius.UITests;

[TestFixture]
public class SanityTests
{
    [Test]
    public void Framework_Is_Ready() => Assert.That(true, Is.True);
}
```

- [ ] **Step 3: Register the project in the solution**

Run:
```bash
dotnet sln source/TrackGenius.sln add source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: solution updated to include the new project.

- [ ] **Step 4: Build and run tests to verify the harness works**

Run: `dotnet test source/TrackGenius.sln`
Expected: build succeeds; `Framework_Is_Ready` passes (1 test).

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius.UITests source/TrackGenius.sln
git commit -m "Add TrackGenius.UITests project"
# append Co-Authored-By trailer per Global Constraints
```

---

## Task 2: Position-change value converters

**Files:**
- Create: `source/TrackGenius/Converters/PositionChangeState.cs`
- Create: `source/TrackGenius/Converters/RacerPositionChangeSignConverter.cs`
- Create: `source/TrackGenius/Converters/AbsoluteValueConverter.cs`
- Create: `source/TrackGenius/Converters/TimeSpanFormatConverter.cs`
- Test: `source/TrackGenius.UITests/Converters/AbsoluteValueConverterTests.cs`
- Test: `source/TrackGenius.UITests/Converters/RacerPositionChangeSignConverterTests.cs`
- Test: `source/TrackGenius.UITests/Converters/TimeSpanFormatConverterTests.cs`

**Interfaces:**
- Produces: `AbsoluteValueConverter` (`IValueConverter`, int→int), `RacerPositionChangeSignConverter` (`IValueConverter`, int→`PositionChangeState`), `TimeSpanFormatConverter` (`IValueConverter`, TimeSpan→string). All used by the XAML in Task 5.

- [ ] **Step 1: Write the failing tests**

`source/TrackGenius.UITests/Converters/AbsoluteValueConverterTests.cs`:

```csharp
using System.Globalization;
using NUnit.Framework;
using TrackGenius.UI.Converters;

namespace TrackGenius.UITests.Converters;

[TestFixture]
public class AbsoluteValueConverterTests
{
    private readonly AbsoluteValueConverter _converter = new();

    [TestCase(0, ExpectedResult = 0)]
    [TestCase(5, ExpectedResult = 5)]
    [TestCase(-3, ExpectedResult = 3)]
    [TestCase(-99, ExpectedResult = 99)]
    public int GivenInteger_WhenConverted_ThenAbsoluteValueReturned(int value)
        => (int)_converter.Convert(value, typeof(int), null, CultureInfo.InvariantCulture);

    [Test]
    public void GivenNull_WhenConverted_ThenZeroReturned()
        => Assert.That(_converter.Convert(null, typeof(int), null, CultureInfo.InvariantCulture), Is.EqualTo(0));
}
```

`source/TrackGenius.UITests/Converters/RacerPositionChangeSignConverterTests.cs`:

```csharp
using System.Globalization;
using NUnit.Framework;
using TrackGenius.UI.Converters;

namespace TrackGenius.UITests.Converters;

[TestFixture]
public class RacerPositionChangeSignConverterTests
{
    private readonly RacerPositionChangeSignConverter _converter = new();

    [TestCase(-5, ExpectedResult = PositionChangeState.Improved)]
    [TestCase(-1, ExpectedResult = PositionChangeState.Improved)]
    [TestCase(0, ExpectedResult = PositionChangeState.Unchanged)]
    [TestCase(1, ExpectedResult = PositionChangeState.Worsened)]
    [TestCase(7, ExpectedResult = PositionChangeState.Worsened)]
    public PositionChangeState GivenDelta_WhenConverted_ThenExpectedStateReturned(int delta)
        => (PositionChangeState)_converter.Convert(delta, typeof(PositionChangeState), null, CultureInfo.InvariantCulture);

    [Test]
    public void GivenNull_WhenConverted_ThenUnchangedReturned()
        => Assert.That(_converter.Convert(null, typeof(PositionChangeState), null, CultureInfo.InvariantCulture),
                       Is.EqualTo(PositionChangeState.Unchanged));
}
```

`source/TrackGenius.UITests/Converters/TimeSpanFormatConverterTests.cs`:

```csharp
using System;
using System.Globalization;
using NUnit.Framework;
using TrackGenius.UI.Converters;

namespace TrackGenius.UITests.Converters;

[TestFixture]
public class TimeSpanFormatConverterTests
{
    private readonly TimeSpanFormatConverter _converter = new();

    [Test]
    public void GivenTimeSpan_WhenConvertedWithDefaultFormat_ThenFormattedStringReturned()
    {
        var oneMinTwoSec345Ms = new TimeSpan(0, 0, 1, 2, 345); // exact, avoids double-rounding
        var text = (string)_converter.Convert(oneMinTwoSec345Ms, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.That(text, Is.EqualTo("1:02.345"));
    }

    [Test]
    public void GivenTimeSpan_WhenConvertedWithParameter_ThenParameterFormatUsed()
    {
        var text = (string)_converter.Convert(TimeSpan.Zero, typeof(string), @"ss\.ff", CultureInfo.InvariantCulture);
        Assert.That(text, Is.EqualTo("00.00"));
    }

    [Test]
    public void GivenNonTimeSpan_WhenConverted_ThenEmptyStringReturned()
        => Assert.That(_converter.Convert("not a timespan", typeof(string), null, CultureInfo.InvariantCulture),
                       Is.EqualTo(string.Empty));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj`
Expected: FAIL — types `AbsoluteValueConverter`, `RacerPositionChangeSignConverter`, `PositionChangeState`, `TimeSpanFormatConverter` do not exist (compile error).

- [ ] **Step 3: Implement the enum and converters**

`source/TrackGenius/Converters/PositionChangeState.cs`:

```csharp
namespace TrackGenius.UI.Converters;

public enum PositionChangeState
{
    Improved,
    Worsened,
    Unchanged,
}
```

`source/TrackGenius/Converters/AbsoluteValueConverter.cs`:

```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace TrackGenius.UI.Converters;

public sealed class AbsoluteValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return 0;

        try
        {
            return Math.Abs(System.Convert.ToInt32(value, culture));
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            return 0;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
```

`source/TrackGenius/Converters/RacerPositionChangeSignConverter.cs`:

```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace TrackGenius.UI.Converters;

public sealed class RacerPositionChangeSignConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return PositionChangeState.Unchanged;

        try
        {
            var delta = System.Convert.ToInt32(value, culture);
            if (delta < 0) return PositionChangeState.Improved;
            if (delta > 0) return PositionChangeState.Worsened;
            return PositionChangeState.Unchanged;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            return PositionChangeState.Unchanged;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
```

`source/TrackGenius/Converters/TimeSpanFormatConverter.cs`:

```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace TrackGenius.UI.Converters;

public sealed class TimeSpanFormatConverter : IValueConverter
{
    public string DefaultFormat { get; set; } = @"m\:ss\.fff";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
            return timeSpan.ToString(parameter as string ?? DefaultFormat, culture);

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj`
Expected: PASS — all converter tests green.

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius/Converters source/TrackGenius.UITests/Converters
git commit -m "Add position-change and time value converters"
```

---

## Task 3: Column model (RaceDataColumnOption + RaceDataColumnSettings)

**Files:**
- Create: `source/TrackGenius/ViewModels/RaceDataColumnOption.cs`
- Create: `source/TrackGenius/ViewModels/RaceDataColumnSettings.cs`
- Test: `source/TrackGenius.UITests/ViewModels/RaceDataColumnSettingsTests.cs`

**Interfaces:**
- Produces: `RaceDataColumnOption` (`Key`, `Label`, `CanHide`, INPC `IsVisible`); `RaceDataColumnSettings` with named options `Position`, `CarNumber`, `Driver`, `Laps`, `Gap`, `Interval`, `LastLap`, `BestLap`, `Transponder`, `Notes`, an `All` aggregate (display order), `ApplyHiddenKeys(IEnumerable<string>)`, and `GetHiddenKeys()`. Consumed by Task 4 (persistence) and Task 5 (control).

- [ ] **Step 1: Write the failing test**

`source/TrackGenius.UITests/ViewModels/RaceDataColumnSettingsTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using TrackGenius.UI.ViewModels;

namespace TrackGenius.UITests.ViewModels;

[TestFixture]
public class RaceDataColumnSettingsTests
{
    [Test]
    public void GivenDefaultSettings_WhenCreated_ThenAllTenColumnsVisibleAndPositionNonHideable()
    {
        var settings = new RaceDataColumnSettings();

        Assert.That(settings.All, Has.Count.EqualTo(10));
        Assert.That(settings.All[0], Is.SameAs(settings.Position));
        Assert.That(settings.Position.CanHide, Is.False);
        Assert.That(settings.All.Skip(1), Has.All.Property("CanHide").EqualTo(true));
        foreach (var option in settings.All)
            Assert.That(option.IsVisible, Is.True);
    }

    [Test]
    public void GivenNonHideableOption_WhenIsVisibleSetFalse_ThenStaysVisible()
    {
        var option = new RaceDataColumnOption("Position", "Position", canHide: false);

        option.IsVisible = false;

        Assert.That(option.IsVisible, Is.True);
    }

    [Test]
    public void GivenHiddenKeys_WhenApplied_ThenMatchingColumnsHiddenAndPositionStaysVisible()
    {
        var settings = new RaceDataColumnSettings();

        settings.ApplyHiddenKeys(new[] { "Laps", "Notes", "Position" });

        Assert.Multiple(() =>
        {
            Assert.That(settings.Laps.IsVisible, Is.False);
            Assert.That(settings.Notes.IsVisible, Is.False);
            Assert.That(settings.Position.IsVisible, Is.True);
            Assert.That(settings.Driver.IsVisible, Is.True);
        });
    }

    [Test]
    public void GivenHiddenColumns_WhenGetHiddenKeysCalled_ThenReturnsOnlyHiddenKeys()
    {
        var settings = new RaceDataColumnSettings();
        settings.Laps.IsVisible = false;
        settings.Notes.IsVisible = false;

        CollectionAssert.AreEquivalent(new[] { "Laps", "Notes" }, settings.GetHiddenKeys());
    }

    [Test]
    public void GivenAppliedKeys_WhenRoundTrippedThroughGetThenApply_ThenStateIsStable()
    {
        var settings = new RaceDataColumnSettings();
        settings.Laps.IsVisible = false;
        settings.Transponder.IsVisible = false;
        var hidden = settings.GetHiddenKeys().ToArray();

        var roundTripped = new RaceDataColumnSettings();
        roundTripped.ApplyHiddenKeys(hidden);

        CollectionAssert.AreEquivalent(hidden, roundTripped.GetHiddenKeys());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj`
Expected: FAIL — `RaceDataColumnOption` / `RaceDataColumnSettings` do not exist.

- [ ] **Step 3: Implement the column model**

`source/TrackGenius/ViewModels/RaceDataColumnOption.cs`:

```csharp
using System.ComponentModel;
using JetBrains.Annotations;
using TrackGenius.Const;

namespace TrackGenius.UI.ViewModels;

public sealed class RaceDataColumnOption : INotifyPropertyChanged
{
    private bool _isVisible;

    public string Key { get; }

    public string Label { get; }

    public bool CanHide { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            // Non-hideable columns (Position) stay visible.
            if (!CanHide && !value)
                return;

            PropertyChanged.RaiseIfChanged(this, ref _isVisible, value, nameof(IsVisible));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public RaceDataColumnOption([NotNull] string key, [NotNull] string label, bool canHide, bool isVisible = true)
    {
        Key = key ?? throw new System.ArgumentNullException(nameof(key));
        Label = label ?? throw new System.ArgumentNullException(nameof(label));
        CanHide = canHide;
        _isVisible = isVisible;
    }
}
```

`source/TrackGenius/ViewModels/RaceDataColumnSettings.cs`:

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace TrackGenius.UI.ViewModels;

public sealed class RaceDataColumnSettings : INotifyPropertyChanged
{
    public RaceDataColumnOption Position { get; }
    public RaceDataColumnOption CarNumber { get; }
    public RaceDataColumnOption Driver { get; }
    public RaceDataColumnOption Laps { get; }
    public RaceDataColumnOption Gap { get; }
    public RaceDataColumnOption Interval { get; }
    public RaceDataColumnOption LastLap { get; }
    public RaceDataColumnOption BestLap { get; }
    public RaceDataColumnOption Transponder { get; }
    public RaceDataColumnOption Notes { get; }

    public IList<RaceDataColumnOption> All { get; }

    public event PropertyChangedEventHandler PropertyChanged;

    public RaceDataColumnSettings()
    {
        Position = new RaceDataColumnOption("Position", "Position", canHide: false);
        CarNumber = new RaceDataColumnOption("CarNumber", "Car #", canHide: true);
        Driver = new RaceDataColumnOption("Driver", "Driver", canHide: true);
        Laps = new RaceDataColumnOption("Laps", "Laps", canHide: true);
        Gap = new RaceDataColumnOption("Gap", "Gap", canHide: true);
        Interval = new RaceDataColumnOption("Interval", "Interval", canHide: true);
        LastLap = new RaceDataColumnOption("LastLap", "Last Lap", canHide: true);
        BestLap = new RaceDataColumnOption("BestLap", "Best Lap", canHide: true);
        Transponder = new RaceDataColumnOption("Transponder", "Transponder", canHide: true);
        Notes = new RaceDataColumnOption("Notes", "Notes", canHide: true);

        All = new List<RaceDataColumnOption>
        {
            Position, CarNumber, Driver, Laps, Gap, Interval, LastLap, BestLap, Transponder, Notes
        };
    }

    public void ApplyHiddenKeys(IEnumerable<string> hiddenKeys)
    {
        var set = hiddenKeys as ISet<string> ?? new HashSet<string>(hiddenKeys ?? System.Array.Empty<string>(), System.StringComparer.Ordinal);
        foreach (var option in All)
            option.IsVisible = !(option.CanHide && set.Contains(option.Key));
    }

    public IEnumerable<string> GetHiddenKeys()
        => All.Where(o => !o.IsVisible).Select(o => o.Key);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius/ViewModels/RaceDataColumnOption.cs source/TrackGenius/ViewModels/RaceDataColumnSettings.cs source/TrackGenius.UITests/ViewModels
git commit -m "Add RaceDataColumnSettings column model"
```

---

## Task 4: Preferences persistence (JSON helpers + store)

**Files:**
- Create: `source/TrackGenius/Persistence/RaceDataColumnPreferences.cs`
- Create: `source/TrackGenius/Persistence/RaceDataColumnPreferencesStore.cs`
- Test: `source/TrackGenius.UITests/Persistence/RaceDataColumnPreferencesTests.cs`

**Interfaces:**
- Consumes: nothing (pure JSON + file I/O).
- Produces: `RaceDataColumnPreferences.ParseHiddenColumns(string?)` → `IReadOnlyList<string>`; `RaceDataColumnPreferences.SerializeHiddenColumns(IEnumerable<string>)` → `string`; `RaceDataColumnPreferencesStore` with `Load()` / `Save(IEnumerable<string>)`. Consumed by Task 5.

- [ ] **Step 1: Write the failing test**

`source/TrackGenius.UITests/Persistence/RaceDataColumnPreferencesTests.cs`:

```csharp
using System;
using NUnit.Framework;
using TrackGenius.UI.Persistence;

namespace TrackGenius.UITests.Persistence;

[TestFixture]
public class RaceDataColumnPreferencesTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not json")]
    [TestCase("{")]
    public void GivenInvalidJson_WhenParsed_ThenEmptyListReturned(string json)
        => Assert.That(RaceDataColumnPreferences.ParseHiddenColumns(json), Is.Empty);

    [Test]
    public void GivenValidJson_WhenParsed_ThenKeysReturned()
    {
        var json = "{\"hiddenColumns\":[\"Laps\",\"Notes\"]}";
        CollectionAssert.AreEqual(new[] { "Laps", "Notes" }, RaceDataColumnPreferences.ParseHiddenColumns(json));
    }

    [Test]
    public void GivenJsonWithoutHiddenColumns_WhenParsed_ThenEmptyListReturned()
    {
        var json = "{\"somethingElse\":42}";
        Assert.That(RaceDataColumnPreferences.ParseHiddenColumns(json), Is.Empty);
    }

    [Test]
    public void GivenKeys_WhenSerializedThenParsed_ThenRoundTrips()
    {
        var json = RaceDataColumnPreferences.SerializeHiddenColumns(new[] { "Laps", "Notes" });
        CollectionAssert.AreEqual(new[] { "Laps", "Notes" }, RaceDataColumnPreferences.ParseHiddenColumns(json));
    }

    [Test]
    public void GivenEmptyKeys_WhenSerializedThenParsed_ThenEmpty()
    {
        var json = RaceDataColumnPreferences.SerializeHiddenColumns(Array.Empty<string>());
        Assert.That(RaceDataColumnPreferences.ParseHiddenColumns(json), Is.Empty);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj`
Expected: FAIL — `RaceDataColumnPreferences` does not exist.

- [ ] **Step 3: Implement the pure JSON helpers**

`source/TrackGenius/Persistence/RaceDataColumnPreferences.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace TrackGenius.UI.Persistence;

public static class RaceDataColumnPreferences
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static IReadOnlyList<string> ParseHiddenColumns(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("hiddenColumns", out var array))
                return Array.Empty<string>();

            return array.EnumerateArray()
                        .Select(e => e.GetString() ?? string.Empty)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    public static string SerializeHiddenColumns(IEnumerable<string> keys)
    {
        var payload = new Payload
        {
            HiddenColumns = (keys ?? Enumerable.Empty<string>()).ToList(),
        };
        return JsonSerializer.Serialize(payload, Options);
    }

    private sealed class Payload
    {
        public List<string> HiddenColumns { get; set; } = new();
    }
}
```

`source/TrackGenius/Persistence/RaceDataColumnPreferencesStore.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;

namespace TrackGenius.UI.Persistence;

public sealed class RaceDataColumnPreferencesStore
{
    public string FilePath { get; }

    public RaceDataColumnPreferencesStore(string filePath = null)
    {
        FilePath = filePath ?? DefaultFilePath();
    }

    public IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return Array.Empty<string>();

            return RaceDataColumnPreferences.ParseHiddenColumns(File.ReadAllText(FilePath));
        }
        catch
        {
            // Missing/locked/corrupt file → start fresh; never surface to the UI.
            return Array.Empty<string>();
        }
    }

    public void Save(IEnumerable<string> hiddenKeys)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(FilePath, RaceDataColumnPreferences.SerializeHiddenColumns(hiddenKeys));
    }

    private static string DefaultFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrackGenius");
        return Path.Combine(folder, "raceDataColumns.json");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius/Persistence source/TrackGenius.UITests/Persistence
git commit -m "Add JSON column-preferences persistence"
```

---

## Task 5: RaceDataListControl — XAML + code-behind

This task rewrites the control's XAML and code-behind together because they reference each other (`HeaderBorder` x:Name, `ColumnSettings` bindings) and must compile as a unit.

**Files:**
- Modify: `source/TrackGenius/Views/Controls/RaceDataListControl.xaml` (full content replacement)
- Modify: `source/TrackGenius/Views/Controls/RaceDataListControl.xaml.cs` (full content replacement)

**Interfaces:**
- Consumes: converters (Task 2), `RaceDataColumnSettings`/`RaceDataColumnOption` (Task 3), `RaceDataColumnPreferencesStore` (Task 4), `RaceDataItem` (existing).
- Produces: the finished columnar control with live position sorting, toggleable header context menu, position-change indicator, and persisted column visibility.

- [ ] **Step 1: Replace the code-behind**

`source/TrackGenius/Views/Controls/RaceDataListControl.xaml.cs` (replace entire file):

```csharp
using System;
using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TrackGenius.UI.Persistence;
using TrackGenius.UI.ViewModels;

namespace TrackGenius.UI.Views.Controls
{
    public partial class RaceDataListControl : UserControl
    {
        private readonly RaceDataColumnPreferencesStore _preferencesStore = new();

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(RaceDataListControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        // Read by XAML cell/header Visibility bindings. Set before InitializeComponent via initializer.
        public RaceDataColumnSettings ColumnSettings { get; } = new();

        public RaceDataListControl()
        {
            InitializeComponent();
            LoadColumnPreferences();
            BuildHeaderContextMenu();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not RaceDataListControl control)
                return;
            if (e.NewValue is not IEnumerable source)
                return;

            var view = CollectionViewSource.GetDefaultView(source);
            using (view.DeferRefresh())
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(
                    new SortDescription(nameof(RaceDataItem.RacerPosition), ListSortDirection.Ascending));
            }

            if (view is ICollectionViewLiveShaping liveShaping)
            {
                liveShaping.LiveSortingProperties.Add(nameof(RaceDataItem.RacerPosition));
                liveShaping.IsLiveSortingRequested = true;
            }
        }

        private void BuildHeaderContextMenu()
        {
            var menu = new ContextMenu();
            foreach (var option in ColumnSettings.All)
            {
                var item = new MenuItem
                {
                    Header = option.Label,
                    IsCheckable = true,
                    IsEnabled = option.CanHide,
                    IsChecked = option.IsVisible,
                };
                item.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(RaceDataColumnOption.IsVisible))
                {
                    Source = option,
                    Mode = BindingMode.TwoWay,
                });
                menu.Items.Add(item);
            }
            HeaderBorder.ContextMenu = menu;
        }

        private void LoadColumnPreferences()
        {
            ColumnSettings.ApplyHiddenKeys(_preferencesStore.Load());

            // Subscribe AFTER applying, so the initial load does not trigger a save.
            foreach (var option in ColumnSettings.All)
                option.PropertyChanged += OnOptionVisibilityChanged;
        }

        private void OnOptionVisibilityChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RaceDataColumnOption.IsVisible))
                SaveColumnPreferences();
        }

        private void SaveColumnPreferences()
        {
            try
            {
                _preferencesStore.Save(ColumnSettings.GetHiddenKeys());
            }
            catch
            {
                // Persistence is best-effort; never crash the UI over a failed save.
            }
        }
    }
}
```

- [ ] **Step 2: Replace the XAML**

`source/TrackGenius/Views/Controls/RaceDataListControl.xaml` (replace entire file):

```xml
<UserControl x:Class="TrackGenius.UI.Views.Controls.RaceDataListControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:local="clr-namespace:TrackGenius.UI.Views.Controls"
             xmlns:vm="clr-namespace:TrackGenius.UI.ViewModels"
             xmlns:conv="clr-namespace:TrackGenius.UI.Converters"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             mc:Ignorable="d"
             d:DesignHeight="300"
             d:DesignWidth="700">
    <UserControl.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis" />
        <conv:AbsoluteValueConverter x:Key="AbsValue" />
        <conv:RacerPositionChangeSignConverter x:Key="PositionChangeSign" />
        <conv:TimeSpanFormatConverter x:Key="TimeSpanFormat" />
    </UserControl.Resources>

    <Grid>
        <Border Grid.IsSharedSizeScope="True"
                BorderThickness="1"
                BorderBrush="Gray"
                CornerRadius="5">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>

                <!-- HEADER ROW -->
                <Border x:Name="HeaderBorder" Grid.Row="0" Padding="12,8">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Position"/>
                            <ColumnDefinition Width="Auto" SharedSizeGroup="Col_CarNumber"/>
                            <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Driver"/>
                            <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Laps"/>
                            <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Gap"/>
                            <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Interval"/>
                            <ColumnDefinition Width="Auto" SharedSizeGroup="Col_LastLap"/>
                            <ColumnDefinition Width="Auto" SharedSizeGroup="Col_BestLap"/>
                            <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Transponder"/>
                            <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Notes"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>

                        <TextBlock Grid.Column="0" FontWeight="SemiBold" Text="Position"/>
                        <TextBlock Grid.Column="1" FontWeight="SemiBold" Text="Car #" Margin="12,0,0,0"
                                   Visibility="{Binding ColumnSettings.CarNumber.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                        <TextBlock Grid.Column="2" FontWeight="SemiBold" Text="Driver" Margin="12,0,0,0"
                                   Visibility="{Binding ColumnSettings.Driver.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                        <TextBlock Grid.Column="3" FontWeight="SemiBold" Text="Laps" Margin="12,0,0,0"
                                   Visibility="{Binding ColumnSettings.Laps.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                        <TextBlock Grid.Column="4" FontWeight="SemiBold" Text="Gap" Margin="12,0,0,0"
                                   Visibility="{Binding ColumnSettings.Gap.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                        <TextBlock Grid.Column="5" FontWeight="SemiBold" Text="Interval" Margin="12,0,0,0"
                                   Visibility="{Binding ColumnSettings.Interval.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                        <TextBlock Grid.Column="6" FontWeight="SemiBold" Text="Last Lap" Margin="12,0,0,0"
                                   Visibility="{Binding ColumnSettings.LastLap.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                        <TextBlock Grid.Column="7" FontWeight="SemiBold" Text="Best Lap" Margin="12,0,0,0"
                                   Visibility="{Binding ColumnSettings.BestLap.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                        <TextBlock Grid.Column="8" FontWeight="SemiBold" Text="Transponder" Margin="12,0,0,0"
                                   Visibility="{Binding ColumnSettings.Transponder.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                        <TextBlock Grid.Column="9" FontWeight="SemiBold" Text="Notes" Margin="12,0,0,0"
                                   Visibility="{Binding ColumnSettings.Notes.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                    </Grid>
                </Border>

                <!-- ROWS -->
                <ui:ListView Grid.Row="1"
                             HorizontalAlignment="Stretch"
                             VerticalAlignment="Stretch"
                             Margin="0"
                             BorderThickness="0"
                             ScrollViewer.CanContentScroll="False"
                             ScrollViewer.VerticalScrollBarVisibility="Auto"
                             ScrollViewer.HorizontalScrollBarVisibility="Hidden"
                             ItemsSource="{Binding ItemsSource, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}">
                    <ui:ListView.ItemContainerStyle>
                        <Style TargetType="ListViewItem">
                            <Setter Property="HorizontalContentAlignment" Value="Stretch" />
                            <Setter Property="Margin" Value="0,0,0,8" />
                        </Style>
                    </ui:ListView.ItemContainerStyle>

                    <ui:ListView.ItemTemplate>
                        <DataTemplate DataType="vm:RaceDataItem">
                            <Border Padding="12">
                                <Grid>
                                    <Grid.RowDefinitions>
                                        <RowDefinition Height="Auto"/>
                                        <RowDefinition Height="8"/>
                                        <RowDefinition Height="Auto"/>
                                    </Grid.RowDefinitions>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Position"/>
                                        <ColumnDefinition Width="Auto" SharedSizeGroup="Col_CarNumber"/>
                                        <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Driver"/>
                                        <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Laps"/>
                                        <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Gap"/>
                                        <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Interval"/>
                                        <ColumnDefinition Width="Auto" SharedSizeGroup="Col_LastLap"/>
                                        <ColumnDefinition Width="Auto" SharedSizeGroup="Col_BestLap"/>
                                        <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Transponder"/>
                                        <ColumnDefinition Width="Auto" SharedSizeGroup="Col_Notes"/>
                                        <ColumnDefinition Width="*"/>
                                    </Grid.ColumnDefinitions>

                                    <!-- POSITION cell (always visible): number + change indicator -->
                                    <StackPanel Grid.Row="0" Grid.Column="0" Orientation="Vertical" MinWidth="70">
                                        <TextBlock FontSize="20" FontWeight="SemiBold" Text="{Binding RacerPosition}"/>
                                        <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                                            <!-- improved: green up triangle -->
                                            <TextBlock FontSize="12" Text="▲" Foreground="Green" Margin="0,0,2,0">
                                                <TextBlock.Style>
                                                    <Style TargetType="TextBlock">
                                                        <Setter Property="Visibility" Value="Collapsed"/>
                                                        <Style.Triggers>
                                                            <DataTrigger Binding="{Binding RacerPositionChange, Converter={StaticResource PositionChangeSign}}" Value="Improved">
                                                                <Setter Property="Visibility" Value="Visible"/>
                                                            </DataTrigger>
                                                        </Style.Triggers>
                                                    </Style>
                                                </TextBlock.Style>
                                            </TextBlock>
                                            <!-- worsened: red down triangle -->
                                            <TextBlock FontSize="12" Text="▼" Foreground="Red" Margin="0,0,2,0">
                                                <TextBlock.Style>
                                                    <Style TargetType="TextBlock">
                                                        <Setter Property="Visibility" Value="Collapsed"/>
                                                        <Style.Triggers>
                                                            <DataTrigger Binding="{Binding RacerPositionChange, Converter={StaticResource PositionChangeSign}}" Value="Worsened">
                                                                <Setter Property="Visibility" Value="Visible"/>
                                                            </DataTrigger>
                                                        </Style.Triggers>
                                                    </Style>
                                                </TextBlock.Style>
                                            </TextBlock>
                                            <!-- absolute value (shown for improved/worsened) -->
                                            <TextBlock FontSize="12" Text="{Binding RacerPositionChange, Converter={StaticResource AbsValue}}">
                                                <TextBlock.Style>
                                                    <Style TargetType="TextBlock">
                                                        <Setter Property="Visibility" Value="Visible"/>
                                                        <Style.Triggers>
                                                            <DataTrigger Binding="{Binding RacerPositionChange, Converter={StaticResource PositionChangeSign}}" Value="Unchanged">
                                                                <Setter Property="Visibility" Value="Collapsed"/>
                                                            </DataTrigger>
                                                        </Style.Triggers>
                                                    </Style>
                                                </TextBlock.Style>
                                            </TextBlock>
                                            <!-- dash (shown only when unchanged) -->
                                            <TextBlock FontSize="12" Text="—">
                                                <TextBlock.Style>
                                                    <Style TargetType="TextBlock">
                                                        <Setter Property="Visibility" Value="Collapsed"/>
                                                        <Style.Triggers>
                                                            <DataTrigger Binding="{Binding RacerPositionChange, Converter={StaticResource PositionChangeSign}}" Value="Unchanged">
                                                                <Setter Property="Visibility" Value="Visible"/>
                                                            </DataTrigger>
                                                        </Style.Triggers>
                                                    </Style>
                                                </TextBlock.Style>
                                            </TextBlock>
                                        </StackPanel>
                                    </StackPanel>

                                    <!-- Data cells -->
                                    <TextBlock Grid.Row="0" Grid.Column="1" VerticalAlignment="Center" Margin="12,0,0,0" Text="{Binding RacerNumber}"
                                               Visibility="{Binding ColumnSettings.CarNumber.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                                    <TextBlock Grid.Row="0" Grid.Column="2" VerticalAlignment="Center" Margin="12,0,0,0" Text="{Binding DriverName}"
                                               Visibility="{Binding ColumnSettings.Driver.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                                    <TextBlock Grid.Row="0" Grid.Column="3" VerticalAlignment="Center" Margin="12,0,0,0" Text="{Binding LapsCount}"
                                               Visibility="{Binding ColumnSettings.Laps.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                                    <TextBlock Grid.Row="0" Grid.Column="4" VerticalAlignment="Center" Margin="12,0,0,0" Text="{Binding GapTime, Converter={StaticResource TimeSpanFormat}}"
                                               Visibility="{Binding ColumnSettings.Gap.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                                    <TextBlock Grid.Row="0" Grid.Column="5" VerticalAlignment="Center" Margin="12,0,0,0" Text="{Binding IntervalTime, Converter={StaticResource TimeSpanFormat}}"
                                               Visibility="{Binding ColumnSettings.Interval.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                                    <TextBlock Grid.Row="0" Grid.Column="6" VerticalAlignment="Center" Margin="12,0,0,0" Text="{Binding LastLapTime, Converter={StaticResource TimeSpanFormat}}"
                                               Visibility="{Binding ColumnSettings.LastLap.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                                    <TextBlock Grid.Row="0" Grid.Column="7" VerticalAlignment="Center" Margin="12,0,0,0" Text="{Binding BestLapTime, Converter={StaticResource TimeSpanFormat}}"
                                               Visibility="{Binding ColumnSettings.BestLap.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                                    <TextBlock Grid.Row="0" Grid.Column="8" VerticalAlignment="Center" Margin="12,0,0,0" Text="{Binding TransponderID}"
                                               Visibility="{Binding ColumnSettings.Transponder.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
                                    <TextBlock Grid.Row="0" Grid.Column="9" VerticalAlignment="Center" Margin="12,0,0,0" Text="{Binding Description}"
                                               Visibility="{Binding ColumnSettings.Notes.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>

                                    <!-- Full-width progress bar (composed layout preserved) -->
                                    <ProgressBar Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="11" Height="6"
                                                 Minimum="0" Maximum="100" Value="{Binding LapsCount}"/>
                                </Grid>
                            </Border>
                        </DataTemplate>
                    </ui:ListView.ItemTemplate>
                </ui:ListView>
            </Grid>
        </Border>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Build to verify XAML + code-behind compile**

Run: `dotnet build source/TrackGenius/TrackGenius.UI.csproj`
Expected: build succeeds (0 errors). Warnings are acceptable (existing CS8632/CS4014 etc.).

- [ ] **Step 4: Commit**

```bash
git add source/TrackGenius/Views/Controls/RaceDataListControl.xaml source/TrackGenius/Views/Controls/RaceDataListControl.xaml.cs
git commit -m "Rebuild RaceDataListControl as columnar table with toggleable columns"
```

---

## Task 6: Demo data + full verification

**Files:**
- Modify: `source/TrackGenius/ViewModels/RacePageViewModel.cs` (enrich `AddTestData`)

**Interfaces:**
- Consumes: the finished control (Task 5).

- [ ] **Step 1: Add realistic demo data**

In `source/TrackGenius/ViewModels/RacePageViewModel.cs`, add `using System;` to the using block, then replace the `AddTestData` method body with:

```csharp
private void AddTestData()
{
    var items = new[]
    {
        new RaceDataItem("88156")
        {
            RacerNumber = 88, RacerPosition = 1, LapsCount = 12,
            BestLapTime = TimeSpan.FromSeconds(18.234), LastLapTime = TimeSpan.FromSeconds(19.012),
            GapTime = TimeSpan.Zero, IntervalTime = TimeSpan.Zero, Description = "Leader"
        },
        new RaceDataItem("48825")
        {
            RacerNumber = 7, RacerPosition = 2, LapsCount = 12,
            BestLapTime = TimeSpan.FromSeconds(18.401), LastLapTime = TimeSpan.FromSeconds(18.890),
            GapTime = TimeSpan.FromSeconds(0.8), IntervalTime = TimeSpan.FromSeconds(0.8)
        },
        new RaceDataItem("44401")
        {
            RacerNumber = 44, RacerPosition = 3, LapsCount = 11,
            BestLapTime = TimeSpan.FromSeconds(18.567), LastLapTime = TimeSpan.FromSeconds(18.945),
            GapTime = TimeSpan.FromSeconds(2.1), IntervalTime = TimeSpan.FromSeconds(1.5)
        },
        new RaceDataItem("99812")
        {
            RacerNumber = 99, RacerPosition = 4, LapsCount = 10,
            BestLapTime = TimeSpan.FromSeconds(18.900), LastLapTime = TimeSpan.FromSeconds(19.300),
            GapTime = TimeSpan.FromSeconds(5.0), IntervalTime = TimeSpan.FromSeconds(2.1)
        },
        new RaceDataItem("35890")
        {
            RacerNumber = 23, RacerPosition = 5, LapsCount = 9,
            BestLapTime = TimeSpan.FromSeconds(19.123), LastLapTime = TimeSpan.FromSeconds(20.000),
            GapTime = TimeSpan.FromSeconds(8.4), IntervalTime = TimeSpan.FromSeconds(3.2)
        },
    };

    foreach (var item in items)
        RaceDataItems.Add(item);
}
```

Note: demo items use the `string transponderID` constructor, so `_racerStartPosition` is `0` and `RacerPositionChange` equals `RacerPosition` (positive) → the indicator shows the red ▼ branch. The green ▲ (improved) branch is covered by the unit tests in Task 2; live green requires real race data with a non-zero start position.

- [ ] **Step 2: Build and run the full test suite**

Run: `dotnet test source/TrackGenius.sln`
Expected: all tests pass (ProtocolTests + ConstTests + UITests).

- [ ] **Step 3: Build the whole solution**

Run: `dotnet build source/TrackGenius.sln`
Expected: 0 errors.

- [ ] **Step 4: Manual verification (interactive)**

Run: `dotnet run --project source/TrackGenius/TrackGenius.UI.csproj`, navigate to the Quick Race page, and confirm:
1. Table shows 10 columns; 5 rows ordered by Position 1→5; each row has a position number, a red ▼ + value indicator, and a `ProgressBar` beneath.
2. Right-click anywhere in the header → context menu lists all 10 columns, each checkable except **Position** (disabled, checked).
3. Uncheck **Laps** → the Laps column disappears from header and every row (column collapses cleanly, no gap). Re-check → reappears.
4. Uncheck several, close the app, reopen → same columns stay hidden (persisted to `%LocalAppData%\TrackGenius\raceDataColumns.json`).
5. Delete that JSON file, reopen → all columns visible again (defaults).
6. (Live sort, best checked with real data:) editing a row's `RacerPosition` reorders the rows ascending in real time.

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius/ViewModels/RacePageViewModel.cs
git commit -m "Add realistic demo standings data"
```
