using RJCP.IO.Ports;
using System;
using System.Collections.Generic;
using TrackGenius.Protocol;
using Parity = RJCP.IO.Ports.Parity;
using StopBits = RJCP.IO.Ports.StopBits;

namespace TrackGenius.Communication
{
    public class SerialPortWrapper : ISerialPortWrapper, ISerialPortsEnumlator, IDisposable
    {
        private SerialPortStream _serialPortStream;

        public string Name { get; }

        public int PortNumber { get; }

        public ISerialPortSettings Settings { get; }

        public SerialPortWrapper(ISerialPortSettings settings)
        {
            Settings = settings;
        }

        public void OpenPort(string portName, int baud, int data, Parity parity, StopBits stopBits)
        {
            _serialPortStream = new SerialPortStream(portName, baud, data, parity, stopBits);
        }

        public void ClosePort()
        {
            _serialPortStream?.Close();
        }

        public IEnumerable<string> GetValidPortNames()
        {
            return SerialPortStream.GetPortNames();
        }

        public void SendBytes(byte[] sendData)
        {
            
        }

        public void Dispose()
        {
            ClosePort();
            _serialPortStream.Dispose();
        }
    }
}