# UI Theme Tokens & Control Restyling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the design images' color theme and control style to the existing TrackGenius UI, with every theme color defined as a named resource in two palette dictionaries (light/dark) that the existing theme switcher swaps and persists.

**Architecture:** A semantic token layer on top of WPF-UI's Fluent theming: `Palette.Light/Dark.xaml` define identical key sets consumed via `DynamicResource`; `RaceStyles.xaml` provides property-setter styles; `ThemeManager` is the single owner of switching (Fluent theme apply with `updateAccent: false` + green accent override + palette dictionary swap + persistence). Surfaces (`RaceDataListControl`, `QuickRacePage`, `MainForm` status bar) consume tokens/styles; no layout changes.

**Tech Stack:** .NET 8 WPF, WPF-UI 4.3.0 (`Wpf.Ui.Appearance`), NUnit via existing `TrackGenius.UITests` (75 passing baseline), System.Text.Json for preferences.

**Spec:** `docs/superpowers/specs/2026-08-20-ui-theme-tokens-design.md`

## Global Constraints

- Branch: `RaceUIImprove`. Work from repo root `E:\source\repo\TrackGenius`; solution `source/TrackGenius.sln`.
- Test gate: `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj` — baseline **75 passed / 0 failed**.
- `dotnet build source/TrackGenius.sln` must stay 0 errors. If `MSB3021`/`MSB3027` (output locked — the app is running), kill `TrackGenius.exe` (`taskkill //IM TrackGenius.exe //F`) or report BLOCKED.
- Never commit `CLAUDE.md`; stage explicit paths only, never `git add -A`.
- Commit messages end with `Co-Authored-By: Claude <noreply@anthropic.com>`.
- Token names are binding (spec, incl. the review rename): `RaceAccentBrush`, `RaceAccentHoverBrush`, `RaceAccentSecondaryBrush`, `RaceAlertBrush`, `RaceStopBrush`, `RaceSuccessBrush`, `RaceBackgroundBrush`, `RaceCardBackgroundBrush`, `RaceBorderBrush`, `RaceTextPrimaryBrush`, `RaceTextSecondaryBrush`, `RaceTextMutedBrush`, `RaceRowHoverBrush`.
- Accent green `#10B981` identical in both themes; surfaces/text differ per palette table in the spec.
- WPF-UI API facts (verified against 4.3.0 source — do not deviate):
  - `ApplicationThemeManager.Apply(ApplicationTheme, WindowBackdropType backgroundEffect = Mica, bool updateAccent = true)` — always call with `updateAccent: false`, else it resets the accent to the system colorization color.
  - `ApplicationAccentColorManager.Apply(Color systemAccent, ApplicationTheme applicationTheme = Light, bool systemGlassColor = false, bool systemAccentColor = false)`.
  - `ApplicationThemeManager.ApplySystemTheme(bool updateAccent)`; `ApplicationThemeManager.GetAppTheme()`; `ApplicationThemeManager.Changed` event.
  - `SystemThemeWatcher.Watch(Window? window, WindowBackdropType backdrop = Mica, bool updateAccents = true)` — returns immediately when `window` is null.
- New `.xaml` files are auto-globbed as `<Page>` by the WindowsDesktop SDK — **no csproj edits**.
- New test files: file-scoped `namespace TrackGenius.UITests.<Folder>;`, NUnit, `Given…_When…_Then…` naming. New C# in the UI project: file-scoped namespaces.
- No `<Nullable>` enable (CS8632 warnings tolerated).
- Only the final task (Task 5) may launch the GUI app; earlier tasks must not.

---

## Task 1: `ThemeType.System` + `ThemePreferences` + `ThemePreferencesStore` (TDD)

**Files:**
- Modify: `source/TrackGenius/ViewModels/ThemeType.cs`
- Create: `source/TrackGenius/Persistence/ThemePreferencesStore.cs`
- Test: `source/TrackGenius.UITests/Persistence/ThemePreferencesStoreTests.cs`

**Interfaces:**
- Consumes: existing `ThemeType` enum (`TrackGenius.UI.ViewModels`).
- Produces (used by Tasks 2–3): `ThemeType.System` (= 2); `ThemePreferences.Parse(string json) : ThemeType` and `ThemePreferences.Serialize(ThemeType theme) : string` (static, `TrackGenius.UI.Persistence`); `ThemePreferencesStore(string filePath = null)` with `FilePath`, `ThemeType Load()`, `void Save(ThemeType)` — default file `%LocalAppData%\TrackGenius\theme.json`, JSON shape `{"theme":"Dark"}` (camelCase policy).

- [ ] **Step 1: Write the failing tests**

`source/TrackGenius.UITests/Persistence/ThemePreferencesStoreTests.cs`:

