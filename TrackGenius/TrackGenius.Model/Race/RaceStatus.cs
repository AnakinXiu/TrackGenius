using System;

namespace TrackGenius.Model
{
    public class RaceStatus
    {
        public IDriver Driver { get; }

        public ICar Car { get; }

        public int LapsCount { get; set; }

        public TimeSpan RacedTime { get; set; }

        public RaceStatus(IDriver driver, ICar car)
        {
            Driver = driver;
            Car = car;
        }
    }
}