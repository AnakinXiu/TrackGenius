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

        public void RaceStart(ICollection<RaceStatus> racers)
        {
            _race = new Race(new Guid(), RaceType.FreePractice, new RaceClass("World GT"), racers);
        }

        private void OnCarDetected(object sender, CarDetectMessage message)
        {
            _race.UpdateRaceStatus(message);
        }
    }
}
