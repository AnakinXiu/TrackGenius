using TrackGenius.Const;

namespace TrackGenius.Model;

public class Transponder : ITransponder
{
    public string RecoderName { get; set; }

    public string RecoderNumber { get; set; }

    public TransponderType RecoderType { get; set; }
}
