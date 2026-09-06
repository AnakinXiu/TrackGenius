using System;
using Parity = RJCP.IO.Ports.Parity;

namespace TrackGenius.Communication
{
    internal static class ParityExtension
    {
        internal static Parity ToRJCPModel(this Protocol.SerialPort.Parity parity)
            => (Parity)Enum.Parse(typeof(Parity), Enum.GetName(typeof(Protocol.SerialPort.Parity), parity));
    }
}