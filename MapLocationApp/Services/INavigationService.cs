using System;
using System.Threading.Tasks;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Navigation status information
    /// </summary>
    public class NavigationStatus
    {
        public bool IsActive { get; set; }
        public bool IsVoiceEnabled { get; set; }
        public Route? CurrentRoute { get; set; }
        public NavigationSession? CurrentSession { get; set; }
        public NavigationInstruction? NextInstruction { get; set; }
        public AppLocation? CurrentLocation { get; set; }
        public RouteProgress? Progress { get; set; }
        public LocationSignalQuality SignalQuality { get; set; }
        public bool IsOfflineMode { get; set; }
    }

    /// <summary>
    /// Enhanced navigation service interface for turn-by-turn navigation
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Starts navigation for the given route
        /// </summary>
        Task<bool> StartNavigationAsync(Route route);

        /// <summary>
        /// Stops the current navigation session
        /// </summary>
        Task StopNavigationAsync();

        /// <summary>
        /// Gets the current navigation status
        /// </summary>
        Task<NavigationStatus> GetCurrentStatusAsync();

        /// <summary>
        /// Updates navigation with current location
        /// </summary>
        Task UpdateLocationAsync(AppLocation location);

        /// <summary>
        /// Enables or disables voice guidance
        /// </summary>
        Task SetVoiceGuidanceAsync(bool enabled);

        /// <summary>
        /// Sets the voice guidance language
        /// </summary>
        Task SetVoiceLanguageAsync(string languageCode);

        /// <summary>
        /// Manually triggers route recalculation
        /// </summary>
        Task<bool> RecalculateRouteAsync();

        /// <summary>
        /// Event fired when navigation instruction changes
        /// </summary>
        event EventHandler<NavigationInstruction> InstructionUpdated;

        /// <summary>
        /// Event fired when location is updated during navigation
        /// </summary>
        event EventHandler<AppLocation> LocationUpdated;

        /// <summary>
        /// Event fired when navigation status changes
        /// </summary>
        event EventHandler<NavigationStatus> StatusChanged;

        /// <summary>
        /// Event fired when route deviation is detected
        /// </summary>
        event EventHandler<RouteDeviationResult> RouteDeviationDetected;

        /// <summary>
        /// Event fired when navigation is completed
        /// </summary>
        event EventHandler<NavigationSession> NavigationCompleted;
    }

    /// <summary>
    /// Enhanced navigation service implementation
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly IRouteService _routeService;
        private readonly IAdvancedLocationService _locationService;
        private readonly ITTSService _ttsService;
        private readonly IRouteTrackingService _routeTrackingService;
        
        private NavigationSession? _currentSession;
        private Route? _currentRoute;
        private bool _isVoiceEnabled = true;
        private string _voiceLanguage = "zh-TW";
        private AppLocation? _lastLocation;
        private NavigationInstruction? _lastInstruction;
        private System.Timers.Timer? _navigationTimer;

        public event EventHandler<NavigationInstruction>? InstructionUpdated;
        public event EventHandler<AppLocation>? LocationUpdated;
        public event EventHandler<NavigationStatus>? StatusChanged;
        public event EventHandler<RouteDeviationResult>? RouteDeviationDetected;
        public event EventHandler<NavigationSession>? NavigationCompleted;

        public NavigationService(
            IRouteService routeService,
            IAdvancedLocationService locationService,
            ITTSService ttsService,
            IRouteTrackingService routeTrackingService)
        {
            _routeService = routeService;
            _locationService = locationService;
            _ttsService = ttsService;
            _routeTrackingService = routeTrackingService;

            // Subscribe to events
            _locationService.LocationChanged += OnLocationChanged;
            _routeTrackingService.RouteDeviationDetected += OnRouteDeviationDetected;
        }

        public async Task<bool> StartNavigationAsync(Route route)
        {
            try
            {
                if (route == null) return false;

                // Stop any existing navigation
                await StopNavigationAsync();

                // Start new navigation session
                _currentRoute = route;
                _currentSession = await _routeService.StartNavigationAsync(route.Id);
                
                if (_currentSession == null) return false;

                // Start location updates
                await _locationService.StartLocationUpdatesAsync();

                // Start navigation timer for periodic updates
                _navigationTimer = new System.Timers.Timer(2000); // Update every 2 seconds
                _navigationTimer.Elapsed += OnNavigationTimerElapsed;
                _navigationTimer.Start();

                // Reset route tracking
                _routeTrackingService.ResetDeviationTracking();

                // Fire status changed event
                await FireStatusChangedEvent();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Start navigation error: {ex.Message}");
                return false;
            }
        }

        public async Task StopNavigationAsync()
        {
            try
            {
                // Stop navigation timer
                _navigationTimer?.Stop();
                _navigationTimer?.Dispose();
                _navigationTimer = null;

                // Stop location updates
                await _locationService.StopLocationUpdatesAsync();

                // End navigation session
                if (_currentSession != null)
                {
                    await _routeService.EndNavigationAsync(_currentSession.Id);
                    NavigationCompleted?.Invoke(this, _currentSession);
                }

                // Reset state
                _currentSession = null;
                _currentRoute = null;
                _lastLocation = null;
                _lastInstruction = null;

                // Fire status changed event
                await FireStatusChangedEvent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop navigation error: {ex.Message}");
            }
        }

        public async Task<NavigationStatus> GetCurrentStatusAsync()
        {
            var status = new NavigationStatus
            {
                IsActive = _currentSession?.IsActive ?? false,
                IsVoiceEnabled = _isVoiceEnabled,
                CurrentRoute = _currentRoute,
                CurrentSession = _currentSession,
                NextInstruction = _lastInstruction,
                CurrentLocation = _lastLocation,
                SignalQuality = _lastLocation != null ? _locationService.GetSignalQuality(_lastLocation) : LocationSignalQuality.Poor,
                IsOfflineMode = false // TODO: Implement offline detection
            };

            // Get route progress if navigation is active
            if (_currentRoute != null && _lastLocation != null)
            {
                status.Progress = _routeTrackingService.CalculateRouteProgress(_lastLocation, _currentRoute);
            }

            return status;
        }

        public async Task UpdateLocationAsync(AppLocation location)
        {
            if (location == null || _currentSession == null || _currentRoute == null) return;

            try
            {
                _lastLocation = location;

                // Get navigation update
                var update = await _routeService.GetNavigationUpdateAsync(_currentSession.Id, location.Latitude, location.Longitude);
                if (update != null)
                {
                    await ProcessNavigationUpdate(update, location);
                }

                // Check for route deviation
                var deviationResult = await _routeTrackingService.CheckRouteDeviationAsync(location, _currentRoute);
                if (deviationResult.IsDeviated)
                {
                    RouteDeviationDetected?.Invoke(this, deviationResult);
                    
                    // Handle automatic recalculation if needed
                    if (deviationResult.SuggestedAction == RouteAction.Recalculate)
                    {
                        await RecalculateRouteAsync();
                    }
                }

                LocationUpdated?.Invoke(this, location);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update location error: {ex.Message}");
            }
        }

        public async Task SetVoiceGuidanceAsync(bool enabled)
        {
            _isVoiceEnabled = enabled;
            
            if (!enabled)
            {
                await _ttsService.StopAsync();
            }

            await FireStatusChangedEvent();
        }

        public async Task SetVoiceLanguageAsync(string languageCode)
        {
            _voiceLanguage = languageCode;
            await Task.CompletedTask;
        }

        public async Task<bool> RecalculateRouteAsync()
        {
            if (_currentRoute == null || _lastLocation == null) return false;

            try
            {
                // Recalculate route from current location to destination
                var routeResult = await _routeService.CalculateRouteAsync(
                    _lastLocation.Latitude, _lastLocation.Longitude,
                    _currentRoute.EndLatitude, _currentRoute.EndLongitude,
                    _currentRoute.Type);

                if (routeResult?.Success == true && routeResult.Route != null)
                {
                    _currentRoute = routeResult.Route;
                    
                    // Reset navigation session with new route
                    if (_currentSession != null)
                    {
                        _currentSession.Route = _currentRoute;
                        _currentSession.CurrentStepIndex = 0;
                    }

                    // Reset route tracking
                    _routeTrackingService.ResetDeviationTracking();

                    // Announce recalculation
                    if (_isVoiceEnabled)
                    {
                        await _ttsService.SpeakAsync(NavigationInstructions.GetInstruction(NavigationType.Continue, _voiceLanguage).Replace("{distance}", "重新計算路線"), _voiceLanguage);
                    }

                    await FireStatusChangedEvent();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Recalculate route error: {ex.Message}");
                return false;
            }
        }

        private async void OnLocationChanged(object? sender, AppLocation location)
        {
            await UpdateLocationAsync(location);
        }

        private async void OnRouteDeviationDetected(object? sender, RouteDeviationResult result)
        {
            RouteDeviationDetected?.Invoke(this, result);

            // Announce deviation if voice is enabled
            if (_isVoiceEnabled && result.IsDeviated)
            {
                await _ttsService.SpeakAsync("您已偏離路線，正在重新規劃", _voiceLanguage);
            }
        }

        private async void OnNavigationTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_lastLocation != null)
            {
                await UpdateLocationAsync(_lastLocation);
            }
        }

        private async Task ProcessNavigationUpdate(NavigationUpdate update, AppLocation location)
        {
            if (update.IsNavigationComplete)
            {
                // Navigation completed
                if (_isVoiceEnabled)
                {
                    await _ttsService.SpeakAsync(NavigationInstructions.ZhTW.ARRIVE_DESTINATION, _voiceLanguage);
                }

                await StopNavigationAsync();
                return;
            }

            // Create navigation instruction
            if (update.CurrentStep != null)
            {
                var instruction = new NavigationInstruction
                {
                    Text = update.CurrentStep.Instruction,
                    Distance = NavigationInstructions.FormatDistance(update.DistanceToNextTurn, _voiceLanguage),
                    DistanceMeters = update.DistanceToNextTurn,
                    Type = ConvertStepTypeToNavigationType(update.CurrentStep.Type),
                    Timing = NavigationInstructions.GetInstructionTiming(update.DistanceToNextTurn)
                };

                // Check if we should announce this instruction
                if (ShouldAnnounceInstruction(instruction))
                {
                    _lastInstruction = instruction;
                    InstructionUpdated?.Invoke(this, instruction);

                    // Speak instruction if voice is enabled
                    if (_isVoiceEnabled && !instruction.IsSpoken)
                    {
                        var spokenText = NavigationInstructions.CreateInstruction(instruction.Type, instruction.DistanceMeters, _voiceLanguage);
                        await _ttsService.SpeakAsync(spokenText, _voiceLanguage);
                        instruction.IsSpoken = true;
                    }
                }
            }
        }

        private bool ShouldAnnounceInstruction(NavigationInstruction instruction)
        {
            // Announce if this is a new instruction or if we're getting close to the turn
            if (_lastInstruction == null || _lastInstruction.Type != instruction.Type)
            {
                return true;
            }

            // Re-announce if we're getting very close to the turn
            return instruction.DistanceMeters <= NavigationInstructions.IMMEDIATE_TURN_DISTANCE;
        }

        private NavigationType ConvertStepTypeToNavigationType(StepType stepType)
        {
            return stepType switch
            {
                StepType.TurnLeft => NavigationType.TurnLeft,
                StepType.TurnRight => NavigationType.TurnRight,
                StepType.UTurn => NavigationType.UTurn,
                StepType.RoundaboutEnter => NavigationType.RoundaboutEnter,
                StepType.RoundaboutExit => NavigationType.RoundaboutExit,
                _ => NavigationType.Continue
            };
        }

        private async Task FireStatusChangedEvent()
        {
            var status = await GetCurrentStatusAsync();
            StatusChanged?.Invoke(this, status);
        }
    }
}