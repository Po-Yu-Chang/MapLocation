using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapLocationApp.Models
{
    /// <summary>
    /// Navigation instruction model for turn-by-turn guidance
    /// </summary>
    public class NavigationInstruction : INotifyPropertyChanged
    {
        private string _text = string.Empty;
        private string _distance = string.Empty;
        private string _arrowIcon = "arrow_straight";
        private NavigationType _type = NavigationType.Continue;
        private InstructionTiming _timing = InstructionTiming.Normal;
        private bool _isSpoken = false;

        /// <summary>
        /// The instruction text to display/speak
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
        /// Icon resource name for the direction arrow
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
            set 
            { 
                SetProperty(ref _type, value);
                UpdateArrowIcon();
            }
        }

        /// <summary>
        /// When this instruction should be delivered
        /// </summary>
        public InstructionTiming Timing
        {
            get => _timing;
            set => SetProperty(ref _timing, value);
        }

        /// <summary>
        /// Whether this instruction has been spoken via TTS
        /// </summary>
        public bool IsSpoken
        {
            get => _isSpoken;
            set => SetProperty(ref _isSpoken, value);
        }

        /// <summary>
        /// Distance in meters for calculations
        /// </summary>
        public double DistanceMeters { get; set; }

        /// <summary>
        /// Priority level for instruction delivery
        /// </summary>
        public int Priority => Type switch
        {
            NavigationType.Arrive => 5,
            NavigationType.UTurn => 4,
            NavigationType.TurnLeft or NavigationType.TurnRight => 3,
            NavigationType.TurnSlightLeft or NavigationType.TurnSlightRight => 2,
            _ => 1
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        private void UpdateArrowIcon()
        {
            ArrowIcon = Type switch
            {
                NavigationType.TurnLeft => "arrow_turn_left",
                NavigationType.TurnRight => "arrow_turn_right",
                NavigationType.TurnSlightLeft => "arrow_slight_left",
                NavigationType.TurnSlightRight => "arrow_slight_right",
                NavigationType.UTurn => "arrow_u_turn",
                NavigationType.Merge => "arrow_merge",
                NavigationType.Exit => "arrow_exit",
                NavigationType.Arrive => "flag_destination",
                NavigationType.RoundaboutEnter => "roundabout_enter",
                NavigationType.RoundaboutExit => "roundabout_exit",
                _ => "arrow_straight"
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

    /// <summary>
    /// Navigation types from NavigationInstructions.cs
    /// </summary>
    public enum NavigationType
    {
        TurnLeft,
        TurnRight,
        TurnSlightLeft,
        TurnSlightRight,
        Continue,
        UTurn,
        Merge,
        Exit,
        Arrive,
        RoundaboutEnter,
        RoundaboutExit
    }

    /// <summary>
    /// Instruction timing from NavigationInstructions.cs
    /// </summary>
    public enum InstructionTiming
    {
        Immediate,      // 0-50m
        Prepare,        // 50-200m
        Normal,         // 200m-1km
        LongDistance    // >1km
    }
}