using System;
using System.Collections.Generic;
using TrackGenius.Protocol;
using TrackGenius.Protocol.Interfaces;

namespace TrackGenius.Communication
{
    public class CommunicateService : IDisposable
    {
        private SerialPortWrapper _portWrapper;

        private readonly IMessageParser _messageParser;

        private readonly Queue<IUplinkMessage> _upwardMessages = new();

        public bool IsOpened => _portWrapper.IsOpened;

        public MessageReceivedEventHandler MessageReceived;

        public EventHandler PortOpenStateEventHandler;
        private readonly SerialPortSetting _serialPortSettings;

        public CommunicateService(IProtocol protocol)
        {
            _messageParser = protocol.MessageParser;
            _serialPortSettings = SerialPortSettingConvert.ToSerialPortSetting(protocol.SerialPortSettings);
        }

        public void StartService(string portName)
        {
            _portWrapper = SerialPortWrapper.CreatePort(portName,
                _serialPortSettings.Baud,
                _serialPortSettings.DataBits,
                _serialPortSettings.Parity,
                _serialPortSettings.StopBits);
            if (!_portWrapper.IsOpened)
            {
                _portWrapper.OpenPort();
                RaisePortOpenStateChanged();
            }

            _portWrapper.DataReceived += OnDataReceived;
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
        }

        public void Dispose()
        {
            CloseService();
            _portWrapper.Dispose();
        }
    }
}