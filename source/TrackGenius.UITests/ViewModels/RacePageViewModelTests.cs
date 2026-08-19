using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Communication;
using TrackGenius.Core;
using TrackGenius.Protocol.Robitronic;
using TrackGenius.UI.ViewModels;
using TrackGenius.UITests.Communication;

namespace TrackGenius.UITests.ViewModels;

[TestFixture]
public class RacePageViewModelTests
{
    private CommunicateService _communicateService;
    private RaceConnectionService _connection;
    private RacePageViewModel _viewModel;

    [SetUp]
    public void SetUp()
    {
        _communicateService = new CommunicateService(
            new FakeSerialPortWrapper(), NullLogger<CommunicateService>.Instance);
        _connection = new RaceConnectionService(_communicateService);
        _viewModel = new RacePageViewModel(
            new RaceEngineFactory(_connection, _communicateService), _connection);
    }

    [TearDown]
    public void TearDown() => _connection.Close();

    // Threading: the VM is constructed on the test thread, where Dispatcher.CurrentDispatcher
    // creates a dispatcher and CheckAccess() is true — the handler applies synchronously, no pumping.

    [Test]
    public void GivenOpenPortAndStartedRace_WhenDetectionArrives_ThenRaceDataItemsGainsRowWithMappedFields()
    {
        _connection.Open("COM3", new RobitronicProtocol());
        _viewModel.StartRaceCommand.Execute(null);

        // Full pipeline: RaceStart subscribed the consumer to MessageReceived.
        _communicateService.MessageReceived?.Invoke(
            _communicateService, MakeDetectedMessage(transponder: 100, milliseconds: 3_600_000));

        Assert.That(_viewModel.RaceDataItems, Has.Count.EqualTo(1));
        var row = _viewModel.RaceDataItems[0];
        Assert.Multiple(() =>
        {
            Assert.That(row.TransponderID, Is.EqualTo("100"));
            Assert.That(row.RacerPosition, Is.EqualTo(1));
            Assert.That(row.LapsCount, Is.EqualTo(1));
            Assert.That(row.Gap, Is.EqualTo("-"));
            Assert.That(row.Interval, Is.EqualTo("-"));
        });
    }

    [Test]
    public void GivenStartedRace_WhenSecondRacerDetected_ThenBothRowsPresentWithComputedGap()
    {
        _connection.Open("COM3", new RobitronicProtocol());
        _viewModel.StartRaceCommand.Execute(null);

        _communicateService.MessageReceived?.Invoke(
            _communicateService, MakeDetectedMessage(transponder: 100, milliseconds: 3_600_000));
        _communicateService.MessageReceived?.Invoke(
            _communicateService, MakeDetectedMessage(transponder: 200, milliseconds: 3_601_000));

        Assert.That(_viewModel.RaceDataItems, Has.Count.EqualTo(2));
        var first = _viewModel.RaceDataItems.Single(item => item.TransponderID == "100");
        var second = _viewModel.RaceDataItems.Single(item => item.TransponderID == "200");
        Assert.Multiple(() =>
        {
            Assert.That(first.RacerPosition, Is.EqualTo(1));
            Assert.That(second.RacerPosition, Is.EqualTo(2));
            Assert.That(second.Gap, Is.EqualTo("0:01.000"));
        });
    }

    [Test]
    public void GivenCompletedRace_WhenRaceRestarted_ThenRaceDataItemsClearedForNewRace()
    {
        _connection.Open("COM3", new RobitronicProtocol());
        _viewModel.StartRaceCommand.Execute(null);
        _communicateService.MessageReceived?.Invoke(
            _communicateService, MakeDetectedMessage(transponder: 100, milliseconds: 3_600_000));
        Assert.That(_viewModel.RaceDataItems, Has.Count.EqualTo(1));

        _viewModel.StartRaceCommand.Execute(null);

        Assert.That(_viewModel.RaceDataItems, Is.Empty);
    }

    private static DetectedMessage MakeDetectedMessage(long transponder, int milliseconds)
    {
        var data = new byte[13];
        data[0] = 13;   // packet length
        data[2] = 0x84; // CarDetect packet type
        data[3] = (byte)(transponder & 0xFF);
        data[4] = (byte)((transponder >> 8) & 0xFF);
        data[5] = (byte)((transponder >> 16) & 0xFF);
        data[6] = (byte)((transponder >> 24) & 0xFF);
        data[7] = (byte)(milliseconds & 0xFF);
        data[8] = (byte)((milliseconds >> 8) & 0xFF);
        data[9] = (byte)((milliseconds >> 16) & 0xFF);
        data[10] = (byte)((milliseconds >> 24) & 0xFF);
        return new DetectedMessage(data);
    }
}
