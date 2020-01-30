using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.Protocol
{
    public interface IUplinkMessage : ICommonMessage
    {
        byte Checksum { get; }

        PacketType PacketType { get; }

        string Deserialize();
    }
}