using System;
using TrackGenius.Model;
using TrackGenius.Protocol;

namespace TrackGenius.Core
{
    public interface IMessageConsumer
    {
        event EventHandler<CarDetectMessage> CarDetected;

        void ConsumeMessage(object sender, ICommonMessage message);
    }
}