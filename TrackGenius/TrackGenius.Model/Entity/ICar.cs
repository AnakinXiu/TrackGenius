using System.Windows.Media;

namespace TrackGenius.Model
{
    public interface ICar
    {
        string CarName { get; set; }

        ITransponder Transponder { get; set; }

        string CarClass { get; set; }

        Color CarColor { get; set; }

    }
}