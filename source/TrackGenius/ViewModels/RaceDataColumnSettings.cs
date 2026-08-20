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
        Transponder = new RaceDataColumnOption("Transponder", "Transponder", canHide: true);
        Notes = new RaceDataColumnOption("Notes", "Notes", canHide: true);

        All = new List<RaceDataColumnOption>
        {
            Position, CarNumber, Driver, Laps, Gap, Interval, LastLap, BestLap, Transponder, Notes
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
