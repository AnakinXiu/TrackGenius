using TrackGenius.Protocol.Interfaces;
using TrackGenius.Protocol.SerialPort;

namespace TrackGenius.Protocol.Robitronic;

public class RobitronicProtocol : IProtocol
{
    public string ProtocolName { get; } = "Robitronic";
    public IMessageParser MessageParser { get; } = new RobitronicMessageParser();

    public ISerialPortSettings SerialPortSettings { get; } = new RobitronicCommConfig();
}

public class KyoshoProtocol : IProtocol
{
    public string ProtocolName { get; } = "Kyosho";
    public IMessageParser MessageParser { get; } = new KyoshoMessageParser();
    public ISerialPortSettings SerialPortSettings { get; } = new KyoshoCommConfig();
}

public class KyoshoCommConfig : ISerialPortSettings
{
    public int BaudRate { get; }
    public StopBits StopBit { get; }
    public Parity Parity { get; }
    public int DataBits { get; }
}