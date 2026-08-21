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
