using System;
using TrackGenius.Const;

namespace TrackGenius.Protocol.Robitronic
{
    public class DetectedMessage : IUplinkMessage
    {
        private byte[] ByteData { get; }

        public byte Checksum => ByteData[1];

        public PacketType PacketType => (PacketType)ByteData[2];

        public int PacketLength => ByteData[0];

        public string CarID => ByteData.Cut(3, 4).Reverse().ToInt32().ToString();

        public int Milliseconds => ByteData.Cut(7, 4).Reverse().ToInt32();

        public int Hits => ByteData[11];
        
        public int SignalLevel => ByteData[12];

        public DetectedMessage(byte[] data)
        {
            ByteData = data;
        }

        public byte[] GetBytes() => ByteData;

        public string Deserialize()=> BitConverter.ToString(ByteData).Replace('-', SplitChar.Separator);

        public override string ToString() => Deserialize();
    }
}