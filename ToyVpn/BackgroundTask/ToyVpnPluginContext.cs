using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
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
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CallbackTemplate(int number, IntPtr option);
        private static CallbackTemplate _callbackTemplate;

        [DllImport("ToyVpnManager.dll", EntryPoint = "ExternInitializeCallbackTemplate", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ExternInitializeCallbackTemplate(CallbackTemplate callbackTemplate);

        [DllImport("ToyVpnManager.dll", EntryPoint = "ExternInitializeHandshake", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr ExternInitializeHandshake(string vpnConfig);
        internal IntPtr InitializeHandshake(string vpnConfig) => ExternInitializeHandshake(vpnConfig);

        [DllImport("ToyVpnManager.dll", EntryPoint = "ExternHandleHandshakeResponse", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ExternHandleHandshakeResponse(string response);
        internal IntPtr HandleHandshakeResponse(string response) => ExternHandleHandshakeResponse(response);

        [DllImport("ToyVpnManager.dll", EntryPoint = "ExternEncapsulate", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ExternEncapsulate(IntPtr capsule);
        internal IntPtr Encapsulate(IntPtr capsule) => ExternEncapsulate(capsule);

        [DllImport("ToyVpnManager.dll", EntryPoint = "ExternDecapsulate", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ExternDecapsulate(IntPtr capsule);
        internal IntPtr Decapsulate(IntPtr capsule) => ExternDecapsulate(capsule);

        [StructLayout(LayoutKind.Sequential)]
        internal struct CAPSULE
        {
            public int length;
            public IntPtr payload;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HANDSHAKE_PARAMETER
        {
            public int socketType;
            public IntPtr remoteHostNamePtr;
            public IntPtr remoteServiceNamePtr;
            public IntPtr bytesToWritePtr;
            public int bytesToWriteLength;
        }
        
        internal HandshakeState HandshakeState { get; set; }
        internal DatagramSocket DatagramSocket { get; set; }
        
        internal IAsyncAction Handshake(DatagramSocket datagramSocket, byte[] bytesToWrite)
        {
            return Task.Run(async () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var dataWriter = new DataWriter(datagramSocket.OutputStream)
                    {
                        UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8
                    };
                    dataWriter.WriteByte(0);
                    dataWriter.WriteBytes(bytesToWrite);
                    await dataWriter.StoreAsync();
                    dataWriter.DetachStream();
                }
            }).AsAsyncAction();
        }
        
        internal void ConfigureAndConnect(VpnChannel channel, byte[] responseAsBytes)
        {
            var responseAsString = Encoding.UTF8.GetString(responseAsBytes);
            responseAsString = responseAsString.TrimEnd();
            uint mtuSize = 68;
            var assignedClientIPv4list = new List<HostName>();
            var dnsServerList = new List<HostName>();
            VpnRouteAssignment assignedRoutes = new VpnRouteAssignment();
            VpnDomainNameAssignment assignedDomainName = new VpnDomainNameAssignment();
            var ipv4InclusionRoutes = assignedRoutes.Ipv4InclusionRoutes;
            foreach (var parameter in responseAsString.Split(null))
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
                channel.StartExistingTransports(assignedClientIPv4list, null, null, assignedRoutes, assignedDomainName, mtuSize, mtuSize + 18, false);
            }
            catch (Exception e)
            {
                channel.TerminateConnection(e.Message);
            }
        }

        internal void MarshalUnmanagedArrayToManagedArray<T>(IntPtr unmanagedArray, int length, out T[] managedArray)
        {
            var size = Marshal.SizeOf(typeof(T));
            managedArray = new T[length];
            for (int i = 0; i < length; i++)
            {
                IntPtr intPtr = new IntPtr(unmanagedArray.ToInt64() + i * size);
                managedArray[i] = Marshal.PtrToStructure<T>(intPtr);
            }
        }

        internal void InitiateCallbackTemplate()
        {
            _callbackTemplate = new CallbackTemplate((n, o) =>
            {
                string option = Marshal.PtrToStringAnsi(o);
                return 0;
            });
            ExternInitializeCallbackTemplate(_callbackTemplate);
        }

        internal IAsyncAction HandShakeControl()
        {
            return Task.Run(async () =>
            {
                if (HandshakeState == HandshakeState.Received) return;
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

        internal IAsyncAction HandShake(DatagramSocket datagramSocket, byte[] bytesToWrite)
        {
            return Task.Run(async () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var dataWriter = new DataWriter(datagramSocket.OutputStream)
                    {
                        UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8
                    };
                    dataWriter.WriteByte(0);
                    dataWriter.WriteBytes(bytesToWrite);
                    await dataWriter.StoreAsync();
                    dataWriter.DetachStream();
                }
            }).AsAsyncAction();
        }
    }
}
