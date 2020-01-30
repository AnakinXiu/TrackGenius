using JetBrains.Annotations;
using RJCP.IO.Ports;
using System;
using System.Collections.Generic;
using Parity = RJCP.IO.Ports.Parity;
using StopBits = RJCP.IO.Ports.StopBits;

namespace TrackGenius.Communication
{
    public class SerialPortWrapper : ISerialPortWrapper, ISerialPortsEnumlator, IDisposable
    {
        private ISerialPortStream _serialPortStream;

        public event DataReceivedEventHandler DataReceived;

        public string Name { get; }

        public int PortNumber { get; }

        public bool isOpened => _serialPortStream.IsOpen;

        private byte[] _buffer = new byte[1024];

        public void OpenPort(string portName, int baud, int data, Parity parity, StopBits stopBits)
        {
            _serialPortStream = new SerialPortStream(portName, baud, data, parity, stopBits);
            _serialPortStream.DataReceived += SerialPort_DataReceived;
            _serialPortStream.Open();
        }

        public void ClosePort()
        {
            _serialPortStream?.Close();
        }

        public IEnumerable<string> GetValidPortNames()
        {
            return SerialPortStream.GetPortNames();
        }

        public void SendBytes([NotNull]byte[] sendData)
        {
            if(_serialPortStream.CanWrite)
                _serialPortStream.Write(sendData, 0, sendData.Length);
        }

        private byte[] ReadBytes()
        {
            if (_serialPortStream.CanRead)
            {
                var dataLength = Math.Min(_serialPortStream.BytesToRead, _buffer.Length - 1);
                _serialPortStream.Read(_buffer, 0, dataLength);

                var result = new byte[dataLength];
                _buffer.CopyTo(result, dataLength);

                return result;
            }

            return null;
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if(e.EventType == SerialData.Chars)
            {
                var data = ReadBytes();
                if(data != null && data.Length > 0)
                    DataReceived(sender, new DataReceivedArgs(data));
            }
        }

        public void Dispose()
        {
            ClosePort();
            _serialPortStream?.Dispose();
        }
    }
}