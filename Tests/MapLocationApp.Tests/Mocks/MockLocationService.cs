using MapLocationApp.Services;
using Microsoft.Maui.Devices.Sensors;

namespace MapLocationApp.Tests.Mocks;

/// <summary>
/// Mock implementation of ILocationService for testing GPS scenarios without actual hardware
/// T022: Provides simulated GPS locations for geofence and navigation tests
/// </summary>
public class MockLocationService : ILocationService
{
    private readonly Queue<Location> _locationQueue = new();
    private bool _permissionGranted = true;
    private AppLocation? _currentAppLocation;

    public bool IsListening { get; private set; }

    public event EventHandler<AppLocation>? LocationChanged;

    /// <summary>
    /// Enqueues a location to be returned by GetCurrentLocationAsync()
    /// </summary>
    public void EnqueueLocation(Location location)
    {
        _locationQueue.Enqueue(location);
    }

    /// <summary>
    /// Enqueues multiple locations (simulates GPS movement path)
    /// </summary>
    public void EnqueueLocations(params Location[] locations)
    {
        foreach (var location in locations)
        {
            _locationQueue.Enqueue(location);
        }
    }

    /// <summary>
    /// Sets a fixed location (returned by all GetCurrentLocationAsync calls until changed)
    /// </summary>
    public void SetFixedLocation(Location location)
    {
        _currentAppLocation = new AppLocation
        {
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Accuracy = location.Accuracy ?? 10.0,
            Timestamp = location.Timestamp.UtcDateTime
        };
    }

    /// <summary>
    /// Simulates permission denial for testing permission error scenarios
    /// </summary>
    public void SimulatePermissionDenied()
    {
        _permissionGranted = false;
    }

    /// <summary>
    /// Restores permission grant status
    /// </summary>
    public void RestorePermission()
    {
        _permissionGranted = true;
    }

    public Task<AppLocation?> GetCurrentLocationAsync()
    {
        if (!_permissionGranted)
        {
            return Task.FromResult<AppLocation?>(null);
        }

        // Return fixed location if set
        if (_currentAppLocation != null)
        {
            return Task.FromResult<AppLocation?>(_currentAppLocation);
        }

        // Return queued location if available
        if (_locationQueue.Count > 0)
        {
            var location = _locationQueue.Dequeue();
            _currentAppLocation = new AppLocation
            {
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Accuracy = location.Accuracy ?? 10.0,
                Timestamp = location.Timestamp.UtcDateTime
            };
            return Task.FromResult<AppLocation?>(_currentAppLocation);
        }

        // Default test location (Taipei 101)
        return Task.FromResult<AppLocation?>(new AppLocation
        {
            Latitude = 25.0330,
            Longitude = 121.5654,
            Accuracy = 10.0,
            Timestamp = DateTime.UtcNow
        });
    }

    public Task<bool> RequestLocationPermissionAsync()
    {
        // In mock, permission is controlled by SimulatePermissionDenied/RestorePermission
        return Task.FromResult(_permissionGranted);
    }

    public Task<bool> IsLocationEnabledAsync()
    {
        return Task.FromResult(true);
    }

    public Task StartLocationUpdatesAsync()
    {
        if (!_permissionGranted)
        {
            return Task.CompletedTask;
        }

        IsListening = true;
        return Task.CompletedTask;
    }

    public Task StopLocationUpdatesAsync()
    {
        IsListening = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Simulates a location update event (fires LocationChanged)
    /// Call this in tests to simulate continuous GPS updates
    /// </summary>
    public void SimulateLocationUpdate(Location location)
    {
        var appLocation = new AppLocation
        {
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Accuracy = location.Accuracy ?? 10.0,
            Timestamp = location.Timestamp.UtcDateTime
        };

        _currentAppLocation = appLocation;
        LocationChanged?.Invoke(this, appLocation);
    }

    /// <summary>
    /// Simulates a sequence of location updates (movement path)
    /// Useful for testing navigation and geofence transitions
    /// </summary>
    public async Task SimulateMovementAsync(Location[] path, TimeSpan updateInterval)
    {
        foreach (var location in path)
        {
            SimulateLocationUpdate(location);
            await Task.Delay(updateInterval);
        }
    }

    /// <summary>
    /// Test helper: Creates a linear path between two points (useful for navigation tests)
    /// </summary>
    public static Location[] CreateLinearPath(Location start, Location end, int steps)
    {
        var path = new Location[steps];
        var latStep = (end.Latitude - start.Latitude) / (steps - 1);
        var lonStep = (end.Longitude - start.Longitude) / (steps - 1);

        for (int i = 0; i < steps; i++)
        {
            path[i] = new Location(
                start.Latitude + (latStep * i),
                start.Longitude + (lonStep * i)
            )
            {
                Accuracy = 10.0,
                Timestamp = DateTimeOffset.UtcNow.AddSeconds(i * 5)
            };
        }

        return path;
    }

    /// <summary>
    /// Test helper: Creates a circular path around a center point (useful for geofence tests)
    /// </summary>
    public static Location[] CreateCircularPath(Location center, double radiusMeters, int steps)
    {
        var path = new Location[steps];
        var earthRadiusMeters = 6378137.0;
        var radiusRadians = radiusMeters / earthRadiusMeters;

        for (int i = 0; i < steps; i++)
        {
            var angle = (2 * Math.PI * i) / steps;
            var lat = center.Latitude + (radiusRadians * Math.Sin(angle) * (180 / Math.PI));
            var lon = center.Longitude + (radiusRadians * Math.Cos(angle) * (180 / Math.PI) / Math.Cos(center.Latitude * Math.PI / 180));

            path[i] = new Location(lat, lon)
            {
                Accuracy = 10.0,
                Timestamp = DateTimeOffset.UtcNow.AddSeconds(i * 5)
            };
        }

        return path;
    }

    /// <summary>
    /// Test helper: Reset mock to initial state
    /// </summary>
    public void Reset()
    {
        _locationQueue.Clear();
        _currentAppLocation = null;
        _permissionGranted = true;
        IsListening = false;
    }
}
