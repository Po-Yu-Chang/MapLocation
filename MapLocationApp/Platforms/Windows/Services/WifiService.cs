using MapLocationApp.Services.Interfaces;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;

namespace MapLocationApp.Platforms.Windows.Services;

public sealed class WifiService : IWifiService
{
    public bool ScanSupported => true;

    public async Task<bool> IsWifiEnabledAsync()
    {
        try
        {
            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            return adapters.Count > 0;
        }
        catch { return false; }
    }

    public Task<WifiNetwork?> GetCurrentAsync()
    {
        // ConnectionProfile is the Wi-Fi-agnostic way to read the current SSID without needing
        // the wiFiControl capability prompt. We pair it with WiFiAdapter scan results in ScanAsync
        // for the BSSID, but for "current" alone, the SSID is enough.
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile?.IsWlanConnectionProfile == true)
            {
                var ssid = profile.WlanConnectionProfileDetails?.GetConnectedSsid();
                if (!string.IsNullOrEmpty(ssid))
                {
                    return Task.FromResult<WifiNetwork?>(new WifiNetwork(ssid, null));
                }
            }
        }
        catch { /* fall through */ }

        return Task.FromResult<WifiNetwork?>(null);
    }

    public async Task<IReadOnlyList<WifiNetwork>?> ScanAsync()
    {
        try
        {
            // Capability check — must be called before FindAllAdaptersAsync per WinRT docs.
            var access = await WiFiAdapter.RequestAccessAsync();
            if (access != WiFiAccessStatus.Allowed) return null;

            var adapterInfos = await DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());
            if (adapterInfos.Count == 0) return null;

            var adapter = await WiFiAdapter.FromIdAsync(adapterInfos[0].Id);
            if (adapter == null) return null;

            await adapter.ScanAsync();
            return adapter.NetworkReport.AvailableNetworks
                .Where(n => !string.IsNullOrEmpty(n.Ssid))
                .Select(n => new WifiNetwork(n.Ssid, n.Bssid, (int)n.NetworkRssiInDecibelMilliwatts))
                .ToList();
        }
        catch
        {
            return null;
        }
    }
}
