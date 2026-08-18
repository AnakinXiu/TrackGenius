using System;
using System.Collections.Generic;
using System.Drawing;

namespace TrackGenius.Model;

public class Driver : IDriver
{
    public string DriverName { get; set; }

    public Guid DriverID { get; }

    public string NickName { get; set; }

    public Guid ClubID { get; set; }

    public Bitmap Photo { get; set; }

    public ICollection<ICar> Cars { get; }

    public Driver()
    {
        DriverID = Guid.NewGuid();
        Cars = new List<ICar>();
    }
}