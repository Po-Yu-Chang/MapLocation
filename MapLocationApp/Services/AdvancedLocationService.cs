using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Location signal quality enumeration
    /// </summary>
    public enum LocationSignalQuality
    {
        Poor,       // >20m accuracy
        Fair,       // 10-20m accuracy  
        Good,       // 5-10m accuracy
        Excellent   // <5m accuracy
    }

    /// <summary>
    /// Advanced location service interface for high-precision positioning
    /// </summary>
    public interface IAdvancedLocationService : ILocationService
    {
        /// <summary>
        /// Gets high-accuracy location with best possible precision
        /// </summary>
        Task<AppLocation?> GetHighAccuracyLocationAsync();

        /// <summary>
        /// Applies smoothing algorithm to reduce GPS noise
        /// </summary>
        AppLocation SmoothLocation(AppLocation newLocation);

        /// <summary>
        /// Gets current GPS signal quality
        /// </summary>
        LocationSignalQuality GetSignalQuality(AppLocation location);

        /// <summary>
        /// Event fired when location signal quality changes
        /// </summary>
        event EventHandler<LocationSignalQuality> SignalQualityChanged;

        /// <summary>
        /// Gets location history for analysis
        /// </summary>
        IReadOnlyList<AppLocation> LocationHistory { get; }

        /// <summary>
        /// Clears location history
        /// </summary>
        void ClearLocationHistory();
    }

    /// <summary>
    /// Advanced location service implementation with Kalman filtering and signal quality monitoring
    /// </summary>
    public class AdvancedLocationService : IAdvancedLocationService
    {
        private readonly ILocationService _baseLocationService;
        private readonly Queue<AppLocation> _locationHistory = new(20); // Keep last 20 locations
        private AppLocation? _lastKnownLocation;
        private LocationSignalQuality _currentSignalQuality = LocationSignalQuality.Poor;

        // Kalman filter parameters
        private double _processNoise = 0.125; // Process noise covariance
        private double _measurementNoise = 0.8; // Measurement noise covariance
        private double _estimatedError = 1.0; // Estimation error covariance
        private double _kalmanGain = 0.0;

        public event EventHandler<AppLocation>? LocationChanged;
        public event EventHandler<LocationSignalQuality>? SignalQualityChanged;

        public IReadOnlyList<AppLocation> LocationHistory => _locationHistory.ToList().AsReadOnly();

        public AdvancedLocationService(ILocationService baseLocationService)
        {
            _baseLocationService = baseLocationService;
            _baseLocationService.LocationChanged += OnBaseLocationChanged;
        }

        public async Task<AppLocation?> GetCurrentLocationAsync()
        {
            var location = await _baseLocationService.GetCurrentLocationAsync();
            if (location != null)
            {
                location = SmoothLocation(location);
                UpdateSignalQuality(location);
            }
            return location;
        }

        public async Task<AppLocation?> GetHighAccuracyLocationAsync()
        {
            try
            {
                // Request high accuracy location (implementation would use platform-specific APIs)
                var location = await _baseLocationService.GetCurrentLocationAsync();
                
                if (location != null)
                {
                    // Apply additional filtering for high accuracy requests
                    location = SmoothLocation(location);
                    location = ApplyKalmanFilter(location);
                    UpdateSignalQuality(location);
                }

                return location;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"High accuracy location error: {ex.Message}");
                return await GetCurrentLocationAsync(); // Fallback to regular accuracy
            }
        }

        public AppLocation SmoothLocation(AppLocation newLocation)
        {
            if (_locationHistory.Count < 3)
            {
                AddToHistory(newLocation);
                return newLocation;
            }

            // Simple moving average smoothing
            var recentLocations = _locationHistory.TakeLast(3).ToList();
            var avgLatitude = recentLocations.Average(l => l.Latitude);
            var avgLongitude = recentLocations.Average(l => l.Longitude);
            var avgAccuracy = recentLocations.Average(l => l.Accuracy);

            // Weight the new location with the average
            var weight = 0.7; // Give 70% weight to new location, 30% to average
            var smoothedLocation = new AppLocation
            {
                Latitude = newLocation.Latitude * weight + avgLatitude * (1 - weight),
                Longitude = newLocation.Longitude * weight + avgLongitude * (1 - weight),
                Accuracy = newLocation.Accuracy * weight + avgAccuracy * (1 - weight),
                Timestamp = newLocation.Timestamp,
                Altitude = newLocation.Altitude,
                Speed = newLocation.Speed,
                Course = newLocation.Course
            };

            AddToHistory(smoothedLocation);
            return smoothedLocation;
        }

        private AppLocation ApplyKalmanFilter(AppLocation newLocation)
        {
            if (_lastKnownLocation == null)
            {
                _lastKnownLocation = newLocation;
                return newLocation;
            }

            // Simple 1D Kalman filter for latitude and longitude
            var filteredLat = ApplyKalmanFilter1D(_lastKnownLocation.Latitude, newLocation.Latitude, newLocation.Accuracy);
            var filteredLng = ApplyKalmanFilter1D(_lastKnownLocation.Longitude, newLocation.Longitude, newLocation.Accuracy);

            var filteredLocation = new AppLocation
            {
                Latitude = filteredLat,
                Longitude = filteredLng,
                Accuracy = Math.Min(newLocation.Accuracy, _lastKnownLocation.Accuracy * 0.9),
                Timestamp = newLocation.Timestamp,
                Altitude = newLocation.Altitude,
                Speed = newLocation.Speed,
                Course = newLocation.Course
            };

            _lastKnownLocation = filteredLocation;
            return filteredLocation;
        }

        private double ApplyKalmanFilter1D(double lastEstimate, double measurement, double measurementAccuracy)
        {
            // Prediction step
            var predictedEstimate = lastEstimate;
            var predictedError = _estimatedError + _processNoise;

            // Update step
            _kalmanGain = predictedError / (predictedError + measurementAccuracy);
            var estimate = predictedEstimate + _kalmanGain * (measurement - predictedEstimate);
            _estimatedError = (1 - _kalmanGain) * predictedError;

            return estimate;
        }

        public LocationSignalQuality GetSignalQuality(AppLocation location)
        {
            return location.Accuracy switch
            {
                <= 5 => LocationSignalQuality.Excellent,
                <= 10 => LocationSignalQuality.Good,
                <= 20 => LocationSignalQuality.Fair,
                _ => LocationSignalQuality.Poor
            };
        }

        private void UpdateSignalQuality(AppLocation location)
        {
            var newQuality = GetSignalQuality(location);
            if (newQuality != _currentSignalQuality)
            {
                _currentSignalQuality = newQuality;
                SignalQualityChanged?.Invoke(this, newQuality);
            }
        }

        private void AddToHistory(AppLocation location)
        {
            _locationHistory.Enqueue(location);
            
            // Keep only the most recent locations
            while (_locationHistory.Count > 20)
            {
                _locationHistory.Dequeue();
            }
        }

        private void OnBaseLocationChanged(object? sender, AppLocation location)
        {
            var smoothedLocation = SmoothLocation(location);
            UpdateSignalQuality(smoothedLocation);
            LocationChanged?.Invoke(this, smoothedLocation);
        }

        public void ClearLocationHistory()
        {
            _locationHistory.Clear();
            _lastKnownLocation = null;
            _estimatedError = 1.0; // Reset Kalman filter
        }

        // Delegate other interface methods to base service
        public Task<bool> RequestLocationPermissionAsync() => _baseLocationService.RequestLocationPermissionAsync();
        public Task<bool> IsLocationEnabledAsync() => _baseLocationService.IsLocationEnabledAsync();
        public Task StartLocationUpdatesAsync() => _baseLocationService.StartLocationUpdatesAsync();
        public Task StopLocationUpdatesAsync() => _baseLocationService.StopLocationUpdatesAsync();
    }
}