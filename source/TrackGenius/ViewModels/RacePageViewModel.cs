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
    }

    private void StartRace()
    {
        var raceEngine = new RaceEngine(new RobitronicMessageConsumer(), _comService);
    }
}