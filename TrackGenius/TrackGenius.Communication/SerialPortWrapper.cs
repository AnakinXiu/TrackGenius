using System;
using System.IO.Ports;
using TrackGenius.Protocol;

namespace TrackGenius.Communication
{
    public class SerialPortWrapper : ISerialPortWrapper
    {
        public SerialPort Port { get; private set; }
        public string Name { get; }
        public int PortNumber { get; }

        public SerialPortWrapper(ISerialPortSettings settings)
        {
            
        }

        public void CreatePort(string portName, int baudRate, Parity parity)
        {
            Port = new SerialPort(portName, baudRate, parity);
        }

        public void OpenPort(int portNumber)
        {
            Port.Open();
            
        }

        public void ClosePort()
        {
            Port.Close();
        }
    }
}