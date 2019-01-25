using System;
using TrackGenius.Const;

namespace TrackGenius.Model
{
    public interface IRecoder
    {
        string RecoderName { get; set; }

        string RecoderNumber { get; set; }

        RecoderType RecoderType { get; set; }
    }
}