using System;

namespace TrackGenius.Model;

public interface IRaceClub
{
    Guid ClubID { get; set; }

    string ClubName { get; set; }
}