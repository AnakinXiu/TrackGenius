using System;

namespace TrackGenius.Protocol.Robitronic
{
    public class TimeStampMessage : IUplinkMessage
    {
        public byte Checksum => throw new NotImplementedException();

        public PacketType PacketType => throw new NotImplementedException();

        public int PacketLength => throw new NotImplementedException();

        public string Deserialize()
        {
            throw new NotImplementedException();
        }

        public byte[] GetBytes()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}
