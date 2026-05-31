using System;
using System.Collections.Generic;
using System.Linq;

namespace TrackGenius.Model
{
    public class Race : IRace
    {
        private const int MinLapIntervalMilliseconds = 1500;

        private readonly Dictionary<string, int> _lastDetectedMillisecondsByTransponder = new();

        public Guid RaceID { get; }

        public RaceType RaceType { get; private set; }
 
        public RaceClass RaceClass { get; }
        public RaceTimer RaceTimer { get; private set; }

        public IRaceRanker RaceRanker { get; set; }

        public int CountDownTime { get; set; }

        public Race(Guid raceID, RaceType raceType, RaceClass raceClass, ICollection<RaceStatus> racersCollection)
            : this(raceID, raceType, raceClass, racersCollection, new RaceTimer(10))
        {
        }

        public Race(Guid raceID, RaceType raceType, RaceClass raceClass, ICollection<RaceStatus> racersCollection, RaceTimer raceTimer)
        {
            RaceID = raceID;
            RaceType = raceType;
            RaceClass = raceClass ?? throw new ArgumentNullException(nameof(raceClass));
            RacersCollection = racersCollection ?? throw new ArgumentNullException(nameof(racersCollection));
            RaceTimer = raceTimer ?? throw new ArgumentNullException(nameof(raceTimer));
        }

        public ICollection<RaceStatus> RacersCollection { get; private set; }

        public void UpdateRaceStatus(CarDetectMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var racer = GetRacer(message.TransponderID);
            if (racer == null)
                return;

            if (_lastDetectedMillisecondsByTransponder.TryGetValue(message.TransponderID, out var lastDetectedMilliseconds))
            {
                var interval = message.Milliseconds - lastDetectedMilliseconds;
                if (interval <= 0 || interval < MinLapIntervalMilliseconds)
                    return;
            }

            _lastDetectedMillisecondsByTransponder[message.TransponderID] = message.Milliseconds;

            racer.LapsCount++;
            racer.RacedTime = TimeSpan.FromMilliseconds(message.Milliseconds);
        }

        private RaceStatus GetRacer(string transponderID) => RacersCollection
            .FirstOrDefault(racer => racer.Car.Transponder.RecoderNumber == transponderID);
    }
}