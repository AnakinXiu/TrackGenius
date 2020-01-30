using RJCP.IO.Ports;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Parity = RJCP.IO.Ports.Parity;
using StopBits = RJCP.IO.Ports.StopBits;

namespace TrackGenius.Communication
{
    public class SerialPortWrapper : ISerialPortWrapper, ISerialPortsEnumlator, IDisposable, INotifyPropertyChanged
    {
        private SerialPortStream _serialPortStream;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get; }

        public int PortNumber { get; }

        private byte[] _buffer = new byte[1024];

        public void OpenPort(string portName, int baud, int data, Parity parity, StopBits stopBits)
        {
            _serialPortStream = new SerialPortStream(portName, baud, data, parity, stopBits);
            _serialPortStream.DataReceived += _serialPortStream_DataReceived;
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

        public void SendBytes(byte[] sendData)
        {
            if(_serialPortStream.CanWrite)
                _serialPortStream.Write(sendData, 0, sendData.Length);
        }

        public byte[] ReadBytes()
        {
            if (_serialPortStream.CanRead)
                _serialPortStream.Read(_buffer, 0, Math.Min(_serialPortStream.BytesToRead, _buffer.Length - 1));

            return _buffer;
        }

        private void _serialPortStream_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if(e.EventType == SerialData.Chars)
            {
                PropertyChanged(sender, new PropertyChangedEventArgs(nameof(SerialPortWrapper)));
            }
        }

        public void Dispose()
        {
            ClosePort();
            _serialPortStream?.Dispose();
        }
    }
}