using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrackGenius.Model;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests.Abstractions;

[TestFixture]
public class ModelContractTests
{
    [Test]
    public void GivenPriorities_WhenRead_ThenNumericValuesAreStable()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)AnnouncementPriority.Background, Is.EqualTo(0));
            Assert.That((int)AnnouncementPriority.Normal, Is.EqualTo(10));
            Assert.That((int)AnnouncementPriority.Important, Is.EqualTo(50));
            Assert.That((int)AnnouncementPriority.High, Is.EqualTo(70));
            Assert.That((int)AnnouncementPriority.Critical, Is.EqualTo(100));
        });
    }

    [Test]
    public void GivenIntent_WhenConstructedWithDefaults_ThenExpiresAfterNull()
    {
        var intent = new AnnouncementIntent("FastestLap", AnnouncementPriority.High, "100", "Car 100",
            new List<RaceStandingsEntry>(), DateTimeOffset.Now);

        Assert.That(intent.ExpiresAfter, Is.Null);
    }

    [Test]
    public void GivenSpeechMessage_WhenConstructed_ThenAllFieldsHold()
    {
        var created = DateTimeOffset.Now;
        var message = new SpeechMessage(Guid.NewGuid(), "hello", null,
            AnnouncementPriority.High, "FastestLap", created, created + TimeSpan.FromSeconds(15),
            Interruptible: false, "en-US", null);

        Assert.Multiple(() =>
        {
            Assert.That(message.Category, Is.EqualTo("FastestLap"));
            Assert.That(message.Interruptible, Is.False);
            Assert.That(message.Language, Is.EqualTo("en-US"));
        });
    }
}
