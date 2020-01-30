using System.Collections.Generic;
using TrackGenius.Protocol;

namespace TrackGenius.Communication
{
    public class CommunicateService
    {
        private SerialPortWrapper _serialPortWrapper;

        private Queue<IUplinkMessage> _upwardMessages = new Queue<IUplinkMessage>();

        public CommunicateService()
        {
            _serialPortWrapper = new SerialPortWrapper();
        }

        public void StartService(string portName, SerialPortSettings settings)
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
            var message = MessageParser.ParserMessage(args.Buffer);
            _upwardMessages.Enqueue(message);
        }
    }
}
