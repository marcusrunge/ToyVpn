using Windows.ApplicationModel.Background;
using Windows.Networking.Vpn;

namespace BackgroundTask
{
    public sealed class VpnBackgroundTask : IBackgroundTask
    {
        private static IVpnPlugIn _toyVpnPlugin;
        private static IVpnPlugIn CreateOrGetToyVpnPlugIn => _toyVpnPlugin ?? new ToyVpnPlugin();
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();            
            VpnChannel.ProcessEventAsync(CreateOrGetToyVpnPlugIn, taskInstance.TriggerDetails);
            deferral.Complete();
        }
    }
}
