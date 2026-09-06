using System;
using StopBits = RJCP.IO.Ports.StopBits;

namespace TrackGenius.Communication
{
    internal static class StopBitExtension
    {
        internal static StopBits ToRJCPModel(this Protocol.SerialPort.StopBits stopBits)
            => (StopBits)Enum.Parse(typeof(StopBits), Enum.GetName(typeof(Protocol.SerialPort.StopBits), stopBits));
    }
}