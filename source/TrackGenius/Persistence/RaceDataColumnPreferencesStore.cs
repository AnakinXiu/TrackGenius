using System;
using System.Collections.Generic;
using System.IO;

namespace TrackGenius.UI.Persistence;

/// <summary>
/// Reads/writes the race-data column preference file. Load methods return <c>null</c>
/// when the file is missing or unreadable (nothing to apply — keep constructor defaults);
/// an empty list means "file present, list empty".
/// </summary>
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
                return null;

            return RaceDataColumnPreferences.ParseHiddenColumns(File.ReadAllText(FilePath));
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<string> LoadShown()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            return RaceDataColumnPreferences.ParseShownColumns(File.ReadAllText(FilePath));
        }
        catch
        {
            return null;
        }
    }

    public void Save(IEnumerable<string> hiddenKeys)
        => Save(hiddenKeys, Array.Empty<string>());

    public void Save(IEnumerable<string> hiddenKeys, IEnumerable<string> shownKeys)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(FilePath, RaceDataColumnPreferences.SerializeColumns(hiddenKeys, shownKeys));
    }

    private static string DefaultFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrackGenius");
        return Path.Combine(folder, "raceDataColumns.json");
    }
}
