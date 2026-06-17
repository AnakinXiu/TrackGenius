using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using TrackGenius.Core;
using TrackGenius.UI.Commands;

namespace TrackGenius.UI.ViewModels;

public class RacePageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<RaceDataItem> RaceDataItems { get; set; } = new();

    public ICommand StartRaceCommand { get; set; }

    public RacePageViewModel()
    {
        StartRaceCommand = new RelayCommand(StartRace);
    }

    private void StartRace()
    {
        var raceEngine = new RaceEngine();
    }
}