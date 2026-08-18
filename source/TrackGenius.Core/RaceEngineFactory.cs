using System;
using JetBrains.Annotations;
using TrackGenius.Communication;

namespace TrackGenius.Core;

public class RaceEngineFactory
{
    private readonly CommunicateService _communicateService;

    public RaceEngineFactory([NotNull] CommunicateService communicateService)
    {
        _communicateService = communicateService ?? throw new ArgumentNullException(nameof(communicateService));
    }

    public RaceEngine CreateRaceEngine()
    {
        var messageConsumer = GetMessageConsumer();
        return new RaceEngine(messageConsumer, _communicateService);
    }

    [NotNull]
    private static IMessageConsumer GetMessageConsumer()
    {
        return new RobitronicMessageConsumer();
    }
}