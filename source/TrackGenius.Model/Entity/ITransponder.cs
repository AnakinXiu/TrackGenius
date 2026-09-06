using TrackGenius.Const;

namespace TrackGenius.Model
{
    public interface ITransponder
    {
        string RecoderNumber { get; set; }

        TransponderType RecoderType { get; set; }
    }
}