using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace TrackGenius.Model;

public class Race : IRace
{
    private RaceOrderRule _orderRule = RaceOrderRule.LapsThenRaceTime;

    public int MinLapIntervalMilliseconds { get; set; } = 1500;

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

    public string RaceName { get; set; } = string.Empty;

    public RaceType RaceType { get; }

    public RaceClass RaceClass { get; }

    public IRaceRanker RaceRanker { get; set; }

    public int CountDownTime { get; set; }

    public Race(Guid raceID, RaceType raceType, RaceClass raceClass, ICollection<RaceData> raceDataCollection)
        : this(raceID, raceType, raceClass, 10, raceDataCollection)
    {
    }

    public Race(Guid raceID, RaceType raceType, RaceClass raceClass, int countDownTime,
        [CanBeNull] ICollection<RaceData> raceDataCollection)
    {
        RaceID = raceID;
        RaceType = raceType;
        RaceClass = raceClass ?? throw new ArgumentNullException(nameof(raceClass));
        RaceDataCollection = raceDataCollection ?? new List<RaceData>();
        CountDownTime = countDownTime;
        OrderCalculator = CreateCalculatorFor(_orderRule);
    }

    public ICollection<RaceData> RaceDataCollection { get; private set; }

    [CanBeNull]
    public RaceData GetRaceDataByTransponder(string transponderID)
        => RaceDataCollection.FirstOrDefault(racer => racer.Car.Transponder.RecoderNumber == transponderID);

    // Future rules switch here; today the default calculator serves the only rule.
    private static IRaceOrderCalculator CreateCalculatorFor(RaceOrderRule rule)
        => rule switch
        {
            RaceOrderRule.LapsThenRaceTime => new LapsRaceTimeOrderCalculator(),
            RaceOrderRule.FastestLap => throw new NotImplementedException($"No calculator implemented for rule {rule}"),
            RaceOrderRule.Top3Consecutive => throw new NotImplementedException($"No calculator implemented for rule {rule}"),
            RaceOrderRule.Custom => throw new NotImplementedException($"No calculator implemented for rule {rule}"),
            _ => throw new NotImplementedException($"No calculator implemented for rule {rule}")
        };
}