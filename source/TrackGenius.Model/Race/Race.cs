using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace TrackGenius.Model
{
    public class Race : IRace
    {
        public int MinLapIntervalMilliseconds { get; set; } = 1500;

        private RaceOrderRule _orderRule = RaceOrderRule.LapsThenRaceTime;

        public RaceOrderRule OrderRule
        {
            get => _orderRule;
            set
            {
                _orderRule = value;
                OrderCalculator = CreateCalculatorFor(value);
            }
        }

        public IRaceOrderCalculator OrderCalculator { get; private set; }

        public Guid RaceID { get; }

        public RaceType RaceType { get; }
 
        public RaceClass RaceClass { get; }

        public RaceTimer RaceTimer { get; }

        public IRaceRanker RaceRanker { get; set; }

        public int CountDownTime => RaceTimer.CountDownTime;

        public Race(Guid raceID, RaceType raceType, RaceClass raceClass, ICollection<RaceData> raceDataCollection)
            : this(raceID, raceType, raceClass, new RaceTimer(10), raceDataCollection)
        { }

        public Race(Guid raceID, RaceType raceType, RaceClass raceClass, RaceTimer raceTimer,
            [CanBeNull] ICollection<RaceData> raceDataCollection)
        {
            RaceID = raceID;
            RaceType = raceType;
            RaceClass = raceClass ?? throw new ArgumentNullException(nameof(raceClass));
            RaceDataCollection = raceDataCollection ?? new List<RaceData>();
            RaceTimer = raceTimer ?? throw new ArgumentNullException(nameof(raceTimer));
            OrderCalculator = CreateCalculatorFor(_orderRule);
        }

        public ICollection<RaceData> RaceDataCollection { get; private set; }

        [CanBeNull]
        public RaceData GetRaceDataByTransponder(string transponderID)
            => RaceDataCollection.FirstOrDefault(racer => racer.Car.Transponder.RecoderNumber == transponderID);

        // Future rules switch here; today the default calculator serves the only rule.
        private static IRaceOrderCalculator CreateCalculatorFor(RaceOrderRule rule)
            => new LapsRaceTimeOrderCalculator();
    }
}