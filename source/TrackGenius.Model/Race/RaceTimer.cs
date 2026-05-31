using System;
using System.Diagnostics;

namespace TrackGenius.Model
{
    public class RaceTimer
    {
        private readonly Stopwatch _stopwatch = new();

        public int CountDownTime { get; set; }

        public RaceTimer(int countDownTime)
        {
            IsStarted = false;
            CountDownTime = countDownTime;
        }

        public bool IsStarted { get; private set; }

        public TimeSpan GetRaceTime() => _stopwatch.Elapsed - TimeSpan.FromSeconds(CountDownTime);

        public void Start()
        {
            _stopwatch.Start();
            IsStarted = true;
        }

        public void Stop()
        {
            _stopwatch.Stop();
            IsStarted = false;
        }
    }
}