```csharp
using System;
using System.IO;
using NUnit.Framework;
using TrackGenius.UI.Persistence;
using TrackGenius.UI.ViewModels;

namespace TrackGenius.UITests.Persistence;

[TestFixture]
public class ThemePreferencesStoreTests
{
    private string _tempPath;

    [SetUp]
    public void SetUp()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"theme-test-{Guid.NewGuid():N}.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_tempPath))
            File.Delete(_tempPath);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not json")]
    [TestCase("{")]
    [TestCase("{\"somethingElse\":42}")]
    [TestCase("{\"theme\":5}")]
    [TestCase("{\"theme\":\"Neon\"}")]
    public void GivenInvalidJson_WhenParsed_ThenSystemThemeReturned(string json)
        => Assert.That(ThemePreferences.Parse(json), Is.EqualTo(ThemeType.System));

    [TestCase("Light", ThemeType.Light)]
    [TestCase("Dark", ThemeType.Dark)]
    [TestCase("System", ThemeType.System)]
    public void GivenValidJson_WhenParsed_ThenThemeReturned(string jsonValue, ThemeType expected)
        => Assert.That(ThemePreferences.Parse($"{{\"theme\":\"{jsonValue}\"}}"), Is.EqualTo(expected));

    [Test]
    public void GivenTheme_WhenSerializedThenParsed_ThenRoundTrips()
    {
        foreach (var theme in new[] { ThemeType.Light, ThemeType.Dark, ThemeType.System })
        {
            var json = ThemePreferences.Serialize(theme);
            Assert.That(ThemePreferences.Parse(json), Is.EqualTo(theme), $"round-trip failed for {theme}");
        }
    }

    [Test]
    public void GivenMissingFile_WhenLoad_ThenSystemThemeReturned()
    {
        var store = new ThemePreferencesStore(_tempPath);

        Assert.That(store.Load(), Is.EqualTo(ThemeType.System));
    }

    [Test]
    public void GivenSavedTheme_WhenLoad_ThenThemeReturned()
    {
        var store = new ThemePreferencesStore(_tempPath);
        store.Save(ThemeType.Dark);

        Assert.That(store.Load(), Is.EqualTo(ThemeType.Dark));
        Assert.That(File.ReadAllText(_tempPath), Does.Contain("\"theme\": \"Dark\""));
    }

    [Test]
    public void GivenCorruptFile_WhenLoad_ThenSystemThemeReturned()
    {
        File.WriteAllText(_tempPath, "{corrupt");
        var store = new ThemePreferencesStore(_tempPath);

        Assert.That(store.Load(), Is.EqualTo(ThemeType.System));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~ThemePreferencesStoreTests"
```
Expected: compile errors — `ThemePreferences` / `ThemeType.System` do not exist.

- [ ] **Step 3: Implement**

`source/TrackGenius/ViewModels/ThemeType.cs` — full replacement:

```csharp
namespace TrackGenius.UI.ViewModels;

public enum ThemeType
{
    Light = 0,
    Dark = 1,
    System = 2,
}
```

`source/TrackGenius/Persistence/ThemePreferencesStore.cs`:

```csharp
using System;
using System.IO;
using System.Text.Json;
using TrackGenius.UI.ViewModels;

namespace TrackGenius.UI.Persistence;

/// <summary>Serializes the theme preference as {"theme":"Dark"} (camelCase).</summary>
public static class ThemePreferences
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static ThemeType Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ThemeType.System;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("theme", out var value)
                || value.ValueKind != JsonValueKind.String)
                return ThemeType.System;

            return value.GetString() switch
            {
                nameof(ThemeType.Light) => ThemeType.Light,
                nameof(ThemeType.Dark) => ThemeType.Dark,
                nameof(ThemeType.System) => ThemeType.System,
                _ => ThemeType.System,
            };
        }
        catch (JsonException)
        {
            return ThemeType.System;
        }
    }

    public static string Serialize(ThemeType theme)
        => JsonSerializer.Serialize(new Payload { Theme = theme.ToString() }, Options);

    private sealed class Payload
    {
        public string Theme { get; set; } = nameof(ThemeType.System);
    }
}

/// <summary>Loads/saves the theme preference; read failures fall back to System.</summary>
public sealed class ThemePreferencesStore
{
    public string FilePath { get; }

    public ThemePreferencesStore(string filePath = null)
    {
        FilePath = filePath ?? DefaultFilePath();
    }

    public ThemeType Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return ThemeType.System;

            return ThemePreferences.Parse(File.ReadAllText(FilePath));
        }
        catch
        {
            // Missing/locked/corrupt file → default; never surface to the UI.
            return ThemeType.System;
        }
    }

    public void Save(ThemeType theme)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(FilePath, ThemePreferences.Serialize(theme));
    }

    private static string DefaultFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrackGenius");
        return Path.Combine(folder, "theme.json");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~ThemePreferencesStoreTests"
```
Expected: 10 passed (suite total 85).

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius/ViewModels/ThemeType.cs source/TrackGenius/Persistence/ThemePreferencesStore.cs source/TrackGenius.UITests/Persistence/ThemePreferencesStoreTests.cs
git commit -m "Add theme preference persistence with System option

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 2: Palette dictionaries + `ThemeManager` (TDD)

**Files:**
- Create: `source/TrackGenius/Theme/Palette.Light.xaml`
- Create: `source/TrackGenius/Theme/Palette.Dark.xaml`
- Create: `source/TrackGenius/Theme/ThemeManager.cs`
- Modify: `source/TrackGenius/App.xaml`
- Test: `source/TrackGenius.UITests/Theme/ThemeManagerTests.cs`

