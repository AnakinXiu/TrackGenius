using System;
using JetBrains.Annotations;
using TrackGenius.Communication;

namespace TrackGenius.Core;

public class RaceEngineFactory
{
    private readonly IRaceConnectionService _connectionService;
    private readonly CommunicateService _communicateService;
    private readonly MessageConsumerFactory _messageConsumerFactory = new();

    public RaceEngineFactory([NotNull] IRaceConnectionService connectionService,
                             [NotNull] CommunicateService communicateService)
    {
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _communicateService = communicateService ?? throw new ArgumentNullException(nameof(communicateService));
    }

    public RaceEngine CreateRaceEngine()
    {
        if (!_connectionService.CanStartRace)
            throw new InvalidOperationException(
                $"The race cannot be started: the serial port is not open with a valid protocol (Port='{_connectionService.PortName}', Protocol='{_connectionService.CurrentProtocolName}').");

        var messageConsumer = _messageConsumerFactory.Create(_connectionService.CurrentProtocol);
        return new RaceEngine(messageConsumer, _communicateService);
    }
}
