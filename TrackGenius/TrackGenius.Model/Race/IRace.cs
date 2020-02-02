using System;
using System.Collections.Generic;

namespace TrackGenius.Model
{

    public interface IRace
    {
        Guid RaceID { get; }

        RaceType RaceType { get; }

        RaceClass RaceClass { get; }

        RaceTimer RaceTimer { get; }

        IRaceRanker RaceRanker { get; set; }

        int CountDownTime { get; set; }

        ICollection<RaceStatus> RacersCollection { get; }

        void UpdateRaceStatus(CarDetectMessage message);
    }

}