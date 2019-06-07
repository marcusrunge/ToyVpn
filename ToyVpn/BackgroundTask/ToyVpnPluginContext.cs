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
        private DatagramSocket _mainOuterTunnelTransportEndpoint;
        private DatagramSocket _tunnel;
        private string _parameters;
        private Task _dequeueTask;
        private Task _enqueueTask;
        internal ConcurrentQueue<byte[]> EncapsulationConcurrentQueue { get; set; }
        internal ConcurrentQueue<byte[]> DecapsulationConcurrentQueue { get; set; }

        internal void Init(HostName remoteHostName, string remoteServiceName)
        {            
            EncapsulationConcurrentQueue = new ConcurrentQueue<byte[]>();
            DecapsulationConcurrentQueue = new ConcurrentQueue<byte[]>();
            _mainOuterTunnelTransportEndpoint = new DatagramSocket();
            _tunnel = new DatagramSocket();
            _mainOuterTunnelTransportEndpoint.BindServiceNameAsync("11885").AsTask().Wait();
            _mainOuterTunnelTransportEndpoint.ConnectAsync(new HostName("127.0.0.1"), "11884").AsTask().Wait();
            _tunnel.ConnectAsync(remoteHostName, remoteServiceName).AsTask().Wait();
            _dequeueTask = Task.Factory.StartNew(DequeueAction);
            _enqueueTask = Task.Factory.StartNew(EnqueueAction);
            _tunnel.MessageReceived += (s, e) =>
            {
                DataReader dataReader = e.GetDataReader();
                if (dataReader.UnconsumedBufferLength > 0)
                {
                    if (dataReader.ReadByte() == 0)
                    {
                        _parameters = dataReader.ReadString(dataReader.UnconsumedBufferLength);
                        HandshakeState = HandshakeState.Received;
                    }
                    else
                    {
                        byte[] readBytes = new byte[dataReader.UnconsumedBufferLength];
                        dataReader.ReadBytes(readBytes);
                        DecapsulationConcurrentQueue.Enqueue(readBytes);
                    }
                }
            };            
        }

        private void EnqueueAction()
        {
            while (true)
            {
                if (!EncapsulationConcurrentQueue.IsEmpty)
                {
                    if (EncapsulationConcurrentQueue.TryDequeue(out byte[] encapsulationPacket))
                    {
                        var dataWriter = new DataWriter(_tunnel.OutputStream)
                        {
                            UnicodeEncoding = UnicodeEncoding.Utf8
                        };
                        dataWriter.WriteBytes(encapsulationPacket);
                        dataWriter.StoreAsync().AsTask().Wait();
                        dataWriter.DetachStream();
                        dataWriter.Dispose();
                    }
                }
            }
        }

        private void DequeueAction()
        {
            while (true)
            {
                if (!DecapsulationConcurrentQueue.IsEmpty)
                {
                    if (DecapsulationConcurrentQueue.TryDequeue(out byte[] decapsulationPacket))
                    {
                        var dataWriter = new DataWriter(_mainOuterTunnelTransportEndpoint.OutputStream)
                        {
                            UnicodeEncoding = UnicodeEncoding.Utf8
                        };
                        dataWriter.WriteBytes(decapsulationPacket);
                        dataWriter.StoreAsync().AsTask().Wait();
                        dataWriter.DetachStream();
                        dataWriter.Dispose();
                    }
                }
            }
        }

        internal IAsyncAction HandShake(string secret)
        {
            return Task.Run(async () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var dataWriter = new DataWriter(_tunnel.OutputStream)
                    {
                        UnicodeEncoding = UnicodeEncoding.Utf8
                    };
                    dataWriter.WriteByte(0);
                    dataWriter.WriteString(secret);
                    await dataWriter.StoreAsync();
                    dataWriter.DetachStream();
                    dataWriter.Dispose();
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

        internal void ConfigureAndConnect(VpnChannel vpnChannel)
        {
            _parameters = _parameters.TrimEnd();
            uint mtuSize = 68;
            var assignedClientIPv4list = new List<HostName>();
            var dnsServerList = new List<HostName>();
            VpnRouteAssignment assignedRoutes = new VpnRouteAssignment();
            VpnDomainNameAssignment assignedDomainName = new VpnDomainNameAssignment();
            var ipv4InclusionRoutes = assignedRoutes.Ipv4InclusionRoutes;
            foreach (var parameter in _parameters.Split(null))
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

        internal void Dispose()
        {
            _tunnel.CancelIOAsync().AsTask().Wait();
            _tunnel.Dispose();
            _mainOuterTunnelTransportEndpoint.CancelIOAsync().AsTask().Wait();
            _mainOuterTunnelTransportEndpoint.Dispose();
        }
    }
}
