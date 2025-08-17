using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapLocationApp.Models
{
    /// <summary>
    /// Represents a navigation instruction with voice guidance support
    /// </summary>
    public class NavigationInstruction : INotifyPropertyChanged
    {
        private string _text = string.Empty;
        private string _distance = string.Empty;
        private string _arrowIcon = string.Empty;
        private NavigationType _type = NavigationType.Continue;
        private int _distanceInMeters = 0;

        /// <summary>
        /// The instruction text to display and speak
        /// </summary>
        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        /// <summary>
        /// Formatted distance string (e.g., "100 公尺", "1.2 公里")
        /// </summary>
        public string Distance
        {
            get => _distance;
            set => SetProperty(ref _distance, value);
        }

        /// <summary>
        /// Distance in meters for calculations
        /// </summary>
        public int DistanceInMeters
        {
            get => _distanceInMeters;
            set => SetProperty(ref _distanceInMeters, value);
        }

        /// <summary>
        /// Icon resource for the navigation arrow/direction
        /// </summary>
        public string ArrowIcon
        {
            get => _arrowIcon;
            set => SetProperty(ref _arrowIcon, value);
        }

        /// <summary>
        /// Type of navigation instruction
        /// </summary>
        public NavigationType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        /// <summary>
        /// Gets the formatted voice instruction for TTS
        /// </summary>
        public string VoiceInstruction => FormatVoiceInstruction();

        private string FormatVoiceInstruction()
        {
            var distanceText = DistanceInMeters switch
            {
                < 50 => "立即",
                < 100 => "50 公尺後",
                < 500 => $"{DistanceInMeters} 公尺後",
                < 1000 => $"{DistanceInMeters} 公尺後",
                _ => $"{DistanceInMeters / 1000.0:F1} 公里後"
            };

            return Type switch
            {
                NavigationType.TurnLeft => $"{distanceText}左轉",
                NavigationType.TurnRight => $"{distanceText}右轉",
                NavigationType.TurnSlightLeft => $"{distanceText}靠左",
                NavigationType.TurnSlightRight => $"{distanceText}靠右",
                NavigationType.Continue => $"直行 {distanceText}",
                NavigationType.UTurn => $"{distanceText}迴轉",
                NavigationType.Merge => $"{distanceText}匯入主線",
                NavigationType.Exit => $"{distanceText}出口",
                NavigationType.Arrive => "您已到達目的地",
                NavigationType.Recalculating => "正在重新計算路線",
                _ => Text
            };
        }

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
    /// Types of navigation instructions
    /// </summary>
    public enum NavigationType
    {
        /// <summary>
        /// Continue straight
        /// </summary>
        Continue,

        /// <summary>
        /// Turn left
        /// </summary>
        TurnLeft,

        /// <summary>
        /// Turn right
        /// </summary>
        TurnRight,

        /// <summary>
        /// Slight turn left
        /// </summary>
        TurnSlightLeft,

        /// <summary>
        /// Slight turn right
        /// </summary>
        TurnSlightRight,

        /// <summary>
        /// U-turn
        /// </summary>
        UTurn,

        /// <summary>
        /// Merge onto highway/main road
        /// </summary>
        Merge,

        /// <summary>
        /// Take exit
        /// </summary>
        Exit,

        /// <summary>
        /// Arrive at destination
        /// </summary>
        Arrive,

        /// <summary>
        /// Route is being recalculated
        /// </summary>
        Recalculating
    }

    /// <summary>
    /// Navigation instruction templates for localization
    /// </summary>
    public static class NavigationInstructions
    {
        // Distance-based instruction templates
        public const string TURN_RIGHT_TEMPLATE = "前方 {0} 右轉";
        public const string TURN_LEFT_TEMPLATE = "前方 {0} 左轉";
        public const string CONTINUE_STRAIGHT_TEMPLATE = "直行 {0}";
        public const string SLIGHT_RIGHT_TEMPLATE = "前方 {0} 靠右";
        public const string SLIGHT_LEFT_TEMPLATE = "前方 {0} 靠左";
        public const string MERGE_TEMPLATE = "前方 {0} 匯入主線";
        public const string EXIT_TEMPLATE = "前方 {0} 出口";
        public const string UTURN_TEMPLATE = "前方 {0} 迴轉";

        // Arrival and status messages
        public const string ARRIVE_DESTINATION = "您已到達目的地";
        public const string RECALCULATING = "正在重新計算路線";
        public const string GPS_LOST = "GPS 訊號中斷";
        public const string ROUTE_DEVIATION = "偏離路線，正在重新規劃";

        /// <summary>
        /// Creates a formatted instruction based on type and distance
        /// </summary>
        public static string CreateInstruction(NavigationType type, int distanceInMeters)
        {
            var distanceText = FormatDistance(distanceInMeters);

            return type switch
            {
                NavigationType.TurnLeft => string.Format(TURN_LEFT_TEMPLATE, distanceText),
                NavigationType.TurnRight => string.Format(TURN_RIGHT_TEMPLATE, distanceText),
                NavigationType.TurnSlightLeft => string.Format(SLIGHT_LEFT_TEMPLATE, distanceText),
                NavigationType.TurnSlightRight => string.Format(SLIGHT_RIGHT_TEMPLATE, distanceText),
                NavigationType.Continue => string.Format(CONTINUE_STRAIGHT_TEMPLATE, distanceText),
                NavigationType.UTurn => string.Format(UTURN_TEMPLATE, distanceText),
                NavigationType.Merge => string.Format(MERGE_TEMPLATE, distanceText),
                NavigationType.Exit => string.Format(EXIT_TEMPLATE, distanceText),
                NavigationType.Arrive => ARRIVE_DESTINATION,
                NavigationType.Recalculating => RECALCULATING,
                _ => $"繼續前進 {distanceText}"
            };
        }

        private static string FormatDistance(int meters)
        {
            return meters switch
            {
                < 50 => "立即",
                < 1000 => $"{meters} 公尺",
                _ => $"{meters / 1000.0:F1} 公里"
            };
        }
    }
}