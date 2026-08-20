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
