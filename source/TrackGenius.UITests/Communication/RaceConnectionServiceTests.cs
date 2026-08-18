using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Communication;
using TrackGenius.Protocol.Kyosho;
using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.UITests.Communication;

[TestFixture]
public class RaceConnectionServiceTests
{
    [Test]
    public void GivenClosedConnection_WhenCreated_ThenDefaultsAreClosedAndCannotStartRace()
    {
        var service = CreateService();

        Assert.Multiple(() =>
        {
            Assert.That(service.IsPortOpen, Is.False);
            Assert.That(service.CurrentProtocol, Is.Null);
            Assert.That(service.CurrentProtocolName, Is.Empty);
            Assert.That(service.PortName, Is.Empty);
            Assert.That(service.CanStartRace, Is.False);
        });
    }

    [Test]
    public void GivenClosedConnection_WhenOpenSucceeds_ThenProtocolPortRecordedAndCanStartRace()
    {
        var service = CreateService();

        service.Open("COM3", new RobitronicProtocol());

        Assert.Multiple(() =>
        {
            Assert.That(service.IsPortOpen, Is.True);
            Assert.That(service.CurrentProtocol, Is.InstanceOf<RobitronicProtocol>());
            Assert.That(service.CurrentProtocolName, Is.EqualTo("Robitronic"));
            Assert.That(service.PortName, Is.EqualTo("COM3"));
            Assert.That(service.CanStartRace, Is.True);
        });
    }

    [Test]
    public void GivenOpenConnection_WhenClosed_ThenStateClearedAndCannotStartRace()
    {
        var service = CreateService();
        service.Open("COM3", new RobitronicProtocol());

        service.Close();

        Assert.Multiple(() =>
        {
            Assert.That(service.IsPortOpen, Is.False);
            Assert.That(service.CurrentProtocol, Is.Null);
            Assert.That(service.CurrentProtocolName, Is.Empty);
            Assert.That(service.PortName, Is.Empty);
            Assert.That(service.CanStartRace, Is.False);
        });
    }

    [Test]
    public void GivenOpenConnection_WhenReopenedWithDifferentProtocol_ThenCurrentProtocolFollowsNewSelection()
    {
        var service = CreateService();
        service.Open("COM3", new RobitronicProtocol());

        service.Open("COM3", new KyoshoProtocol());

        Assert.That(service.CurrentProtocolName, Is.EqualTo("Kyosho"));
    }

    [Test]
    public void GivenClosedConnection_WhenOpenFails_ThenStateUnchangedAndCannotStartRace()
    {
        var (service, wrapper) = CreateServiceWithWrapper();
        wrapper.FailOnOpen = true;

        Assert.Throws<InvalidOperationException>(() => service.Open("COM3", new RobitronicProtocol()));

        Assert.Multiple(() =>
        {
            Assert.That(service.IsPortOpen, Is.False);
            Assert.That(service.CurrentProtocol, Is.Null);
            Assert.That(service.CanStartRace, Is.False);
        });
    }

    [Test]
    public void GivenService_WhenPortOpens_ThenPropertyChangedRaisedForStatusProperties()
    {
        var service = CreateService();
        var raised = new List<string>();
        service.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        service.Open("COM3", new RobitronicProtocol());

        Assert.That(raised, Does.Contain(nameof(RaceConnectionService.IsPortOpen)));
        Assert.That(raised, Does.Contain(nameof(RaceConnectionService.CanStartRace)));
    }

    [Test]
    public void GivenOpenConnection_WhenPortClosesExternally_ThenStateClearedAndCannotStartRace()
    {
        var wrapper = new FakeSerialPortWrapper();
        var communicateService = new CommunicateService(wrapper, NullLogger<CommunicateService>.Instance);
        var service = new RaceConnectionService(communicateService);
        service.Open("COM3", new RobitronicProtocol());

        // Simulate an external close (e.g. device unplugged): the wrapper's state flips
        // without going through RaceConnectionService.Close().
        wrapper.ClosePort();
        communicateService.RaisePortOpenStateChanged();

        Assert.Multiple(() =>
        {
            Assert.That(service.IsPortOpen, Is.False);
            Assert.That(service.CurrentProtocol, Is.Null);
            Assert.That(service.CurrentProtocolName, Is.Empty);
            Assert.That(service.PortName, Is.Empty);
            Assert.That(service.CanStartRace, Is.False);
        });
    }

    private static RaceConnectionService CreateService() => CreateServiceWithWrapper().Service;

    private static (RaceConnectionService Service, FakeSerialPortWrapper Wrapper) CreateServiceWithWrapper()
    {
        var wrapper = new FakeSerialPortWrapper();
        var communicateService = new CommunicateService(wrapper, NullLogger<CommunicateService>.Instance);
        return (new RaceConnectionService(communicateService), wrapper);
    }
}
