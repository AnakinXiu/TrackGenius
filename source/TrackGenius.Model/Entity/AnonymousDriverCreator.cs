using System.Drawing;

namespace TrackGenius.Model;

public static class AnonymousDriverCreator
{
    public static IDriver CreateAnonymous(string transponderID) =>
        new Driver
        {
            DriverName = transponderID,
            Cars =
            {
                new Car
                {
                    CarName = transponderID,
                    CarColor = Color.CadetBlue,   //TODO: Change to a random color
                    Transponder = new Transponder { RecoderNumber = transponderID }
                }
            }
        };
}