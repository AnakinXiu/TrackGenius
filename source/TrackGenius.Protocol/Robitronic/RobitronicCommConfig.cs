using TrackGenius.Protocol.SerialPort;

namespace TrackGenius.Protocol.Robitronic;

public record RobitronicCommConfig : ISerialPortSettings
{
    public int BaudRate => 38400;

    public StopBits StopBit => StopBits.One;

    public Parity Parity => Parity.None;

    public int Length => 8;
}