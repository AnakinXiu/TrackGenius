namespace TrackGenius.Protocol.Robitronic
{
    public class InitializeResponse : IUplinkMessage
    {
        public byte[] ByteData { get; }

        public int PacketLength => 1;

        public byte Checksum => 0;

        public PacketType PacketType => PacketType.InitializeResponse;

        public InitializeResponse(byte[] data)
        {
            ByteData = data;
        }

        public string Deserialize() => "00";

    }
}