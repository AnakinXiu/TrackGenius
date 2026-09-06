namespace TrackGenius.Protocol
{
    public interface IMessageParser
    {
        IUplinkMessage ParseMessage(byte[] dataBytes);
    }
}