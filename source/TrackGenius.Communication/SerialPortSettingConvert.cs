using System;
using RJCP.IO.Ports;
using TrackGenius.Protocol;

namespace TrackGenius.Communication;

internal class SerialPortSettingConvert
{
    internal static SerialPortSetting ToSerialPortSetting(ISerialPortSettings portSettings)
        => new(
            Baud: portSettings.BaudRate,
            DataBits: portSettings.DataBits,
            Parity: ConvertParity(portSettings.Parity),
            StopBits: ConvertStopBits(portSettings.StopBit));

    private static Parity ConvertParity(Protocol.SerialPort.Parity parity)
    {
        return parity switch
        {
            Protocol.SerialPort.Parity.None => Parity.None,
            Protocol.SerialPort.Parity.Odd => Parity.Odd,
            Protocol.SerialPort.Parity.Even => Parity.Even,
            Protocol.SerialPort.Parity.Mark => Parity.Mark,
            Protocol.SerialPort.Parity.Space => Parity.Space,
            _ => throw new ArgumentOutOfRangeException(nameof(parity), parity, null)
        };
    }

    private static StopBits ConvertStopBits(Protocol.SerialPort.StopBits stopBits)
    {
        return stopBits switch
        {
            Protocol.SerialPort.StopBits.One => StopBits.One,
            Protocol.SerialPort.StopBits.One5 => StopBits.One5,
            Protocol.SerialPort.StopBits.Two => StopBits.Two,
            _ => throw new ArgumentOutOfRangeException(nameof(stopBits), stopBits, null)
        };
    }
}