namespace TrackGenius.Communication.interfaces;

public interface ISerialPortDescription
{
    string PortName { get; }
    string Description { get; }
    string Manufacturer { get; }
}