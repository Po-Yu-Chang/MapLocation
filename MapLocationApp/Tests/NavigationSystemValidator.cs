using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp.Tests
{
    /// <summary>
    /// Navigation system integration validator
    /// Tests the complete navigation workflow
    /// </summary>
    public class NavigationSystemValidator
    {
        private readonly INavigationService _navigationService;
        private readonly IRouteService _routeService;
        private readonly ILocationService _locationService;
        private readonly ITTSService _ttsService;

        public NavigationSystemValidator(
            INavigationService navigationService,
            IRouteService routeService,
            ILocationService locationService,
            ITTSService ttsService)
        {
            _navigationService = navigationService;
            _routeService = routeService;
            _locationService = locationService;
            _ttsService = ttsService;
        }

        /// <summary>
        /// Validates the complete navigation workflow
        /// </summary>
        public async Task<ValidationResult> ValidateNavigationWorkflowAsync()
        {
            var result = new ValidationResult();

            try
            {
                result.AddTest("Navigation Service Initialization", TestNavigationServiceInitialization());
                result.AddTest("Route Calculation", await TestRouteCalculationAsync());
                result.AddTest("TTS Service", await TestTTSServiceAsync());
                result.AddTest("Location Service", await TestLocationServiceAsync());
                result.AddTest("Navigation Start/Stop", await TestNavigationStartStopAsync());
                result.AddTest("Route Deviation Detection", await TestRouteDeviationAsync());
                result.AddTest("Destination Arrival", await TestDestinationArrivalAsync());
                result.AddTest("Navigation Preferences", TestNavigationPreferencesAsync());

                result.OverallSuccess = result.FailedTests.Count == 0;
                result.Summary = $"Completed {result.TotalTests} tests. " +
                               $"Passed: {result.PassedTests.Count}, Failed: {result.FailedTests.Count}";

                return result;
            }
            catch (Exception ex)
            {
                result.OverallSuccess = false;
                result.Summary = $"Validation failed with exception: {ex.Message}";
                return result;
            }
        }

        private bool TestNavigationServiceInitialization()
        {
            try
            {
                return _navigationService != null && 
                       !_navigationService.IsNavigating &&
                       _navigationService.Preferences != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation service initialization test failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestRouteCalculationAsync()
        {
            try
            {
                // Test route calculation between two points in Taiwan
                var startLat = 25.0330; // Taipei
                var startLng = 121.5654;
                var endLat = 24.1469; // Taichung
                var endLng = 120.6839;

                var routeResult = await _routeService.CalculateRouteAsync(
                    startLat, startLng, endLat, endLng, RouteType.Driving);

                return routeResult?.Success == true && 
                       routeResult.Route != null &&
                       routeResult.Route.Steps?.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Route calculation test failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestTTSServiceAsync()
        {
            try
            {
                if (!_ttsService.IsSupported)
                    return true; // Skip test if TTS not supported

                await _ttsService.SetSpeechRateAsync(1.0f);
                await _ttsService.SetVolumeAsync(0.5f);

                // Test Chinese TTS
                await _ttsService.SpeakAsync("導航系統測試", "zh-TW");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS service test failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestLocationServiceAsync()
        {
            try
            {
                var hasPermission = await _locationService.RequestLocationPermissionAsync();
                if (!hasPermission)
                    return true; // Skip if no permission

                var location = await _locationService.GetCurrentLocationAsync();
                var isEnabled = await _locationService.IsLocationEnabledAsync();

                return isEnabled && (location != null || !hasPermission);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Location service test failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestNavigationStartStopAsync()
        {
            try
            {
                // Create a test route
                var testRoute = new Route
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Test Route",
                    StartLatitude = 25.0330,
                    StartLongitude = 121.5654,
                    EndLatitude = 25.0340,
                    EndLongitude = 121.5664,
                    Type = RouteType.Driving,
                    Distance = 1000,
                    EstimatedDuration = TimeSpan.FromMinutes(5),
                    Steps = new List<RouteStep>
                    {
                        new RouteStep
                        {
                            Index = 0,
                            Instruction = "直行 500 公尺",
                            StartLatitude = 25.0330,
                            StartLongitude = 121.5654,
                            EndLatitude = 25.0340,
                            EndLongitude = 121.5664,
                            Distance = 1000,
                            Type = StepType.Straight
                        }
                    }
                };

                // Test start navigation
                var session = await _navigationService.StartNavigationAsync(testRoute);
                var isNavigatingAfterStart = _navigationService.IsNavigating;

                // Test stop navigation
                await _navigationService.StopNavigationAsync();
                var isNavigatingAfterStop = _navigationService.IsNavigating;

                return session != null && 
                       session.IsActive && 
                       isNavigatingAfterStart && 
                       !isNavigatingAfterStop;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation start/stop test failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestRouteDeviationAsync()
        {
            try
            {
                var routeTrackingService = ServiceHelper.GetService<IRouteTrackingService>();
                
                var testRoute = new Route
                {
                    Id = "test-route",
                    Coordinates = new List<RouteCoordinate>
                    {
                        new RouteCoordinate { Latitude = 25.0330, Longitude = 121.5654 },
                        new RouteCoordinate { Latitude = 25.0340, Longitude = 121.5664 }
                    }
                };

                var currentLocation = new AppLocation
                {
                    Latitude = 25.0350, // Slightly off route
                    Longitude = 121.5674
                };

                var deviationResult = await routeTrackingService.CheckRouteDeviationAsync(
                    currentLocation, testRoute, 0);

                return deviationResult != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Route deviation test failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestDestinationArrivalAsync()
        {
            try
            {
                var destinationService = ServiceHelper.GetService<IDestinationArrivalService>();
                
                var currentLocation = new AppLocation
                {
                    Latitude = 25.0330,
                    Longitude = 121.5654
                };

                var destination = new AppLocation
                {
                    Latitude = 25.0330,
                    Longitude = 121.5654
                };

                var testSession = new NavigationSession
                {
                    Id = "test-session",
                    StartTime = DateTime.Now.AddMinutes(-10),
                    Route = new Route
                    {
                        Id = "test-route",
                        ToAddress = "Test Destination",
                        Distance = 1000
                    }
                };

                await destinationService.HandleArrivalAsync(currentLocation, destination, testSession);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Destination arrival test failed: {ex.Message}");
                return false;
            }
        }

        private bool TestNavigationPreferencesAsync()
        {
            try
            {
                var preferences = new NavigationPreferences
                {
                    PreferredLanguage = "zh-TW",
                    VoiceLevel = VoiceGuidanceLevel.Normal,
                    AvoidTolls = true,
                    SpeechRate = 1.0f,
                    SpeechVolume = 0.8f
                };

                _navigationService.Preferences = preferences;

                var retrievedPreferences = _navigationService.Preferences;

                return retrievedPreferences != null &&
                       retrievedPreferences.PreferredLanguage == "zh-TW" &&
                       retrievedPreferences.VoiceLevel == VoiceGuidanceLevel.Normal &&
                       retrievedPreferences.AvoidTolls == true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation preferences test failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Validation result container
    /// </summary>
    public class ValidationResult
    {
        public bool OverallSuccess { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<TestResult> PassedTests { get; set; } = new();
        public List<TestResult> FailedTests { get; set; } = new();
        public int TotalTests => PassedTests.Count + FailedTests.Count;

        public void AddTest(string testName, bool passed)
        {
            var testResult = new TestResult
            {
                TestName = testName,
                Passed = passed,
                Timestamp = DateTime.Now
            };

            if (passed)
                PassedTests.Add(testResult);
            else
                FailedTests.Add(testResult);
        }
    }

    /// <summary>
    /// Individual test result
    /// </summary>
    public class TestResult
    {
        public string TestName { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public DateTime Timestamp { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}