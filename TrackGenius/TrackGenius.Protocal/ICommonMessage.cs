namespace TrackGenius.Protocol
{
    public interface ICommonMessage
    {
        byte[] GetBytes();

        string ToString();
    }
}