using System.Collections.Generic;
using TrackGenius.Protocol.Interfaces;
using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.Protocol;

public static class Protocols
{
    public static List<IProtocol> AvailableProtocols = [new RobitronicProtocol(), new KyoshoProtocol()];
}