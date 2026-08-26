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
        => ParseArray(json, "hiddenColumns");

    public static IReadOnlyList<string> ParseShownColumns(string json)
        => ParseArray(json, "shownColumns");

    public static string SerializeHiddenColumns(IEnumerable<string> keys)
        => SerializeColumns(keys, Array.Empty<string>());

    public static string SerializeColumns(IEnumerable<string> hiddenKeys, IEnumerable<string> shownKeys)
    {
        var payload = new Payload
        {
            HiddenColumns = (hiddenKeys ?? Enumerable.Empty<string>()).ToList(),
            ShownColumns = (shownKeys ?? Enumerable.Empty<string>()).ToList(),
        };
        return JsonSerializer.Serialize(payload, Options);
    }

    private static IReadOnlyList<string> ParseArray(string json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(propertyName, out var array))
                return Array.Empty<string>();

            if (array.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            return array.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString() ?? string.Empty)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private sealed class Payload
    {
        public List<string> HiddenColumns { get; set; } = new();
        public List<string> ShownColumns { get; set; } = new();
    }
}
