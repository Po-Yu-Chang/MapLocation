using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Enhanced location service interface with high-precision tracking capabilities
    /// </summary>
    public interface IAdvancedLocationService : ILocationService
    {
        /// <summary>
        /// Gets current location with enhanced precision settings
        /// </summary>
        Task<AppLocation> GetHighPrecisionLocationAsync();

        /// <summary>
        /// Gets location signal quality assessment
        /// </summary>
        LocationSignalQuality GetSignalQuality(AppLocation location);

        /// <summary>
        /// Applies location smoothing using historical data
        /// </summary>
        AppLocation SmoothLocation(AppLocation newLocation);

        /// <summary>
        /// Gets the accuracy of the last known location in meters
        /// </summary>
        double LastKnownAccuracy { get; }

        /// <summary>
        /// Event fired when location accuracy changes significantly
        /// </summary>
        event EventHandler<LocationAccuracyChangedEventArgs> AccuracyChanged;
    }

    /// <summary>
    /// Enhanced location service implementation with high-precision tracking
    /// Based on the requirements document specifications
    /// </summary>
    public class AdvancedLocationService : IAdvancedLocationService
    {
        private readonly Queue<AppLocation> _locationHistory = new(10);
        private bool _isTracking = false;
        private CancellationTokenSource _cancelTokenSource;
        private AppLocation _lastKnownLocation;
        private double _lastKnownAccuracy = 0;

        public double LastKnownAccuracy => _lastKnownAccuracy;

        public event EventHandler<AppLocation> LocationChanged;
        public event EventHandler<LocationAccuracyChangedEventArgs> AccuracyChanged;

        public async Task<AppLocation> GetCurrentLocationAsync()
        {
            try
            {
                // Check permissions first
                var hasPermission = await RequestLocationPermissionAsync();
                if (!hasPermission)
                {
                    System.Diagnostics.Debug.WriteLine("Location permission not granted");
                    return null;
                }

                var request = new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Best,
                    Timeout = TimeSpan.FromSeconds(30)
                };

                var location = await Geolocation.Default.GetLocationAsync(request);
                
                if (location != null)
                {
                    var appLocation = new AppLocation
                    {
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        Accuracy = location.Accuracy ?? 0,
                        Altitude = location.Altitude,
                        Speed = location.Speed,
                        Course = location.Course,
                        Timestamp = location.Timestamp.DateTime
                    };

                    // Apply smoothing if we have history
                    if (_locationHistory.Count > 0)
                    {
                        appLocation = SmoothLocation(appLocation);
                    }

                    // Update history
                    UpdateLocationHistory(appLocation);

                    return appLocation;
                }
            }
            catch (FeatureNotSupportedException)
            {
                System.Diagnostics.Debug.WriteLine("Location service not supported on device");
            }
            catch (FeatureNotEnabledException)
            {
                System.Diagnostics.Debug.WriteLine("Location service not enabled");
            }
            catch (PermissionException)
            {
                System.Diagnostics.Debug.WriteLine("Location permission denied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Location service error: {ex.Message}");
            }

            return null;
        }

        public async Task<AppLocation> GetHighPrecisionLocationAsync()
        {
            try
            {
                var hasPermission = await RequestLocationPermissionAsync();
                if (!hasPermission)
                {
                    System.Diagnostics.Debug.WriteLine("Location permission not granted for high precision");
                    return null;
                }

                // High precision request settings
                var request = new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Best,
                    Timeout = TimeSpan.FromSeconds(60), // Longer timeout for better accuracy
                    RequestFullAccuracy = true // Request the highest accuracy available
                };

                var location = await Geolocation.Default.GetLocationAsync(request);
                
                if (location != null)
                {
                    var appLocation = new AppLocation
                    {
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        Accuracy = location.Accuracy ?? 0,
                        Altitude = location.Altitude,
                        Speed = location.Speed,
                        Course = location.Course,
                        Timestamp = location.Timestamp.DateTime
                    };

                    // Apply advanced smoothing for high precision
                    if (_locationHistory.Count >= 3)
                    {
                        appLocation = ApplyKalmanFilter(appLocation, _locationHistory.ToArray());
                    }

                    UpdateLocationHistory(appLocation);

                    return appLocation;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"High precision location error: {ex.Message}");
            }

            return null;
        }

        public LocationSignalQuality GetSignalQuality(AppLocation location)
        {
            if (location == null)
                return LocationSignalQuality.NoSignal;

            // Assess signal quality based on accuracy
            return location.Accuracy switch
            {
                <= 5 => LocationSignalQuality.Excellent,
                <= 10 => LocationSignalQuality.Good,
                <= 20 => LocationSignalQuality.Fair,
                <= 50 => LocationSignalQuality.Poor,
                _ => LocationSignalQuality.VeryPoor
            };
        }

        public AppLocation SmoothLocation(AppLocation newLocation)
        {
            if (newLocation == null || _locationHistory.Count < 2)
            {
                return newLocation;
            }

            // Simple weighted average smoothing
            var recentLocations = _locationHistory.TakeLast(3).ToArray();
            var weightedLat = 0.0;
            var weightedLng = 0.0;
            var totalWeight = 0.0;

            // Give more weight to more accurate and recent locations
            for (int i = 0; i < recentLocations.Length; i++)
            {
                var loc = recentLocations[i];
                var ageWeight = (i + 1) / (double)recentLocations.Length; // More recent = higher weight
                var accuracyWeight = Math.Max(0.1, 1.0 / Math.Max(1.0, loc.Accuracy)); // More accurate = higher weight
                var weight = ageWeight * accuracyWeight;

                weightedLat += loc.Latitude * weight;
                weightedLng += loc.Longitude * weight;
                totalWeight += weight;
            }

            // Include new location with highest weight
            var newWeight = 2.0 / Math.Max(1.0, newLocation.Accuracy);
            weightedLat += newLocation.Latitude * newWeight;
            weightedLng += newLocation.Longitude * newWeight;
            totalWeight += newWeight;

            // Create smoothed location
            var smoothedLocation = new AppLocation
            {
                Latitude = weightedLat / totalWeight,
                Longitude = weightedLng / totalWeight,
                Accuracy = newLocation.Accuracy, // Keep original accuracy for reference
                Altitude = newLocation.Altitude,
                Speed = newLocation.Speed,
                Course = newLocation.Course,
                Timestamp = newLocation.Timestamp
            };

            return smoothedLocation;
        }

        public async Task<bool> RequestLocationPermissionAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                return status == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Location permission error: {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine("Cannot start location tracking: permission denied");
                    return;
                }

                _isTracking = true;
                _cancelTokenSource = new CancellationTokenSource();

                // Start background location tracking with enhanced precision
                _ = Task.Run(async () =>
                {
                    while (_isTracking && !_cancelTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var location = await GetHighPrecisionLocationAsync();
                            if (location != null)
                            {
                                // Check for significant accuracy changes
                                var previousAccuracy = _lastKnownAccuracy;
                                _lastKnownAccuracy = location.Accuracy;

                                if (Math.Abs(previousAccuracy - location.Accuracy) > 10) // 10 meter threshold
                                {
                                    AccuracyChanged?.Invoke(this, new LocationAccuracyChangedEventArgs
                                    {
                                        PreviousAccuracy = previousAccuracy,
                                        NewAccuracy = location.Accuracy,
                                        SignalQuality = GetSignalQuality(location)
                                    });
                                }

                                _lastKnownLocation = location;
                                LocationChanged?.Invoke(this, location);

                                System.Diagnostics.Debug.WriteLine(
                                    $"Location update: {location.Latitude:F6}, {location.Longitude:F6} " +
                                    $"(±{location.Accuracy:F1}m) - {GetSignalQuality(location)}");
                            }

                            // Adaptive update interval based on signal quality
                            var updateInterval = GetSignalQuality(_lastKnownLocation) switch
                            {
                                LocationSignalQuality.Excellent => TimeSpan.FromSeconds(5),
                                LocationSignalQuality.Good => TimeSpan.FromSeconds(10),
                                LocationSignalQuality.Fair => TimeSpan.FromSeconds(15),
                                LocationSignalQuality.Poor => TimeSpan.FromSeconds(30),
                                _ => TimeSpan.FromSeconds(60)
                            };

                            await Task.Delay(updateInterval, _cancelTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Location tracking error: {ex.Message}");
                            await Task.Delay(TimeSpan.FromSeconds(30), _cancelTokenSource.Token);
                        }
                    }
                }, _cancelTokenSource.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start location tracking: {ex.Message}");
                _isTracking = false;
            }
        }

        public Task StopLocationUpdatesAsync()
        {
            _isTracking = false;
            _cancelTokenSource?.Cancel();
            _cancelTokenSource?.Dispose();
            _cancelTokenSource = null;

            System.Diagnostics.Debug.WriteLine("Location tracking stopped");
            return Task.CompletedTask;
        }

        private void UpdateLocationHistory(AppLocation location)
        {
            if (_locationHistory.Count >= 10)
            {
                _locationHistory.Dequeue();
            }
            _locationHistory.Enqueue(location);
        }

        private AppLocation ApplyKalmanFilter(AppLocation newLocation, AppLocation[] history)
        {
            // Simplified Kalman filter implementation
            // In a production app, you would use a proper Kalman filter library
            
            if (history.Length < 2)
                return newLocation;

            // Calculate velocity from recent history
            var recent = history.TakeLast(2).ToArray();
            var deltaTime = (recent[1].Timestamp - recent[0].Timestamp).TotalSeconds;
            var deltaLat = recent[1].Latitude - recent[0].Latitude;
            var deltaLng = recent[1].Longitude - recent[0].Longitude;

            // Predict next position based on velocity
            var predictedLat = recent[1].Latitude + (deltaLat / deltaTime) * (newLocation.Timestamp - recent[1].Timestamp).TotalSeconds;
            var predictedLng = recent[1].Longitude + (deltaLng / deltaTime) * (newLocation.Timestamp - recent[1].Timestamp).TotalSeconds;

            // Weight between prediction and measurement based on accuracy
            var measurementWeight = 1.0 / Math.Max(1.0, newLocation.Accuracy);
            var predictionWeight = 0.5; // Fixed weight for prediction

            var totalWeight = measurementWeight + predictionWeight;
            var filteredLat = (newLocation.Latitude * measurementWeight + predictedLat * predictionWeight) / totalWeight;
            var filteredLng = (newLocation.Longitude * measurementWeight + predictedLng * predictionWeight) / totalWeight;

            return new AppLocation
            {
                Latitude = filteredLat,
                Longitude = filteredLng,
                Accuracy = newLocation.Accuracy,
                Altitude = newLocation.Altitude,
                Speed = newLocation.Speed,
                Course = newLocation.Course,
                Timestamp = newLocation.Timestamp
            };
        }
    }

    /// <summary>
    /// Location signal quality levels
    /// </summary>
    public enum LocationSignalQuality
    {
        NoSignal,
        VeryPoor,
        Poor,
        Fair,
        Good,
        Excellent
    }

    /// <summary>
    /// Event arguments for location accuracy changes
    /// </summary>
    public class LocationAccuracyChangedEventArgs : EventArgs
    {
        public double PreviousAccuracy { get; set; }
        public double NewAccuracy { get; set; }
        public LocationSignalQuality SignalQuality { get; set; }
    }
}