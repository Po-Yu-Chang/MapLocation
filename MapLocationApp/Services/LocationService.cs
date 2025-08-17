// using Microsoft.Maui.Authentication.WebAuthenticator; // 移除錯誤的 using

namespace MapLocationApp.Services;

public class LocationService : ILocationService
{
    private bool _isTracking = false;
    private CancellationTokenSource? _cancelTokenSource;

    public event EventHandler<AppLocation>? LocationChanged;

    public async Task<AppLocation?> GetCurrentLocationAsync()
    {
        try
        {
            var request = new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Best,
                Timeout = TimeSpan.FromSeconds(10)
            };

            var location = await Geolocation.Default.GetLocationAsync(request);
            
            if (location != null)
            {
                return new AppLocation
                {
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    Accuracy = location.Accuracy ?? 0,
                    Altitude = location.Altitude,
                    Speed = location.Speed,
                    Course = location.Course,
                    Timestamp = location.Timestamp.DateTime
                };
            }
        }
        catch (Exception ex)
        {
            // 記錄錯誤
            System.Diagnostics.Debug.WriteLine($"位置獲取失敗: {ex.Message}");
        }

        return null;
    }

    public async Task<bool> RequestLocationPermissionAsync()
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            
            if (status != PermissionStatus.Granted)
            {
                // 如果需要背景位置，也要請求
                var backgroundStatus = await Permissions.RequestAsync<Permissions.LocationAlways>();
                return backgroundStatus == PermissionStatus.Granted;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"位置權限請求失敗: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsLocationEnabledAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            return status == PermissionStatus.Granted;
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

            _isTracking = true;
            _cancelTokenSource = new CancellationTokenSource();

            // 開始背景位置追蹤
            _ = Task.Run(async () =>
            {
                while (_isTracking && !_cancelTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var location = await GetCurrentLocationAsync();
                        if (location != null)
                        {
                            LocationChanged?.Invoke(this, location);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(30), _cancelTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"位置更新錯誤: {ex.Message}");
                        await Task.Delay(TimeSpan.FromSeconds(60), _cancelTokenSource.Token);
                    }
                }
            }, _cancelTokenSource.Token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"開始位置追蹤失敗: {ex.Message}");
            _isTracking = false;
        }
    }

    public Task StopLocationUpdatesAsync()
    {
        _isTracking = false;
        _cancelTokenSource?.Cancel();
        _cancelTokenSource?.Dispose();
        _cancelTokenSource = null;
        
        return Task.CompletedTask;
    }
}