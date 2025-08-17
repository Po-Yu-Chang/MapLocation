using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Service for providing lane guidance information
    /// </summary>
    public interface ILaneGuidanceService
    {
        /// <summary>
        /// Gets lane guidance for the current location and route
        /// </summary>
        Task<LaneGuidance> GetLaneGuidanceAsync(AppLocation currentLocation, Route route, int currentStepIndex);

        /// <summary>
        /// Gets whether lane guidance is available for the given location
        /// </summary>
        bool IsLaneGuidanceAvailable(AppLocation location);
    }

    /// <summary>
    /// Lane guidance service implementation
    /// </summary>
    public class LaneGuidanceService : ILaneGuidanceService
    {
        public async Task<LaneGuidance> GetLaneGuidanceAsync(AppLocation currentLocation, Route route, int currentStepIndex)
        {
            if (currentLocation == null || route?.Steps == null || currentStepIndex >= route.Steps.Count)
            {
                return new LaneGuidance();
            }

            try
            {
                // In a real implementation, this would query map data APIs for lane information
                // For now, we'll provide a mock implementation based on the route step type
                var currentStep = route.Steps[currentStepIndex];
                var lanes = GenerateMockLaneInfo(currentStep);

                var guidance = new LaneGuidance
                {
                    Lanes = lanes,
                    Instructions = GenerateLaneInstructions(lanes, currentStep),
                    DistanceToLaneChange = CalculateDistanceToManeuver(currentLocation, currentStep)
                };

                return guidance;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lane guidance error: {ex.Message}");
                return new LaneGuidance();
            }
        }

        public bool IsLaneGuidanceAvailable(AppLocation location)
        {
            // In a real implementation, check if we have lane data for this location
            // For now, assume it's available for major roads
            return true;
        }

        private List<LaneInfo> GenerateMockLaneInfo(RouteStep step)
        {
            var lanes = new List<LaneInfo>();

            // Generate mock lane data based on step type
            switch (step.Type)
            {
                case StepType.TurnLeft:
                    lanes.AddRange(new[]
                    {
                        new LaneInfo { LaneNumber = 0, Directions = new List<LaneDirection> { LaneDirection.Left }, IsRecommended = true, IsHighlighted = true },
                        new LaneInfo { LaneNumber = 1, Directions = new List<LaneDirection> { LaneDirection.Straight } },
                        new LaneInfo { LaneNumber = 2, Directions = new List<LaneDirection> { LaneDirection.Straight, LaneDirection.Right } }
                    });
                    break;

                case StepType.TurnRight:
                    lanes.AddRange(new[]
                    {
                        new LaneInfo { LaneNumber = 0, Directions = new List<LaneDirection> { LaneDirection.Left, LaneDirection.Straight } },
                        new LaneInfo { LaneNumber = 1, Directions = new List<LaneDirection> { LaneDirection.Straight } },
                        new LaneInfo { LaneNumber = 2, Directions = new List<LaneDirection> { LaneDirection.Right }, IsRecommended = true, IsHighlighted = true }
                    });
                    break;

                case StepType.Straight:
                default:
                    lanes.AddRange(new[]
                    {
                        new LaneInfo { LaneNumber = 0, Directions = new List<LaneDirection> { LaneDirection.Left } },
                        new LaneInfo { LaneNumber = 1, Directions = new List<LaneDirection> { LaneDirection.Straight }, IsRecommended = true, IsHighlighted = true },
                        new LaneInfo { LaneNumber = 2, Directions = new List<LaneDirection> { LaneDirection.Straight, LaneDirection.Right } }
                    });
                    break;
            }

            return lanes;
        }

        private string GenerateLaneInstructions(List<LaneInfo> lanes, RouteStep step)
        {
            var recommendedLanes = lanes.Where(l => l.IsRecommended).ToList();

            if (recommendedLanes.Count == 0)
                return "請選擇適當車道";

            if (recommendedLanes.Count == 1)
            {
                var lane = recommendedLanes[0];
                var position = GetLanePositionText(lane.LaneNumber, lanes.Count);
                return $"請使用{position}車道{lane.DirectionText}";
            }

            return "請使用建議車道";
        }

        private string GetLanePositionText(int laneNumber, int totalLanes)
        {
            if (totalLanes <= 2)
                return laneNumber == 0 ? "左側" : "右側";

            if (laneNumber == 0)
                return "最左側";
            if (laneNumber == totalLanes - 1)
                return "最右側";
            if (laneNumber == 1 && totalLanes == 3)
                return "中間";

            return $"第{laneNumber + 1}";
        }

        private double CalculateDistanceToManeuver(AppLocation currentLocation, RouteStep step)
        {
            // Calculate distance to the end of current step
            const double earthRadius = 6371; // Earth radius in kilometers

            var lat1Rad = currentLocation.Latitude * Math.PI / 180;
            var lat2Rad = step.EndLatitude * Math.PI / 180;
            var deltaLatRad = (step.EndLatitude - currentLocation.Latitude) * Math.PI / 180;
            var deltaLngRad = (step.EndLongitude - currentLocation.Longitude) * Math.PI / 180;

            var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadius * c * 1000; // Return in meters
        }
    }
}