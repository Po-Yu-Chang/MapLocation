using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp.ViewModels
{
    /// <summary>
    /// ViewModel for the enhanced navigation UI
    /// Supports Google Maps-style navigation interface
    /// </summary>
    public class NavigationViewModel : INotifyPropertyChanged
    {
        private readonly INavigationService _navigationService;
        private readonly ILaneGuidanceService _laneGuidanceService;
        private readonly ITrafficService _trafficService;

        // Navigation state
        private bool _isNavigating = false;
        private NavigationInstruction _currentInstruction;
        private LaneGuidance _laneGuidance;
        private string _estimatedArrivalTime = string.Empty;
        private string _remainingDistance = string.Empty;
        private string _remainingTime = string.Empty;
        private double _routeProgress = 0;

        // Current location and route info
        private AppLocation _currentLocation;
        private Route _currentRoute;
        private TrafficInfo _currentTrafficInfo;

        // UI display properties
        private string _nextInstructionText = string.Empty;
        private string _distanceToNext = string.Empty;
        private string _directionArrowIcon = "arrow_straight";
        private string _speedLimitText = string.Empty;
        private string _currentSpeedText = string.Empty;

        // Collections for UI binding
        public ObservableCollection<LaneInfo> CurrentLanes { get; set; }
        public ObservableCollection<string> UpcomingInstructions { get; set; }

        public NavigationViewModel(
            INavigationService navigationService,
            ILaneGuidanceService laneGuidanceService,
            ITrafficService trafficService)
        {
            _navigationService = navigationService;
            _laneGuidanceService = laneGuidanceService;
            _trafficService = trafficService;

            CurrentLanes = new ObservableCollection<LaneInfo>();
            UpcomingInstructions = new ObservableCollection<string>();

            // Subscribe to navigation events
            if (_navigationService != null)
            {
                _navigationService.InstructionUpdated += OnInstructionUpdated;
                _navigationService.LocationUpdated += OnLocationUpdated;
                _navigationService.NavigationCompleted += OnNavigationCompleted;
            }
        }

        #region Public Properties

        /// <summary>
        /// Whether navigation is currently active
        /// </summary>
        public bool IsNavigating
        {
            get => _isNavigating;
            set => SetProperty(ref _isNavigating, value);
        }

        /// <summary>
        /// Current navigation instruction
        /// </summary>
        public NavigationInstruction CurrentInstruction
        {
            get => _currentInstruction;
            set => SetProperty(ref _currentInstruction, value);
        }

        /// <summary>
        /// Current lane guidance information
        /// </summary>
        public LaneGuidance LaneGuidance
        {
            get => _laneGuidance;
            set => SetProperty(ref _laneGuidance, value);
        }

        /// <summary>
        /// Estimated arrival time as formatted string
        /// </summary>
        public string EstimatedArrivalTime
        {
            get => _estimatedArrivalTime;
            set => SetProperty(ref _estimatedArrivalTime, value);
        }

        /// <summary>
        /// Remaining distance as formatted string
        /// </summary>
        public string RemainingDistance
        {
            get => _remainingDistance;
            set => SetProperty(ref _remainingDistance, value);
        }

        /// <summary>
        /// Remaining time as formatted string
        /// </summary>
        public string RemainingTime
        {
            get => _remainingTime;
            set => SetProperty(ref _remainingTime, value);
        }

        /// <summary>
        /// Route progress percentage (0-100)
        /// </summary>
        public double RouteProgress
        {
            get => _routeProgress;
            set => SetProperty(ref _routeProgress, value);
        }

        /// <summary>
        /// Next instruction text for large display
        /// </summary>
        public string NextInstructionText
        {
            get => _nextInstructionText;
            set => SetProperty(ref _nextInstructionText, value);
        }

        /// <summary>
        /// Distance to next turn/instruction
        /// </summary>
        public string DistanceToNext
        {
            get => _distanceToNext;
            set => SetProperty(ref _distanceToNext, value);
        }

        /// <summary>
        /// Direction arrow icon resource name
        /// </summary>
        public string DirectionArrowIcon
        {
            get => _directionArrowIcon;
            set => SetProperty(ref _directionArrowIcon, value);
        }

        /// <summary>
        /// Speed limit display text
        /// </summary>
        public string SpeedLimitText
        {
            get => _speedLimitText;
            set => SetProperty(ref _speedLimitText, value);
        }

        /// <summary>
        /// Current speed display text
        /// </summary>
        public string CurrentSpeedText
        {
            get => _currentSpeedText;
            set => SetProperty(ref _currentSpeedText, value);
        }

        /// <summary>
        /// Current location information
        /// </summary>
        public AppLocation CurrentLocation
        {
            get => _currentLocation;
            set => SetProperty(ref _currentLocation, value);
        }

        /// <summary>
        /// Current route being navigated
        /// </summary>
        public Route CurrentRoute
        {
            get => _currentRoute;
            set => SetProperty(ref _currentRoute, value);
        }

        /// <summary>
        /// Current traffic information
        /// </summary>
        public TrafficInfo CurrentTrafficInfo
        {
            get => _currentTrafficInfo;
            set => SetProperty(ref _currentTrafficInfo, value);
        }

        /// <summary>
        /// Traffic condition color for UI
        /// </summary>
        public string TrafficColor
        {
            get
            {
                return CurrentTrafficInfo?.Condition switch
                {
                    TrafficCondition.Light => "#4CAF50",      // Green
                    TrafficCondition.Moderate => "#FF9800",   // Orange
                    TrafficCondition.Heavy => "#F44336",      // Red
                    TrafficCondition.Stopped => "#9C27B0",    // Purple
                    _ => "#9E9E9E"                             // Gray
                };
            }
        }

        /// <summary>
        /// Whether lane guidance is available
        /// </summary>
        public bool HasLaneGuidance => LaneGuidance?.IsAvailable == true;

        /// <summary>
        /// Whether traffic information is available
        /// </summary>
        public bool HasTrafficInfo => CurrentTrafficInfo != null;

        #endregion

        #region Event Handlers

        private async void OnInstructionUpdated(object sender, NavigationInstruction instruction)
        {
            CurrentInstruction = instruction;
            NextInstructionText = instruction?.Text ?? string.Empty;
            DistanceToNext = instruction?.Distance ?? string.Empty;
            DirectionArrowIcon = instruction?.ArrowIcon ?? "arrow_straight";

            // Update lane guidance
            if (_currentLocation != null && _currentRoute != null && _navigationService.CurrentSession != null)
            {
                try
                {
                    var laneGuidance = await _laneGuidanceService.GetLaneGuidanceAsync(
                        _currentLocation, _currentRoute, _navigationService.CurrentSession.CurrentStepIndex);
                    
                    LaneGuidance = laneGuidance;
                    UpdateLaneCollection(laneGuidance?.Lanes);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lane guidance update error: {ex.Message}");
                }
            }

            // Update upcoming instructions
            UpdateUpcomingInstructions();
        }

        private async void OnLocationUpdated(object sender, AppLocation location)
        {
            CurrentLocation = location;

            // Update speed display
            if (location.Speed.HasValue)
            {
                CurrentSpeedText = $"{location.Speed.Value * 3.6:F0} km/h"; // Convert m/s to km/h
            }

            // Update traffic information
            try
            {
                var trafficInfo = await _trafficService.GetTrafficInfoAsync(location.Latitude, location.Longitude);
                CurrentTrafficInfo = trafficInfo;
                OnPropertyChanged(nameof(TrafficColor));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Traffic info update error: {ex.Message}");
            }

            // Update navigation status
            await UpdateNavigationStatus();
        }

        private void OnNavigationCompleted(object sender, NavigationCompletedEventArgs e)
        {
            IsNavigating = false;
            CurrentInstruction = null;
            LaneGuidance = null;
            CurrentLanes.Clear();
            UpcomingInstructions.Clear();
            
            // Reset display properties
            NextInstructionText = string.Empty;
            DistanceToNext = string.Empty;
            DirectionArrowIcon = "arrow_straight";
            EstimatedArrivalTime = string.Empty;
            RemainingDistance = string.Empty;
            RemainingTime = string.Empty;
            RouteProgress = 0;
        }

        #endregion

        #region Private Methods

        private async System.Threading.Tasks.Task UpdateNavigationStatus()
        {
            if (!IsNavigating)
                return;

            try
            {
                var status = await _navigationService.GetCurrentStatusAsync();
                if (status != null)
                {
                    // Update time and distance
                    var estimatedArrival = DateTime.Now.Add(status.EstimatedTimeRemaining);
                    EstimatedArrivalTime = estimatedArrival.ToString("HH:mm");
                    
                    RemainingDistance = FormatDistance(status.DistanceRemainingMeters);
                    RemainingTime = FormatTimeSpan(status.EstimatedTimeRemaining);

                    // Calculate progress percentage
                    if (_currentRoute != null && _currentRoute.Distance > 0)
                    {
                        var completedDistance = _currentRoute.Distance - status.DistanceRemainingMeters;
                        RouteProgress = (completedDistance / _currentRoute.Distance) * 100;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation status update error: {ex.Message}");
            }
        }

        private void UpdateLaneCollection(System.Collections.Generic.List<LaneInfo> lanes)
        {
            CurrentLanes.Clear();
            if (lanes != null)
            {
                foreach (var lane in lanes)
                {
                    CurrentLanes.Add(lane);
                }
            }
        }

        private void UpdateUpcomingInstructions()
        {
            UpcomingInstructions.Clear();

            if (_currentRoute?.Steps != null && _navigationService.CurrentSession != null)
            {
                var currentStepIndex = _navigationService.CurrentSession.CurrentStepIndex;
                var stepsToShow = Math.Min(3, _currentRoute.Steps.Count - currentStepIndex - 1);

                for (int i = 1; i <= stepsToShow; i++)
                {
                    var stepIndex = currentStepIndex + i;
                    if (stepIndex < _currentRoute.Steps.Count)
                    {
                        var step = _currentRoute.Steps[stepIndex];
                        UpcomingInstructions.Add(step.Instruction);
                    }
                }
            }
        }

        private string FormatDistance(double meters)
        {
            if (meters < 1000)
                return $"{meters:F0} 公尺";
            else
                return $"{meters / 1000:F1} 公里";
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalMinutes < 60)
                return $"{timeSpan.TotalMinutes:F0} 分鐘";
            else
                return $"{timeSpan.Hours} 小時 {timeSpan.Minutes} 分鐘";
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts navigation with the specified route
        /// </summary>
        public async System.Threading.Tasks.Task StartNavigationAsync(Route route)
        {
            try
            {
                CurrentRoute = route;
                var session = await _navigationService.StartNavigationAsync(route);
                IsNavigating = session != null && session.IsActive;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Start navigation error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Stops current navigation
        /// </summary>
        public async System.Threading.Tasks.Task StopNavigationAsync()
        {
            try
            {
                await _navigationService.StopNavigationAsync();
                IsNavigating = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop navigation error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Updates navigation preferences
        /// </summary>
        public void UpdatePreferences(NavigationPreferences preferences)
        {
            if (_navigationService != null)
            {
                _navigationService.Preferences = preferences;
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}