**Interfaces:**
- Consumes: Task 1's `ThemeType`/`ThemePreferencesStore`; WPF-UI `ApplicationThemeManager`/`ApplicationAccentColorManager`/`SystemThemeWatcher`.
- Produces (used by Tasks 3–4): `TrackGenius.UI.Theme.ThemeManager` static class with `ExpectedTokenKeys: string[]`, `AccentColor: Color`, `PreferencesStore: ThemePreferencesStore` (static settable), `void Apply(ThemeType)`, `ResourceDictionary LoadPalette(ThemeType)`, `IReadOnlyList<string> ValidatePalette(ResourceDictionary)`, `void SwapPalette(ResourceDictionaryCollection, ResourceDictionary)`; palette marker key `RacePaletteMarker`.

- [ ] **Step 1: Write the failing tests**

`source/TrackGenius.UITests/Theme/ThemeManagerTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using TrackGenius.UI.Theme;
using TrackGenius.UI.ViewModels;

namespace TrackGenius.UITests.Theme;

[TestFixture]
public class ThemeManagerTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("TrackGenius.sln").Any())
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("TrackGenius.sln not found upward from test bin.");
    }

    private static string PalettePath(string file)
        => Path.Combine(RepoRoot(), "source", "TrackGenius", "Theme", file);

    /// <summary>x:Key attributes starting with "Race" (excludes the marker string).</summary>
    private static ISet<string> ExtractTokenKeys(string paletteFile)
    {
        var doc = XDocument.Load(PalettePath(paletteFile));
        var xaml = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        return doc.Descendants()
            .Select(e => (string)e.Attribute(xaml + "Key"))
            .Where(key => key != null && key.StartsWith("Race", StringComparison.Ordinal) && key != "RacePaletteMarker")
            .ToHashSet();
    }

    [Test]
    public void GivenLightPalette_WhenKeysInspected_ThenExactlyExpectedTokens()
        => Assert.That(ExtractTokenKeys("Palette.Light.xaml"), Is.EquivalentTo(ThemeManager.ExpectedTokenKeys));

    [Test]
    public void GivenDarkPalette_WhenKeysInspected_ThenExactlyExpectedTokens()
        => Assert.That(ExtractTokenKeys("Palette.Dark.xaml"), Is.EquivalentTo(ThemeManager.ExpectedTokenKeys));

    [Test]
    public void GivenBothPalettes_WhenKeysCompared_ThenIdentical()
        => Assert.That(ExtractTokenKeys("Palette.Light.xaml"), Is.EquivalentTo(ExtractTokenKeys("Palette.Dark.xaml")));

    [Test]
    public void GivenPalettes_WhenAccentRead_ThenGreenInBothThemes()
    {
        foreach (var file in new[] { "Palette.Light.xaml", "Palette.Dark.xaml" })
        {
            var doc = XDocument.Load(PalettePath(file));
            var accent = doc.Descendants()
                .Single(e => (string)e.Attribute(XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml") + "Key") == "RaceAccentBrush");
            Assert.That((string)accent.Attribute("Color"), Is.EqualTo("#10B981"), $"{file} accent must stay green");
        }
    }

    [Test]
    public void GivenPaletteMissingKeys_WhenValidated_ThenMissingKeysReported()
    {
        var palette = new System.Windows.ResourceDictionary();
        palette["RaceAccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);

        var missing = ThemeManager.ValidatePalette(palette);

        Assert.That(missing, Does.Contain("RaceStopBrush"));
        Assert.That(missing.Count, Is.EqualTo(ThemeManager.ExpectedTokenKeys.Length - 1));
    }

    [Test]
    public void GivenMergedDictionariesWithPalette_WhenSwapped_ThenSinglePaletteRemains()
    {
        var host = new System.Windows.ResourceDictionary();
        var oldPalette = new System.Windows.ResourceDictionary();
        oldPalette["RacePaletteMarker"] = "Light";
        var unrelated = new System.Windows.ResourceDictionary();
        unrelated["SomeOther"] = 1;
        host.MergedDictionaries.Add(oldPalette);
        host.MergedDictionaries.Add(unrelated);

        var newPalette = new System.Windows.ResourceDictionary();
        newPalette["RacePaletteMarker"] = "Dark";
        ThemeManager.SwapPalette(host.MergedDictionaries, newPalette);

        Assert.That(host.MergedDictionaries, Has.Count.EqualTo(2));
        Assert.That(host.MergedDictionaries.Count(d => (string)d["RacePaletteMarker"] == "Dark"), Is.EqualTo(1));
        Assert.That(host.MergedDictionaries, Has.Some.Property("Item").None.EqualTo(null));
        Assert.That(host.MergedDictionaries[1]["SomeOther"], Is.EqualTo(1));
    }
}
```

