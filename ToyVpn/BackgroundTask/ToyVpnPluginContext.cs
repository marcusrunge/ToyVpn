using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Networking.Vpn;
using Windows.Storage.Streams;

namespace BackgroundTask
{
    internal sealed class ToyVpnPluginContext
    {
        internal HandshakeState HandshakeState { get; set; }
        //internal ConcurrentQueue<byte[]> ConcurrentQueue { get; set; }
        internal IAsyncAction HandShake(DatagramSocket datagramSocket, string secret)
        {
            return Task.Run(async () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var dataWriter = new DataWriter(datagramSocket.OutputStream)
                    {
                        UnicodeEncoding = UnicodeEncoding.Utf8
                    };
                    dataWriter.WriteByte(0);
                    dataWriter.WriteString(secret);
                    await dataWriter.StoreAsync();
                    dataWriter.DetachStream();
                }

                for (int i = 0; i < 50; i++)
                {
                    await Task.Delay(100);
                    switch (HandshakeState)
                    {
                        case HandshakeState.Waiting:
                            break;
                        case HandshakeState.Received:
                            return;
                        case HandshakeState.Canceled:
                            throw new OperationCanceledException();
                        default:
                            break;
                    }
                }
            }).AsAsyncAction();
        }

        internal void ConfigureAndConnect(VpnChannel vpnChannel, string parameters)
        {
            parameters = parameters.TrimEnd();
            uint mtuSize = 68;
            var assignedClientIPv4list = new List<HostName>();
            var dnsServerList = new List<HostName>();
            VpnRouteAssignment assignedRoutes = new VpnRouteAssignment();
            VpnDomainNameAssignment assignedDomainName = new VpnDomainNameAssignment();
            var ipv4InclusionRoutes = assignedRoutes.Ipv4InclusionRoutes;
            foreach (var parameter in parameters.Split(null))
            {
                var fields = parameter.Split(",");
                switch (fields[0])
                {
                    case "m":
                        mtuSize = uint.Parse(fields[1]);
                        break;
                    case "a":
                        assignedClientIPv4list.Add(new HostName(fields[1]));
                        break;
                    case "r":
                        ipv4InclusionRoutes.Add(new VpnRoute(new HostName(fields[1]), (byte)(int.Parse(fields[2]))));
                        break;
                    case "d":
                        dnsServerList.Add(new HostName(fields[1]));
                        break;
                    default:
                        break;
                }
            }

            assignedRoutes.Ipv4InclusionRoutes = ipv4InclusionRoutes;
            assignedDomainName.DomainNameList.Add(new VpnDomainNameInfo(".", VpnDomainNameType.Suffix, dnsServerList, null));

            try
            {
                vpnChannel.StartExistingTransports(assignedClientIPv4list, null, null, assignedRoutes, assignedDomainName, mtuSize, ushort.MaxValue, false);
            }
            catch (Exception e)
            {
                vpnChannel.TerminateConnection(e.Message);
            }
        }
    }
}
