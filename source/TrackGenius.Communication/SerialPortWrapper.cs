using JetBrains.Annotations;
using RJCP.IO.Ports;
using System;
using Parity = RJCP.IO.Ports.Parity;
using StopBits = RJCP.IO.Ports.StopBits;

namespace TrackGenius.Communication
{
    public class SerialPortWrapper : ISerialPortWrapper, IDisposable
    {
        [NotNull]
        private SerialPortStream _serialPortStream;

        public event DataReceivedEventHandler DataReceived;

        public string Name { get; }

        public int PortNumber { get; }

        public bool IsOpened => _serialPortStream.IsOpen;

        private byte[] _buffer = new byte[1024];

        public static SerialPortWrapper CreatePort(string portName)
        {
            var portWrapper = new SerialPortWrapper();
            portWrapper._serialPortStream.PortName = portName;
            portWrapper._serialPortStream.GetPortSettings();

            return portWrapper;
        }

        public static SerialPortWrapper CreatePort(string portName, int baud, int data, Parity parity, StopBits stopBits)
        {
            var portWrapper = new SerialPortWrapper();
            portWrapper.OpenPort(portName, baud, data, parity, stopBits);

            return portWrapper;
        }

        private SerialPortWrapper()
        {
            _serialPortStream = new SerialPortStream();
        }

        public void OpenPort()
        {
            _serialPortStream.DataReceived += SerialPort_DataReceived;
            _serialPortStream.Open();
        }

        public void OpenPort(string portName, int baud, int data, Parity parity, StopBits stopBits)
        {
            _serialPortStream = new SerialPortStream(portName, baud, data, parity, stopBits);
            _serialPortStream.DataReceived += SerialPort_DataReceived;
            _serialPortStream.Open();
        }

        public void ClosePort()
        {
            if (_serialPortStream.IsOpen)
                _serialPortStream?.Close();
        }

        public void SendBytes([NotNull] byte[] sendData)
        {
            if (_serialPortStream.CanWrite)
                _serialPortStream.Write(sendData, 0, sendData.Length);
        }

        private byte[] ReadBytes()
        {
            if (_serialPortStream.CanRead)
            {
                // var dataLength = Math.Min(_serialPortStream.BytesToRead, _buffer.Length - 1);
                var dataLength = _serialPortStream.Read(_buffer);

                var result = new byte[dataLength];
                Array.Copy(_buffer, result, dataLength);

                return result;
            }

            return null;
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (e.EventType != SerialData.Chars) 
                return;

            var data = ReadBytes();
            if(data != null && data.Length > 0)
                DataReceived?.Invoke(sender, new DataReceivedArgs(data));
        }

        public void Dispose()
        {
            ClosePort();

            if (!_serialPortStream.IsDisposed)
                _serialPortStream?.Dispose();
        }
    }
}