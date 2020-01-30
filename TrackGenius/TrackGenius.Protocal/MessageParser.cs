using TrackGenius.Protocol.Parser;

namespace TrackGenius.Protocol
{
    public static class MessageParser
    {
        public static IUplinkMessage ParserMessage(byte[] dataBytes)
        {
            var detectedMessage = new DetectedMessageParser().ParseMessage(dataBytes);

            return detectedMessage;
        }
    }
}
