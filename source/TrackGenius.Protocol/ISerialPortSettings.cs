using TrackGenius.Protocol.SerialPort;

namespace TrackGenius.Protocol
{
    public interface ISerialPortSettings
    {
        int BaudRate { get; }

        StopBits StopBit { get; }

        Parity Parity { get; }

        int DataBits { get; }
    }
}