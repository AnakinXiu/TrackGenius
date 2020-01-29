using RJCP.IO.Ports;

namespace TrackGenius.Communication
{
    public interface ISerialPortWrapper
    {
        string Name { get; }

        int PortNumber { get; }

        void OpenPort(string portName, int baud, int data, Parity parity, StopBits stopBits);

        void ClosePort();

        void SendBytes(byte[] sendData);
    }
}