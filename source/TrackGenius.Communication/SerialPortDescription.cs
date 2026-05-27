using TrackGenius.Communication.interfaces;

namespace TrackGenius.Communication;

internal record SerialPortDescription(string PortName, string Description, string Manufacturer)
    : ISerialPortDescription
{
    public string PortName { get; } = PortName;
    public string Description { get; } = Description;
    public string Manufacturer { get; } = Manufacturer;
}