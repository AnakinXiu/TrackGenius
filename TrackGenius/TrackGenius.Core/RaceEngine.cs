using System;
using System.Collections.Generic;
using TrackGenius.Communication;
using TrackGenius.Model;
using TrackGenius.Protocol;

namespace TrackGenius.Core
{
    public class RaceEngine
    {
        private CommunicateService _communicateService;

        private readonly IMessageConsumer _messageConsumer;

        private IRace _race;

        public RaceEngine(IMessageConsumer messageConsumer)
        {
            _messageConsumer = messageConsumer;
            _communicateService = new CommunicateService(new RobitronicMessageParser());

            _communicateService.MessageReceived += _messageConsumer.ConsumeMessage;
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
