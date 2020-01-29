using System;

namespace TrackGenius.Model.Interfaces
{
    interface IRaceClub
    {
        Guid ClubID { get; set; }

        string ClubName { get; set; }
    }
}
