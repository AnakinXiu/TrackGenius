using System;
using System.Collections.Generic;
using System.Drawing;

namespace TrackGenius.Model
{
    public interface IDriver
    {
        Guid DriverID { get; }

        string DriverName { get; set; }

        string NickName { get; set; }

        Guid ClubID { get; set; }

        Bitmap Photo { get; set; }

        ICollection<ICar> Cars { get; }
    }
}