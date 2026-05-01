using MapLocationApp.Services.Interfaces;

namespace MapLocationApp.Services;

/// <summary>Fallback used when running on a platform without a concrete Wi-Fi implementation.</summary>
public sealed class NullWifiService : IWifiService
{
    public bool ScanSupported => false;
    public Task<WifiNetwork?> GetCurrentAsync() => Task.FromResult<WifiNetwork?>(null);
    public Task<IReadOnlyList<WifiNetwork>?> ScanAsync() => Task.FromResult<IReadOnlyList<WifiNetwork>?>(null);
    public Task<bool> IsWifiEnabledAsync() => Task.FromResult(false);
}
