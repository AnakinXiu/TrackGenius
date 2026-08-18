using System.Drawing;

namespace TrackGenius.Model;

public class Car : ICar
{
    public string CarName { get; set; }
    public ITransponder Transponder { get; set; }
    public string CarClass { get; set; }
    public Color CarColor { get; set; }
}