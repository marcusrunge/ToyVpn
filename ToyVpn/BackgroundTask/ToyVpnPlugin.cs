﻿using System;
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
        public void Connect(VpnChannel channel)
        {
            channel.PlugInContext = new ToyVpnPluginContext();

            string serverPort = "8000";
            string secret = "test";
            string parameters = null;

            var datagramSocket = new DatagramSocket();
            channel.AssociateTransport(datagramSocket, null);

            datagramSocket.MessageReceived += (s, e) =>
            {
                DataReader dataReader = e.GetDataReader();
                if (dataReader.UnconsumedBufferLength > 0 && dataReader.ReadByte() == 0)
                {
                    parameters = dataReader.ReadString(dataReader.UnconsumedBufferLength);
                    ((ToyVpnPluginContext)channel.PlugInContext).HandshakeState = HandshakeState.Received;
                }
            };

            var serverHostName = channel.Configuration.ServerHostNameList[0];

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

            datagramSocket.ConnectAsync(serverHostName, serverPort).AsTask().GetAwaiter().GetResult();
            ((ToyVpnPluginContext)channel.PlugInContext).HandshakeState = HandshakeState.Waiting;
            ((ToyVpnPluginContext)channel.PlugInContext).HandShake(datagramSocket, secret).AsTask().GetAwaiter().GetResult();
            if (((ToyVpnPluginContext)channel.PlugInContext).HandshakeState == HandshakeState.Received) ((ToyVpnPluginContext)channel.PlugInContext).ConfigureAndConnect(channel, parameters);
            else channel.Stop();
        }

        public void Disconnect(VpnChannel channel)
        {
            channel.Stop();
        }

        public void GetKeepAlivePayload(VpnChannel channel, out VpnPacketBuffer keepAlivePacket)
        {
            keepAlivePacket = null;
        }

        public void Encapsulate(VpnChannel channel, VpnPacketBufferList packets, VpnPacketBufferList encapulatedPackets)
        {
            //while (packets.Size > 0) encapulatedPackets.Append(packets.RemoveAtBegin());
            while (packets.Size > 0)
            {                
                var vpnSendPacketBuffer = channel.GetVpnSendPacketBuffer();
                var packet = packets.RemoveAtBegin();
                var packetBuffer = packet.Buffer.ToArray();
                packetBuffer.CopyTo(0, vpnSendPacketBuffer.Buffer, 0, packetBuffer.Length);
                encapulatedPackets.Append(vpnSendPacketBuffer);
            }
        }

        public void Decapsulate(VpnChannel channel, VpnPacketBuffer encapBuffer, VpnPacketBufferList decapsulatedPackets, VpnPacketBufferList controlPacketsToSend)
        {
            //decapsulatedPackets.Append(encapBuffer);
            var vpnReceivePacketBuffer = channel.GetVpnReceivePacketBuffer();
            var packetBuffer = encapBuffer.Buffer.ToArray();
            packetBuffer.CopyTo(0, vpnReceivePacketBuffer.Buffer, 0, packetBuffer.Length);
            decapsulatedPackets.Append(vpnReceivePacketBuffer);
        }        
    }
}
