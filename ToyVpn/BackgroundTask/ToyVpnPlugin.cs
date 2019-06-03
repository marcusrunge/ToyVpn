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
            Marshal.FreeCoTaskMem(handshakeParameterStruct.remoteHostNamePtr);
            Marshal.FreeCoTaskMem(handshakeParameterStruct.remoteServiceNamePtr);

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

            ((ToyVpnPluginContext)channel.PlugInContext).DatagramSocket.ConnectAsync(new HostName(remoteHostName), remoteServiceName).AsTask().Wait();
            ((ToyVpnPluginContext)channel.PlugInContext).HandshakeState = HandshakeState.Waiting;

            ((ToyVpnPluginContext)channel.PlugInContext).MarshalUnmanagedArrayToManagedArray<byte>(handshakeParameterStruct.bytesToWritePtr, handshakeParameterStruct.bytesToWriteLength, out byte[] bytesToWrite);
            Marshal.FreeCoTaskMem(handshakeParameterStruct.bytesToWritePtr);
            Marshal.FreeCoTaskMem(handshakeParameterPointer);
            ((ToyVpnPluginContext)channel.PlugInContext).Handshake(((ToyVpnPluginContext)channel.PlugInContext).DatagramSocket, bytesToWrite).AsTask().Wait();
            ((ToyVpnPluginContext)channel.PlugInContext).HandShakeControl().AsTask().Wait();


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
            keepAlivePacket = new VpnPacketBuffer(channel.GetVpnSendPacketBuffer(), 0, 0);
        }

        public unsafe void Encapsulate(VpnChannel channel, VpnPacketBufferList packets, VpnPacketBufferList encapulatedPackets)
        {
            while (packets.Size > 0)
            {
                var packet = packets.RemoveAtBegin();
                if (packet.Buffer.Capacity <= short.MaxValue && (ToyVpnPluginContext)channel.PlugInContext != null)
                {
                    var packetBuffer = packet.Buffer.ToArray();
                    ToyVpnPluginContext.CAPSULE* unencryptedCapsule = (ToyVpnPluginContext.CAPSULE*)Marshal.AllocHGlobal(sizeof(ToyVpnPluginContext.CAPSULE)).ToPointer();
                    unencryptedCapsule->length = packetBuffer.Length;
                    unencryptedCapsule->payload = (byte*)Marshal.AllocHGlobal(packetBuffer.Length * sizeof(byte)).ToPointer();
                    IntPtr packetBufferPtr = Marshal.AllocHGlobal(packetBuffer.Length * sizeof(byte));
                    Marshal.Copy(packetBuffer, 0, packetBufferPtr, packetBuffer.Length);
                    unencryptedCapsule->payload = (byte*)packetBufferPtr.ToPointer();
                    var encryptedCapsulePtr = ((ToyVpnPluginContext)channel.PlugInContext).Encapsulate(ref unencryptedCapsule);
                    var encryptedCapsule = (ToyVpnPluginContext.CAPSULE)Marshal.PtrToStructure(encryptedCapsulePtr, typeof(ToyVpnPluginContext.CAPSULE));
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("Encapsulate:");
#endif
                    for (int i = 0; i < packetBuffer.Length; i++)
                    {
                        packetBuffer[i] = encryptedCapsule.payload[i];
#if DEBUG
                        System.Diagnostics.Debug.Write(packetBuffer[i]);
#endif
                    }
#if DEBUG
                    System.Diagnostics.Debug.WriteLine(null);
#endif
                    Marshal.FreeHGlobal(new IntPtr(unencryptedCapsule));
                    var vpnPacketBuffer = new VpnPacketBuffer(packet, 0, packet.Buffer.Length);
                    packetBuffer.CopyTo(0, vpnPacketBuffer.Buffer, 0, packetBuffer.Length);
                    encapulatedPackets.Append(vpnPacketBuffer);
                }
            }
        }

        public unsafe void Decapsulate(VpnChannel channel, VpnPacketBuffer encapBuffer, VpnPacketBufferList decapsulatedPackets, VpnPacketBufferList controlPacketsToSend)
        {
            var encapBufferBuffer = encapBuffer.Buffer.ToArray();
            if (encapBuffer.Buffer.Length > short.MaxValue || (ToyVpnPluginContext)channel.PlugInContext == null) return;
            ToyVpnPluginContext.CAPSULE* encryptedCapsule = (ToyVpnPluginContext.CAPSULE*)Marshal.AllocHGlobal(sizeof(ToyVpnPluginContext.CAPSULE)).ToPointer();
            encryptedCapsule->length = encapBufferBuffer.Length;
            encryptedCapsule->payload = (byte*)Marshal.AllocHGlobal(encapBufferBuffer.Length * sizeof(byte)).ToPointer();
            IntPtr encapBufferBufferPtr = Marshal.AllocHGlobal(encapBufferBuffer.Length * sizeof(byte));
            Marshal.Copy(encapBufferBuffer, 0, encapBufferBufferPtr, encapBufferBuffer.Length);
            encryptedCapsule->payload = (byte*)encapBufferBufferPtr.ToPointer();
            var unencryptedCapsulePtr = ((ToyVpnPluginContext)channel.PlugInContext).Decapsulate(ref encryptedCapsule);
            var unencryptedCapsule = (ToyVpnPluginContext.CAPSULE)Marshal.PtrToStructure(unencryptedCapsulePtr, typeof(ToyVpnPluginContext.CAPSULE));
#if DEBUG
            System.Diagnostics.Debug.WriteLine("Decapsulate:");
#endif
            for (int i = 0; i < encapBufferBuffer.Length; i++)
            {
                encapBufferBuffer[i] = unencryptedCapsule.payload[i];
#if DEBUG
                System.Diagnostics.Debug.Write(encapBufferBuffer[i]);
#endif
            }
#if DEBUG
            System.Diagnostics.Debug.WriteLine(null);
#endif
            Marshal.FreeHGlobal(new IntPtr(encryptedCapsule));
            var vpnPacketBuffer = new VpnPacketBuffer(encapBuffer, 0, encapBuffer.Buffer.Length);
            encapBufferBuffer.CopyTo(0, vpnPacketBuffer.Buffer, 0, encapBufferBuffer.Length);
            decapsulatedPackets.Append(vpnPacketBuffer);
        }
    }
}
