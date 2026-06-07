using System.Collections.ObjectModel;
using System.ComponentModel;
using TrackGenius.Const;

namespace TrackGenius.UI.ViewModels;

public class RacePageViewModel :INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<RaceDataItem> RaceDataItems { get; set; } = new ObservableCollection<RaceDataItem>();
}

public class RaceDataItem : INotifyPropertyChanged
{
    private int _racerNumber;
    private int _racerPosition;
    private int _lapsCount;
    public event PropertyChangedEventHandler PropertyChanged;
    
    public string DriverName { get; set; }

    public int RacerNumber
    {
        get => _racerNumber;
        set => PropertyChanged.RaiseIfChanged(this, ref _racerNumber, value, nameof(RacerNumber));
    }

    public int RacerPosition
    {
        get => _racerPosition;
        set => PropertyChanged.RaiseIfChanged(this, ref _racerPosition, value, nameof(RacerPosition));
    }

    public int LapsCount
    {
        get => _lapsCount;
        set => PropertyChanged.RaiseIfChanged(this, ref _lapsCount, value, nameof(LapsCount));
    }

    public int LapsCount
    {
        get => _lapsCount;
        set => PropertyChanged.RaiseIfChanged(this, ref _lapsCount, value, nameof(LapsCount));
    }

    public string Description { get; set; }
}