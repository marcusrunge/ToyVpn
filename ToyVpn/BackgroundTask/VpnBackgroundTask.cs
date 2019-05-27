using Windows.ApplicationModel.Background;
using Windows.Networking.Vpn;

namespace BackgroundTask
{
    public sealed class VpnBackgroundTask : IBackgroundTask
    {
        private static IVpnPlugIn _toyVpnPlugin;        
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();
            _toyVpnPlugin = _toyVpnPlugin ?? new ToyVpnPlugin();
            VpnChannel.ProcessEventAsync(_toyVpnPlugin, taskInstance.TriggerDetails);
            deferral.Complete();
        }
    }
}
