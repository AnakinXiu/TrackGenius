using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace TrackGenius.Model
{
    public class Race : IRace
    {
        private const int MinLapIntervalMilliseconds = 1500;

        private readonly Dictionary<string, int> _lastDetectedMillisecondsByTransponder = new();

        public Guid RaceID { get; }

        public RaceType RaceType { get; }
 
        public RaceClass RaceClass { get; }

        public RaceTimer RaceTimer { get; }

        public IRaceRanker RaceRanker { get; set; }

        public int CountDownTime => RaceTimer.CountDownTime;

        public Race(Guid raceID, RaceType raceType, RaceClass raceClass, ICollection<RaceData> racersCollection)
            : this(raceID, raceType, raceClass, new RaceTimer(10), racersCollection)
        {
        }

        public Race(Guid raceID, RaceType raceType, RaceClass raceClass, RaceTimer raceTimer,
            [CanBeNull] ICollection<RaceData> racersCollection)
        {
            RaceID = raceID;
            RaceType = raceType;
            RaceClass = raceClass ?? throw new ArgumentNullException(nameof(raceClass));
            RacersCollection = racersCollection ?? new List<RaceData>();
            RaceTimer = raceTimer ?? throw new ArgumentNullException(nameof(raceTimer));
        }

        public ICollection<RaceData> RacersCollection { get; private set; }



        [CanBeNull]
        public RaceData GetRaceDataByTransponder(string transponderID) => RacersCollection
            .FirstOrDefault(racer => racer.Car.Transponder.RecoderNumber == transponderID);
    }
}