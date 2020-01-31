using TrackGenius.Const;

namespace TrackGenius.Protocol
{
    public static class MessageParserFactory
    {
        public static IMessageParser GetParserByProtocal(TransponderType transponder)
        {
            IMessageParser parser;
            switch (transponder)
            {
                case TransponderType.EasyLap:
                case TransponderType.EzLaps:
                case TransponderType.Robitronic:
                    parser = new RobitronicMessageParser();
                    break;

                case TransponderType.KyoshoIcLapCounter:
                    parser = new KyoshoMessageParser();
                    break;

                default:
                    parser = null;
                    break;
            }

            return parser;
        }
    }
}
