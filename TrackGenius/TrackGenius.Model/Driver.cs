using System;

namespace TrackGenius.Model
{
    public class Driver : IDriver
    {

        public string DriverName { get; set; }

        public Guid DriverID => throw new NotImplementedException();

        public string NickName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public string ClubName { get; }


    }
}
