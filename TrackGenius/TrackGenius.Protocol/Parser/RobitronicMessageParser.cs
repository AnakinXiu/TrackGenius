using JetBrains.Annotations;
using System;
using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.Protocol
{
    public class RobitronicMessageParser : IMessageParser
    {
        public IUplinkMessage ParseMessage([NotNull]byte[] dataBytes)
        {
            if (dataBytes == null || dataBytes.Length < 1)
                throw new ArgumentNullException();

            if (dataBytes.Length ==1 && dataBytes[0] == 0)
                return new InitializeResponse(dataBytes);

            if (dataBytes[0] > dataBytes.Length)
                throw new ArgumentOutOfRangeException();

            switch (dataBytes[2])
            {
                case 0x83:
                    return new TimeStampMessage(dataBytes);
                case 0x84:
                    return new DetectedMessage(dataBytes);
            }

            return new EmptyUplinkMessage();
        }
    }
}
