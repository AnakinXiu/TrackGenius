namespace TrackGenius.Model
{
    public interface IRaceDuration
    {
        DurationMode DurationMode { get; }

        string DurationLength { get; set; }

        bool IsRaceOver();
    }
}