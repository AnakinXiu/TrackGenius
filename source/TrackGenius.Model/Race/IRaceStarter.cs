namespace TrackGenius.Model
{
    public interface IRaceStarter
    {
        StartMode StartMode { get; }

        void Start();

        bool RandomStart { get; }
    }
}