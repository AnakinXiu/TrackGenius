using System;
using System.ComponentModel;
using JetBrains.Annotations;
using Serilog;
using TrackGenius.Const;
using TrackGenius.Model;

namespace TrackGenius.UI.ViewModels;

public class RaceDataItemViewModel : INotifyPropertyChanged
{
    private int _racerNumber;
    private int _racerPosition;
    private int _lapsCount;
    private TimeSpan _lastLapTime;
    public event PropertyChangedEventHandler PropertyChanged;

    public string DriverName => _driverInfo?.DriverName ?? TransponderID;

    [CanBeNull]
    private IDriver _driverInfo;

    [CanBeNull]
    private ICar _carInfo;

    private TimeSpan _bestLapTime;

    private readonly int _racerStartPosition = 0;
    private string _gap;
    private string _interval;
    
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

    public string Gap
    {
        get => _gap;
        set => PropertyChanged.RaiseIfChanged(this, ref _gap, value, nameof(Gap));
    }

    public string Interval
    {
        get => _interval;
        set => PropertyChanged.RaiseIfChanged(this, ref _interval, value, nameof(Interval));
    }

    public string Description { get; set; }

    public RaceDataItemViewModel(string transponderID, int startPosition)
    {
        TransponderID = transponderID;
        _racerStartPosition = startPosition;
    }

    public RaceDataItemViewModel(IDriver driver, ICar car, int startPosition)
    {
        if (!driver.Cars.Contains(car))
        {
            Log.Logger.Error("Driver and car information mismatched. Driver: {Driver}, Car: {Car}", driver, car);
            throw new ArgumentException("Driver and car information mismatched.", nameof(car));
        }

        _driverInfo = driver;
        _carInfo = car;

        TransponderID = car.Transponder.RecoderNumber;
        _racerStartPosition = startPosition;
    }
}