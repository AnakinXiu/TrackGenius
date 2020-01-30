using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.Protocol.Parser
{
    internal class DetectedMessageParser : IMessageParser
    {
        public IUplinkMessage ParseMessage(byte[] dataBytes)
        {
            return new DetectedMessage(dataBytes);
        }
    }
}
