namespace TrackGenius.Protocol
{
    public interface IUplinkMessage : ICommonMessage
    {
        string Deserialize();
    }
}