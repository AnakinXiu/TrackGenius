using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace TrackGenius.UI.ViewModels;

public sealed class RaceDataColumnSettings : INotifyPropertyChanged
{
    public RaceDataColumnOption Position { get; }
    public RaceDataColumnOption CarNumber { get; }
    public RaceDataColumnOption Driver { get; }
    public RaceDataColumnOption Laps { get; }
    public RaceDataColumnOption Gap { get; }
    public RaceDataColumnOption Interval { get; }
    public RaceDataColumnOption LastLap { get; }
    public RaceDataColumnOption BestLap { get; }
    public RaceDataColumnOption Top5Average { get; }
    public RaceDataColumnOption Top10Average { get; }
    public RaceDataColumnOption Top3Consecutive { get; }
    public RaceDataColumnOption StdDeviation { get; }
    public RaceDataColumnOption Consistency { get; }
    public RaceDataColumnOption Transponder { get; }
    public RaceDataColumnOption Notes { get; }

    public IList<RaceDataColumnOption> All { get; }

    public event PropertyChangedEventHandler PropertyChanged;

    public RaceDataColumnSettings()
    {
        Position = new RaceDataColumnOption("Position", "Position", canHide: false);
        CarNumber = new RaceDataColumnOption("CarNumber", "Car #", canHide: true);
        Driver = new RaceDataColumnOption("Driver", "Driver", canHide: true);
        Laps = new RaceDataColumnOption("Laps", "Laps", canHide: true);
        Gap = new RaceDataColumnOption("Gap", "Gap", canHide: true);
        Interval = new RaceDataColumnOption("Interval", "Interval", canHide: true);
        LastLap = new RaceDataColumnOption("LastLap", "Last Lap", canHide: true);
        BestLap = new RaceDataColumnOption("BestLap", "Best Lap", canHide: true);
        Top5Average = new RaceDataColumnOption("Top5Average", "Top 5", canHide: true, isVisible: false);
        Top10Average = new RaceDataColumnOption("Top10Average", "Top 10", canHide: true, isVisible: false);
        Top3Consecutive = new RaceDataColumnOption("Top3Consecutive", "Top 3 Consec", canHide: true, isVisible: false);
        StdDeviation = new RaceDataColumnOption("StdDeviation", "Std Dev", canHide: true, isVisible: false);
        Consistency = new RaceDataColumnOption("Consistency", "Consistency", canHide: true, isVisible: false);
        Transponder = new RaceDataColumnOption("Transponder", "Transponder", canHide: true);
        Notes = new RaceDataColumnOption("Notes", "Notes", canHide: true);

        All = new List<RaceDataColumnOption>
        {
            Position, CarNumber, Driver, Laps, Gap, Interval, LastLap, BestLap,
            Top5Average, Top10Average, Top3Consecutive, StdDeviation, Consistency,
            Transponder, Notes
        };
    }

    public void ApplyHiddenKeys(IEnumerable<string> hiddenKeys)
    {
        var set = hiddenKeys as ISet<string> ?? new HashSet<string>(hiddenKeys ?? System.Array.Empty<string>(), System.StringComparer.Ordinal);
        foreach (var option in All)
            option.IsVisible = !(option.CanHide && set.Contains(option.Key));
    }

    public IEnumerable<string> GetHiddenKeys()
        => All.Where(o => !o.IsVisible).Select(o => o.Key);
}
