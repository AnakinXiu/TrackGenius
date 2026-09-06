using JetBrains.Annotations;
using RJCP.IO.Ports;

namespace TrackGenius.Communication
{
    public interface ISerialPortWrapper
    {
        string Name { get; }

        bool IsOpened { get; }

        event DataReceivedEventHandler DataReceived;

        void OpenPort([NotNull] string portName, int baud, int data, Parity parity, StopBits stopBits);

        void ClosePort();

        void SendBytes([NotNull] byte[] sendData);
    }
}