using System;
using TrackGenius.Const;

namespace TrackGenius.Protocol.Robitronic
{
    public class TimeStampMessage : IUplinkMessage
    {
        public byte[] ByteData { get; }

        public int PacketLength => ByteData[0];
        
        public byte Checksum => ByteData[1];

        public PacketType PacketType => (PacketType)ByteData[2];

        public int Milliseconds => ByteData.Cut(3, 4).Reverse().ToInt32();

        public string Deserialize() => BitConverter.ToString(ByteData).Replace('-', SplitChar.Separator);

        public override string ToString() => Deserialize();

        public TimeStampMessage(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException();
            
            if (data.Length < 3)
                throw new ArgumentOutOfRangeException();

            if (data[2] != 0x83)
                throw new FormatException();

            ByteData = data;
        }
    }
}
