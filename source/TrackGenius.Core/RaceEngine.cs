using System;
using System.Collections.Generic;
using TrackGenius.Communication;
using TrackGenius.Model;

namespace TrackGenius.Core
{
    public class RaceEngine
    {
        private readonly CommunicateService _communicateService;

        private readonly IMessageConsumer _messageConsumer;

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

            var racer = _race.GetRacer(message.TransponderID);
            if (racer == null)
            {
                var raceData = new RaceData();
                RacersCollection.Add(raceData);
            }

            if (_lastDetectedMillisecondsByTransponder.TryGetValue(message.TransponderID, out var lastDetectedMilliseconds))
            {
                var interval = message.Milliseconds - lastDetectedMilliseconds;
                if (interval is <= 0 or < MinLapIntervalMilliseconds)
                {
                    // TODO: Should add log and show a message in the UI to indicate that the detection is ignored due to too short interval.
                    return;
                }
            }

            _lastDetectedMillisecondsByTransponder[message.TransponderID] = message.Milliseconds;

            racer.RecordDetection(TimeSpan.FromMilliseconds(message.Milliseconds));
        }
    }
}
