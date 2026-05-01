using MapLocationApp.Models;

namespace MapLocationApp.Services;

public class GeofenceService : IGeofenceService
{
    private readonly List<GeofenceRegion> _geofences = new();
    private readonly Dictionary<string, bool> _geofenceStates = new(); // true = inside, false = outside
    private readonly ILocationService _locationService;
    private readonly IDatabaseService? _databaseService;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _isLoaded = false;
    private bool _isMonitoring = false;

    public event EventHandler<GeofenceEvent>? GeofenceEntered;
    public event EventHandler<GeofenceEvent>? GeofenceExited;

    public GeofenceService(ILocationService locationService, IDatabaseService? databaseService = null)
    {
        _locationService = locationService;
        _databaseService = databaseService;
        _locationService.LocationChanged += OnLocationChanged;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;
        await _loadLock.WaitAsync();
        try
        {
            if (_isLoaded) return;

            if (_databaseService != null)
            {
                try
                {
                    var dbGeofences = await _databaseService.GetAllGeofencesAsync();
                    _geofences.Clear();
                    _geofenceStates.Clear();
                    if (dbGeofences != null)
                    {
                        foreach (var g in dbGeofences)
                        {
                            _geofences.Add(g);
                            _geofenceStates[g.Id] = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"從資料庫載入地理圍欄失敗: {ex.Message}");
                }
            }
            _isLoaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private void InitializeDefaultGeofences()
    {
        // 不再預先建立 hard-coded 工作場所
        // 使用者請透過「打卡地點管理」頁面自行新增
    }
    
    // 新增：為當前位置建立地理圍欄
    public async Task<GeofenceRegion?> CreateGeofenceForCurrentLocationAsync(string name, string category = "Custom", int radiusMeters = 100)
    {
        try
        {
            var location = await _locationService.GetCurrentLocationAsync();
            if (location == null)
                return null;
                
            var geofence = new GeofenceRegion
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                RadiusMeters = radiusMeters,
                Category = category,
                Description = $"於 {DateTime.Now:yyyy-MM-dd HH:mm} 建立"
            };
            
            await AddGeofenceAsync(geofence);
            return geofence;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> AddGeofenceAsync(GeofenceRegion geofence)
    {
        await EnsureLoadedAsync();
        try
        {
            // T025: Validate radius
            if (geofence.RadiusMeters <= 0)
            {
                throw new ArgumentException("Geofence radius must be greater than 0", nameof(geofence));
            }

            // T025: Check for duplicate ID
            if (_geofences.Any(g => g.Id == geofence.Id))
            {
                return false;
            }

            // Save to DB first; only add to memory if persist succeeds (or no DB configured)
            if (_databaseService != null)
            {
                var saved = await _databaseService.SaveGeofenceAsync(geofence);
                if (!saved)
                    return false;
            }

            _geofences.Add(geofence);
            _geofenceStates[geofence.Id] = false;
            return true;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddGeofenceAsync 失敗: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> RemoveGeofenceAsync(string geofenceId)
    {
        await EnsureLoadedAsync();
        try
        {
            var geofence = _geofences.FirstOrDefault(g => g.Id == geofenceId);
            if (geofence != null)
            {
                _geofences.Remove(geofence);
                _geofenceStates.Remove(geofenceId);

                if (_databaseService != null)
                {
                    try { await _databaseService.DeleteGeofenceAsync(geofenceId); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"從資料庫刪除地理圍欄失敗: {ex.Message}"); }
                }
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateGeofenceAsync(string id, GeofenceRegion updatedGeofence)
    {
        await EnsureLoadedAsync();
        try
        {
            var index = _geofences.FindIndex(g => g.Id == id);
            if (index >= 0)
            {
                updatedGeofence.Id = id;
                var original = _geofences[index];

                if (_databaseService != null)
                {
                    var updated = await _databaseService.UpdateGeofenceAsync(updatedGeofence);
                    if (!updated)
                        return false;
                }

                _geofences[index] = updatedGeofence;
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateGeofenceAsync 失敗: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> ToggleGeofenceActiveAsync(string id)
    {
        await EnsureLoadedAsync();
        try
        {
            var geofence = _geofences.FirstOrDefault(g => g.Id == id);
            if (geofence != null)
            {
                geofence.IsActive = !geofence.IsActive;
                if (_databaseService != null)
                {
                    try { await _databaseService.UpdateGeofenceAsync(geofence); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"切換啟用狀態到資料庫失敗: {ex.Message}"); }
                }
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    // T150: Removed SaveGeofenceToDatabaseAsync and LoadGeofencesFromDatabaseAsync
    // Geofences are managed in-memory. Use AddGeofenceAsync for adding new geofences.

    public void ResetCache()
    {
        _isLoaded = false;
        _geofences.Clear();
        _geofenceStates.Clear();
    }

    public bool MatchesWifi(GeofenceRegion geofence, string? ssid, string? bssid)
    {
        if (!geofence.IsWifiBased || string.IsNullOrEmpty(ssid)) return false;
        if (!string.Equals(geofence.Ssid, ssid, StringComparison.Ordinal)) return false;
        // BSSID is optional: if the geofence specifies one, the connection must match it; otherwise SSID alone is enough.
        if (!string.IsNullOrEmpty(geofence.Bssid))
        {
            return string.Equals(geofence.Bssid, bssid, StringComparison.OrdinalIgnoreCase);
        }
        return true;
    }

    public async Task<List<GeofenceRegion>> GetGeofencesMatchingWifiAsync(string? ssid, string? bssid)
    {
        await EnsureLoadedAsync();
        if (string.IsNullOrEmpty(ssid)) return new List<GeofenceRegion>();
        return _geofences.Where(g => g.IsActive && MatchesWifi(g, ssid, bssid)).ToList();
    }

    public async Task<List<GeofenceRegion>> GetGeofencesAsync()
    {
        await EnsureLoadedAsync();
        return _geofences.Where(g => g.IsActive).ToList();
    }

    public Task<bool> IsInsideGeofenceAsync(double latitude, double longitude, GeofenceRegion geofence)
    {
        var distance = CalculateDistance(latitude, longitude, geofence.Latitude, geofence.Longitude);
        return Task.FromResult(distance <= geofence.RadiusMeters);
    }

    public async Task<List<GeofenceRegion>> GetGeofencesContainingLocationAsync(double latitude, double longitude)
    {
        var containingGeofences = new List<GeofenceRegion>();
        
        foreach (var geofence in _geofences.Where(g => g.IsActive))
        {
            if (await IsInsideGeofenceAsync(latitude, longitude, geofence))
            {
                containingGeofences.Add(geofence);
            }
        }
        
        return containingGeofences;
    }

    public async Task StartMonitoringAsync()
    {
        if (_isMonitoring) return;
        
        _isMonitoring = true;
        await _locationService.StartLocationUpdatesAsync();
    }

    public async Task StopMonitoringAsync()
    {
        _isMonitoring = false;
        await _locationService.StopLocationUpdatesAsync();
    }

    private async void OnLocationChanged(object? sender, AppLocation location)
    {
        if (!_isMonitoring) return;

        foreach (var geofence in _geofences.Where(g => g.IsActive))
        {
            var isInside = await IsInsideGeofenceAsync(location.Latitude, location.Longitude, geofence);
            var wasInside = _geofenceStates.GetValueOrDefault(geofence.Id, false);

            if (isInside && !wasInside)
            {
                // 進入地理圍欄
                _geofenceStates[geofence.Id] = true;
                
                if (geofence.TransitionType == GeofenceTransitionType.Enter || 
                    geofence.TransitionType == GeofenceTransitionType.Both)
                {
                    var geofenceEvent = new GeofenceEvent
                    {
                        GeofenceId = geofence.Id,
                        GeofenceName = geofence.Name,
                        TransitionType = GeofenceTransitionType.Enter,
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        Accuracy = location.Accuracy ?? 0,
                        Timestamp = DateTime.UtcNow
                    };

                    GeofenceEntered?.Invoke(this, geofenceEvent);
                }
            }
            else if (!isInside && wasInside)
            {
                // 離開地理圍欄
                _geofenceStates[geofence.Id] = false;
                
                if (geofence.TransitionType == GeofenceTransitionType.Exit || 
                    geofence.TransitionType == GeofenceTransitionType.Both)
                {
                    var geofenceEvent = new GeofenceEvent
                    {
                        GeofenceId = geofence.Id,
                        GeofenceName = geofence.Name,
                        TransitionType = GeofenceTransitionType.Exit,
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        Accuracy = location.Accuracy ?? 0,
                        Timestamp = DateTime.UtcNow
                    };

                    GeofenceExited?.Invoke(this, geofenceEvent);
                }
            }
        }
    }

    public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // 地球半徑（米）
        
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return R * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    // Test-friendly synchronous point-in-polygon check (T024)
    public bool CheckPointInGeofence(Microsoft.Maui.Devices.Sensors.Location location, GeofenceRegion geofence)
    {
        if (!geofence.IsActive)
            return false;

        var distance = CalculateDistance(location.Latitude, location.Longitude, geofence.Latitude, geofence.Longitude);
        // T024: Use small tolerance (1m) for boundary points to account for floating point precision
        return distance <= geofence.RadiusMeters + 1.0;
    }

    // Get all monitored geofences (T025)
    public List<GeofenceRegion> GetMonitoredGeofences()
    {
        return _geofences.ToList();
    }

    // Manually trigger location update for testing (T028)
    public async Task HandleLocationUpdate(Microsoft.Maui.Devices.Sensors.Location location)
    {
        var appLocation = new AppLocation
        {
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Accuracy = location.Accuracy
        };

        OnLocationChanged(this, appLocation);
        await Task.CompletedTask;
    }
}