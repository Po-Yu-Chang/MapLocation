using MapLocationApp.Models;

namespace MapLocationApp.Services;

public class GeofenceService : IGeofenceService
{
    private readonly List<GeofenceRegion> _geofences = new();
    private readonly Dictionary<string, bool> _geofenceStates = new(); // true = inside, false = outside
    private readonly ILocationService _locationService;
    private bool _isMonitoring = false;

    public event EventHandler<GeofenceEvent>? GeofenceEntered;
    public event EventHandler<GeofenceEvent>? GeofenceExited;

    public GeofenceService(ILocationService locationService)
    {
        _locationService = locationService;
        _locationService.LocationChanged += OnLocationChanged;
        
        // 加入一些預設的地理圍欄作為範例
        InitializeDefaultGeofences();
    }

    private void InitializeDefaultGeofences()
    {
        // 保留一些基本的地理圍欄作為範例
        // 但不要只限於台北地區
        
        // 工作場所範例（用戶可以自己新增）
        _geofences.Add(new GeofenceRegion
        {
            Id = "workplace-1",
            Name = "我的工作場所",
            Latitude = 25.0339,
            Longitude = 121.5645,
            RadiusMeters = 100,
            Category = "Office",
            Description = "主要工作地點"
        });

        // 初始化狀態
        foreach (var geofence in _geofences)
        {
            _geofenceStates[geofence.Id] = false;
        }
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

    public Task<bool> AddGeofenceAsync(GeofenceRegion geofence)
    {
        try
        {
            _geofences.Add(geofence);
            _geofenceStates[geofence.Id] = false;
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> RemoveGeofenceAsync(string geofenceId)
    {
        try
        {
            var geofence = _geofences.FirstOrDefault(g => g.Id == geofenceId);
            if (geofence != null)
            {
                _geofences.Remove(geofence);
                _geofenceStates.Remove(geofenceId);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<List<GeofenceRegion>> GetGeofencesAsync()
    {
        return Task.FromResult(_geofences.Where(g => g.IsActive).ToList());
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
                        Accuracy = location.Accuracy,
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
                        Accuracy = location.Accuracy,
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
}