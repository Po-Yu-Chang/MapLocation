namespace MapLocationApp.Services.Interfaces;

/// <summary>
/// Cross-platform Wi-Fi inspection abstraction. Platform implementations live in
/// Platforms/{Android,iOS,Windows}/Services/WifiService.cs.
/// </summary>
public interface IWifiService
{
    /// <summary>Returns the SSID/BSSID currently connected to, or null if not connected.</summary>
    Task<WifiNetwork?> GetCurrentAsync();

    /// <summary>
    /// Scans visible Wi-Fi networks. Returns null if scanning is not supported on this platform
    /// (notably iOS, which only exposes the current connection).
    /// </summary>
    Task<IReadOnlyList<WifiNetwork>?> ScanAsync();

    /// <summary>True if scanning is supported on this platform; iOS returns false.</summary>
    bool ScanSupported { get; }

    /// <summary>True if Wi-Fi is currently enabled on the device.</summary>
    Task<bool> IsWifiEnabledAsync();
}

/// <summary>Lightweight DTO; intentionally not tied to any platform Wi-Fi type.</summary>
public sealed record WifiNetwork(string Ssid, string? Bssid, int? SignalDbm = null);
