using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using JetBrains.Annotations;
using TrackGenius.Const;
using TrackGenius.Model;

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
    private TimeSpan _lastLapTime;
    public event PropertyChangedEventHandler PropertyChanged;

    public string DriverName => _driverInfo?.DriverName ?? string.Empty;

    [CanBeNull]
    private IDriver _driverInfo;

    private TimeSpan _bestLapTime;

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

    public TimeSpan LastLapTime
    {
        get => _lastLapTime;
        set => PropertyChanged.RaiseIfChanged(this, ref _lastLapTime, value, nameof(LastLapTime));
    }

    public TimeSpan BestLapTime
    {
        get => _bestLapTime;
        set => PropertyChanged.RaiseIfChanged(this, ref _bestLapTime, value, nameof(BestLapTime));
    }

    public string Description { get; set; }

    public RaceDataItem()
    {
        
    }
}