using System.Collections.Generic;

namespace TrackGenius.Communication
{
    interface ISerialPortsEnumlator
    {
        IEnumerable<string> GetValidPortNames();
    }
}
