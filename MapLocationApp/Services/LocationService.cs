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
            // 首先檢查權限
            var hasPermission = await RequestLocationPermissionAsync();
            if (!hasPermission)
            {
                System.Diagnostics.Debug.WriteLine("位置權限未授權");
                return null;
            }

            var request = new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Best,
                Timeout = TimeSpan.FromSeconds(30) // 增加超時時間
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
        catch (FeatureNotSupportedException)
        {
            System.Diagnostics.Debug.WriteLine("裝置不支援位置服務");
        }
        catch (FeatureNotEnabledException)
        {
            System.Diagnostics.Debug.WriteLine("位置服務未啟用");
        }
        catch (PermissionException)
        {
            System.Diagnostics.Debug.WriteLine("位置權限被拒絕");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"位置獲取失敗: {ex.Message}");
        }

        return null;
    }

    public async Task<bool> RequestLocationPermissionAsync()
    {
        try
        {
            // 檢查當前權限狀態
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            
            if (status == PermissionStatus.Granted)
                return true;

            if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                // Windows 平台的特殊處理
                System.Diagnostics.Debug.WriteLine("Windows 平台：請在系統設定中啟用位置權限");
                
                // 可以顯示提示讓使用者手動開啟設定
                return false;
            }

            // 請求權限
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            
            if (status != PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine($"位置權限請求失敗: {status}");
                return false;
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
            if (!hasPermission) 
            {
                System.Diagnostics.Debug.WriteLine("無法開始位置追蹤：權限未授權");
                return;
            }

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