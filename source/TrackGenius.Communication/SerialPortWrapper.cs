using JetBrains.Annotations;
using RJCP.IO.Ports;
using System;
using System.IO;
using Parity = RJCP.IO.Ports.Parity;
using StopBits = RJCP.IO.Ports.StopBits;

namespace TrackGenius.Communication;

public class SerialPortWrapper : ISerialPortWrapper, IDisposable
{
    [CanBeNull]
    private SerialPortStream _serialPortStream;

    private bool _disposed;

    public event DataReceivedEventHandler DataReceived;

    public string Name => _serialPortStream?.PortName ?? string.Empty;

    public bool IsOpened => _serialPortStream is { IsOpen: true };

    private readonly byte[] _buffer = new byte[1024];

    public void OpenPort(string portName, int baud, int data, Parity parity, StopBits stopBits)
    {
        ThrowIfDisposed();

        if (portName is null)
            throw new ArgumentException("Port name cannot be null or empty.", nameof(portName));

        if (baud <= 0)
            throw new ArgumentOutOfRangeException(nameof(baud), "Baud rate must be greater than zero.");

        if (data <= 0)
            throw new ArgumentOutOfRangeException(nameof(data), "Data bits must be greater than zero.");

        DisposeCurrentStream();

        var stream = new SerialPortStream(portName, baud, data, parity, stopBits);

        try
        {
            stream.DataReceived += SerialPort_DataReceived;
            stream.Open();
            _serialPortStream = stream;
        }
        catch (UnauthorizedAccessException ex)
        {
            stream.DataReceived -= SerialPort_DataReceived;
            stream.Dispose();
            throw CreateSerialOperationException(nameof(OpenPort), portName, ex);
        }
        catch (IOException ex)
        {
            stream.DataReceived -= SerialPort_DataReceived;
            stream.Dispose();
            throw CreateSerialOperationException(nameof(OpenPort), portName, ex);
        }
        catch (ObjectDisposedException ex)
        {
            stream.DataReceived -= SerialPort_DataReceived;
            stream.Dispose();
            throw CreateSerialOperationException(nameof(OpenPort), portName, ex);
        }
        catch (InvalidOperationException ex)
        {
            stream.DataReceived -= SerialPort_DataReceived;
            stream.Dispose();
            throw CreateSerialOperationException(nameof(OpenPort), portName, ex);
        }
    }

    public void ClosePort()
    {
        ThrowIfDisposed();

        if (_serialPortStream is not { IsOpen: true } stream)
            return;

        try
        {
            stream.Close();
        }
        catch (IOException ex)
        {
            throw CreateSerialOperationException(nameof(ClosePort), stream.PortName, ex);
        }
        catch (ObjectDisposedException ex)
        {
            throw CreateSerialOperationException(nameof(ClosePort), stream.PortName, ex);
        }
        catch (InvalidOperationException ex)
        {
            throw CreateSerialOperationException(nameof(ClosePort), stream.PortName, ex);
        }
    }

    public void SendBytes(byte[] sendData)
    {
        ThrowIfDisposed();

        if (_serialPortStream is not { IsOpen: true, CanWrite: true } stream)
            throw new InvalidOperationException("Serial port is not open for writing.");

        try
        {
            stream.Write(sendData, 0, sendData.Length);
        }
        catch (IOException ex)
        {
            throw CreateSerialOperationException(nameof(SendBytes), stream.PortName, ex);
        }
        catch (ObjectDisposedException ex)
        {
            throw CreateSerialOperationException(nameof(SendBytes), stream.PortName, ex);
        }
        catch (InvalidOperationException ex)
        {
            throw CreateSerialOperationException(nameof(SendBytes), stream.PortName, ex);
        }
    }

    private byte[] ReadBytes()
    {
        if (_serialPortStream is not { IsOpen: true, CanRead: true } stream)
            return [];

        try
        {
            var dataLength = stream.Read(_buffer);

            var result = new byte[dataLength];
            Array.Copy(_buffer, result, dataLength);

            return result;
        }
        catch (IOException)
        {
            return [];
        }
        catch (TimeoutException)
        {
            return [];
        }
        catch (ObjectDisposedException)
        {
            return [];
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (e.EventType != SerialData.Chars) 
            return;

        var data = ReadBytes();
        if (data is { Length: > 0 })
            DataReceived?.Invoke(sender, new DataReceivedArgs(data));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        DisposeCurrentStream();

        GC.SuppressFinalize(this);
    }

    private static InvalidOperationException CreateSerialOperationException(string operation, string portName, Exception innerException)
    {
        return new InvalidOperationException($"Serial operation '{operation}' failed for port '{portName}'.", innerException);
    }

    private void DisposeCurrentStream()
    {
        if (_serialPortStream == null)
            return;

        var stream = _serialPortStream;
        _serialPortStream = null;

        stream.DataReceived -= SerialPort_DataReceived;

        try
        {
            if (stream.IsOpen)
                stream.Close();
        }
        catch (IOException ex)
        {
            throw CreateSerialOperationException(nameof(ClosePort), stream.PortName, ex);
        }
        catch (ObjectDisposedException ex)
        {
            throw CreateSerialOperationException(nameof(ClosePort), stream.PortName, ex);
        }
        catch (InvalidOperationException ex)
        {
            throw CreateSerialOperationException(nameof(ClosePort), stream.PortName, ex);
        }

        if (!stream.IsDisposed)
            stream.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SerialPortWrapper));
    }
}
