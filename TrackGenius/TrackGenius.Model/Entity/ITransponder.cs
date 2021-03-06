﻿using TrackGenius.Const;

namespace TrackGenius.Model
{
    public interface ITransponder
    {
        string RecoderName { get; set; }

        string RecoderNumber { get; set; }

        TransponderType RecoderType { get; set; }
    }
}