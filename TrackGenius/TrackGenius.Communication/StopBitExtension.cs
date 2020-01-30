using System;
using StopBits = RJCP.IO.Ports.StopBits;

namespace TrackGenius.Communication
{
    internal static class StopBitExtension
    {
        internal static StopBits ToRJCPModel(this System.IO.Ports.StopBits stopBits)
            => (StopBits)Enum.Parse(typeof(StopBits), Enum.GetName(typeof(System.IO.Ports.StopBits), stopBits));
    }
}
