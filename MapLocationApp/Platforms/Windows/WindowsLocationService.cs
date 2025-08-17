using Windows.Devices.Geolocation;
using MapLocationApp.Services;
using WindowsPositionChangedEventArgs = Windows.Devices.Geolocation.PositionChangedEventArgs;

namespace MapLocationApp.Platforms.Windows;

public class WindowsLocationService : ILocationService
{
    private Geolocator? _geolocator;
    private bool _isTracking = false;

    public event EventHandler<AppLocation>? LocationChanged;

    public async Task<AppLocation?> GetCurrentLocationAsync()
    {
        try
        {
            // 檢查位置服務是否可用
            var accessStatus = await Geolocator.RequestAccessAsync();
            if (accessStatus != GeolocationAccessStatus.Allowed)
            {
                System.Diagnostics.Debug.WriteLine($"位置存取被拒絕: {accessStatus}");
                return null;
            }

            _geolocator ??= new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.High,
                DesiredAccuracyInMeters = 10,
                ReportInterval = 1000 // 1 秒
            };

            var position = await _geolocator.GetGeopositionAsync(
                TimeSpan.FromMinutes(1), // 最大快取時間
                TimeSpan.FromSeconds(10)  // 超時時間
            );

            if (position?.Coordinate != null)
            {
                return new AppLocation
                {
                    Latitude = position.Coordinate.Point.Position.Latitude,
                    Longitude = position.Coordinate.Point.Position.Longitude,
                    Accuracy = position.Coordinate.Accuracy,
                    Altitude = position.Coordinate.Point.Position.Altitude,
                    Speed = position.Coordinate.Speed ?? 0,
                    Course = position.Coordinate.Heading ?? 0,
                    Timestamp = position.Coordinate.Timestamp.DateTime
                };
            }
        }
        catch (UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine("位置存取未授權");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Windows 位置服務錯誤: {ex.Message}");
        }

        return null;
    }

    public async Task<bool> RequestLocationPermissionAsync()
    {
        try
        {
            var accessStatus = await Geolocator.RequestAccessAsync();
            return accessStatus == GeolocationAccessStatus.Allowed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"請求位置權限失敗: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsLocationEnabledAsync()
    {
        try
        {
            var accessStatus = await Geolocator.RequestAccessAsync();
            return accessStatus == GeolocationAccessStatus.Allowed;
        }
        catch
        {
            return false;
        }
    }

    public async Task StartLocationUpdatesAsync()
    {
        if (_isTracking) return;

        try
        {
            var hasPermission = await RequestLocationPermissionAsync();
            if (!hasPermission) return;

            _geolocator ??= new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.High,
                DesiredAccuracyInMeters = 10,
                ReportInterval = 30000 // 30 秒
            };

            _geolocator.PositionChanged += OnPositionChanged;
            _isTracking = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"開始位置追蹤失敗: {ex.Message}");
        }
    }

    public Task StopLocationUpdatesAsync()
    {
        if (_geolocator != null)
        {
            _geolocator.PositionChanged -= OnPositionChanged;
        }
        _isTracking = false;
        return Task.CompletedTask;
    }

    private void OnPositionChanged(Geolocator sender, WindowsPositionChangedEventArgs args)
    {
        try
        {
            var position = args.Position;
            if (position?.Coordinate != null)
            {
                var location = new AppLocation
                {
                    Latitude = position.Coordinate.Point.Position.Latitude,
                    Longitude = position.Coordinate.Point.Position.Longitude,
                    Accuracy = position.Coordinate.Accuracy,
                    Altitude = position.Coordinate.Point.Position.Altitude,
                    Speed = position.Coordinate.Speed ?? 0,
                    Course = position.Coordinate.Heading ?? 0,
                    Timestamp = position.Coordinate.Timestamp.DateTime
                };

                LocationChanged?.Invoke(this, location);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"位置變更事件錯誤: {ex.Message}");
        }
    }
}