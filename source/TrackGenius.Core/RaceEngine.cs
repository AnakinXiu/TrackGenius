using System;
using System.Collections.Generic;
using System.Linq;
using TrackGenius.Communication;
using TrackGenius.Model;

namespace TrackGenius.Core
{
    public class RaceEngine :IDisposable
    {
        private readonly IMessageConsumer _messageConsumer;
        private readonly CommunicateService _communicateService;

        private IRace _race;

        public RaceEngine(IMessageConsumer messageConsumer, CommunicateService communicateService)
        {
            _messageConsumer = messageConsumer ?? throw new ArgumentNullException(nameof(messageConsumer));
            _communicateService = communicateService ?? throw new ArgumentNullException(nameof(communicateService));

            _communicateService.MessageReceived += _messageConsumer.ConsumeMessage;
            _messageConsumer.CarDetected += OnCarDetected;
        }

        public void RaceStart(ICollection<RaceData> racers)
        {
            _race = new Race(new Guid(), RaceType.FreePractice, new RaceClass("World GT"), racers);
        }

        private void OnCarDetected(object sender, CarDetectMessage message)
        {
            UpdateRaceStatus(message);
        }

        private void UpdateRaceStatus(CarDetectMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var raceData = _race.GetRaceDataByTransponder(message.TransponderID);
            if (raceData == null)
            {
                var anonymousDriver = AnonymousDriverCreator.CreateAnonymous(message.TransponderID);
                raceData = new RaceData(anonymousDriver, anonymousDriver.Cars.First());
                _race.RaceDataCollection.Add(raceData);
            }

            var lastDetectedMilliseconds = raceData.GetLastDetectedTimeSpan().Milliseconds;
            var interval = message.Milliseconds - lastDetectedMilliseconds;
            if (interval <= 0 || interval < _race.MinLapIntervalMilliseconds)
            {
                // TODO: Should add log and show a message in the UI to indicate that the detection is ignored due to too short interval.
                return;
            }

            raceData.RecordDetection(TimeSpan.FromMilliseconds(message.Milliseconds));
        }

        public void Dispose()
        {
            _communicateService.MessageReceived -= _messageConsumer.ConsumeMessage;
            _messageConsumer.CarDetected -= OnCarDetected;
        }
    }
}
