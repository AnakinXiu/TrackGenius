using System;

namespace TrackGenius.Protocol.Robitronic
{
    public class DetectedMessage : IUplinkMessage
    {
        private byte[] ByteData { get; }

        public DetectedMessage(byte[] data)
        {
            ByteData = data;
        }

        public byte[] GetBytes() => ByteData;

        public string Deserialize()=> BitConverter.ToString(ByteData).Replace('-', SplitChar.Separator);

        public override string ToString() => Deserialize();
    }
}