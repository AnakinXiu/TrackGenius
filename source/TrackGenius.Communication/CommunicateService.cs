using System;
using System.Collections.Generic;
using TrackGenius.Protocol;
using TrackGenius.Protocol.Interfaces;

namespace TrackGenius.Communication
{
    public class CommunicateService : IDisposable
    {
        private ISerialPortWrapper _portWrapper;

        private bool _dataReceivedSubscribed;

        private readonly IMessageParser _messageParser;

        private readonly Queue<IUplinkMessage> _upwardMessages = new();

        public bool IsOpened => _portWrapper?.IsOpened ?? false;

        public MessageReceivedEventHandler MessageReceived;

        public EventHandler PortOpenStateEventHandler;
        private readonly SerialPortSetting _serialPortSettings;

        public CommunicateService(IProtocol protocol)
            : this(protocol?.MessageParser, protocol?.SerialPortSettings)
        {
        }

        public CommunicateService(IMessageParser messageParser, ISerialPortSettings serialPortSettings, ISerialPortWrapper portWrapper = null)
        {
            _messageParser = messageParser ?? throw new ArgumentNullException(nameof(messageParser));
            _serialPortSettings = SerialPortSettingConvert.ToSerialPortSetting(serialPortSettings ?? throw new ArgumentNullException(nameof(serialPortSettings)));
            _portWrapper = portWrapper;
        }

        public void StartService(string portName)
        {
            if (_portWrapper == null)
            {
                _portWrapper = SerialPortWrapper.CreatePort(portName,
                    _serialPortSettings.Baud,
                    _serialPortSettings.DataBits,
                    _serialPortSettings.Parity,
                    _serialPortSettings.StopBits);
            }

            if (!_portWrapper.IsOpened)
            {
                _portWrapper.OpenPort(portName,
                    _serialPortSettings.Baud,
                    _serialPortSettings.DataBits,
                    _serialPortSettings.Parity,
                    _serialPortSettings.StopBits);
                RaisePortOpenStateChanged();
            }

            if (!_dataReceivedSubscribed)
            {
                _portWrapper.DataReceived += OnDataReceived;
                _dataReceivedSubscribed = true;
            }
        }

        public void RaisePortOpenStateChanged()
        {
            PortOpenStateEventHandler?.Invoke(this, EventArgs.Empty);
        }

        public void CloseService()
        {
            if (_portWrapper == null)
                return;

            _portWrapper.ClosePort();
            RaisePortOpenStateChanged();
        }   

        public void SendCommand(IDownlinkMessage message)
        {
            if (_portWrapper == null)
                return;

            _portWrapper.SendBytes(message.Serialize());
        }

        public bool TryGetNextMessage(out IUplinkMessage uplinkMessage)
        {
            if (_upwardMessages.Count > 0)
            {
                uplinkMessage = _upwardMessages.Dequeue();
                return true;
            }

            uplinkMessage = null;
            return false;
        }

        private void OnDataReceived(object sender, DataReceivedArgs args)
        {
            var message = _messageParser.ParseMessage(args.Buffer);
            _upwardMessages.Enqueue(message);
            MessageReceived?.Invoke(this, message);
        }

        public void Dispose()
        {
            if (_portWrapper == null)
                return;

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
}