*(If the `Has.Some.Property` line is awkward, replace it with `Assert.That(host.MergedDictionaries.Any(d => d.ContainsKey("SomeOther")), Is.True);` — the meaningful assertions are the count, single-marker, and unrelated-survives checks. Use that `Any` form directly; drop the `Property` line.)*

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~ThemeManagerTests"
```
Expected: compile errors — `TrackGenius.UI.Theme` namespace / `ThemeManager` do not exist.

- [ ] **Step 3: Create the palette dictionaries**

`source/TrackGenius/Theme/Palette.Light.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">
    <!-- Identifies this dictionary as the active palette for ThemeManager.SwapPalette. -->
    <sys:String x:Key="RacePaletteMarker">Light</sys:String>

    <!-- Accent family: identical in both themes (per design images). -->
    <SolidColorBrush x:Key="RaceAccentBrush" Color="#10B981" />
    <SolidColorBrush x:Key="RaceAccentHoverBrush" Color="#059669" />
    <SolidColorBrush x:Key="RaceAccentSecondaryBrush" Color="#8B5CF6" />
    <SolidColorBrush x:Key="RaceAlertBrush" Color="#F59E0B" />
    <SolidColorBrush x:Key="RaceStopBrush" Color="#EF4444" />
    <SolidColorBrush x:Key="RaceSuccessBrush" Color="#10B981" />

    <!-- Surfaces: differ per theme. -->
    <SolidColorBrush x:Key="RaceBackgroundBrush" Color="#F8F9FA" />
    <SolidColorBrush x:Key="RaceCardBackgroundBrush" Color="#FFFFFF" />
    <SolidColorBrush x:Key="RaceBorderBrush" Color="#E5E7EB" />
    <SolidColorBrush x:Key="RaceTextPrimaryBrush" Color="#111827" />
    <SolidColorBrush x:Key="RaceTextSecondaryBrush" Color="#374151" />
    <SolidColorBrush x:Key="RaceTextMutedBrush" Color="#6B7280" />
    <SolidColorBrush x:Key="RaceRowHoverBrush" Color="#F3F4F6" />
</ResourceDictionary>
```

`source/TrackGenius/Theme/Palette.Dark.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">
    <!-- Identifies this dictionary as the active palette for ThemeManager.SwapPalette. -->
    <sys:String x:Key="RacePaletteMarker">Dark</sys:String>

    <!-- Accent family: identical in both themes (per design images). -->
    <SolidColorBrush x:Key="RaceAccentBrush" Color="#10B981" />
    <SolidColorBrush x:Key="RaceAccentHoverBrush" Color="#059669" />
    <SolidColorBrush x:Key="RaceAccentSecondaryBrush" Color="#8B5CF6" />
    <SolidColorBrush x:Key="RaceAlertBrush" Color="#F59E0B" />
    <SolidColorBrush x:Key="RaceStopBrush" Color="#EF4444" />
    <SolidColorBrush x:Key="RaceSuccessBrush" Color="#10B981" />

    <!-- Surfaces: differ per theme. -->
    <SolidColorBrush x:Key="RaceBackgroundBrush" Color="#121212" />
    <SolidColorBrush x:Key="RaceCardBackgroundBrush" Color="#1E1E1E" />
    <SolidColorBrush x:Key="RaceBorderBrush" Color="#2D2D2D" />
    <SolidColorBrush x:Key="RaceTextPrimaryBrush" Color="#FFFFFF" />
    <SolidColorBrush x:Key="RaceTextSecondaryBrush" Color="#A3A3A3" />
    <SolidColorBrush x:Key="RaceTextMutedBrush" Color="#6B7280" />
    <SolidColorBrush x:Key="RaceRowHoverBrush" Color="#2D2D2D" />
</ResourceDictionary>
```

- [ ] **Step 4: Create ThemeManager**

`source/TrackGenius/Theme/ThemeManager.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Serilog;
using TrackGenius.UI.Persistence;
using TrackGenius.UI.ViewModels;
using Wpf.Ui.Appearance;

namespace TrackGenius.UI.Theme;

/// <summary>
/// Single owner of theme switching: applies the Fluent theme (without letting
/// WPF-UI reset the accent), applies the TrackGenius green accent, swaps the
/// Race palette dictionary, validates palette key parity, and persists the choice.
/// </summary>
public static class ThemeManager
{
    private const string PaletteMarkerKey = "RacePaletteMarker";

    /// <summary>Every token both palettes must define. Keep in sync with Palette.*.xaml (guarded by ThemeManagerTests).</summary>
    public static readonly string[] ExpectedTokenKeys =
    {
        "RaceAccentBrush",
        "RaceAccentHoverBrush",
        "RaceAccentSecondaryBrush",
        "RaceAlertBrush",
        "RaceStopBrush",
        "RaceSuccessBrush",
        "RaceBackgroundBrush",
        "RaceCardBackgroundBrush",
        "RaceBorderBrush",
        "RaceTextPrimaryBrush",
        "RaceTextSecondaryBrush",
        "RaceTextMutedBrush",
        "RaceRowHoverBrush",
    };

    /// <summary>Green #10B981 from the design images — the single Fluent accent.</summary>
    public static readonly Color AccentColor = Color.FromArgb(0xFF, 0x10, 0xB9, 0x81);

    public static ThemePreferencesStore PreferencesStore { get; set; } = new();

    static ThemeManager()
    {
        // System-follow: when the OS theme flips (SystemThemeWatcher re-applies the
        // Fluent theme), re-apply our accent + palette on top of it.
        ApplicationThemeManager.Changed += (_, _) =>
        {
            if (Application.Current is null)
                return;
            ApplyAccentAndPalette(ApplicationThemeManager.GetAppTheme());
        };
    }

