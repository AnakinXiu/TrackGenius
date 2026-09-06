using System;
using System.Collections.Concurrent;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrackGenius.Protocol;
using TrackGenius.Protocol.Interfaces;

namespace TrackGenius.Communication;

public class CommunicateService : IDisposable
{
    [NotNull]
    private readonly ISerialPortWrapper _portWrapper;

    private readonly ILogger<CommunicateService> _logger;

    private bool _dataReceivedSubscribed;
    private bool _disposed;

    private IMessageParser _messageParser;
    private SerialPortSetting _serialPortSettings;

    private readonly ConcurrentQueue<IUplinkMessage> _upwardMessages = new();

    public bool IsOpened => _portWrapper.IsOpened;

    public MessageReceivedEventHandler MessageReceived;

    public event EventHandler PortOpenStateEventHandler;

    public CommunicateService() : this(new SerialPortWrapper(), NullLogger<CommunicateService>.Instance)
    {
    }

    public CommunicateService(ISerialPortWrapper portWrapper, ILogger<CommunicateService> logger)
    {
        _portWrapper = portWrapper ?? throw new ArgumentNullException(nameof(portWrapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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

        _logger.LogInformation("PortOpenRequested PortName={PortName} ProtocolName={ProtocolName}", portName, currentProtocol.ProtocolName);

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
            _logger.LogError(ex, "PortOpenFailed PortName={PortName} ProtocolName={ProtocolName}", portName, currentProtocol.ProtocolName);
            throw new InvalidOperationException($"Failed to start communication service on port '{portName}'.", ex);
        }

        _logger.LogInformation("PortOpened PortName={PortName} ProtocolName={ProtocolName}", portName, currentProtocol.ProtocolName);

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

        _logger.LogInformation("PortCloseRequested");

        try
        {
            _portWrapper.ClosePort();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "PortCloseFailed");
            throw new InvalidOperationException("Failed to close communication service.", ex);
        }

        _logger.LogInformation("PortClosed");

        RaisePortOpenStateChanged();
    }   

    public void SendCommand(IDownlinkMessage message)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogDebug("CommandSendRequested MessageType={MessageType}", message.GetType().Name);

        try
        {
            _portWrapper.SendBytes(message.Serialize());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "CommandSendFailed MessageType={MessageType}", message.GetType().Name);
            throw new InvalidOperationException("Failed to send command through communication service.", ex);
        }

        _logger.LogDebug("CommandSent MessageType={MessageType}", message.GetType().Name);
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
            _logger.LogWarning(ex, "MessageParseFailed due to invalid payload.");
            throw new InvalidOperationException("Failed to parse received serial message.", ex);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "MessageParseFailed due to invalid parser state.");
            throw new InvalidOperationException("Failed to parse received serial message.", ex);
        }

        _upwardMessages.Enqueue(message);
        _logger.LogDebug("MessageParsed MessageType={MessageType}", message.GetType().Name);

        try
        {
            MessageReceived?.Invoke(this, message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "MessageReceivedHandlerFailed MessageType={MessageType}", message.GetType().Name);
            throw new InvalidOperationException("MessageReceived handler failed while processing a received message.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logger.LogInformation("Communication service disposing.");

        if (_dataReceivedSubscribed)
        {
            _portWrapper.DataReceived -= OnDataReceived;
            _dataReceivedSubscribed = false;
        }

        if (_portWrapper.IsOpened)
            _portWrapper.ClosePort();

        if (_portWrapper is IDisposable disposablePort)
            disposablePort.Dispose();

        _logger.LogInformation("Communication service disposed.");
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CommunicateService));
    }
}