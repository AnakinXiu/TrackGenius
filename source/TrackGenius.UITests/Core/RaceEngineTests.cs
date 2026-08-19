using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Communication;
using TrackGenius.Core;
using TrackGenius.Model;
using TrackGenius.Protocol.Robitronic;
using TrackGenius.UITests.Communication;

namespace TrackGenius.UITests.Core;

[TestFixture]
public class RaceEngineTests
{
    private FakeSerialPortWrapper _wrapper;
    private CommunicateService _communicateService;
    private RobitronicMessageConsumer _consumer;
    private RaceEngine _engine;
    private List<IReadOnlyList<RaceStandingsEntry>> _received;

    [SetUp]
    public void SetUp()
    {
        _wrapper = new FakeSerialPortWrapper();
        _communicateService = new CommunicateService(_wrapper, NullLogger<CommunicateService>.Instance);
        _consumer = new RobitronicMessageConsumer();
        _engine = new RaceEngine(_consumer, _communicateService);
        _received = new List<IReadOnlyList<RaceStandingsEntry>>();
        _engine.RaceDataChanged += (_, entries) => _received.Add(entries);
    }

    [TearDown]
    public void TearDown() => _engine.Dispose();

    /// <summary>
    /// DetectedMessage reads TransponderID from bytes 3-6 and Milliseconds from bytes 7-10,
    /// each little-endian (Cut -> Reverse -> big-endian ToInt32 of the reversed slice).
    /// </summary>
    private static DetectedMessage MakeDetectedMessage(long transponder, int milliseconds)
    {
        var data = new byte[13];
        data[0] = 13;   // packet length
        data[2] = 0x84; // CarDetect packet type (ctor requirement)
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

    private void SendDetection(long transponder, int milliseconds)
        => _consumer.ConsumeMessage(this, MakeDetectedMessage(transponder, milliseconds));

    [Test]
    public void GivenEncodedFrame_WhenParsed_ThenTransponderAndMillisecondsMatch()
    {
        var message = MakeDetectedMessage(100, 3_600_000);

        Assert.Multiple(() =>
        {
            Assert.That(message.TransponderID, Is.EqualTo("100"));
            Assert.That(message.Milliseconds, Is.EqualTo(3_600_000));
        });
    }

    [Test]
    public void GivenStartedRace_WhenFirstDetectionArrives_ThenRaceDataChangedFiresWithNewRacer()
    {
        _engine.RaceStart(new List<RaceData>());

        SendDetection(transponder: 100, milliseconds: 3_600_000);

        Assert.That(_received.Count, Is.EqualTo(1));
        Assert.That(_received[0].Count, Is.EqualTo(1));
        var entry = _received[0][0];
        Assert.Multiple(() =>
        {
            Assert.That(entry.Position, Is.EqualTo(1));
            Assert.That(entry.RaceData.Car.Transponder.RecoderNumber, Is.EqualTo("100"));
            Assert.That(entry.RaceData.LapsCount, Is.EqualTo(1));
            Assert.That(entry.Gap, Is.EqualTo("-"));
            Assert.That(entry.Interval, Is.EqualTo("-"));
        });
    }

    [Test]
    public void GivenFirstLapRecorded_WhenSecondDetectionWithinMinInterval_ThenNoEvent()
    {
        _engine.RaceStart(new List<RaceData>());
        SendDetection(transponder: 100, milliseconds: 3_600_000);

        // Whole-second timestamps make the last lap's sub-second component 0, so the
        // interval check compares against 0 and 1_000 < 1500 (MinLapIntervalMilliseconds) → suppressed.
        SendDetection(transponder: 100, milliseconds: 1_000);

        Assert.That(_received.Count, Is.EqualTo(1));   // only the first detection fired
    }

    [Test]
    public void GivenFirstLapRecorded_WhenSecondDetectionAfterMinInterval_ThenLapCountedAndEventFires()
    {
        _engine.RaceStart(new List<RaceData>());
        SendDetection(transponder: 100, milliseconds: 3_600_000);

        SendDetection(transponder: 100, milliseconds: 3_601_000);

        Assert.That(_received.Count, Is.EqualTo(2));
        var entry = _received[1].Single();
        Assert.Multiple(() =>
        {
            Assert.That(entry.RaceData.LapsCount, Is.EqualTo(2));
            Assert.That(entry.LastLapTime, Is.EqualTo(TimeSpan.FromMilliseconds(1_000)));
        });
    }

    [Test]
    public void GivenTwoRacersDetected_WhenStandingsRaised_ThenOrderAndGapComputed()
    {
        _engine.RaceStart(new List<RaceData>());

        SendDetection(transponder: 100, milliseconds: 3_600_000);
        SendDetection(transponder: 200, milliseconds: 3_601_000);

        var standings = _received[1];
        Assert.Multiple(() =>
        {
            Assert.That(standings[0].RaceData.Car.Transponder.RecoderNumber, Is.EqualTo("100"));
            Assert.That(standings[1].RaceData.Car.Transponder.RecoderNumber, Is.EqualTo("200"));
            Assert.That(standings[1].Gap, Is.EqualTo("0:01.000"));
            Assert.That(standings[1].Interval, Is.EqualTo("0:01.000"));
        });
    }
}
