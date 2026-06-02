namespace TrackGenius.Protocol.Interfaces;

public interface IProtocol
{
    IMessageParser MessageParser { get; }

    ISerialPortSettings SerialPortSettings { get; }
}