namespace TrackGenius.Protocol.Robitronic
{
    public enum PacketType
    {
        CarDetect = 0x84,
        TimeStamp = 0x83,
        InitializeResponse = 0x00,
        Unknown = 0xFF,
    }
}
