using System.Collections.Generic;
using TrackGenius.Protocol;

namespace TrackGenius.Communication
{
    public class CommunicateService
    {
        private readonly SerialPortWrapper _serialPortWrapper;

        private readonly IMessageParser _messageParser;

        private readonly Queue<IUplinkMessage> _upwardMessages = new Queue<IUplinkMessage>();

        public CommunicateService(IMessageParser messageParser)
        {
            _serialPortWrapper = new SerialPortWrapper();
            _messageParser = messageParser;
        }

        public void StartService(string portName, ISerialPortSettings settings)
        {
            _serialPortWrapper.OpenPort(portName, settings.BaudRate, settings.Length, settings.Parity.ToRJCPModel(), settings.StopBit.ToRJCPModel());
            _serialPortWrapper.DataReceived += OnDataReceived;
        }

        public void CloseService()
        {
            _serialPortWrapper.Dispose();
        }

        public void SendCommand(IDownlinkMessage message)
        {
            _serialPortWrapper.SendBytes(message.Serialize());
        }

        private void OnDataReceived(object sender, DataReceivedArgs args)
        {
            var message = _messageParser.ParseMessage(args.Buffer);
            _upwardMessages.Enqueue(message);
        }
    }
}
