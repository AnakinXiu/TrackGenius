using System.IO.Ports;

namespace TrackGenius.Protocol
{
    public class SerialPortSettings : ISerialPortSettings
    {
        public int BaudRate { get; private set; }

        public StopBits StopBit { get; private set; }

        public Parity Parity { get; private set; }

        public int Length { get; private set; }

        public SerialPortSettings(int baudRate, StopBits stopBits, Parity parity, int length)
        {
            BaudRate = baudRate;
            StopBit = stopBits;
            Parity = Parity;
            Length = length;
        }
    }
}
