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
        // 台北101 區域（範例）
        _geofences.Add(new GeofenceRegion
        {
            Id = "taipei-101",
            Name = "台北101辦公區",
            Latitude = 25.0339,
            Longitude = 121.5645,
            RadiusMeters = 200,
            Category = "Office",
            Description = "台北101商業區域"
        });

        // 台北車站區域（範例）
        _geofences.Add(new GeofenceRegion
        {
            Id = "taipei-station",
            Name = "台北車站",
            Latitude = 25.0478,
            Longitude = 121.5170,
            RadiusMeters = 300,
            Category = "Transport",
            Description = "台北車站交通樞紐"
        });

        // 初始化狀態
        foreach (var geofence in _geofences)
        {
            _geofenceStates[geofence.Id] = false;
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