    public static void Apply(ThemeType theme)
    {
        if (theme == ThemeType.System)
        {
            ApplicationThemeManager.ApplySystemTheme(updateAccent: false);
            var window = Application.Current?.MainWindow;
            if (window is not null)
                SystemThemeWatcher.Watch(window, WindowBackdropType.Mica, updateAccents: false);
            ApplyAccentAndPalette(ApplicationThemeManager.GetAppTheme());
        }
        else
        {
            var appTheme = theme == ThemeType.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            ApplicationThemeManager.Apply(appTheme, WindowBackdropType.Mica, updateAccent: false);
            ApplyAccentAndPalette(appTheme);
        }

        PreferencesStore.Save(theme);
    }

    private static void ApplyAccentAndPalette(ApplicationTheme appTheme)
    {
        if (Application.Current is null)
            return;

        ApplicationAccentColorManager.Apply(AccentColor, appTheme, systemGlassColor: false, systemAccentColor: false);

        var requested = appTheme == ApplicationTheme.Dark ? ThemeType.Dark : ThemeType.Light;
        var palette = LoadPalette(requested);
        var missing = ValidatePalette(palette);
        if (missing.Count > 0)
        {
            Log.Warning("ThemePaletteIncomplete Palette={Palette} MissingKeys={MissingKeys}",
                requested, string.Join(",", missing));

            var fallback = LoadPalette(requested == ThemeType.Dark ? ThemeType.Light : ThemeType.Dark);
            if (ValidatePalette(fallback).Count == 0)
                palette = fallback;
        }

        SwapPalette(Application.Current.Resources.MergedDictionaries, palette);
    }

    public static ResourceDictionary LoadPalette(ThemeType theme)
    {
        var name = theme == ThemeType.Dark ? "Palette.Dark" : "Palette.Light";
        return new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/TrackGenius.UI;component/Theme/{name}.xaml"),
        };
    }

    /// <summary>Returns the expected token keys missing from the palette (empty = valid).</summary>
    public static IReadOnlyList<string> ValidatePalette(ResourceDictionary palette)
        => ExpectedTokenKeys.Where(key => palette[key] == null).ToList();

    /// <summary>Replaces any palette dictionaries (identified by the marker key) with <paramref name="palette"/>.</summary>
    public static void SwapPalette(ResourceDictionaryCollection mergedDictionaries, ResourceDictionary palette)
    {
        if (mergedDictionaries is null)
            throw new ArgumentNullException(nameof(mergedDictionaries));
        if (palette is null)
            throw new ArgumentNullException(nameof(palette));

        for (var i = mergedDictionaries.Count - 1; i >= 0; i--)
        {
            if (mergedDictionaries[i].ContainsKey(PaletteMarkerKey))
                mergedDictionaries.RemoveAt(i);
        }

        mergedDictionaries.Add(palette);
    }
}
```

- [ ] **Step 5: Merge palette into App.xaml**

`source/TrackGenius/App.xaml` — full replacement:

```xml
<Application x:Class="TrackGenius.UI.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ThemesDictionary Theme="Light" />
                <ui:ControlsDictionary />
                <ResourceDictionary Source="Theme/Palette.Light.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

(The initial Light palette is replaced at startup by `ThemeManager` — Task 3 — per the persisted preference.)

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj --filter "FullyQualifiedName~ThemeManagerTests"
```
Expected: 6 passed (suite total 91).

- [ ] **Step 7: Run the full suite**

```bash
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: 91 passed, 0 failed.

- [ ] **Step 8: Commit**

```bash
git add source/TrackGenius/Theme/Palette.Light.xaml source/TrackGenius/Theme/Palette.Dark.xaml source/TrackGenius/Theme/ThemeManager.cs source/TrackGenius/App.xaml source/TrackGenius.UITests/Theme/ThemeManagerTests.cs
git commit -m "Add race palette tokens and theme manager with green accent

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 3: Wire startup + Settings through `ThemeManager`

**Files:**
- Modify: `source/TrackGenius/App.xaml.cs`
- Modify: `source/TrackGenius/ViewModels/SettingPageViewModel.cs`

**Interfaces:**
- Consumes: Task 1 `ThemePreferencesStore`, Task 2 `ThemeManager.Apply`.
- Produces: startup applies the persisted theme before the window shows; the Settings combo (`Light`/`Dark`/`System`) routes through `ThemeManager.Apply` (which persists); `SettingPageViewModel` ctor seeds `_selectedTheme` from the store.

- [ ] **Step 1: Update App.xaml.cs**

In `source/TrackGenius/App.xaml.cs`:

Add usings (top of file, after the existing ones):

```csharp
using TrackGenius.UI.Theme;
using TrackGenius.UI.ViewModels;
```

Delete these members entirely:

```csharp
        // Default appearance settings. These can be persisted to user settings later
        // and re-applied at startup to honour the user's preference.
        private const ApplicationTheme DefaultTheme = ApplicationTheme.Unknown; // System-follow
        private const WindowBackdropType DefaultBackdrop = WindowBackdropType.Mica;
```

and the whole `ApplyAppearance` method:

```csharp
        private static void ApplyAppearance()
        {
            // Apply the configured theme. ApplicationTheme.Unknown means "follow the system theme".
            ApplicationThemeManager.Apply(DefaultTheme, DefaultBackdrop, updateAccent: true);

            if (DefaultTheme == ApplicationTheme.Unknown)
            {
                SystemThemeWatcher.Watch(null, DefaultBackdrop, updateAccents: true);
            }
        }
