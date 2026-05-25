using System;

namespace TrackGenius.Model
{
    interface IRaceClub
    {
        Guid ClubID { get; set; }

        string ClubName { get; set; }


    }
}