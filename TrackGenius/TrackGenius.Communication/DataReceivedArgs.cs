using TrackGenius.Protocol;

namespace TrackGenius.Communication
{
    public class DataReceivedArgs
    {
        public byte[] Buffer { get; }

        public DataReceivedArgs(byte[] dataReceived)
        {
            Buffer = dataReceived;
        }
    }
}
