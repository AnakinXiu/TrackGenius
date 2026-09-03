using System.Windows.Media;
using TrackGenius.Model;

namespace TrackGenius.UI.ViewModels;

/// <summary>
/// Composite driver display for the standings grid: name (with nationality flag
/// when known) on top, transponder code beneath. Additional lines follow later.
/// </summary>
public class RaceDataDriverViewModel
{
    private readonly IDriver _driver;
    private readonly IRaceClub _club;

    public string DriverName => _driver?.DriverName ?? TransponderCode;

    /// <summary>The car's transponder number; the identity fallback for anonymous drivers.</summary>
    public string TransponderCode { get; }

    public bool ShowTransponderCode { get; } = true;

    public bool ShowNationality { get; } = false;

    /// <summary>WPF image source for the driver's national flag; null until nationality data exists.</summary>
    public ImageSource Flag { get; }

    public string ClubName => _club?.ClubName;

    /// <summary>Design-time ctor: sample data, no driver.</summary>
    public RaceDataDriverViewModel()
        : this(null, null, "00000")
    {
    }

    public RaceDataDriverViewModel(IDriver driver, IRaceClub club, string transponderCode)
    {
        _driver = driver;
        _club = club;
        TransponderCode = transponderCode ?? string.Empty;
    }
}
