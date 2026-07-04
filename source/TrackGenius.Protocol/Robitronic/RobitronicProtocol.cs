using TrackGenius.Protocol.Interfaces;

namespace TrackGenius.Protocol.Robitronic;

public class RobitronicProtocol : IProtocol
{
    public string ProtocolName { get; } = "Robitronic";
    public IMessageParser MessageParser { get; } = new RobitronicMessageParser();

    public ISerialPortSettings SerialPortSettings { get; } = new RobitronicCommConfig();
}