using System;
using TrackGenius.Const;

namespace TrackGenius.Protocol.Robitronic
{
    public class DetectedMessage : IUplinkMessage
    {
        public byte[] ByteData { get; }

        public byte Checksum => ByteData[1];

        public PacketType PacketType => (PacketType)ByteData[2];

        public int PacketLength => ByteData[0];

        public string TransponderID { get; }

        public int Milliseconds { get; }

        public int Hits => ByteData[11];
        
        public int SignalLevel => ByteData[12];

        public DetectedMessage(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException();

            if (data.Length < 3)
                throw new ArgumentOutOfRangeException();

            if (data[2] != 0x84)
                throw new FormatException();

            ByteData = data;

            TransponderID = ByteData.Cut(3, 4).ToInt32().ToString();
            Milliseconds = ByteData.Cut(7, 4).ToInt32();
        }

        public string Deserialize()=> BitConverter.ToString(ByteData).Replace('-', SplitChar.Separator);

        public override string ToString() => Deserialize();
    }
}