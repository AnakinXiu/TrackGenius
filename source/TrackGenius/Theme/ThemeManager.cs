using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Serilog;
using TrackGenius.UI.Persistence;
using TrackGenius.UI.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

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
        "RaceBestTimeBrush",
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
    public static void SwapPalette(Collection<ResourceDictionary> mergedDictionaries, ResourceDictionary palette)
    {
        if (mergedDictionaries is null)
            throw new ArgumentNullException(nameof(mergedDictionaries));
        if (palette is null)
            throw new ArgumentNullException(nameof(palette));

        for (var i = mergedDictionaries.Count - 1; i >= 0; i--)
        {
            if (mergedDictionaries[i].Contains(PaletteMarkerKey))
                mergedDictionaries.RemoveAt(i);
        }

        mergedDictionaries.Add(palette);
    }
}
