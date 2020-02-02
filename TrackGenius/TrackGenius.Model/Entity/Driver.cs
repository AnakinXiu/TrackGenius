using System;
using System.Collections.Generic;

namespace TrackGenius.Model
{
    public class Driver : IDriver
    {
        public string DriverName { get; set; }

        public Guid DriverID => throw new NotImplementedException();

        public string NickName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Guid ClubID{ get; set; }

        public ICollection<ICar> Cars { get; }

        public Driver()
        {
            Cars = new List<ICar>();
        }
    }
}
