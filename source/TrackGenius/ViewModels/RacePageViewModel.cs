using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using JetBrains.Annotations;
using TrackGenius.Communication;
using TrackGenius.Core;
using TrackGenius.Model;
using TrackGenius.Speech;
using TrackGenius.UI.Commands;

namespace TrackGenius.UI.ViewModels;

public class RacePageViewModel : INotifyPropertyChanged
{
    private readonly RaceEngineFactory _raceEngineFactory;
    private readonly IRaceConnectionService _connectionService;
    private readonly RaceAnnouncer _speechAnnouncer;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _clockTimer;
    private RaceEngine _raceEngine;
    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<RaceDataItemViewModel> RaceDataItems { get; set; } = new();

    public ICommand StartRaceCommand { get; set; }

    public bool CanStartRace => _connectionService.CanStartRace;

    [CanBeNull]
    private IRace CurrentRace
    {
        get => _currentRace;
        set
        {
            _currentRace = value; 
            OnPropertyChanged(nameof(RaceName));
        }
    }

    public string RaceName => CurrentRace?.RaceName ?? string.Empty;

    public string RaceTime => _raceEngine?.RaceTime.ToString(@"m\:ss") ?? string.Empty;

    public string RemainTime => _raceEngine?.RemainTime.ToString(@"m\:ss") ?? string.Empty;

    public string CurrentTime => DateTime.Now.ToLongTimeString();

    public RacePageViewModel([NotNull] RaceEngineFactory raceEngineFactory,
        [NotNull] IRaceConnectionService connectionService,
        [NotNull] RaceAnnouncer speechAnnouncer)
    {
        _raceEngineFactory = raceEngineFactory ?? throw new ArgumentNullException(nameof(raceEngineFactory));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _speechAnnouncer = speechAnnouncer ?? throw new ArgumentNullException(nameof(speechAnnouncer));
        StartRaceCommand = new RelayCommand(StartRace);

        // One clock for the board: ticks the three time properties every second.
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _clockTimer.Tick += (_, _) =>
        {
            OnPropertyChanged(nameof(RaceTime));
            OnPropertyChanged(nameof(RemainTime));
            OnPropertyChanged(nameof(CurrentTime));
        };
        _clockTimer.Start();

        _connectionService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IRaceConnectionService.IsPortOpen)
                or nameof(IRaceConnectionService.CanStartRace))
                OnPropertyChanged(nameof(CanStartRace));
        };

        AddTestData();
    }

    private string _lastError;
    [CanBeNull]
    private IRace _currentRace;

    /// <summary>Last race start failure message; empty when the last start succeeded (not yet bound in XAML).</summary>
    public string LastError
    {
        get => _lastError;
        private set
        {
            if (_lastError == value)
                return;
            _lastError = value;
            OnPropertyChanged(nameof(LastError));
        }
    }

    public void ApplyCurrentRace(IRace race)
    {
        CurrentRace = race;
    }

    private void StartRace()
    {
        if (!CanStartRace)
            return;

        // Stale rows from a previous race must not leak into the new one:
        // ApplyStandings sizes new positions from RaceDataItems.Count.
        RaceDataItems.Clear();

        try
        {
            if (_raceEngine != null)
            {
                // Keep the engine alive for the race: disposing it immediately would
                // unsubscribe its handlers before any detection could arrive.
                _raceEngine.RaceDataChanged -= OnRaceDataChanged;
                _speechAnnouncer.Detach(_raceEngine);
                _raceEngine.Dispose();
            }

            _raceEngine = _raceEngineFactory.CreateRaceEngine();
            _raceEngine.RaceDataChanged += OnRaceDataChanged;
            _speechAnnouncer.Attach(_raceEngine);

            var race = new Race(Guid.NewGuid(), RaceType.FreePractice, new RaceClass("World GT"), 10, new List<RaceData>())
            {
                RaceName = $"Quick Race {DateTime.Now:yyyy-MM-dd HH:mm}",
            };
            _raceEngine.RaceStart(race);
            ApplyCurrentRace(race);
            LastError = string.Empty;
        }
        catch (NotSupportedException ex)
        {
            // e.g. a protocol without a message consumer is selected.
            _raceEngine = null;
            LastError = ex.Message;
        }
    }

    private void OnRaceDataChanged(object sender, IReadOnlyList<RaceStandingsEntry> entries)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => ApplyStandings(entries));
            return;
        }

        ApplyStandings(entries);
    }

    private void ApplyStandings(IReadOnlyList<RaceStandingsEntry> entries)
    {
        foreach (var entry in entries)
        {
            var item = RaceDataItems.FirstOrDefault(
                existing => existing.TransponderID == entry.RaceData.Car.Transponder.RecoderNumber);
            if (item == null)
            {
                item = new RaceDataItemViewModel(entry.RaceData.Driver, entry.RaceData.Car, RaceDataItems.Count + 1);
                RaceDataItems.Add(item);
            }

            item.RacerPosition = entry.Position;
            item.LapsCount = entry.RaceData.LapsCount;
            item.LastLapTime = entry.LastLapTime;
            item.BestLapTime = entry.BestLapTime;
            item.IsRaceBestLap = entry.IsRaceBestLap;
            item.Gap = entry.Gap;
            item.Interval = entry.Interval;
            item.Top5Average = entry.Top5Average;
            item.Top10Average = entry.Top10Average;
            item.Top3Consecutive = entry.Top3Consecutive;
            item.StdDeviation = entry.StdDeviation;
            item.Consistency = entry.Consistency;
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void AddTestData()
    {
        var items = new[]
        {
            new RaceDataItemViewModel("88156", 1)
            {
                RacerNumber = 88, RacerPosition = 1, LapsCount = 12,
                BestLapTime = TimeSpan.FromSeconds(18.234), LastLapTime = TimeSpan.FromSeconds(19.012),
                Gap = "-", Interval = "-", Description = "Leader", IsRaceBestLap = true
            },
            new RaceDataItemViewModel("48825", 2)
            {
                RacerNumber = 7, RacerPosition = 2, LapsCount = 12,
                BestLapTime = TimeSpan.FromSeconds(18.401), LastLapTime = TimeSpan.FromSeconds(18.890),
                Gap = "0:00.800", Interval = "0:00.800"
            },
            new RaceDataItemViewModel("44401", 4)
            {
                RacerNumber = 44, RacerPosition = 3, LapsCount = 11,
                BestLapTime = TimeSpan.FromSeconds(18.567), LastLapTime = TimeSpan.FromSeconds(18.945),
                Gap = "0:02.100", Interval = "0:00.700"
            },
            new RaceDataItemViewModel("99812", 5)
            {
                RacerNumber = 99, RacerPosition = 4, LapsCount = 10,
                BestLapTime = TimeSpan.FromSeconds(18.900), LastLapTime = TimeSpan.FromSeconds(19.300),
                Gap = "0:05.000", Interval = "0:02.900"
            },
            new RaceDataItemViewModel("35890", 3)
            {
                RacerNumber = 23, RacerPosition = 5, LapsCount = 9,
                BestLapTime = TimeSpan.FromSeconds(19.123), LastLapTime = TimeSpan.FromSeconds(20.000),
                Gap = "0:08.400", Interval = "0:03.400"
            },
        };

        foreach (var item in items)
            RaceDataItems.Add(item);
    }

}