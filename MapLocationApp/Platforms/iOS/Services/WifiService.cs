using MapLocationApp.Services.Interfaces;
using NetworkExtension;

namespace MapLocationApp.Platforms.iOS.Services;

public sealed class WifiService : IWifiService
{
    /// <summary>
    /// iOS does not expose a public Wi-Fi scan API; only the currently-connected SSID is
    /// retrievable, and only when the app holds the right entitlements + Info.plist keys.
    /// </summary>
    public bool ScanSupported => false;

    public Task<bool> IsWifiEnabledAsync()
    {
        // No public iOS API exposes the radio toggle state; assume enabled if a SSID is reachable.
        return Task.FromResult(true);
    }

    public async Task<WifiNetwork?> GetCurrentAsync()
    {
        try
        {
            var network = await NEHotspotNetwork.FetchCurrentAsync();
            if (network == null) return null;
            var ssid = network.Ssid;
            if (string.IsNullOrEmpty(ssid)) return null;
            return new WifiNetwork(ssid, network.Bssid);
        }
        catch
        {
            // FetchCurrentAsync requires "Access WiFi Information" entitlement + a Location permission
            // grant on iOS 13+. If either is missing the call throws; we surface as "no current Wi-Fi".
            return null;
        }
    }

    public Task<IReadOnlyList<WifiNetwork>?> ScanAsync()
        => Task.FromResult<IReadOnlyList<WifiNetwork>?>(null);
}
