using System;
using System.Linq;
using TrackGenius.Model;
using TrackGenius.Speech.Abstractions;

namespace TrackGenius.SpeechTests;

public static class TestStandings
{
    public static RaceStandingsEntry Entry(string transponder, int position, double bestSeconds, bool isRaceBest)
        => new(new RaceData(AnonymousDriverCreator.CreateAnonymous(transponder),
                AnonymousDriverCreator.CreateAnonymous(transponder).Cars.First()),
            position, TimeSpan.FromSeconds(bestSeconds), TimeSpan.FromSeconds(bestSeconds),
            "-", "-", "-", "-", "-", "-", "-", isRaceBest);
}
