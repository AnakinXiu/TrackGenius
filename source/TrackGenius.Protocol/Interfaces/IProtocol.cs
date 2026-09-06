using JetBrains.Annotations;

namespace TrackGenius.Protocol.Interfaces;

public interface IProtocol
{
    [NotNull]
    public string ProtocolName { get; }

    IMessageParser MessageParser { get; }

    ISerialPortSettings SerialPortSettings { get; }
}