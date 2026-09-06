using System;
using JetBrains.Annotations;
using TrackGenius.Protocol.Interfaces;

namespace TrackGenius.Core;

/// <summary>
/// Maps the connection's current protocol to the message consumer that understands
/// its messages. Adding a protocol later = one case here + its consumer class.
/// </summary>
public class MessageConsumerFactory
{
    public IMessageConsumer Create([NotNull] IProtocol protocol)
    {
        if (protocol == null)
            throw new ArgumentNullException(nameof(protocol));

        return protocol.ProtocolName switch
        {
            "Robitronic" => new RobitronicMessageConsumer(),
            _ => throw new NotSupportedException(
                $"No message consumer is registered for protocol '{protocol.ProtocolName}'."),
        };
    }
}
