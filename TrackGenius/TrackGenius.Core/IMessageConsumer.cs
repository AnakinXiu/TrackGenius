using TrackGenius.Protocol;

namespace TrackGenius.Core
{
    public interface IMessageConsumer
    {
        void ConsumeMessage(object sender, ICommonMessage message);
    }
}