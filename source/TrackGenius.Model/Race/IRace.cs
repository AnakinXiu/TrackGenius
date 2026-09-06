using System;
using System.Collections.Generic;

namespace TrackGenius.Model;

public interface IRace
{
    Guid RaceID { get; }

    string RaceName { get; }

    RaceType RaceType { get; }

    RaceClass RaceClass { get; }

    IRaceRanker RaceRanker { get; set; }

    int CountDownTime { get; }

    ICollection<RaceData> RaceDataCollection { get; }

    int MinLapIntervalMilliseconds { get; }

    RaceOrderRule OrderRule { get; set; }

    IRaceOrderCalculator OrderCalculator { get; }
    TimeSpan TotalRaceTime { get; set; }

    RaceData GetRaceDataByTransponder(string transponderID);
}