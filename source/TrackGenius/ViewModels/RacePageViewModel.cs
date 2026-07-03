using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using TrackGenius.Communication;
using TrackGenius.Core;
using TrackGenius.UI.Commands;

namespace TrackGenius.UI.ViewModels;

public class RacePageViewModel : INotifyPropertyChanged
{
    private readonly CommunicateService _comService;
    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<RaceDataItem> RaceDataItems { get; set; } = new();

    public ICommand StartRaceCommand { get; set; }

    public RacePageViewModel(CommunicateService comService)
    {
        _comService = comService;
        StartRaceCommand = new RelayCommand(StartRace);

        AddTestData();
    }

    private void AddTestData()
    {
        RaceDataItems.Add(new RaceDataItem("88156"));
        RaceDataItems.Add(new RaceDataItem("44401"));
        RaceDataItems.Add(new RaceDataItem("48825"));
        RaceDataItems.Add(new RaceDataItem("35890"));
        RaceDataItems.Add(new RaceDataItem("99812"));
    }

    private void StartRace()
    {
        var raceEngine = new RaceEngine(new RobitronicMessageConsumer(), _comService);
    }
}