```

Replace the startup sequence in `OnStartup`:

```csharp
            ApplyAppearance();

            _mainForm = CreateMainWindow();
            MainWindow = _mainForm;
            _mainForm.Show();
```

with:

```csharp
            _mainForm = CreateMainWindow();
            MainWindow = _mainForm;

            // Apply the persisted theme before first render: palette + Fluent theme + green accent.
            ThemeManager.Apply(ThemeManager.PreferencesStore.Load());

            _mainForm.Show();
```

(The `using Wpf.Ui.Appearance;` import may become unused — remove it if the compiler flags nothing else uses it. `Wpf.Ui.Controls` is still used by the `MessageBox` in the exception handler.)

- [ ] **Step 2: Update SettingPageViewModel**

In `source/TrackGenius/ViewModels/SettingPageViewModel.cs`:

Add using:

```csharp
using TrackGenius.UI.Theme;
```

Replace the ctor initializer line:

```csharp
            _selectedTheme = GetThemeTypeFromCurrentTheme();
```

with:

```csharp
            _selectedTheme = ThemeManager.PreferencesStore.Load();
```

Delete the `GetThemeTypeFromCurrentTheme` method entirely:

```csharp
    private static ThemeType GetThemeTypeFromCurrentTheme()
    {
        return ApplicationThemeManager.GetAppTheme() switch
        {
            ApplicationTheme.Dark => ThemeType.Dark,
            _ => ThemeType.Light,
        };
    }
```

Replace the `ApplyTheme` method:

```csharp
    private static void ApplyTheme(ThemeType theme)
    {
        var applicationTheme = theme switch
        {
            ThemeType.Dark => ApplicationTheme.Dark,
            _ => ThemeType.Light,
        };

        ApplicationThemeManager.Apply(applicationTheme);
    }
```

with:

```csharp
    private static void ApplyTheme(ThemeType theme)
        => ThemeManager.Apply(theme);
```

Remove the now-unused `using Wpf.Ui.Appearance;` if nothing else references it in the file.

- [ ] **Step 3: Build and run the full suite**

```bash
dotnet build source/TrackGenius.sln
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: build 0 errors; 91 passed, 0 failed.

- [ ] **Step 4: Commit**

```bash
git add source/TrackGenius/App.xaml.cs source/TrackGenius/ViewModels/SettingPageViewModel.cs
git commit -m "Apply persisted theme at startup and route settings through theme manager

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 4: `RaceStyles` + surface restyle

**Files:**
- Create: `source/TrackGenius/Theme/RaceStyles.xaml`
- Modify: `source/TrackGenius/App.xaml` (add one merged dictionary)
- Modify: `source/TrackGenius/Views/Controls/RaceDataListControl.xaml`
- Modify: `source/TrackGenius/Views/Pages/QuickRacePage.xaml`
- Modify: `source/TrackGenius/Views/MainForm.xaml`

**Interfaces:**
- Consumes: Task 2 palette tokens (`DynamicResource`); WPF-UI implicit `ui:Button` style (for `BasedOn`).
- Produces: styles `RacePrimaryButtonStyle`, `RaceStopButtonStyle`, `RaceCardBorder`, `RaceRowBorderStyle`, `RaceHeaderTextStyle`, `RaceMutedTextStyle`, `RaceBestLapTextStyle`, `RaceProgressBarStyle`, `RaceStatusDot`; resource `RaceControlCornerRadius` (CornerRadius 8).

- [ ] **Step 1: Create RaceStyles.xaml**

`source/TrackGenius/Theme/RaceStyles.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">

    <CornerRadius x:Key="RaceControlCornerRadius">8</CornerRadius>

    <!-- Buttons: color comes from the green Fluent accent (Appearance=Primary) or the stop token (Appearance=Custom). -->
    <Style x:Key="RacePrimaryButtonStyle" TargetType="{x:Type ui:Button}" BasedOn="{StaticResource {x:Type ui:Button}}">
        <Setter Property="Appearance" Value="Primary" />
        <Setter Property="CornerRadius" Value="{StaticResource RaceControlCornerRadius}" />
        <Setter Property="Padding" Value="16,10" />
        <Setter Property="FontWeight" Value="SemiBold" />
    </Style>

    <Style x:Key="RaceStopButtonStyle" TargetType="{x:Type ui:Button}" BasedOn="{StaticResource {x:Type ui:Button}}">
        <Setter Property="Appearance" Value="Custom" />
        <Setter Property="Background" Value="{DynamicResource RaceStopBrush}" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="CornerRadius" Value="{StaticResource RaceControlCornerRadius}" />
        <Setter Property="Padding" Value="16,10" />
        <Setter Property="FontWeight" Value="SemiBold" />
    </Style>

    <Style x:Key="RaceCardBorder" TargetType="{x:Type Border}">
        <Setter Property="CornerRadius" Value="{StaticResource RaceControlCornerRadius}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="BorderBrush" Value="{DynamicResource RaceBorderBrush}" />
        <Setter Property="Background" Value="{DynamicResource RaceCardBackgroundBrush}" />
    </Style>

    <Style x:Key="RaceRowBorderStyle" TargetType="{x:Type Border}">
        <Setter Property="CornerRadius" Value="6" />
        <Style.Triggers>
            <DataTrigger Binding="{Binding IsMouseOver, RelativeSource={RelativeSource FindAncestor, AncestorType=ListViewItem}}" Value="True">
                <Setter Property="Background" Value="{DynamicResource RaceRowHoverBrush}" />
            </DataTrigger>
        </Style.Triggers>
    </Style>

    <Style x:Key="RaceHeaderTextStyle" TargetType="{x:Type TextBlock}">
        <Setter Property="Foreground" Value="{DynamicResource RaceTextSecondaryBrush}" />
        <Setter Property="FontWeight" Value="SemiBold" />
    </Style>

    <Style x:Key="RaceMutedTextStyle" TargetType="{x:Type TextBlock}">
        <Setter Property="Foreground" Value="{DynamicResource RaceTextMutedBrush}" />
    </Style>

    <Style x:Key="RaceBestLapTextStyle" TargetType="{x:Type TextBlock}">
        <Setter Property="Foreground" Value="{DynamicResource RaceAccentSecondaryBrush}" />
        <Setter Property="FontWeight" Value="SemiBold" />
    </Style>

    <Style x:Key="RaceProgressBarStyle" TargetType="{x:Type ProgressBar}">
        <Setter Property="Foreground" Value="{DynamicResource RaceAccentBrush}" />
        <Setter Property="Background" Value="{DynamicResource RaceBorderBrush}" />
    </Style>

    <Style x:Key="RaceStatusDot" TargetType="{x:Type Ellipse}">
        <Setter Property="Fill" Value="{DynamicResource RaceSuccessBrush}" />
        <Setter Property="Width" Value="8" />
        <Setter Property="Height" Value="8" />
    </Style>
