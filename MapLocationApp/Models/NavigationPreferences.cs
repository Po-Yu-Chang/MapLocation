using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapLocationApp.Models
{
    /// <summary>
    /// User preferences for navigation behavior
    /// </summary>
    public class NavigationPreferences : INotifyPropertyChanged
    {
        private bool _avoidTolls = false;
        private bool _avoidHighways = false;
        private bool _avoidFerries = false;
        private VoiceGuidanceLevel _voiceLevel = VoiceGuidanceLevel.Normal;
        private string _preferredLanguage = "zh-TW";
        private RouteOptimization _defaultOptimization = RouteOptimization.Time;
        private float _speechRate = 1.0f;
        private float _speechVolume = 1.0f;
        private bool _vibrateOnTurn = true;
        private bool _showLaneGuidance = true;
        private int _arrivalNotificationDistance = 100; // meters

        /// <summary>
        /// Avoid toll roads when possible
        /// </summary>
        public bool AvoidTolls
        {
            get => _avoidTolls;
            set => SetProperty(ref _avoidTolls, value);
        }

        /// <summary>
        /// Avoid highways when possible
        /// </summary>
        public bool AvoidHighways
        {
            get => _avoidHighways;
            set => SetProperty(ref _avoidHighways, value);
        }

        /// <summary>
        /// Avoid ferry routes when possible
        /// </summary>
        public bool AvoidFerries
        {
            get => _avoidFerries;
            set => SetProperty(ref _avoidFerries, value);
        }

        /// <summary>
        /// Level of voice guidance detail
        /// </summary>
        public VoiceGuidanceLevel VoiceLevel
        {
            get => _voiceLevel;
            set => SetProperty(ref _voiceLevel, value);
        }

        /// <summary>
        /// Preferred language for voice guidance (e.g., "zh-TW", "en-US")
        /// </summary>
        public string PreferredLanguage
        {
            get => _preferredLanguage;
            set => SetProperty(ref _preferredLanguage, value);
        }

        /// <summary>
        /// Default route optimization preference
        /// </summary>
        public RouteOptimization DefaultOptimization
        {
            get => _defaultOptimization;
            set => SetProperty(ref _defaultOptimization, value);
        }

        /// <summary>
        /// Speech rate for voice guidance (0.1 to 2.0)
        /// </summary>
        public float SpeechRate
        {
            get => _speechRate;
            set => SetProperty(ref _speechRate, Math.Clamp(value, 0.1f, 2.0f));
        }

        /// <summary>
        /// Speech volume for voice guidance (0.0 to 1.0)
        /// </summary>
        public float SpeechVolume
        {
            get => _speechVolume;
            set => SetProperty(ref _speechVolume, Math.Clamp(value, 0.0f, 1.0f));
        }

        /// <summary>
        /// Vibrate device when making turns
        /// </summary>
        public bool VibrateOnTurn
        {
            get => _vibrateOnTurn;
            set => SetProperty(ref _vibrateOnTurn, value);
        }

        /// <summary>
        /// Show lane guidance information
        /// </summary>
        public bool ShowLaneGuidance
        {
            get => _showLaneGuidance;
            set => SetProperty(ref _showLaneGuidance, value);
        }

        /// <summary>
        /// Distance in meters to notify arrival at destination
        /// </summary>
        public int ArrivalNotificationDistance
        {
            get => _arrivalNotificationDistance;
            set => SetProperty(ref _arrivalNotificationDistance, Math.Max(value, 10));
        }

        /// <summary>
        /// Gets whether voice guidance is enabled
        /// </summary>
        public bool IsVoiceEnabled => VoiceLevel != VoiceGuidanceLevel.Off;

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
    }

    /// <summary>
    /// Levels of voice guidance detail
    /// </summary>
    public enum VoiceGuidanceLevel
    {
        /// <summary>
        /// No voice guidance
        /// </summary>
        Off,

        /// <summary>
        /// Only essential instructions (turns, arrivals)
        /// </summary>
        Essential,

        /// <summary>
        /// Normal guidance level (recommended)
        /// </summary>
        Normal,

        /// <summary>
        /// Detailed guidance with additional information
        /// </summary>
        Detailed
    }

    /// <summary>
    /// Route optimization preferences
    /// </summary>
    public enum RouteOptimization
    {
        /// <summary>
        /// Optimize for fastest time
        /// </summary>
        Time,

        /// <summary>
        /// Optimize for shortest distance
        /// </summary>
        Distance,

        /// <summary>
        /// Optimize for fuel efficiency/environmental impact
        /// </summary>
        EcoFriendly,

        /// <summary>
        /// Balanced optimization
        /// </summary>
        Balanced
    }
}