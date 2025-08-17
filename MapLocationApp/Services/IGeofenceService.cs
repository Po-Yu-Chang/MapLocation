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
}