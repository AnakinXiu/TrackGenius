using TrackGenius.Communication.interfaces;

namespace TrackGenius.Communication;

internal record SerialPortDescription(string PortName, string Description, string Manufacturer)
    : ISerialPortDescription;