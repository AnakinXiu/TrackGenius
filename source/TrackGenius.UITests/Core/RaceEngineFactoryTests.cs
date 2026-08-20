using System;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Communication;
using TrackGenius.Core;
using TrackGenius.Protocol.Kyosho;
using TrackGenius.Protocol.Robitronic;
using TrackGenius.UITests.Communication;

namespace TrackGenius.UITests.Core;

[TestFixture]
public class RaceEngineFactoryTests
{
    [Test]
    public void GivenNoOpenConnection_WhenCreateRaceEngine_ThenInvalidOperationExceptionThrown()
    {
        var factory = CreateFactory(out _);

        Assert.Throws<InvalidOperationException>(() => factory.CreateRaceEngine());
    }

    [Test]
    public void GivenOpenRobitronicConnection_WhenCreateRaceEngine_ThenEngineCreated()
    {
        var factory = CreateFactory(out var connection);
        connection.Open("COM3", new RobitronicProtocol());

        var engine = factory.CreateRaceEngine();

        Assert.That(engine, Is.Not.Null);
        engine.Dispose();
        connection.Close();
    }

    [Test]
    public void GivenOpenUnsupportedProtocol_WhenCreateRaceEngine_ThenNotSupportedExceptionThrown()
    {
        var factory = CreateFactory(out var connection);
        connection.Open("COM3", new KyoshoProtocol());

        Assert.Throws<NotSupportedException>(() => factory.CreateRaceEngine());

        connection.Close();
    }

    private static RaceEngineFactory CreateFactory(out RaceConnectionService connection)
    {
        var wrapper = new FakeSerialPortWrapper();
        var communicateService = new CommunicateService(wrapper, NullLogger<CommunicateService>.Instance);
        connection = new RaceConnectionService(communicateService);
        return new RaceEngineFactory(connection, communicateService);
    }
}
