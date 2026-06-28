using System;
using System.Collections.Concurrent;
using JetBrains.Annotations;
using TrackGenius.Protocol;
using TrackGenius.Protocol.Interfaces;

namespace TrackGenius.Communication;

public class CommunicateService : IDisposable
{
    [NotNull]
    private readonly ISerialPortWrapper _portWrapper = new SerialPortWrapper();

    private bool _dataReceivedSubscribed;
    private bool _disposed;

    private IMessageParser _messageParser;
    private SerialPortSetting _serialPortSettings;

    private readonly ConcurrentQueue<IUplinkMessage> _upwardMessages = new();

    public bool IsOpened => _portWrapper.IsOpened;

    public MessageReceivedEventHandler MessageReceived;

    public event EventHandler PortOpenStateEventHandler;

    public void StartService([NotNull] string portName, IProtocol protocol)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name cannot be null or empty.", nameof(portName));

        var currentProtocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _messageParser = currentProtocol.MessageParser ?? throw new ArgumentNullException(nameof(protocol.MessageParser));
        var serialPortSettings = currentProtocol.SerialPortSettings 
                                 ?? throw new ArgumentNullException(nameof(protocol.SerialPortSettings));
        _serialPortSettings = SerialPortSettingConvert.ToSerialPortSetting(serialPortSettings);

        try
        {
            if (_portWrapper.IsOpened)
                _portWrapper.ClosePort();

            _portWrapper.OpenPort(portName,
                _serialPortSettings.Baud,
                _serialPortSettings.DataBits,
                _serialPortSettings.Parity,
                _serialPortSettings.StopBits);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Failed to start communication service on port '{portName}'.", ex);
        }

        RaisePortOpenStateChanged();

        if (_dataReceivedSubscribed) 
            return;

        _portWrapper.DataReceived += OnDataReceived;
        _dataReceivedSubscribed = true;
    }

    public void RaisePortOpenStateChanged()
    {
        PortOpenStateEventHandler?.Invoke(this, EventArgs.Empty);
    }

    public void CloseService()
    {
        ThrowIfDisposed();

        try
        {
            _portWrapper.ClosePort();
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("Failed to close communication service.", ex);
        }

        RaisePortOpenStateChanged();
    }   

    public void SendCommand(IDownlinkMessage message)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            _portWrapper.SendBytes(message.Serialize());
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("Failed to send command through communication service.", ex);
        }
    }

    public bool TryGetNextMessage(out IUplinkMessage uplinkMessage)
    {
        ThrowIfDisposed();
        return _upwardMessages.TryDequeue(out uplinkMessage);
    }

    private void OnDataReceived(object sender, DataReceivedArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (_messageParser is null)
            throw new InvalidOperationException("Message parser is not initialized. StartService must be called before receiving data.");

        IUplinkMessage message;
        try
        {
            message = _messageParser.ParseMessage(args.Buffer);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Failed to parse received serial message.", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("Failed to parse received serial message.", ex);
        }

        _upwardMessages.Enqueue(message);

        try
        {
            MessageReceived?.Invoke(this, message);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("MessageReceived handler failed while processing a received message.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_dataReceivedSubscribed)
        {
            _portWrapper.DataReceived -= OnDataReceived;
            _dataReceivedSubscribed = false;
        }

        if (_portWrapper.IsOpened)
            _portWrapper.ClosePort();

        if (_portWrapper is IDisposable disposablePort)
            disposablePort.Dispose();

        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CommunicateService));
    }
}