</ResourceDictionary>
```

- [ ] **Step 2: Merge RaceStyles into App.xaml**

In `source/TrackGenius/App.xaml`, add after the palette line (order matters — styles must follow `ControlsDictionary` for the `BasedOn` implicit-style lookup):

```xml
                <ResourceDictionary Source="Theme/Palette.Light.xaml" />
                <ResourceDictionary Source="Theme/RaceStyles.xaml" />
```

- [ ] **Step 3: Restyle RaceDataListControl**

In `source/TrackGenius/Views/Controls/RaceDataListControl.xaml`:

a) Outer border (line ~26) — replace:

```xml
        <Border BorderThickness="1"
                BorderBrush="Gray"
                CornerRadius="5">
```

with:

```xml
        <Border Style="{StaticResource RaceCardBorder}">
```

b) All ten header-row `TextBlock`s (lines ~51–69) — add `Style="{StaticResource RaceHeaderTextStyle}"` next to the existing `FontSize="16" FontWeight="SemiBold"` (keep those; the style adds the themed foreground). Example for the first; apply the same attribute to each of the ten:

```xml
                        <TextBlock Grid.Column="0" FontSize="16" FontWeight="SemiBold" Style="{StaticResource RaceHeaderTextStyle}" Text="Position"/>
```

c) Row `Border` (line ~92) — replace `<Border Padding="16">` with:

```xml
                            <Border Padding="16" Style="{StaticResource RaceRowBorderStyle}">
```

d) ▲ triangle (line ~117) — replace `Foreground="Green"` with:

```xml
Foreground="{DynamicResource RaceAccentBrush}"
```

e) ▼ triangle (line ~130) — replace `Foreground="Red"` with:

```xml
Foreground="{DynamicResource RaceStopBrush}"
```

f) Best Lap cell (line ~184) — add the style:

```xml
                                    <TextBlock Grid.Row="0" Grid.Column="7" FontSize="16" VerticalAlignment="Center" Margin="12,0,0,0" Text="{Binding BestLapTime, Converter={StaticResource TimeSpanFormat}}" Style="{StaticResource RaceBestLapTextStyle}"
                                               Visibility="{Binding ColumnSettings.BestLap.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource={RelativeSource AncestorType=local:RaceDataListControl}}"/>
```

g) Progress bar (line ~192) — add the style:

```xml
                                    <ProgressBar Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="10" Height="6"
                                                 Minimum="0" Maximum="100" Value="{Binding LapsCount}"
                                                 Style="{StaticResource RaceProgressBarStyle}"/>
```

- [ ] **Step 4: Restyle QuickRacePage Start button**

In `source/TrackGenius/Views/Pages/QuickRacePage.xaml`:

Add the WPF-UI namespace to the root `Page` element (after the existing xmlns declarations):

```xml
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
```

Replace the Start button:

```xml
            <Button Content="Start"
                    Command="{Binding StartRaceCommand}"
                    IsEnabled="{Binding CanStartRace}"/>
```

with:

```xml
            <ui:Button Content="Start"
                       Appearance="Primary"
                       Style="{StaticResource RacePrimaryButtonStyle}"
                       Command="{Binding StartRaceCommand}"
                       IsEnabled="{Binding CanStartRace}"/>
```

- [ ] **Step 5: Theme the MainForm status bar text**

In `source/TrackGenius/Views/MainForm.xaml`, the two status TextBlocks (lines ~119–126) — add themed foregrounds:

```xml
                    <ui:TextBlock Grid.Column="1" Grid.Row="0"
                                  Text="Com 5"
                                  Foreground="{DynamicResource RaceTextSecondaryBrush}"
                                  VerticalAlignment="Center"
                                  HorizontalAlignment="Center"/>
                    <ui:TextBlock Grid.Column="1" Grid.Row="1"
                                  Text="Serial Port Status"
                                  Foreground="{DynamicResource RaceTextMutedBrush}"
                                  VerticalAlignment="Center"
                                  HorizontalAlignment="Center"/>
