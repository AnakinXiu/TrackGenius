using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace TrackGenius.Model;

public class RaceData
{
    public IDriver Driver { get; }

    public ICar Car { get; }

    public TimeSpan RacedTime { get; private set; }

    public int LapsCount => LapRecords.Count;

    [NotNull]
    public IList<LapRecord> LapRecords { get; } = new List<LapRecord>();

    public RaceData(IDriver driver, ICar car)
    {
        Driver = driver;
        Car = car;
    }

    public void RecordDetection(TimeSpan raceTime)
    {
        RacedTime = raceTime;
        var lapNumber = LapsCount + 1;
        var lapRecord = new LapRecord
        {
            LapNumber = lapNumber,
            LapTime = raceTime - (LapRecords.Any() ? LapRecords.Last().LapTime : TimeSpan.Zero)
        };
        LapRecords.Add(lapRecord);
    }

    public TimeSpan GetLastDetectedTimeSpan()
    {
        return LapRecords.Any() ? LapRecords.Last().LapTime : TimeSpan.Zero;
    }
}

public record LapRecord
{
    public int LapNumber { get; init; }

    public TimeSpan LapTime { get; init; }
}