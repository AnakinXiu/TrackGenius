using System;
using Parity = RJCP.IO.Ports.Parity;

namespace TrackGenius.Communication
{
    internal static class ParityExtension
    {
        internal static Parity ToRJCPModel(this System.IO.Ports.Parity parity)
            => (Parity)Enum.Parse(typeof(Parity), Enum.GetName(typeof(System.IO.Ports.Parity), parity));
    }
}