using System.IO.Ports;

namespace TrackGenius.Protocol
{
    public interface ISerialPortSettings
    {
        int BaudRate { get; }

        StopBits StopBit { get; }

        Parity Parity { get; }

        int Length { get; }
    }
}