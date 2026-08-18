using System;
using System.ComponentModel;
using JetBrains.Annotations;
using TrackGenius.Protocol.Interfaces;

namespace TrackGenius.Communication;

public class RaceConnectionService : IRaceConnectionService
{
    private readonly CommunicateService _communicateService;

    public event PropertyChangedEventHandler PropertyChanged;

    public RaceConnectionService([NotNull] CommunicateService communicateService)
    {
        _communicateService = communicateService ?? throw new ArgumentNullException(nameof(communicateService));
        _communicateService.PortOpenStateEventHandler += OnPortOpenStateChanged;
    }

    public IProtocol CurrentProtocol { get; private set; }

    public string CurrentProtocolName => CurrentProtocol?.ProtocolName ?? string.Empty;

    public string PortName { get; private set; } = string.Empty;

    // Delegates to CommunicateService so unplugged-device state can never go stale.
    public bool IsPortOpen => _communicateService.IsOpened;

    public bool CanStartRace => IsPortOpen && CurrentProtocol != null;

    public void Open([NotNull] string portName, [NotNull] IProtocol protocol)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name cannot be null or empty.", nameof(portName));
        if (protocol == null)
            throw new ArgumentNullException(nameof(protocol));

        _communicateService.StartService(portName, protocol);

        // StartService throws on failure; reaching here means the port opened with this protocol.
        CurrentProtocol = protocol;
        PortName = portName;
        RaisePropertiesChanged();
    }

    public void Close()
    {
        _communicateService.CloseService();

        CurrentProtocol = null;
        PortName = string.Empty;
        RaisePropertiesChanged();
    }

    private void OnPortOpenStateChanged(object sender, EventArgs e)
    {
        // Propagate external state changes (e.g. unexpected close) to subscribers.
        if (!_communicateService.IsOpened)
        {
            CurrentProtocol = null;
            PortName = string.Empty;
        }

        RaisePropertiesChanged();
    }

    private void RaisePropertiesChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPortOpen)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanStartRace)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentProtocol)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentProtocolName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PortName)));
    }
}
