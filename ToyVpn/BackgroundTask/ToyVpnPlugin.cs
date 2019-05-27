using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
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
            ((ToyVpnPluginContext)channel.PlugInContext).DatagramSocket = new DatagramSocket();
            channel.AssociateTransport(((ToyVpnPluginContext)channel.PlugInContext).DatagramSocket, null);
            string vpnConfig = channel.Configuration.ServerHostNameList[0] + "\n"
                            + "8000\n"
                            + "test\n"
                            + "udp";
            var handshakeParameterPointer = ((ToyVpnPluginContext)channel.PlugInContext).InitializeHandshake(vpnConfig);
            var handshakeParameterStruct = Marshal.PtrToStructure<ToyVpnPluginContext.HANDSHAKE_PARAMETER>(handshakeParameterPointer);
            var remoteHostName = Marshal.PtrToStringAnsi(handshakeParameterStruct.remoteHostNamePtr);
            var remoteServiceName = Marshal.PtrToStringAnsi(handshakeParameterStruct.remoteServiceNamePtr);

            byte[] responseAsBytes = null;

            ((ToyVpnPluginContext)channel.PlugInContext).DatagramSocket.MessageReceived += (s, e) =>
            {
                DataReader dataReader = e.GetDataReader();
                if (dataReader.UnconsumedBufferLength > 0 && dataReader.ReadByte() == 0)
                {
                    var response = dataReader.ReadString(dataReader.UnconsumedBufferLength);
                    responseAsBytes = Encoding.ASCII.GetBytes(response);
                    ((ToyVpnPluginContext)channel.PlugInContext).HandshakeState = HandshakeState.Received;
                }
            };

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

            ((ToyVpnPluginContext)channel.PlugInContext).DatagramSocket.ConnectAsync(new HostName(remoteHostName), remoteServiceName).AsTask().GetAwaiter().GetResult();
            ((ToyVpnPluginContext)channel.PlugInContext).HandshakeState = HandshakeState.Waiting;

            ((ToyVpnPluginContext)channel.PlugInContext).MarshalUnmanagedArrayToManagedArray<byte>(handshakeParameterStruct.bytesToWritePtr, handshakeParameterStruct.bytesToWriteLength, out byte[] bytesToWrite);
            ((ToyVpnPluginContext)channel.PlugInContext).Handshake(((ToyVpnPluginContext)channel.PlugInContext).DatagramSocket, bytesToWrite).AsTask().GetAwaiter().GetResult();
            ((ToyVpnPluginContext)channel.PlugInContext).HandShakeControl().AsTask().GetAwaiter().GetResult();

            
            if (((ToyVpnPluginContext)channel.PlugInContext).HandshakeState == HandshakeState.Received) ((ToyVpnPluginContext)channel.PlugInContext).ConfigureAndConnect(channel, responseAsBytes);
            else channel.Stop();
        }

        public void Disconnect(VpnChannel channel)
        {
            channel.Stop();
            channel.PlugInContext = null;
        }

        public void GetKeepAlivePayload(VpnChannel channel, out VpnPacketBuffer keepAlivePacket)
        {
            keepAlivePacket = new VpnPacketBuffer(null, 0, 0);
        }

        public void Encapsulate(VpnChannel channel, VpnPacketBufferList packets, VpnPacketBufferList encapulatedPackets)
        {
            while (packets.Size > 0)
            {
                var vpnSendPacketBuffer = channel.GetVpnSendPacketBuffer();
                var unencryptedPackets = packets.RemoveAtBegin();
                var unencryptedPacketBuffer = unencryptedPackets.Buffer.ToArray();
                var unencryptedCapsule = new ToyVpnPluginContext.CAPSULE
                {
                    length = unencryptedPacketBuffer.Length
                };
                int size = Marshal.SizeOf(unencryptedPacketBuffer[0]) * unencryptedPacketBuffer.Length;
                unencryptedCapsule.payload = Marshal.AllocHGlobal(size);
                Marshal.Copy(unencryptedPacketBuffer, 0, unencryptedCapsule.payload, unencryptedPacketBuffer.Length);
                IntPtr unencryptedCapsulePtr = Marshal.AllocHGlobal(Marshal.SizeOf(unencryptedCapsule));
                Marshal.StructureToPtr(unencryptedCapsule, unencryptedCapsulePtr, true);
                var encryptedCapsulePtr = ((ToyVpnPluginContext)channel.PlugInContext).Encapsulate(unencryptedCapsulePtr);
                var encryptedCapsule = (ToyVpnPluginContext.CAPSULE)Marshal.PtrToStructure(encryptedCapsulePtr, typeof(ToyVpnPluginContext.CAPSULE));
                ((ToyVpnPluginContext)channel.PlugInContext).MarshalUnmanagedArrayToManagedArray(encryptedCapsule.payload, encryptedCapsule.length, out byte[] encryptedPacketBuffer);
                encryptedPacketBuffer.CopyTo(0, vpnSendPacketBuffer.Buffer, 0, encryptedPacketBuffer.Length);
                encapulatedPackets.Append(vpnSendPacketBuffer);
            }
        }

        public void Decapsulate(VpnChannel channel, VpnPacketBuffer encapBuffer, VpnPacketBufferList decapsulatedPackets, VpnPacketBufferList controlPacketsToSend)
        {
            var vpnReceivePacketBuffer = channel.GetVpnReceivePacketBuffer();
            var encryptedPacketBuffer = encapBuffer.Buffer.ToArray();
            if (encapBuffer.Buffer.Length > vpnReceivePacketBuffer.Buffer.Length) return;
            var encryptedCapsule = new ToyVpnPluginContext.CAPSULE
            {
                length = encryptedPacketBuffer.Length
            };
            int size = Marshal.SizeOf(encryptedPacketBuffer[0]) * encryptedPacketBuffer.Length;
            encryptedCapsule.payload = Marshal.AllocHGlobal(size);
            Marshal.Copy(encryptedPacketBuffer, 0, encryptedCapsule.payload, encryptedPacketBuffer.Length);
            IntPtr encryptedCapsulePtr = Marshal.AllocHGlobal(Marshal.SizeOf(encryptedCapsule));
            Marshal.StructureToPtr(encryptedCapsule, encryptedCapsulePtr, true);
            var unencryptedCapsulePtr = ((ToyVpnPluginContext)channel.PlugInContext).Encapsulate(encryptedCapsulePtr);
            var unencryptedCapsule = (ToyVpnPluginContext.CAPSULE)Marshal.PtrToStructure(unencryptedCapsulePtr, typeof(ToyVpnPluginContext.CAPSULE));
            ((ToyVpnPluginContext)channel.PlugInContext).MarshalUnmanagedArrayToManagedArray(unencryptedCapsule.payload, unencryptedCapsule.length, out byte[] unencryptedPacketBuffer);
            unencryptedPacketBuffer.CopyTo(0, vpnReceivePacketBuffer.Buffer, 0, unencryptedPacketBuffer.Length);
            decapsulatedPackets.Append(vpnReceivePacketBuffer);
        }        
    }
}
