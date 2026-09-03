namespace TrackGenius.Speech.Abstractions;

public interface IAnnouncementPolicy
{
    PolicyDecision Evaluate(SpeechMessage message, PolicyContext context);
}
