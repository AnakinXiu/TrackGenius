namespace TrackGenius.Protocol
{
    public interface ICommonMessage
    {
        int PacketLength { get; }

        byte[] GetBytes();

        string ToString();
    }
}