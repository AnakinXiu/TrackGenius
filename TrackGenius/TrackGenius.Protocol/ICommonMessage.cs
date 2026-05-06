namespace TrackGenius.Protocol
{
    public interface ICommonMessage
    {
        int PacketLength { get; }

        byte[] ByteData { get; }

        string ToString();
    }
}