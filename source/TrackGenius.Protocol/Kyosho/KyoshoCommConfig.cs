using TrackGenius.Protocol.SerialPort;

namespace TrackGenius.Protocol.Kyosho;

public class KyoshoCommConfig : ISerialPortSettings
{
    public int BaudRate { get; }
    public StopBits StopBit { get; }
    public Parity Parity { get; }
    public int DataBits { get; }
}