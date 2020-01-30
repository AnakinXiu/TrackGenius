using System.Collections.Generic;
using System.ComponentModel;
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
            _serialPortWrapper.PropertyChanged += OnDataReceived;
        }

        public void CloseService()
        {
            _serialPortWrapper.PropertyChanged -= OnDataReceived;
            _serialPortWrapper.Dispose();
        }

        public void SendCommand(IDownlinkMessage message)
        {
            _serialPortWrapper.SendBytes(message.Serialize());
        }

        private void OnDataReceived(object sender, PropertyChangedEventArgs args)
        {
            var message = MessageParser.ParserMessage(_serialPortWrapper.ReadBytes());
            _upwardMessages.Enqueue(message);
        }
    }
}
