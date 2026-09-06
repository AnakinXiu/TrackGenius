using TrackGenius.Protocol.Interfaces;
using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.Protocol.Kyosho;

public class KyoshoProtocol : IProtocol
{
    public string ProtocolName { get; } = "Kyosho";
    public IMessageParser MessageParser { get; } = new KyoshoMessageParser();
    public ISerialPortSettings SerialPortSettings { get; } = new KyoshoCommConfig();
}