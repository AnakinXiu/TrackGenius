using System.Collections.Generic;
using System.Linq;
using RJCP.IO.Ports;
using TrackGenius.Communication.interfaces;

namespace TrackGenius.Communication;

public static class SerialPortEnumerator
{
    public static IEnumerable<string> GetValidPortNames()
    {
        return new SerialPortStream().GetPortNames();
    }

    public static IEnumerable<ISerialPortDescription> GetValidPortDescriptions()
    {
        return new SerialPortStream().GetPortDescriptions()
            .Select(description => new SerialPortDescription(description.Port,
                description.Description,
                description.Manufacturer));
    }
}