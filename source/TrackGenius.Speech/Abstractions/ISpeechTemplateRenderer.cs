namespace TrackGenius.Speech.Abstractions;

public interface ISpeechTemplateRenderer
{
    SpeechContent Render(AnnouncementIntent intent);
}
