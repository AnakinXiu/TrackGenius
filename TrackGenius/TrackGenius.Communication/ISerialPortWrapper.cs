using System.IO.Ports;

namespace TrackGenius.Communication
{
    public interface ISerialPortWrapper
    {
        SerialPort Port { get; }
        string Name { get; }

        int PortNumber { get; }

        void CreatePort(string portName, int baudRate, Parity parity);

        void OpenPort(int portNumber);

        void ClosePort();
    }
}