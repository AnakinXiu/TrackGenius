using System;
using System.ComponentModel;
using JetBrains.Annotations;
using TrackGenius.Const;
using TrackGenius.Model;

namespace TrackGenius.UI.ViewModels;

public class RaceDataItem : INotifyPropertyChanged
{
    private int _racerNumber;
    private int _racerPosition;
    private int _lapsCount;
    private TimeSpan _lastLapTime;
    public event PropertyChangedEventHandler PropertyChanged;

    public string DriverName => _driverInfo?.DriverName ?? TransponderID;

    [CanBeNull]
    private IDriver _driverInfo;

    private TimeSpan _bestLapTime;

    private readonly int _racerStartPosition = 0;
    private TimeSpan _gapTime;
    private TimeSpan _intervalTime;
    

    public string TransponderID { get; }

    public int RacerNumber
    {
        get => _racerNumber;
        set => PropertyChanged.RaiseIfChanged(this, ref _racerNumber, value, nameof(RacerNumber));
    }

    public int RacerPosition
    {
        get => _racerPosition;
        set => PropertyChanged.RaiseIfChanged(this, ref _racerPosition, value, nameof(RacerPosition), nameof(RacerPositionChange));
    }

    public int RacerPositionChange => RacerPosition - _racerStartPosition;

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

    public TimeSpan GapTime
    {
        get => _gapTime;
        set => PropertyChanged.RaiseIfChanged(this, ref _gapTime, value, nameof(GapTime));
    }

    public TimeSpan IntervalTime
    {
        get => _intervalTime;
        set => PropertyChanged.RaiseIfChanged(this, ref _intervalTime, value, nameof(IntervalTime));
    }

    public string Description { get; set; }

    public RaceDataItem(string transponderID)
    {
        TransponderID = transponderID;
    }

    public RaceDataItem(string transponderID, int startPosition)
    {
        TransponderID = transponderID;
        _racerStartPosition = startPosition;
    }
}