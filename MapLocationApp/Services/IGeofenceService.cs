using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp.Services;

public interface IGeofenceService
{
    Task<bool> AddGeofenceAsync(GeofenceRegion geofence);
    Task<bool> RemoveGeofenceAsync(string geofenceId);
    Task<List<GeofenceRegion>> GetGeofencesAsync();
    Task<bool> IsInsideGeofenceAsync(double latitude, double longitude, GeofenceRegion geofence);
    Task StartMonitoringAsync();
    Task StopMonitoringAsync();
    event EventHandler<GeofenceEvent> GeofenceEntered;
    event EventHandler<GeofenceEvent> GeofenceExited;
    
    // 檢查位置是否在任何地理圍欄內
    Task<List<GeofenceRegion>> GetGeofencesContainingLocationAsync(double latitude, double longitude);
    
    // 計算距離（米）
    double CalculateDistance(double lat1, double lon1, double lat2, double lon2);
    
    // 為當前位置建立地理圍欄
    Task<GeofenceRegion?> CreateGeofenceForCurrentLocationAsync(string name, string category = "Custom", int radiusMeters = 100);

    // Test-friendly synchronous point-in-polygon check
    bool CheckPointInGeofence(Microsoft.Maui.Devices.Sensors.Location location, GeofenceRegion geofence);

    // Get all monitored geofences
    List<GeofenceRegion> GetMonitoredGeofences();

    // Manually trigger location update for testing
    Task HandleLocationUpdate(Microsoft.Maui.Devices.Sensors.Location location);

    // Update an existing geofence
    Task<bool> UpdateGeofenceAsync(string id, GeofenceRegion updatedGeofence);

    // Toggle geofence active status
    Task<bool> ToggleGeofenceActiveAsync(string id);

    // T150: Removed SaveGeofenceToDatabaseAsync and LoadGeofencesFromDatabaseAsync
    // Geofences are stored in-memory only. For persistent storage, use AddGeofenceAsync
    // and maintain geofences through application lifecycle or external persistence layer.

    /// <summary>Clears in-memory cache and forces reload from database on next access.</summary>
    void ResetCache();
}