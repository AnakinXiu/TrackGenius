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
