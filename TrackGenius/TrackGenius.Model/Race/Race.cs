using System;
using System.Collections.Generic;
using System.Linq;

namespace TrackGenius.Model
{
    public class Race : IRace
    {
        public Guid RaceID { get; }

        public RaceType RaceType { get; private set; }
 
        public RaceClass RaceClass { get; }
        public RaceTimer RaceTimer { get; private set; }

        public IRaceRanker RaceRanker { get; set; }

        public int CountDownTime { get; set; }

        public Race(Guid raceID, RaceType raceType, RaceClass raceClass, ICollection<RaceStatus> racersCollection)
        {
            RaceID = raceID;
            RaceType = raceType;
            RaceClass = raceClass;
            RacersCollection = racersCollection;

            RaceTimer = new RaceTimer(10);
        }

        public ICollection<RaceStatus> RacersCollection { get; private set; }

        public void UpdateRaceStatus(CarDetectMessage message)
        {
            var racer = GetRacer(message.TransponderID);
            racer.LapsCount++;
        }

        private RaceStatus GetRacer(string transponderID) => RacersCollection.ToList()
            .Find(racer => racer.Car.Transponder.RecoderNumber == transponderID);
    }
}