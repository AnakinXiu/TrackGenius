using TrackGenius.Model;
using TrackGenius.Protocol;
using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.Core
{
    public class RobitronicMessageConsumer : IMessageConsumer
    {
        public void ConsumeMessage(object sender, ICommonMessage message)
        {
            if (!(message is IUplinkMessage uplinkMessage)) 
                return;

            if (uplinkMessage is DetectedMessage detectedMessage)
            {
                var carDetectMessage = new CarDetectMessage(detectedMessage.TransponderID,
                    detectedMessage.Milliseconds,
                    detectedMessage.Hits,
                    detectedMessage.SignalLevel);

                
            }
        }
    }
}