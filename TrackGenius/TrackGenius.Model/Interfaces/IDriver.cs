using System;

namespace TrackGenius.Model
{
    public interface IDriver
    {
        Guid DriverID { get; }

        string DriverName { get; set; }

        string NickName { get; set; }

        string ClubName { get; }
    }
}