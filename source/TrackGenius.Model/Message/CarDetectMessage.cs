namespace TrackGenius.Model
{
    public class CarDetectMessage
    {
        public string TransponderID { get; }

        public int Milliseconds { get; }

        public int Hits { get; }

        public int SignalLevel { get; }

        public CarDetectMessage(string transponderID, int milliseconds, int hits, int signalLevel)
        {
            TransponderID = transponderID;
            Milliseconds = milliseconds;
            Hits = hits;
            SignalLevel = signalLevel;
        }
    }
}