using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Main navigation service implementation coordinating all navigation features
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly IRouteService _routeService;
        private readonly ILocationService _locationService;
        private readonly ITTSService _ttsService;
        private readonly IRouteTrackingService _routeTrackingService;
        private readonly ITelegramNotificationService _telegramService;

        private NavigationSession _currentSession;
        private NavigationInstruction _currentInstruction;
        private NavigationPreferences _preferences;
        private Timer _navigationUpdateTimer;
        private CancellationTokenSource _cancellationTokenSource;

        private const int NAVIGATION_UPDATE_INTERVAL_MS = 5000; // 5 seconds
        private const int ARRIVAL_THRESHOLD_METERS = 20;

        public bool IsNavigating => _currentSession?.IsActive == true;
        public NavigationSession CurrentSession => _currentSession;
        public NavigationInstruction CurrentInstruction => _currentInstruction;

        public NavigationPreferences Preferences
        {
            get => _preferences ??= new NavigationPreferences();
            set => _preferences = value;
        }

        public event EventHandler<NavigationInstruction> InstructionUpdated;
        public event EventHandler<AppLocation> LocationUpdated;
        public event EventHandler<RouteDeviationEventArgs> RouteDeviationDetected;
        public event EventHandler<DestinationArrivalEventArgs> DestinationArrived;
        public event EventHandler<NavigationCompletedEventArgs> NavigationCompleted;

        public NavigationService(
            IRouteService routeService,
            ILocationService locationService,
            ITTSService ttsService,
            IRouteTrackingService routeTrackingService,
            ITelegramNotificationService telegramService)
        {
            _routeService = routeService;
            _locationService = locationService;
            _ttsService = ttsService;
            _routeTrackingService = routeTrackingService;
            _telegramService = telegramService;
            _preferences = new NavigationPreferences();

            // Subscribe to location updates
            _locationService.LocationChanged += OnLocationChanged;
        }

        public async Task<NavigationSession> StartNavigationAsync(Route route)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            try
            {
                // Stop any existing navigation
                if (IsNavigating)
                {
                    await StopNavigationAsync();
                }

                // Create new navigation session
                _currentSession = new NavigationSession
                {
                    Id = Guid.NewGuid().ToString(),
                    RouteId = route.Id,
                    Route = route,
                    StartTime = DateTime.Now,
                    IsActive = true,
                    CurrentStepIndex = 0
                };

                // Get current location
                var currentLocation = await _locationService.GetCurrentLocationAsync();
                if (currentLocation != null)
                {
                    _currentSession.CurrentLocation = currentLocation;
                }

                // Start location tracking
                await _locationService.StartLocationUpdatesAsync();

                // Initialize first instruction
                await UpdateNavigationInstruction();

                // Setup navigation update timer
                _cancellationTokenSource = new CancellationTokenSource();
                _navigationUpdateTimer = new Timer(
                    OnNavigationTimerTick, 
                    null, 
                    TimeSpan.FromMilliseconds(NAVIGATION_UPDATE_INTERVAL_MS),
                    TimeSpan.FromMilliseconds(NAVIGATION_UPDATE_INTERVAL_MS));

                // Configure TTS
                await _ttsService.SetSpeechRateAsync(Preferences.SpeechRate);
                await _ttsService.SetVolumeAsync(Preferences.SpeechVolume);

                // Announce navigation start
                if (Preferences.IsVoiceEnabled)
                {
                    await _ttsService.SpeakAsync("開始導航", Preferences.PreferredLanguage);
                }

                // Send Telegram notification
                await _telegramService.SendNavigationStartNotificationAsync(
                    route.FromAddress, route.ToAddress);

                System.Diagnostics.Debug.WriteLine($"Navigation started for route: {route.Name}");

                return _currentSession;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start navigation: {ex.Message}");
                throw;
            }
        }

        public async Task StopNavigationAsync()
        {
            try
            {
                if (_currentSession != null)
                {
                    _currentSession.IsActive = false;
                    _currentSession.EndTime = DateTime.Now;

                    // Stop location updates
                    await _locationService.StopLocationUpdatesAsync();

                    // Stop TTS
                    await _ttsService.StopSpeechAsync();

                    // Stop timer
                    _navigationUpdateTimer?.Dispose();
                    _navigationUpdateTimer = null;

                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = null;

                    // Fire completion event
                    var completedArgs = new NavigationCompletedEventArgs
                    {
                        Session = _currentSession,
                        WasSuccessful = true,
                        Reason = "User stopped navigation",
                        TotalTime = DateTime.Now - _currentSession.StartTime,
                        TotalDistanceMeters = _currentSession.Route?.Distance ?? 0
                    };

                    NavigationCompleted?.Invoke(this, completedArgs);

                    // Send Telegram notification
                    await _telegramService.SendNavigationEndNotificationAsync();

                    System.Diagnostics.Debug.WriteLine("Navigation stopped");
                }

                _currentSession = null;
                _currentInstruction = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping navigation: {ex.Message}");
            }
        }

        public async Task<NavigationStatus> GetCurrentStatusAsync()
        {
            if (!IsNavigating)
            {
                return new NavigationStatus { IsActive = false };
            }

            var currentLocation = await _locationService.GetCurrentLocationAsync();
            var progress = _routeTrackingService.CalculateRouteProgress(
                currentLocation, _currentSession.Route, _currentSession.CurrentStepIndex);

            return new NavigationStatus
            {
                IsActive = true,
                CurrentRoute = _currentSession.Route,
                CurrentLocation = currentLocation,
                NextInstruction = _currentInstruction,
                EstimatedTimeRemaining = progress.EstimatedTimeRemaining,
                DistanceRemainingMeters = progress.DistanceRemainingMeters,
                CurrentStepIndex = _currentSession.CurrentStepIndex,
                StartTime = _currentSession.StartTime,
                SessionId = _currentSession.Id
            };
        }

        public async Task<NavigationInstruction> UpdateLocationAsync(AppLocation currentLocation)
        {
            if (!IsNavigating || currentLocation == null)
                return _currentInstruction;

            try
            {
                _currentSession.CurrentLocation = currentLocation;

                // Check for route deviation
                var deviationResult = await _routeTrackingService.CheckRouteDeviationAsync(
                    currentLocation, _currentSession.Route, _currentSession.CurrentStepIndex);

                if (deviationResult.IsDeviated)
                {
                    var deviationArgs = new RouteDeviationEventArgs
                    {
                        DeviationDistanceMeters = deviationResult.DeviationDistance,
                        CurrentLocation = currentLocation,
                        RequiresRecalculation = deviationResult.SuggestedAction == RouteAction.Recalculate,
                        Message = deviationResult.Message
                    };

                    RouteDeviationDetected?.Invoke(this, deviationArgs);

                    if (deviationArgs.RequiresRecalculation)
                    {
                        await HandleRouteRecalculation(currentLocation);
                        return _currentInstruction;
                    }
                }

                // Check if we've reached the destination
                var distanceToDestination = CalculateDistance(
                    currentLocation.Latitude, currentLocation.Longitude,
                    _currentSession.Route.EndLatitude, _currentSession.Route.EndLongitude);

                if (distanceToDestination <= ARRIVAL_THRESHOLD_METERS)
                {
                    await HandleDestinationArrival(currentLocation);
                    return _currentInstruction;
                }

                // Update navigation instruction
                await UpdateNavigationInstruction();

                LocationUpdated?.Invoke(this, currentLocation);

                return _currentInstruction;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating navigation location: {ex.Message}");
                return _currentInstruction;
            }
        }

        private async void OnLocationChanged(object sender, AppLocation location)
        {
            if (IsNavigating)
            {
                await UpdateLocationAsync(location);
            }
        }

        private async void OnNavigationTimerTick(object state)
        {
            if (!IsNavigating)
                return;

            try
            {
                var currentLocation = await _locationService.GetCurrentLocationAsync();
                if (currentLocation != null)
                {
                    await UpdateLocationAsync(currentLocation);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation timer error: {ex.Message}");
            }
        }

        private async Task UpdateNavigationInstruction()
        {
            if (!IsNavigating || _currentSession.Route?.Steps == null)
                return;

            try
            {
                var currentLocation = _currentSession.CurrentLocation;
                if (currentLocation == null)
                    return;

                var steps = _currentSession.Route.Steps;
                var currentStepIndex = _currentSession.CurrentStepIndex;

                if (currentStepIndex >= steps.Count)
                    return;

                var currentStep = steps[currentStepIndex];
                var distanceToStepEnd = CalculateDistance(
                    currentLocation.Latitude, currentLocation.Longitude,
                    currentStep.EndLatitude, currentStep.EndLongitude) * 1000; // Convert to meters

                // Create navigation instruction
                var instruction = new NavigationInstruction
                {
                    Type = ConvertStepTypeToNavigationType(currentStep.Type),
                    Text = currentStep.Instruction,
                    DistanceInMeters = (int)distanceToStepEnd,
                    Distance = FormatDistance((int)distanceToStepEnd),
                    ArrowIcon = GetArrowIcon(currentStep.Type)
                };

                // Check if we need to advance to next step
                if (distanceToStepEnd < 10 && currentStepIndex < steps.Count - 1)
                {
                    _currentSession.CurrentStepIndex++;
                    await UpdateNavigationInstruction();
                    return;
                }

                // Update current instruction
                var previousInstruction = _currentInstruction;
                _currentInstruction = instruction;

                // Fire event if instruction changed
                if (previousInstruction?.Text != instruction.Text)
                {
                    InstructionUpdated?.Invoke(this, instruction);

                    // Speak instruction if voice guidance is enabled
                    if (Preferences.IsVoiceEnabled && ShouldSpeakInstruction(instruction))
                    {
                        await _ttsService.SpeakAsync(instruction.VoiceInstruction, Preferences.PreferredLanguage);
                    }

                    // Vibrate if enabled
                    if (Preferences.VibrateOnTurn && IsDirectionChange(instruction.Type))
                    {
                        await VibrateDevice();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating navigation instruction: {ex.Message}");
            }
        }

        private async Task HandleRouteRecalculation(AppLocation currentLocation)
        {
            try
            {
                // Announce recalculation
                if (Preferences.IsVoiceEnabled)
                {
                    await _ttsService.SpeakAsync("正在重新計算路線", Preferences.PreferredLanguage);
                }

                // Recalculate route from current location
                var destination = _currentSession.Route;
                var routeResult = await _routeService.CalculateRouteAsync(
                    currentLocation.Latitude, currentLocation.Longitude,
                    destination.EndLatitude, destination.EndLongitude,
                    destination.Type);

                if (routeResult?.Success == true && routeResult.Route != null)
                {
                    _currentSession.Route = routeResult.Route;
                    _currentSession.CurrentStepIndex = 0;

                    await UpdateNavigationInstruction();

                    if (Preferences.IsVoiceEnabled)
                    {
                        await _ttsService.SpeakAsync("路線已重新規劃", Preferences.PreferredLanguage);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error recalculating route: {ex.Message}");
            }
        }

        private async Task HandleDestinationArrival(AppLocation currentLocation)
        {
            try
            {
                var arrivalArgs = new DestinationArrivalEventArgs
                {
                    DestinationLocation = new AppLocation
                    {
                        Latitude = _currentSession.Route.EndLatitude,
                        Longitude = _currentSession.Route.EndLongitude
                    },
                    DistanceToDestinationMeters = CalculateDistance(
                        currentLocation.Latitude, currentLocation.Longitude,
                        _currentSession.Route.EndLatitude, _currentSession.Route.EndLongitude) * 1000,
                    Session = _currentSession,
                    TotalNavigationTime = DateTime.Now - _currentSession.StartTime
                };

                DestinationArrived?.Invoke(this, arrivalArgs);

                // Update instruction for arrival
                _currentInstruction = new NavigationInstruction
                {
                    Type = NavigationType.Arrive,
                    Text = "您已到達目的地",
                    Distance = "已到達",
                    DistanceInMeters = 0,
                    ArrowIcon = "arrival_icon"
                };

                InstructionUpdated?.Invoke(this, _currentInstruction);

                // Announce arrival
                if (Preferences.IsVoiceEnabled)
                {
                    await _ttsService.SpeakAsync("您已到達目的地", Preferences.PreferredLanguage);
                }

                // Vibrate
                await VibrateDevice();

                // Send notification
                await _telegramService.SendDestinationArrivedNotificationAsync(
                    _currentSession.Route.ToAddress);

                // Stop navigation
                await StopNavigationAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling destination arrival: {ex.Message}");
            }
        }

        private bool ShouldSpeakInstruction(NavigationInstruction instruction)
        {
            return Preferences.VoiceLevel switch
            {
                VoiceGuidanceLevel.Off => false,
                VoiceGuidanceLevel.Essential => instruction.Type == NavigationType.TurnLeft ||
                                               instruction.Type == NavigationType.TurnRight ||
                                               instruction.Type == NavigationType.Arrive,
                VoiceGuidanceLevel.Normal => instruction.DistanceInMeters <= 200,
                VoiceGuidanceLevel.Detailed => true,
                _ => true
            };
        }

        private bool IsDirectionChange(NavigationType type)
        {
            return type == NavigationType.TurnLeft ||
                   type == NavigationType.TurnRight ||
                   type == NavigationType.TurnSlightLeft ||
                   type == NavigationType.TurnSlightRight ||
                   type == NavigationType.UTurn;
        }

        private async Task VibrateDevice()
        {
            try
            {
                // Use Microsoft.Maui.Essentials Vibration
                var duration = TimeSpan.FromMilliseconds(500);
                Vibration.Default.Vibrate(duration);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
            }
        }

        private NavigationType ConvertStepTypeToNavigationType(StepType stepType)
        {
            return stepType switch
            {
                StepType.TurnLeft => NavigationType.TurnLeft,
                StepType.TurnRight => NavigationType.TurnRight,
                StepType.UTurn => NavigationType.UTurn,
                StepType.Straight => NavigationType.Continue,
                _ => NavigationType.Continue
            };
        }

        private string GetArrowIcon(StepType stepType)
        {
            return stepType switch
            {
                StepType.TurnLeft => "arrow_left",
                StepType.TurnRight => "arrow_right",
                StepType.UTurn => "arrow_uturn",
                StepType.Straight => "arrow_straight",
                _ => "arrow_straight"
            };
        }

        private string FormatDistance(int meters)
        {
            return meters switch
            {
                < 50 => "立即",
                < 1000 => $"{meters} 公尺",
                _ => $"{meters / 1000.0:F1} 公里"
            };
        }

        private double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            const double earthRadius = 6371; // Earth radius in kilometers

            var lat1Rad = lat1 * Math.PI / 180;
            var lat2Rad = lat2 * Math.PI / 180;
            var deltaLatRad = (lat2 - lat1) * Math.PI / 180;
            var deltaLngRad = (lng2 - lng1) * Math.PI / 180;

            var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadius * c; // Distance in kilometers
        }
    }
}