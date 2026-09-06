using System.ComponentModel;
using System.Runtime.CompilerServices;
using TrackGenius.Const;

namespace TrackGenius.UI.ViewModels;

public class RaceDetailViewModel : INotifyPropertyChanged
{
    private int _racerNumber;
    private int _racerPosition;
    private int _lapsCount;
    private readonly int _racerStartPosition = 0;

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
    public event PropertyChangedEventHandler PropertyChanged;

    public RaceDetailViewModel(string racerName)
    {
    }

    public RaceDetailViewModel(int racerStartPosition)
    {
        _racerStartPosition = racerStartPosition;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}