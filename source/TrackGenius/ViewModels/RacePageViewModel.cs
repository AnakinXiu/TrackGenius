using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using JetBrains.Annotations;
using TrackGenius.Communication;
using TrackGenius.Core;
using TrackGenius.Model;
using TrackGenius.UI.Commands;

namespace TrackGenius.UI.ViewModels;

public class RacePageViewModel : INotifyPropertyChanged
{
    private readonly RaceEngineFactory _raceEngineFactory;
    private readonly IRaceConnectionService _connectionService;
    private RaceEngine _raceEngine;
    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<RaceDataItemViewModel> RaceDataItems { get; set; } = new();

    public ICommand StartRaceCommand { get; set; }

    public bool CanStartRace => _connectionService.CanStartRace;

    public RacePageViewModel([NotNull]RaceEngineFactory raceEngineFactory, [NotNull]IRaceConnectionService connectionService)
    {
        _raceEngineFactory = raceEngineFactory ?? throw new ArgumentNullException(nameof(raceEngineFactory));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        StartRaceCommand = new RelayCommand(StartRace);

        _connectionService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IRaceConnectionService.IsPortOpen)
                                  or nameof(IRaceConnectionService.CanStartRace))
                OnPropertyChanged(nameof(CanStartRace));
        };

        AddTestData();
    }

    private void AddTestData()
    {
        var items = new[]
        {
            new RaceDataItemViewModel("88156", 1)
            {
                RacerNumber = 88, RacerPosition = 1, LapsCount = 12,
                BestLapTime = TimeSpan.FromSeconds(18.234), LastLapTime = TimeSpan.FromSeconds(19.012),
                GapTime = TimeSpan.Zero, IntervalTime = TimeSpan.Zero, Description = "Leader"
            },
            new RaceDataItemViewModel("48825", 2)
            {
                RacerNumber = 7, RacerPosition = 2, LapsCount = 12,
                BestLapTime = TimeSpan.FromSeconds(18.401), LastLapTime = TimeSpan.FromSeconds(18.890),
                GapTime = TimeSpan.FromSeconds(0.8), IntervalTime = TimeSpan.FromSeconds(0.8)
            },
            new RaceDataItemViewModel("44401", 4)
            {
                RacerNumber = 44, RacerPosition = 3, LapsCount = 11,
                BestLapTime = TimeSpan.FromSeconds(18.567), LastLapTime = TimeSpan.FromSeconds(18.945),
                GapTime = TimeSpan.FromSeconds(2.1), IntervalTime = TimeSpan.FromSeconds(1.5)
            },
            new RaceDataItemViewModel("99812", 5)
            {
                RacerNumber = 99, RacerPosition = 4, LapsCount = 10,
                BestLapTime = TimeSpan.FromSeconds(18.900), LastLapTime = TimeSpan.FromSeconds(19.300),
                GapTime = TimeSpan.FromSeconds(5.0), IntervalTime = TimeSpan.FromSeconds(2.1)
            },
            new RaceDataItemViewModel("35890", 3)
            {
                RacerNumber = 23, RacerPosition = 5, LapsCount = 9,
                BestLapTime = TimeSpan.FromSeconds(19.123), LastLapTime = TimeSpan.FromSeconds(20.000),
                GapTime = TimeSpan.FromSeconds(8.4), IntervalTime = TimeSpan.FromSeconds(3.2)
            },
        };

        foreach (var item in items)
            RaceDataItems.Add(item);
    }

    private void StartRace()
    {
        if (!CanStartRace)
            return;

        // Keep the engine alive for the race: disposing it immediately would
        // unsubscribe its handlers before any detection could arrive.
        _raceEngine?.Dispose();
        _raceEngine = _raceEngineFactory.CreateRaceEngine();
        _raceEngine.RaceStart(new List<RaceData>());
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}