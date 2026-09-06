using RJCP.IO.Ports;

namespace TrackGenius.Communication;

internal record SerialPortSetting(int Baud, int DataBits, Parity Parity, StopBits StopBits);