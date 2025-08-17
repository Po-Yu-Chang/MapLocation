using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Linq;

namespace MapLocationApp.Models
{
    /// <summary>
    /// Lane guidance information for navigation
    /// </summary>
    public class LaneGuidance : INotifyPropertyChanged
    {
        private List<LaneInfo> _lanes = new();
        private string _instructions = string.Empty;
        private double _distanceToLaneChange = 0;

        /// <summary>
        /// List of available lanes with their directions
        /// </summary>
        public List<LaneInfo> Lanes
        {
            get => _lanes;
            set => SetProperty(ref _lanes, value);
        }

        /// <summary>
        /// Instructions for lane guidance
        /// </summary>
        public string Instructions
        {
            get => _instructions;
            set => SetProperty(ref _instructions, value);
        }

        /// <summary>
        /// Distance in meters to where lane change is needed
        /// </summary>
        public double DistanceToLaneChange
        {
            get => _distanceToLaneChange;
            set => SetProperty(ref _distanceToLaneChange, value);
        }

        /// <summary>
        /// Gets whether lane guidance is available
        /// </summary>
        public bool IsAvailable => Lanes?.Count > 0;

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
    /// Information about a specific lane
    /// </summary>
    public class LaneInfo : INotifyPropertyChanged
    {
        private int _laneNumber = 0;
        private List<LaneDirection> _directions = new();
        private bool _isRecommended = false;
        private bool _isHighlighted = false;
        private string _laneType = "normal";

        /// <summary>
        /// Lane number (from left, 0-based)
        /// </summary>
        public int LaneNumber
        {
            get => _laneNumber;
            set => SetProperty(ref _laneNumber, value);
        }

        /// <summary>
        /// Available directions for this lane
        /// </summary>
        public List<LaneDirection> Directions
        {
            get => _directions;
            set => SetProperty(ref _directions, value);
        }

        /// <summary>
        /// Whether this lane is recommended for the current route
        /// </summary>
        public bool IsRecommended
        {
            get => _isRecommended;
            set => SetProperty(ref _isRecommended, value);
        }

        /// <summary>
        /// Whether this lane should be highlighted in the UI
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set => SetProperty(ref _isHighlighted, value);
        }

        /// <summary>
        /// Type of lane (normal, bus, hov, etc.)
        /// </summary>
        public string LaneType
        {
            get => _laneType;
            set => SetProperty(ref _laneType, value);
        }

        /// <summary>
        /// Gets the primary direction arrow icon for this lane
        /// </summary>
        public string PrimaryDirectionIcon
        {
            get
            {
                if (Directions?.Count > 0)
                {
                    return GetDirectionIcon(Directions[0]);
                }
                return "lane_straight";
            }
        }

        /// <summary>
        /// Gets display text for this lane's directions
        /// </summary>
        public string DirectionText
        {
            get
            {
                if (Directions?.Count == 0)
                    return "直行";

                var directionTexts = new List<string>();
                foreach (var direction in Directions)
                {
                    directionTexts.Add(GetDirectionText(direction));
                }

                return string.Join(" / ", directionTexts);
            }
        }

        private string GetDirectionIcon(LaneDirection direction)
        {
            return direction switch
            {
                LaneDirection.Straight => "lane_straight",
                LaneDirection.Left => "lane_left",
                LaneDirection.Right => "lane_right",
                LaneDirection.SlightLeft => "lane_slight_left",
                LaneDirection.SlightRight => "lane_slight_right",
                LaneDirection.SharpLeft => "lane_sharp_left",
                LaneDirection.SharpRight => "lane_sharp_right",
                LaneDirection.UTurn => "lane_uturn",
                LaneDirection.Exit => "lane_exit",
                LaneDirection.Merge => "lane_merge",
                _ => "lane_straight"
            };
        }

        private string GetDirectionText(LaneDirection direction)
        {
            return direction switch
            {
                LaneDirection.Straight => "直行",
                LaneDirection.Left => "左轉",
                LaneDirection.Right => "右轉",
                LaneDirection.SlightLeft => "靠左",
                LaneDirection.SlightRight => "靠右",
                LaneDirection.SharpLeft => "急左轉",
                LaneDirection.SharpRight => "急右轉",
                LaneDirection.UTurn => "迴轉",
                LaneDirection.Exit => "出口",
                LaneDirection.Merge => "匯入",
                _ => "直行"
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
    /// Lane direction options
    /// </summary>
    public enum LaneDirection
    {
        /// <summary>
        /// Continue straight
        /// </summary>
        Straight,

        /// <summary>
        /// Turn left
        /// </summary>
        Left,

        /// <summary>
        /// Turn right
        /// </summary>
        Right,

        /// <summary>
        /// Slight left turn
        /// </summary>
        SlightLeft,

        /// <summary>
        /// Slight right turn
        /// </summary>
        SlightRight,

        /// <summary>
        /// Sharp left turn
        /// </summary>
        SharpLeft,

        /// <summary>
        /// Sharp right turn
        /// </summary>
        SharpRight,

        /// <summary>
        /// U-turn
        /// </summary>
        UTurn,

        /// <summary>
        /// Take exit
        /// </summary>
        Exit,

        /// <summary>
        /// Merge
        /// </summary>
        Merge
    }
}