using Windows.ApplicationModel.Background;
using Windows.Networking.Vpn;

namespace BackgroundTask
{
    public sealed class VpnBackgroundTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();
            var vpnPlugIn = new ToyVpnPlugin();
            VpnChannel.ProcessEventAsync(vpnPlugIn, taskInstance.TriggerDetails);
            deferral.Complete();
        }
    }
}
