using System.Collections.Generic;

namespace TrackGenius.Model;

public interface IRaceOrderCalculator
{
    RaceOrderRule Rule { get; }

    IReadOnlyList<RaceStandingsEntry> Calculate(ICollection<RaceData> raceDataCollection);
}
