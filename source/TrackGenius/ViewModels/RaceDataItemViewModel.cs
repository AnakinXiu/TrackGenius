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

    [CanBeNull]
    private IDriver _driverInfo;

    [CanBeNull]
    private ICar _carInfo;

    private TimeSpan _bestLapTime;

    private readonly int _racerStartPosition = 0;
    private string _gap;
    private string _interval;
    private string _top5Average;
    private string _top10Average;
    private string _top3Consecutive;
    private string _stdDeviation;
    private string _consistency;

    /// <summary>Cached composite driver board VM (name + flag / transponder code).</summary>
    public RaceDataDriverViewModel Driver { get; }

    public string DriverName => _driverInfo?.DriverName ?? TransponderID;

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

    public string Top5Average
    {
        get => _top5Average;
        set => PropertyChanged.RaiseIfChanged(this, ref _top5Average, value, nameof(Top5Average));
    }

    public string Top10Average
    {
        get => _top10Average;
        set => PropertyChanged.RaiseIfChanged(this, ref _top10Average, value, nameof(Top10Average));
    }

    public string Top3Consecutive
    {
        get => _top3Consecutive;
        set => PropertyChanged.RaiseIfChanged(this, ref _top3Consecutive, value, nameof(Top3Consecutive));
    }

    public string StdDeviation
    {
        get => _stdDeviation;
        set => PropertyChanged.RaiseIfChanged(this, ref _stdDeviation, value, nameof(StdDeviation));
    }

    public string Consistency
    {
        get => _consistency;
        set => PropertyChanged.RaiseIfChanged(this, ref _consistency, value, nameof(Consistency));
    }

    public string Description { get; set; }

    public RaceDataItemViewModel(string transponderID, int startPosition)
    {
        TransponderID = transponderID;
        _racerStartPosition = startPosition;

        // Anonymous driver: the transponder ID is the identity; club unknown yet.
        Driver = new RaceDataDriverViewModel(null, null, transponderID);
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

        Driver = new RaceDataDriverViewModel(driver, null, TransponderID);
    }
}