﻿using System.Globalization;
using System.Linq;

namespace TrackGenius.Protocol.Robitronic
{
    public class Initialize : IDownlinkMessage
    {
        private readonly string _data = $"03{SplitChar.Separator}B9{SplitChar.Separator}01";

        public byte[] Serialize() => GetBytes();


        public byte[] GetBytes() => _data.Split(SplitChar.Separator).Select(b=>byte.Parse(b, NumberStyles.HexNumber)).ToArray();

        public override string ToString() => _data;

    }
}