using System;

namespace TrackGenius.Model
{
    public class Race : IRace
    {
        public Guid RaceID { get; }

        public RaceType RaceType { get; private set; }
 
        public RaceClass RaceClass { get; }

        public int CountDownTime { get; set; }

        public Race(Guid raceID, RaceType raceType, RaceClass raceClass)
        {
            RaceID = raceID;
            RaceType = raceType;
            RaceClass = raceClass;
        }

        public void StartRace()
        {

        }
    }
}