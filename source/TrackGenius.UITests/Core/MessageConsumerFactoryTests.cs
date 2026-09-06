using System;
using NUnit.Framework;
using TrackGenius.Core;
using TrackGenius.Protocol.Kyosho;
using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.UITests.Core;

[TestFixture]
public class MessageConsumerFactoryTests
{
    private readonly MessageConsumerFactory _factory = new();

    [Test]
    public void GivenRobitronicProtocol_WhenCreated_ThenRobitronicConsumerReturned()
        => Assert.That(_factory.Create(new RobitronicProtocol()), Is.InstanceOf<RobitronicMessageConsumer>());

    [Test]
    public void GivenUnsupportedProtocol_WhenCreated_ThenNotSupportedExceptionThrown()
    {
        var kyosho = new KyoshoProtocol();
        Assert.Throws<NotSupportedException>(() => _factory.Create(kyosho),
            $"No message consumer is registered for protocol '{kyosho.ProtocolName}'.");
    }

    [Test]
    public void GivenNullProtocol_WhenCreated_ThenArgumentNullExceptionThrown()
        => Assert.Throws<ArgumentNullException>(() => _factory.Create(null));
}
