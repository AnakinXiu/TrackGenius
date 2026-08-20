using System;
using RJCP.IO.Ports;
using TrackGenius.Communication;

namespace TrackGenius.UITests.Communication;

/// <summary>In-memory ISerialPortWrapper so the real CommunicateService can be exercised without hardware.</summary>
public sealed class FakeSerialPortWrapper : ISerialPortWrapper
{
    public string Name => "Fake";

    public bool IsOpened { get; private set; }

    public string LastOpenedPortName { get; private set; } = string.Empty;

    /// <summary>When true, OpenPort throws, simulating a device/driver failure.</summary>
    public bool FailOnOpen { get; set; }

    public event DataReceivedEventHandler DataReceived;

    public void OpenPort(string portName, int baud, int data, Parity parity, StopBits stopBits)
    {
        if (FailOnOpen)
        {
            // Mirror the real wrapper: a failed reopen has already closed the old port.
            IsOpened = false;
            throw new InvalidOperationException("Simulated port open failure.");
        }

        LastOpenedPortName = portName;
        IsOpened = true;
    }

    public void ClosePort() => IsOpened = false;

    public void SendBytes(byte[] sendData) { }

    public void RaiseDataReceived(DataReceivedArgs args) => DataReceived?.Invoke(this, args);
}
