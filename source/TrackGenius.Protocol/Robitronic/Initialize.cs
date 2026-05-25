using System;

namespace TrackGenius.Protocol.Robitronic
{
    public class Initialize : IDownlinkMessage
    {
        private static readonly byte[] _byteData = new byte[] { 0x03, 0xB9, 0x01 };
        
        public int PacketLength => 3;

        public byte[] ByteData { get; } = _byteData;

        public byte[] Serialize() => _byteData;

        public override string ToString() => BitConverter.ToString(ByteData).Replace('-', SplitChar.Separator);
    }
}