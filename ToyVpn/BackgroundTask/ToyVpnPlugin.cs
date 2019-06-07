using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Networking.Vpn;
using Windows.Storage.Streams;

namespace BackgroundTask
{

    public sealed class ToyVpnPlugin : IVpnPlugIn
    {
        DatagramSocket _mainOuterTunnelTransport;
        public void Connect(VpnChannel channel)
        {
            channel.PlugInContext = new ToyVpnPluginContext();

            string serverPort = "8000";
            string secret = "test";
            var serverHostName = channel.Configuration.ServerHostNameList[0];



            _mainOuterTunnelTransport = new DatagramSocket();
            channel.AssociateTransport(_mainOuterTunnelTransport, null);
            _mainOuterTunnelTransport.BindEndpointAsync(new HostName("127.0.0.1"), "11884").AsTask().Wait();
            ((ToyVpnPluginContext)channel.PlugInContext).Init(serverHostName, serverPort);
            _mainOuterTunnelTransport.ConnectAsync(new HostName("127.0.0.1"), "11885").AsTask().Wait();

            //XmlDocument xmlDocument = new XmlDocument();
            //xmlDocument.LoadXml(channel.Configuration.CustomField);
            //var firstChild = xmlDocument.FirstChild;
            //if (firstChild.Name.Equals("ToyVpnConfig"))
            //{
            //    foreach (XmlNode childNode in firstChild.ChildNodes)
            //    {
            //        if (childNode.Name.Equals("ServerPort")) serverPort = childNode.InnerText;
            //        else if (childNode.Name.Equals("Secret")) secret = childNode.InnerText;
            //    }
            //}

            ((ToyVpnPluginContext)channel.PlugInContext).HandshakeState = HandshakeState.Waiting;
            ((ToyVpnPluginContext)channel.PlugInContext).HandShake(secret).AsTask().Wait();
            if (((ToyVpnPluginContext)channel.PlugInContext).HandshakeState == HandshakeState.Received) ((ToyVpnPluginContext)channel.PlugInContext).ConfigureAndConnect(channel);
            else channel.Stop();
        }

        public void Disconnect(VpnChannel channel)
        {
            channel.Stop();
            ((ToyVpnPluginContext)channel.PlugInContext).Dispose();
            channel.PlugInContext = null;
        }

        public void GetKeepAlivePayload(VpnChannel channel, out VpnPacketBuffer keepAlivePacket)
        {
            keepAlivePacket = new VpnPacketBuffer(channel.GetVpnSendPacketBuffer(), 0, 0);
        }

        public void Encapsulate(VpnChannel channel, VpnPacketBufferList packets, VpnPacketBufferList encapulatedPackets)
        {
            while (packets.Size > 0)
            {
                var packet = packets.RemoveAtBegin();
                if (packet.Buffer.Capacity <= ushort.MaxValue)
                {
                    var packetBuffer = packet.Buffer.ToArray();
                    ((ToyVpnPluginContext)channel.PlugInContext).EncapsulationConcurrentQueue.Enqueue(packetBuffer);
                }
            }
        }

        public void Decapsulate(VpnChannel channel, VpnPacketBuffer encapBuffer, VpnPacketBufferList decapsulatedPackets, VpnPacketBufferList controlPacketsToSend)
        {
            if (encapBuffer.Buffer.Capacity > ushort.MaxValue) return;
            var packetBuffer = encapBuffer.Buffer.ToArray();
            packetBuffer.CopyTo(0, encapBuffer.Buffer, 0, packetBuffer.Length);
            decapsulatedPackets.Append(encapBuffer);
        }
    }
}
