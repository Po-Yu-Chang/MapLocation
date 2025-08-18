using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MapLocationApp.Services
{
    /// <summary>
    /// 位置訊號品質
    /// </summary>
    public enum LocationSignalQuality
    {
        Poor,       // 差
        Fair,       // 一般
        Good,       // 良好
        Excellent   // 極佳
    }

    /// <summary>
    /// 位置精度要求
    /// </summary>
    public enum LocationAccuracy
    {
        Low,
        Medium,
        High,
        Best
    }

    /// <summary>
    /// 位置請求配置
    /// </summary>
    public class LocationRequest
    {
        public LocationAccuracy DesiredAccuracy { get; set; }
        public TimeSpan Timeout { get; set; }
        public bool RequestFullAccuracy { get; set; }
    }

    /// <summary>
    /// 進階位置服務，提供高精度定位和位置平滑功能
    /// </summary>
    public class AdvancedLocationService : ILocationService
    {
        private readonly ILocationService _baseLocationService;
        private readonly Queue<AppLocation> _locationHistory = new(10);
        private AppLocation _lastKnownLocation;
        private readonly object _lockObject = new();
        
        // 卡爾曼濾波器參數
        private double _processNoise = 0.1; // 過程噪音
        private double _measurementNoise = 1.0; // 測量噪音
        private double _estimatedError = 1.0; // 估計誤差
        private double _kalmanGain = 0.0; // 卡爾曼增益

        public event EventHandler<AppLocation> LocationChanged;

        public AdvancedLocationService(ILocationService baseLocationService)
        {
            _baseLocationService = baseLocationService ?? throw new ArgumentNullException(nameof(baseLocationService));
            
            // 訂閱基礎位置服務的事件
            _baseLocationService.LocationChanged += OnBaseLocationChanged;
        }

        public async Task<AppLocation?> GetCurrentLocationAsync()
        {
            try
            {
                var location = await _baseLocationService.GetCurrentLocationAsync();
                if (location != null)
                {
                    var smoothedLocation = ApplyLocationSmoothing(location);
                    return smoothedLocation;
                }
                return location;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AdvancedLocationService: 取得位置錯誤 - {ex.Message}");
                throw;
            }
        }

        public async Task<bool> RequestLocationPermissionAsync()
        {
            return await _baseLocationService.RequestLocationPermissionAsync();
        }

        public async Task<bool> IsLocationEnabledAsync()
        {
            return await _baseLocationService.IsLocationEnabledAsync();
        }

        public async Task StartLocationUpdatesAsync()
        {
            await _baseLocationService.StartLocationUpdatesAsync();
        }

        public async Task StopLocationUpdatesAsync()
        {
            await _baseLocationService.StopLocationUpdatesAsync();
        }

        public async Task<LocationRequest> GetHighAccuracyRequestAsync()
        {
            await Task.CompletedTask;
            return new LocationRequest
            {
                DesiredAccuracy = LocationAccuracy.Best,
                Timeout = TimeSpan.FromSeconds(15),
                RequestFullAccuracy = true
            };
        }

        public LocationSignalQuality GetSignalQuality(AppLocation location)
        {
            if (location == null)
                return LocationSignalQuality.Poor;

            // 基於位置精度判斷訊號品質
            var accuracy = location.Accuracy ?? double.MaxValue;
            
            return accuracy switch
            {
                <= 5 => LocationSignalQuality.Excellent,
                <= 10 => LocationSignalQuality.Good,
                <= 20 => LocationSignalQuality.Fair,
                _ => LocationSignalQuality.Poor
            };
        }

        public AppLocation ApplyLocationSmoothing(AppLocation newLocation)
        {
            if (newLocation == null)
                return null;

            lock (_lockObject)
            {
                // 如果歷史記錄不足，直接返回新位置
                if (_locationHistory.Count < 3)
                {
                    AddToHistory(newLocation);
                    _lastKnownLocation = newLocation;
                    return newLocation;
                }

                // 檢查位置跳躍
                if (IsLocationJump(newLocation))
                {
                    Debug.WriteLine("檢測到位置跳躍，使用上一個已知位置");
                    return _lastKnownLocation;
                }

                // 應用卡爾曼濾波器
                var filteredLocation = ApplyKalmanFilter(newLocation);
                
                AddToHistory(filteredLocation);
                _lastKnownLocation = filteredLocation;
                
                return filteredLocation;
            }
        }

        private void AddToHistory(AppLocation location)
        {
            if (_locationHistory.Count >= 10)
            {
                _locationHistory.Dequeue();
            }
            _locationHistory.Enqueue(location);
        }

        private bool IsLocationJump(AppLocation newLocation)
        {
            if (_lastKnownLocation == null)
                return false;

            // 計算距離變化
            var distance = CalculateDistance(
                _lastKnownLocation.Latitude, _lastKnownLocation.Longitude,
                newLocation.Latitude, newLocation.Longitude);

            // 計算時間差
            var timeDiff = (newLocation.Timestamp - _lastKnownLocation.Timestamp).TotalSeconds;
            
            // 如果移動速度過快（超過200公里/小時），視為位置跳躍
            if (timeDiff > 0)
            {
                var speed = (distance / timeDiff) * 3.6; // 轉換為公里/小時
                return speed > 200;
            }

            return false;
        }

        private AppLocation ApplyKalmanFilter(AppLocation newLocation)
        {
            if (_lastKnownLocation == null)
                return newLocation;

            try
            {
                // 預測階段
                _estimatedError += _processNoise;

                // 更新階段
                _kalmanGain = _estimatedError / (_estimatedError + _measurementNoise);

                // 應用濾波到緯度
                var filteredLatitude = _lastKnownLocation.Latitude + 
                    _kalmanGain * (newLocation.Latitude - _lastKnownLocation.Latitude);

                // 應用濾波到經度
                var filteredLongitude = _lastKnownLocation.Longitude + 
                    _kalmanGain * (newLocation.Longitude - _lastKnownLocation.Longitude);

                // 更新估計誤差
                _estimatedError = (1 - _kalmanGain) * _estimatedError;

                return new AppLocation
                {
                    Latitude = filteredLatitude,
                    Longitude = filteredLongitude,
                    Altitude = newLocation.Altitude,
                    Accuracy = newLocation.Accuracy,
                    Heading = newLocation.Heading,
                    Speed = newLocation.Speed,
                    Timestamp = newLocation.Timestamp
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"卡爾曼濾波器錯誤: {ex.Message}");
                return newLocation;
            }
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // 地球半徑（公尺）
            
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        private void OnBaseLocationChanged(object sender, AppLocation location)
        {
            try
            {
                var smoothedLocation = ApplyLocationSmoothing(location);
                if (smoothedLocation != null)
                {
                    LocationChanged?.Invoke(this, smoothedLocation);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"位置變更處理錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 取得位置統計資訊
        /// </summary>
        public LocationStatistics GetLocationStatistics()
        {
            lock (_lockObject)
            {
                if (_locationHistory.Count == 0)
                    return new LocationStatistics();

                var accuracies = _locationHistory.Where(l => l.Accuracy.HasValue).Select(l => l.Accuracy.Value);
                var speeds = _locationHistory.Where(l => l.Speed.HasValue).Select(l => l.Speed.Value);

                return new LocationStatistics
                {
                    SampleCount = _locationHistory.Count,
                    AverageAccuracy = accuracies.Any() ? accuracies.Average() : 0,
                    MinAccuracy = accuracies.Any() ? accuracies.Min() : 0,
                    MaxAccuracy = accuracies.Any() ? accuracies.Max() : 0,
                    AverageSpeed = speeds.Any() ? speeds.Average() : 0,
                    CurrentSignalQuality = GetSignalQuality(_lastKnownLocation)
                };
            }
        }

        /// <summary>
        /// 重設位置歷史和濾波器
        /// </summary>
        public void ResetLocationHistory()
        {
            lock (_lockObject)
            {
                _locationHistory.Clear();
                _lastKnownLocation = null;
                _estimatedError = 1.0;
                _kalmanGain = 0.0;
            }
        }

        /// <summary>
        /// 調整濾波器參數
        /// </summary>
        public void ConfigureFilter(double processNoise, double measurementNoise)
        {
            _processNoise = Math.Max(0.01, processNoise);
            _measurementNoise = Math.Max(0.1, measurementNoise);
        }

        public void Dispose()
        {
            if (_baseLocationService != null)
            {
                _baseLocationService.LocationChanged -= OnBaseLocationChanged;
            }
        }
    }

    /// <summary>
    /// 位置統計資訊
    /// </summary>
    public class LocationStatistics
    {
        public int SampleCount { get; set; }
        public double AverageAccuracy { get; set; }
        public double MinAccuracy { get; set; }
        public double MaxAccuracy { get; set; }
        public double AverageSpeed { get; set; }
        public LocationSignalQuality CurrentSignalQuality { get; set; }
    }
}