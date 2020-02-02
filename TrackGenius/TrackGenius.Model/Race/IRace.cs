using System;

namespace TrackGenius.Model
{
    public interface IRace
    {
        Guid RaceID { get; }

        RaceType RaceType { get; }

        RaceClass RaceClass { get; }

        int CountDownTime { get; set; }

        void StartRace();
    }
}