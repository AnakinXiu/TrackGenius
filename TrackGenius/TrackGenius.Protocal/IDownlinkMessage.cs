namespace TrackGenius.Protocol
{
    public interface IDownlinkMessage : ICommonMessage
    {
        byte[] Serialize();
    }
}