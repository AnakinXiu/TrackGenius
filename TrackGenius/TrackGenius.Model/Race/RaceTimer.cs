using System;

namespace TrackGenius.Model
{
    public class RaceTimer
    {
        private long _startTime;

        private long _stopTime;

        public int CountDownTime { get; set; }

        public RaceTimer(int countDownTime)
        {
            IsStarted = false;
            CountDownTime = countDownTime;
        }

        public bool IsStarted { get; private set; }

        public TimeSpan GetRaceTime() => new TimeSpan(DateTime.Now.Ticks - _startTime) - new TimeSpan(0, 0, 0, CountDownTime);

        public void Start()
        {
            _startTime = DateTime.Now.Ticks;
        }

        public void Stop()
        {
            _stopTime = DateTime.Now.Ticks;
        }
    }
}