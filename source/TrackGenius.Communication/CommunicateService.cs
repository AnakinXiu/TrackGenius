using System;
using System.Collections.Generic;
using TrackGenius.Protocol;

namespace TrackGenius.Communication
{
    public class CommunicateService : IDisposable
    {
        private SerialPortWrapper _portWrapper;

        private readonly IMessageParser _messageParser;

        private readonly Queue<IUplinkMessage> _upwardMessages = new Queue<IUplinkMessage>();

        public bool IsOpened => _portWrapper.IsOpened;

        public MessageReceivedEventHandler MessageReceived;

        public EventHandler PortOpenStateEventHandler;

        public CommunicateService(IMessageParser messageParser)
        {
            _messageParser = messageParser;
        }

        public void StartService(string portName)
        {
            _portWrapper = SerialPortWrapper.CreatePort(portName);
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