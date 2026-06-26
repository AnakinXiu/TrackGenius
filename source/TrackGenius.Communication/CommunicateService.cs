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

    private IMessageParser _messageParser;
    private SerialPortSetting _serialPortSettings;

    private readonly ConcurrentQueue<IUplinkMessage> _upwardMessages = new();

    public bool IsOpened => _portWrapper.IsOpened;

    public MessageReceivedEventHandler MessageReceived;

    public event EventHandler PortOpenStateEventHandler;

    public void StartService(string portName, IProtocol protocol)
    {
        var currentProtocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _messageParser = currentProtocol.MessageParser ?? throw new ArgumentNullException(nameof(protocol.MessageParser));
        var serialPortSettings = currentProtocol.SerialPortSettings 
                                 ?? throw new ArgumentNullException(nameof(protocol.SerialPortSettings));
        _serialPortSettings = SerialPortSettingConvert.ToSerialPortSetting(serialPortSettings);

        if (_portWrapper.IsOpened)
            _portWrapper.ClosePort();

        _portWrapper.OpenPort(portName,
            _serialPortSettings.Baud,
            _serialPortSettings.DataBits,
            _serialPortSettings.Parity,
            _serialPortSettings.StopBits);

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
        _portWrapper.ClosePort();
        RaisePortOpenStateChanged();
    }   

    public void SendCommand(IDownlinkMessage message)
    {
        _portWrapper?.SendBytes(message.Serialize());
    }

    public bool TryGetNextMessage(out IUplinkMessage uplinkMessage)
    {
        return _upwardMessages.TryDequeue(out uplinkMessage);
    }

    private void OnDataReceived(object sender, DataReceivedArgs args)
    {
        var message = _messageParser.ParseMessage(args.Buffer);
        _upwardMessages.Enqueue(message);
        MessageReceived?.Invoke(this, message);
    }

    public void Dispose()
    {
        if (_dataReceivedSubscribed)
        {
            _portWrapper.DataReceived -= OnDataReceived;
            _dataReceivedSubscribed = false;
        }

        CloseService();

        if (_portWrapper is IDisposable disposablePort)
            disposablePort.Dispose();
    }
}