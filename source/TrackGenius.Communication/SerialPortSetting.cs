using RJCP.IO.Ports;

namespace TrackGenius.Communication;

internal record SerialPortSetting(int Baud, int DataBits, Parity Parity, StopBits StopBits)
{
    public int Baud { get; } = Baud;
    
    public int DataBits { get; } = DataBits;

    public Parity Parity { get; } = Parity;

    public StopBits StopBits { get; } = StopBits;
}