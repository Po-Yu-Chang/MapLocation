using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MapLocationApp.Services;

namespace MapLocationApp.Models
{
    /// <summary>
    /// ViewModel for the advanced navigation UI
    /// </summary>
    public class NavigationViewModel : INotifyPropertyChanged
    {
        private NavigationInstruction? _currentInstruction;
        private string _estimatedArrivalTime = string.Empty;
        private string _remainingDistance = string.Empty;
        private string _remainingTime = string.Empty;
        private bool _isNavigating = false;
        private string _currentSpeed = "0 km/h";
        private string _speedLimit = string.Empty;
        private bool _hasSpeedLimit = false;
        private LocationSignalQuality _signalQuality = LocationSignalQuality.Poor;
        private string _routeName = string.Empty;
        private double _progressPercentage = 0.0;
        private bool _isVoiceEnabled = true;
        private bool _isOfflineMode = false;

        /// <summary>
        /// Current navigation instruction to display
        /// </summary>
        public NavigationInstruction? CurrentInstruction
        {
            get => _currentInstruction;
            set => SetProperty(ref _currentInstruction, value);
        }

        /// <summary>
        /// Estimated arrival time (e.g., "14:30", "明天 09:15")
        /// </summary>
        public string EstimatedArrivalTime
        {
            get => _estimatedArrivalTime;
            set => SetProperty(ref _estimatedArrivalTime, value);
        }

        /// <summary>
        /// Remaining distance to destination (e.g., "2.5 公里", "850 公尺")
        /// </summary>
        public string RemainingDistance
        {
            get => _remainingDistance;
            set => SetProperty(ref _remainingDistance, value);
        }

        /// <summary>
        /// Remaining time to destination (e.g., "15 分鐘", "1 小時 30 分鐘")
        /// </summary>
        public string RemainingTime
        {
            get => _remainingTime;
            set => SetProperty(ref _remainingTime, value);
        }

        /// <summary>
        /// Whether navigation is currently active
        /// </summary>
        public bool IsNavigating
        {
            get => _isNavigating;
            set => SetProperty(ref _isNavigating, value);
        }

        /// <summary>
        /// Current speed display (e.g., "65 km/h")
        /// </summary>
        public string CurrentSpeed
        {
            get => _currentSpeed;
            set => SetProperty(ref _currentSpeed, value);
        }

        /// <summary>
        /// Speed limit for current road (e.g., "60 km/h")
        /// </summary>
        public string SpeedLimit
        {
            get => _speedLimit;
            set => SetProperty(ref _speedLimit, value);
        }

        /// <summary>
        /// Whether speed limit information is available
        /// </summary>
        public bool HasSpeedLimit
        {
            get => _hasSpeedLimit;
            set => SetProperty(ref _hasSpeedLimit, value);
        }

        /// <summary>
        /// Current GPS signal quality
        /// </summary>
        public LocationSignalQuality SignalQuality
        {
            get => _signalQuality;
            set 
            { 
                SetProperty(ref _signalQuality, value);
                OnPropertyChanged(nameof(SignalQualityText));
                OnPropertyChanged(nameof(SignalQualityColor));
            }
        }

        /// <summary>
        /// Signal quality display text
        /// </summary>
        public string SignalQualityText => SignalQuality switch
        {
            LocationSignalQuality.Excellent => "GPS 訊號極佳",
            LocationSignalQuality.Good => "GPS 訊號良好",
            LocationSignalQuality.Fair => "GPS 訊號普通",
            LocationSignalQuality.Poor => "GPS 訊號微弱",
            _ => "GPS 無訊號"
        };

        /// <summary>
        /// Signal quality indicator color
        /// </summary>
        public string SignalQualityColor => SignalQuality switch
        {
            LocationSignalQuality.Excellent => "#4CAF50",
            LocationSignalQuality.Good => "#8BC34A",
            LocationSignalQuality.Fair => "#FF9800",
            LocationSignalQuality.Poor => "#F44336",
            _ => "#9E9E9E"
        };

        /// <summary>
        /// Name of the current route
        /// </summary>
        public string RouteName
        {
            get => _routeName;
            set => SetProperty(ref _routeName, value);
        }

        /// <summary>
        /// Route completion percentage (0-100)
        /// </summary>
        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, Math.Clamp(value, 0, 100));
        }

        /// <summary>
        /// Whether voice guidance is enabled
        /// </summary>
        public bool IsVoiceEnabled
        {
            get => _isVoiceEnabled;
            set => SetProperty(ref _isVoiceEnabled, value);
        }

        /// <summary>
        /// Whether the app is in offline mode
        /// </summary>
        public bool IsOfflineMode
        {
            get => _isOfflineMode;
            set => SetProperty(ref _isOfflineMode, value);
        }

        /// <summary>
        /// Whether speed limit warning should be shown (when exceeding limit)
        /// </summary>
        public bool ShowSpeedWarning { get; set; }

        /// <summary>
        /// Traffic condition color for the route
        /// </summary>
        public string TrafficColor { get; set; } = "#4CAF50";

        /// <summary>
        /// Traffic condition text
        /// </summary>
        public string TrafficCondition { get; set; } = "順暢";

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Updates navigation status with new data
        /// </summary>
        public void UpdateNavigationStatus(NavigationUpdate update)
        {
            if (update == null) return;

            if (update.IsNavigationComplete)
            {
                IsNavigating = false;
                CurrentInstruction = new NavigationInstruction
                {
                    Text = "您已到達目的地",
                    Type = NavigationType.Arrive,
                    Distance = "",
                    DistanceMeters = 0
                };
                return;
            }

            // Update remaining distance and time
            RemainingDistance = FormatDistance(update.RemainingDistance);
            RemainingTime = FormatTimeSpan(update.EstimatedTimeToDestination);
            
            // Calculate and update ETA
            var eta = DateTime.Now.Add(update.EstimatedTimeToDestination);
            EstimatedArrivalTime = eta.ToString("HH:mm");

            // Update current instruction
            if (update.CurrentStep != null)
            {
                CurrentInstruction = new NavigationInstruction
                {
                    Text = update.CurrentStep.Instruction,
                    Distance = FormatDistance(update.DistanceToNextTurn),
                    DistanceMeters = update.DistanceToNextTurn,
                    Type = ConvertStepTypeToNavigationType(update.CurrentStep.Type)
                };
            }
        }

        /// <summary>
        /// Updates current location-based data
        /// </summary>
        public void UpdateLocationData(AppLocation location)
        {
            if (location == null) return;

            // Update speed
            if (location.Speed.HasValue)
            {
                var speedKmh = location.Speed.Value * 3.6; // Convert m/s to km/h
                CurrentSpeed = $"{speedKmh:F0} km/h";
            }

            // Update signal quality
            SignalQuality = location.Accuracy switch
            {
                <= 5 => LocationSignalQuality.Excellent,
                <= 10 => LocationSignalQuality.Good,
                <= 20 => LocationSignalQuality.Fair,
                _ => LocationSignalQuality.Poor
            };
        }

        private string FormatDistance(double distanceMeters)
        {
            if (distanceMeters < 1000)
            {
                return $"{(int)distanceMeters} 公尺";
            }
            else
            {
                var km = distanceMeters / 1000.0;
                return $"{km:F1} 公里";
            }
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours} 小時 {timeSpan.Minutes} 分鐘";
            }
            else
            {
                return $"{timeSpan.Minutes} 分鐘";
            }
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

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}