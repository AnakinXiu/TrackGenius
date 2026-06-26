using JetBrains.Annotations;
using RJCP.IO.Ports;
using System;
using Parity = RJCP.IO.Ports.Parity;
using StopBits = RJCP.IO.Ports.StopBits;

namespace TrackGenius.Communication;

public class SerialPortWrapper : ISerialPortWrapper, IDisposable
{
    [CanBeNull]
    private SerialPortStream _serialPortStream;

    public event DataReceivedEventHandler DataReceived;

    public string Name => _serialPortStream?.PortName ?? string.Empty;

    public bool IsOpened => _serialPortStream is { IsOpen: true };

    private readonly byte[] _buffer = new byte[1024];

    public void OpenPort(string portName, int baud, int data, Parity parity, StopBits stopBits)
    {
        if (_serialPortStream != null)
        {
            _serialPortStream.DataReceived -= SerialPort_DataReceived;
            _serialPortStream.Dispose();
        }

        _serialPortStream = new SerialPortStream(portName, baud, data, parity, stopBits);
        _serialPortStream.DataReceived += SerialPort_DataReceived;
        _serialPortStream.Open();
    }

    public void ClosePort()
    {
        if (_serialPortStream is { IsOpen: true })
            _serialPortStream.Close();
    }

    public void SendBytes([NotNull] byte[] sendData)
    {
        if (_serialPortStream is { CanWrite: true })
            _serialPortStream.Write(sendData, 0, sendData.Length);
    }

    private byte[] ReadBytes()
    {
        if (_serialPortStream is not { CanRead: true }) 
            return [];

        var dataLength = _serialPortStream.Read(_buffer);

        var result = new byte[dataLength];
        Array.Copy(_buffer, result, dataLength);

        return result;
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (e.EventType != SerialData.Chars) 
            return;

        var data = ReadBytes();
        if(data is { Length: > 0 })
            DataReceived?.Invoke(sender, new DataReceivedArgs(data));
    }

    public void Dispose()
    {
        ClosePort();

        if (_serialPortStream is { IsDisposed: false })
            _serialPortStream.Dispose();
    }
}