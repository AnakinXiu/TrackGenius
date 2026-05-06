using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.Protocol
{
    public class EmptyUplinkMessage : IUplinkMessage
    {
        public byte Checksum => 0;

        public PacketType PacketType => PacketType.Unknown;

        public int PacketLength => 0;

        public byte[] ByteData => new byte[0];

        public string Deserialize() => string.Empty;
    }
}
