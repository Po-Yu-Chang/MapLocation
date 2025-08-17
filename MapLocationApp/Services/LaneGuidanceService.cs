using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Lane direction types for guidance
    /// </summary>
    public enum LaneDirection
    {
        Straight,
        Left,
        Right,
        SlightLeft,
        SlightRight,
        UTurn,
        Exit,
        Merge,
        StraightOrLeft,
        StraightOrRight,
        LeftOrRight
    }

    /// <summary>
    /// Lane information for display
    /// </summary>
    public class LaneInfo
    {
        /// <summary>
        /// Lane number from left (0-based)
        /// </summary>
        public int LaneNumber { get; set; }

        /// <summary>
        /// Primary direction for this lane
        /// </summary>
        public LaneDirection Direction { get; set; }

        /// <summary>
        /// Whether this lane is recommended for the current route
        /// </summary>
        public bool IsRecommended { get; set; }

        /// <summary>
        /// Additional instructions for this lane
        /// </summary>
        public string Instructions { get; set; } = string.Empty;

        /// <summary>
        /// Icon resource name for lane marking
        /// </summary>
        public string IconResource => Direction switch
        {
            LaneDirection.Straight => "lane_straight",
            LaneDirection.Left => "lane_left",
            LaneDirection.Right => "lane_right",
            LaneDirection.SlightLeft => "lane_slight_left",
            LaneDirection.SlightRight => "lane_slight_right",
            LaneDirection.UTurn => "lane_uturn",
            LaneDirection.Exit => "lane_exit",
            LaneDirection.Merge => "lane_merge",
            LaneDirection.StraightOrLeft => "lane_straight_left",
            LaneDirection.StraightOrRight => "lane_straight_right",
            LaneDirection.LeftOrRight => "lane_left_right",
            _ => "lane_straight"
        };

        /// <summary>
        /// Color for lane highlight
        /// </summary>
        public string LaneColor => IsRecommended ? "#2196F3" : "#9E9E9E";

        /// <summary>
        /// Accessibility description
        /// </summary>
        public string AccessibilityLabel => IsRecommended 
            ? $"建議車道：{GetDirectionDescription()}"
            : $"車道：{GetDirectionDescription()}";

        private string GetDirectionDescription()
        {
            return Direction switch
            {
                LaneDirection.Straight => "直行",
                LaneDirection.Left => "左轉",
                LaneDirection.Right => "右轉",
                LaneDirection.SlightLeft => "靠左",
                LaneDirection.SlightRight => "靠右",
                LaneDirection.UTurn => "迴轉",
                LaneDirection.Exit => "出口",
                LaneDirection.Merge => "匯入",
                LaneDirection.StraightOrLeft => "直行或左轉",
                LaneDirection.StraightOrRight => "直行或右轉",
                LaneDirection.LeftOrRight => "左轉或右轉",
                _ => "未知方向"
            };
        }
    }

    /// <summary>
    /// Lane guidance analysis result
    /// </summary>
    public class LaneGuidanceResult
    {
        /// <summary>
        /// All available lanes
        /// </summary>
        public List<LaneInfo> Lanes { get; set; } = new();

        /// <summary>
        /// Recommended lanes for the current route
        /// </summary>
        public List<LaneInfo> RecommendedLanes => Lanes.Where(l => l.IsRecommended).ToList();

        /// <summary>
        /// Distance to the lane guidance point (meters)
        /// </summary>
        public double DistanceToGuidancePoint { get; set; }

        /// <summary>
        /// Main instruction for lane selection
        /// </summary>
        public string MainInstruction { get; set; } = string.Empty;

        /// <summary>
        /// Whether lane guidance is available for this location
        /// </summary>
        public bool IsAvailable => Lanes.Any();

        /// <summary>
        /// Whether the user should prepare for lane change
        /// </summary>
        public bool ShouldPrepareLaneChange { get; set; }
    }

    /// <summary>
    /// Interface for lane guidance service
    /// </summary>
    public interface ILaneGuidanceService
    {
        /// <summary>
        /// Gets lane guidance for the current location and route
        /// </summary>
        Task<LaneGuidanceResult> GetLaneGuidanceAsync(AppLocation currentLocation, Route route);

        /// <summary>
        /// Analyzes upcoming lane requirements for the next instruction
        /// </summary>
        Task<LaneGuidanceResult> AnalyzeUpcomingLanesAsync(RouteStep nextStep, double distanceToStep);

        /// <summary>
        /// Checks if lane guidance is available for the given location
        /// </summary>
        Task<bool> IsLaneGuidanceAvailableAsync(AppLocation location);

        /// <summary>
        /// Event fired when lane guidance becomes available
        /// </summary>
        event EventHandler<LaneGuidanceResult> LaneGuidanceAvailable;

        /// <summary>
        /// Event fired when user should prepare for lane change
        /// </summary>
        event EventHandler<LaneGuidanceResult> LaneChangeRequired;
    }

    /// <summary>
    /// Lane guidance service implementation
    /// </summary>
    public class LaneGuidanceService : ILaneGuidanceService
    {
        private const double LANE_GUIDANCE_DISTANCE = 500; // Show guidance 500m before
        private const double LANE_CHANGE_WARNING_DISTANCE = 200; // Warn 200m before

        public event EventHandler<LaneGuidanceResult>? LaneGuidanceAvailable;
        public event EventHandler<LaneGuidanceResult>? LaneChangeRequired;

        public async Task<LaneGuidanceResult> GetLaneGuidanceAsync(AppLocation currentLocation, Route route)
        {
            try
            {
                // Find the next significant instruction that requires lane guidance
                var currentStep = FindCurrentStep(currentLocation, route);
                if (currentStep == null)
                {
                    return new LaneGuidanceResult();
                }

                var distanceToStep = CalculateDistanceToStep(currentLocation, currentStep);

                // Only provide lane guidance when approaching intersections
                if (distanceToStep > LANE_GUIDANCE_DISTANCE)
                {
                    return new LaneGuidanceResult();
                }

                var laneGuidance = await AnalyzeUpcomingLanesAsync(currentStep, distanceToStep);
                
                // Fire events if guidance is available
                if (laneGuidance.IsAvailable)
                {
                    LaneGuidanceAvailable?.Invoke(this, laneGuidance);

                    if (laneGuidance.ShouldPrepareLaneChange)
                    {
                        LaneChangeRequired?.Invoke(this, laneGuidance);
                    }
                }

                return laneGuidance;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lane guidance error: {ex.Message}");
                return new LaneGuidanceResult();
            }
        }

        public async Task<LaneGuidanceResult> AnalyzeUpcomingLanesAsync(RouteStep nextStep, double distanceToStep)
        {
            try
            {
                await Task.Delay(100); // Simulate analysis delay

                var laneGuidance = new LaneGuidanceResult
                {
                    DistanceToGuidancePoint = distanceToStep,
                    ShouldPrepareLaneChange = distanceToStep <= LANE_CHANGE_WARNING_DISTANCE
                };

                // Generate lane configuration based on step type
                var lanes = GenerateLaneConfiguration(nextStep.Type);
                laneGuidance.Lanes = lanes;
                laneGuidance.MainInstruction = GenerateMainInstruction(nextStep.Type, lanes);

                return laneGuidance;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Analyze lanes error: {ex.Message}");
                return new LaneGuidanceResult();
            }
        }

        public async Task<bool> IsLaneGuidanceAvailableAsync(AppLocation location)
        {
            try
            {
                // In a real implementation, this would check if the location
                // has detailed road network data with lane information
                await Task.Delay(50);

                // For demo purposes, assume lane guidance is available in urban areas
                // This would typically query a mapping service or local database
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Check lane guidance availability error: {ex.Message}");
                return false;
            }
        }

        private List<LaneInfo> GenerateLaneConfiguration(StepType stepType)
        {
            // Generate realistic lane configurations based on turn type
            return stepType switch
            {
                StepType.TurnLeft => CreateLeftTurnLanes(),
                StepType.TurnRight => CreateRightTurnLanes(),
                StepType.Straight => CreateStraightLanes(),
                StepType.UTurn => CreateUTurnLanes(),
                _ => CreateStraightLanes()
            };
        }

        private List<LaneInfo> CreateLeftTurnLanes()
        {
            return new List<LaneInfo>
            {
                new LaneInfo { LaneNumber = 0, Direction = LaneDirection.Left, IsRecommended = true, Instructions = "左轉車道" },
                new LaneInfo { LaneNumber = 1, Direction = LaneDirection.StraightOrLeft, IsRecommended = true, Instructions = "直行或左轉" },
                new LaneInfo { LaneNumber = 2, Direction = LaneDirection.Straight, IsRecommended = false, Instructions = "直行車道" },
                new LaneInfo { LaneNumber = 3, Direction = LaneDirection.Right, IsRecommended = false, Instructions = "右轉車道" }
            };
        }

        private List<LaneInfo> CreateRightTurnLanes()
        {
            return new List<LaneInfo>
            {
                new LaneInfo { LaneNumber = 0, Direction = LaneDirection.Left, IsRecommended = false, Instructions = "左轉車道" },
                new LaneInfo { LaneNumber = 1, Direction = LaneDirection.Straight, IsRecommended = false, Instructions = "直行車道" },
                new LaneInfo { LaneNumber = 2, Direction = LaneDirection.StraightOrRight, IsRecommended = true, Instructions = "直行或右轉" },
                new LaneInfo { LaneNumber = 3, Direction = LaneDirection.Right, IsRecommended = true, Instructions = "右轉車道" }
            };
        }

        private List<LaneInfo> CreateStraightLanes()
        {
            return new List<LaneInfo>
            {
                new LaneInfo { LaneNumber = 0, Direction = LaneDirection.Left, IsRecommended = false, Instructions = "左轉車道" },
                new LaneInfo { LaneNumber = 1, Direction = LaneDirection.Straight, IsRecommended = true, Instructions = "直行車道" },
                new LaneInfo { LaneNumber = 2, Direction = LaneDirection.Straight, IsRecommended = true, Instructions = "直行車道" },
                new LaneInfo { LaneNumber = 3, Direction = LaneDirection.Right, IsRecommended = false, Instructions = "右轉車道" }
            };
        }

        private List<LaneInfo> CreateUTurnLanes()
        {
            return new List<LaneInfo>
            {
                new LaneInfo { LaneNumber = 0, Direction = LaneDirection.UTurn, IsRecommended = true, Instructions = "迴轉車道" },
                new LaneInfo { LaneNumber = 1, Direction = LaneDirection.Left, IsRecommended = false, Instructions = "左轉車道" },
                new LaneInfo { LaneNumber = 2, Direction = LaneDirection.Straight, IsRecommended = false, Instructions = "直行車道" },
                new LaneInfo { LaneNumber = 3, Direction = LaneDirection.Right, IsRecommended = false, Instructions = "右轉車道" }
            };
        }

        private string GenerateMainInstruction(StepType stepType, List<LaneInfo> lanes)
        {
            var recommendedLanes = lanes.Where(l => l.IsRecommended).ToList();
            
            if (!recommendedLanes.Any())
                return "保持目前車道";

            if (recommendedLanes.Count == 1)
            {
                var lane = recommendedLanes.First();
                return stepType switch
                {
                    StepType.TurnLeft => $"請切換到左側第 {lane.LaneNumber + 1} 車道左轉",
                    StepType.TurnRight => $"請切換到右側第 {4 - lane.LaneNumber} 車道右轉",
                    StepType.UTurn => $"請切換到最左側車道準備迴轉",
                    _ => $"保持在第 {lane.LaneNumber + 1} 車道直行"
                };
            }
            else
            {
                var laneNumbers = string.Join("、", recommendedLanes.Select(l => $"第{l.LaneNumber + 1}"));
                return $"請使用 {laneNumbers} 車道";
            }
        }

        private RouteStep? FindCurrentStep(AppLocation currentLocation, Route route)
        {
            if (!route.Steps.Any()) return null;

            // Find the closest upcoming step
            var minDistance = double.MaxValue;
            RouteStep? closestStep = null;

            foreach (var step in route.Steps)
            {
                var stepLocation = new AppLocation
                {
                    Latitude = step.StartLatitude,
                    Longitude = step.StartLongitude
                };

                var distance = CalculateDistance(currentLocation, stepLocation);
                if (distance < minDistance && NeedsLaneGuidance(step.Type))
                {
                    minDistance = distance;
                    closestStep = step;
                }
            }

            return closestStep;
        }

        private bool NeedsLaneGuidance(StepType stepType)
        {
            return stepType switch
            {
                StepType.TurnLeft or StepType.TurnRight or StepType.UTurn => true,
                _ => false
            };
        }

        private double CalculateDistanceToStep(AppLocation currentLocation, RouteStep step)
        {
            var stepLocation = new AppLocation
            {
                Latitude = step.StartLatitude,
                Longitude = step.StartLongitude
            };

            return CalculateDistance(currentLocation, stepLocation);
        }

        private double CalculateDistance(AppLocation location1, AppLocation location2)
        {
            const double earthRadius = 6371000; // Earth radius in meters

            var lat1Rad = location1.Latitude * Math.PI / 180;
            var lat2Rad = location2.Latitude * Math.PI / 180;
            var deltaLatRad = (location2.Latitude - location1.Latitude) * Math.PI / 180;
            var deltaLngRad = (location2.Longitude - location1.Longitude) * Math.PI / 180;

            var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadius * c;
        }
    }
}