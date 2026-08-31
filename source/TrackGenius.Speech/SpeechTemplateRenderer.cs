using System.Globalization;
using System.Linq;
using TrackGenius.Model;
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

    // Car "number" spoken from the transponder id's numeric value; 0 when non-numeric.
    private static int CarNumber(AnnouncementIntent intent)
        => int.TryParse(intent.DriverId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
}
