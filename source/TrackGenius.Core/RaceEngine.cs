using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using TrackGenius.Communication;
using TrackGenius.Model;

namespace TrackGenius.Core
{
    public class RaceEngine : IDisposable
    {
        private readonly IMessageConsumer _messageConsumer;
        private readonly CommunicateService _communicateService;

        private IRace _race;
        private RaceTimer _raceTimer;

        public TimeSpan RaceTime => _raceTimer?.Elapsed ?? TimeSpan.Zero;
        public TimeSpan RemainTime => _race.TotalRaceTime - RaceTime;

        public event EventHandler<IReadOnlyList<RaceStandingsEntry>> RaceDataChanged;

        public RaceEngine([NotNull] IMessageConsumer messageConsumer, [NotNull] CommunicateService communicateService)
        {
            _messageConsumer = messageConsumer ?? throw new ArgumentNullException(nameof(messageConsumer));
            _communicateService = communicateService ?? throw new ArgumentNullException(nameof(communicateService));
        }

        public void RaceStart(IRace race)
        {
            _communicateService.MessageReceived += _messageConsumer.ConsumeMessage;
            _messageConsumer.CarDetected += OnCarDetected;

            _race = race;
            _raceTimer = new RaceTimer(_race.CountDownTime);
            _raceTimer.Start();
        }

        public void RaceStart(ICollection<RaceData> racers)
        {
            // Subscription lives only in the IRace overload to avoid double-subscribing handlers.
            RaceStart(new Race(Guid.NewGuid(), RaceType.FreePractice, new RaceClass("World GT"), 10, racers));
        }

        private void OnCarDetected(object sender, CarDetectMessage message)
        {
            // A detection can arrive after construction but before RaceStart assigns _race.
            if (_race == null)
                return;

            UpdateRaceStatus(message);
        }

        private void UpdateRaceStatus(CarDetectMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var newCarDetected = false;
            var raceData = _race.GetRaceDataByTransponder(message.TransponderID);
            if (raceData == null)
            {
                var anonymousDriver = AnonymousDriverCreator.CreateAnonymous(message.TransponderID);
                raceData = new RaceData(anonymousDriver, anonymousDriver.Cars.First());
                _race.RaceDataCollection.Add(raceData);
                newCarDetected = true;
            }

            var lastDetectedMilliseconds = raceData.GetLastDetectedTimeSpan().Milliseconds;
            var interval = message.Milliseconds - lastDetectedMilliseconds;
            if (interval <= 0 || interval < _race.MinLapIntervalMilliseconds)
            {
                // TODO: Should add log and show a message in the UI to indicate that the detection is ignored due to too short interval.
                if (newCarDetected)
                    RaiseRaceDataChanged(); // the racer was added even though this pass was suppressed
                return;
            }

            raceData.RecordDetection(TimeSpan.FromMilliseconds(message.Milliseconds));
            RaiseRaceDataChanged();
        }

        private void RaiseRaceDataChanged()
            => RaceDataChanged?.Invoke(this, _race.OrderCalculator.Calculate(_race.RaceDataCollection));

        public void Dispose()
        {
            _communicateService.MessageReceived -= _messageConsumer.ConsumeMessage;
            _messageConsumer.CarDetected -= OnCarDetected;
        }
    }
}