```

- [ ] **Step 6: Build and run the full suite**

```bash
dotnet build source/TrackGenius.sln
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: build 0 errors (XAML compiles — catches typos in resource keys resolvable at compile time only for StaticResource); 91 passed, 0 failed.

- [ ] **Step 7: Commit**

```bash
git add source/TrackGenius/Theme/RaceStyles.xaml source/TrackGenius/App.xaml source/TrackGenius/Views/Controls/RaceDataListControl.xaml source/TrackGenius/Views/Pages/QuickRacePage.xaml source/TrackGenius/Views/MainForm.xaml
git commit -m "Restyle race surfaces with themed tokens and card-like rows

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 5: Visual verification (both themes, compared to design images)

**Files:**
- Create (temp, not committed): `%TEMP%\tg-shots\light.png`, `%TEMP%\tg-shots\dark.png`
- Create (temp, not committed): screenshot helper `C:\Users\anaki\AppData\Local\Temp\tg-shots\shot.ps1`

**Interfaces:**
- Consumes: everything from Tasks 1–4; the design images at `G:\OneDrive\TrackGenius\UI Design\Dark.png` / `Light.png` (compare palette, card rows, button color).
- Produces: two screenshots delivered for review; suite green.

- [ ] **Step 1: Write the screenshot helper**

`C:\Users\anaki\AppData\Local\Temp\tg-shots\shot.ps1` (create the folder first):

```powershell
param([string]$OutFile)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Start-Sleep -Milliseconds 500
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$bitmap.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose(); $bitmap.Dispose()
Write-Output "saved $OutFile"
```

- [ ] **Step 2: Capture the Light theme**

```bash
mkdir -p "$TEMP/tg-shots"
printf '{\n  "theme": "Light"\n}' > "$LOCALAPPDATA/TrackGenius/theme.json"
dotnet run --project source/TrackGenius/TrackGenius.UI.csproj &
sleep 8
powershell -NoProfile -ExecutionPolicy Bypass -File "C:/Users/anaki/AppData/Local/Temp/tg-shots/shot.ps1" -OutFile "$TEMP/tg-shots/light.png"
taskkill //IM TrackGenius.exe //F
```

Expected: `light.png` exists and is non-trivially sized. The Quick Race page shows the leaderboard card (white card, `#E5E7EB` border, 8px corners) and a green Start button.

- [ ] **Step 3: Capture the Dark theme**

```bash
printf '{\n  "theme": "Dark"\n}' > "$LOCALAPPDATA/TrackGenius/theme.json"
dotnet run --project source/TrackGenius/TrackGenius.UI.csproj &
sleep 8
powershell -NoProfile -ExecutionPolicy Bypass -File "C:/Users/anaki/AppData/Local/Temp/tg-shots/shot.ps1" -OutFile "$TEMP/tg-shots/dark.png"
taskkill //IM TrackGenius.exe //F
```

- [ ] **Step 4: Compare against the design images**

Read `light.png`, `dark.png`, `G:\OneDrive\TrackGenius\UI Design\Light.png`, `G:\OneDrive\TrackGenius\UI Design\Dark.png` and check:
- Green accent on Start button and Fluent surfaces in both.
- Light: white cards / `#F8F9FA` chrome / dark text ramp. Dark: `#1E1E1E` cards / `#121212` chrome / white primary text.
- Best-lap column tint purple, position-change triangles green/red, progress bars green-on-neutral.
- No stray unthemed patches (gray literal border, blue accent).

If the window shows an obviously wrong theme, check `theme.json` was written before launch and that `ThemeManager.Apply` ran (logs under `<appbase>/logs`).

- [ ] **Step 5: Restore default preference + final suite**

```bash
printf '{\n  "theme": "System"\n}' > "$LOCALAPPDATA/TrackGenius/theme.json"
dotnet build source/TrackGenius.sln
dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj
```
Expected: build 0 errors; 91 passed, 0 failed.

- [ ] **Step 6: Report with screenshots**

Return status with both PNG paths so the controller can view them and hand both to the user for the final visual pass. Do not commit the PNGs or `shot.ps1`.

---

## Final verification (after all tasks)

- [ ] `dotnet build source/TrackGenius.sln` — 0 errors.
- [ ] `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj` — 91 passed / 0 failed.
- [ ] `dotnet test source/TrackGenius.ProtocolTests/TrackGenius.ProtocolTests.csproj` — still exactly 2 pre-existing failures (14 passed).
- [ ] `git log --oneline develop..HEAD` — five feature commits, clean tree.
- [ ] Screenshots of both themes reviewed against the design images and presented to the user.

## Out of scope (do not do)

- New panels/layout from the design images (events log, driver panel, track map, ARM/STOP/RESET buttons, header race info).
- Font family changes; control re-templating beyond property styles; per-window theme overrides.
- Fixing the pre-existing `SystemThemeWatcher.Watch(null, …)` no-op (replaced wholesale by the new startup path).
