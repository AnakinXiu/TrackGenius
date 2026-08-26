using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using TrackGenius.UI.Theme;

namespace TrackGenius.UITests.Theme;

[TestFixture]
public class ThemeManagerTests
{
    /// <summary>Locates the directory holding TrackGenius.sln by walking up from the test bin output.</summary>
    private static string SolutionDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("TrackGenius.sln").Any())
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("TrackGenius.sln not found upward from test bin.");
    }

    private static string PalettePath(string file)
        => Path.Combine(SolutionDir(), "TrackGenius", "Theme", file);

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
        Assert.That(host.MergedDictionaries.Any(d => d.Contains("SomeOther")), Is.True);
    }
}
