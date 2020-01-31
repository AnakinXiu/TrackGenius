using System.IO.Ports;

namespace TrackGenius.Protocol
{
    public class SerialPortSettings : ISerialPortSettings
    {
        public int BaudRate { get; }

        public StopBits StopBit { get; }

        public Parity Parity { get; }

        public int Length { get; }

        public SerialPortSettings(int baudRate, StopBits stopBits, Parity parity, int length)
        {
            BaudRate = baudRate;
            StopBit = stopBits;
            Parity = Parity;
            Length = length;
        }
    }
}
