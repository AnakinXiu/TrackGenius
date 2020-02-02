using System;

namespace TrackGenius.Model
{
    public class RaceClub : IRaceClub
    {
        public Guid ClubID { get ; set ; }

        public string ClubName { get ; set ; }
    }
}