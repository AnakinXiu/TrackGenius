using System.ComponentModel;
using TrackGenius.Protocol.Interfaces;

namespace TrackGenius.Communication;

/// <summary>
/// Shared, observable state of the race-timing connection: the protocol of the
/// currently open port and whether a race can be started. Single write point is
/// <see cref="RaceConnectionService.Open"/>/<see cref="RaceConnectionService.Close"/>.
/// </summary>
public interface IRaceConnectionService : INotifyPropertyChanged
{
    IProtocol CurrentProtocol { get; }

    string CurrentProtocolName { get; }

    string PortName { get; }

    bool IsPortOpen { get; }

    bool CanStartRace { get; }

    void Open(string portName, IProtocol protocol);

    void Close();
}
