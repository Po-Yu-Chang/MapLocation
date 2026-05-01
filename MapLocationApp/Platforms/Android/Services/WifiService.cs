using Android.Content;
using Android.Net.Wifi;
using MapLocationApp.Services.Interfaces;

namespace MapLocationApp.Platforms.Android.Services;

public sealed class WifiService : IWifiService
{
    public bool ScanSupported => true;

    private static WifiManager? GetManager()
    {
        var ctx = global::Android.App.Application.Context;
        return ctx.GetSystemService(Context.WifiService) as WifiManager;
    }

    public Task<bool> IsWifiEnabledAsync()
    {
        var mgr = GetManager();
        return Task.FromResult(mgr?.IsWifiEnabled == true);
    }

    public Task<WifiNetwork?> GetCurrentAsync()
    {
        var mgr = GetManager();
        var info = mgr?.ConnectionInfo;
        if (info == null) return Task.FromResult<WifiNetwork?>(null);

        // SSID arrives wrapped in quotes per Android docs; strip them so DB comparisons match.
        var ssid = info.SSID?.Trim('"');
        if (string.IsNullOrEmpty(ssid) || ssid == "<unknown ssid>")
            return Task.FromResult<WifiNetwork?>(null);

        return Task.FromResult<WifiNetwork?>(new WifiNetwork(ssid, info.BSSID, info.Rssi));
    }

    public async Task<IReadOnlyList<WifiNetwork>?> ScanAsync()
    {
        var mgr = GetManager();
        if (mgr == null) return null;

        // Permission is requested by the caller (CheckInPage). Triggering scan from a UI thread
        // is fine; results may be cached for a few seconds by the OS scan throttling policy.
        try { mgr.StartScan(); } catch { /* StartScan deprecated on API 28+; results still populate */ }

        // Brief delay so freshly-triggered scans have a chance to land.
        await Task.Delay(300);

        var results = mgr.ScanResults ?? new List<ScanResult>();
        return results
            .Where(r => !string.IsNullOrEmpty(r.Ssid))
            .Select(r => new WifiNetwork(r.Ssid!, r.Bssid, r.Level))
            .ToList();
    }
}
