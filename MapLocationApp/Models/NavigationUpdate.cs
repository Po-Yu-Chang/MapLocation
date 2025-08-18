using System;

namespace MapLocationApp.Models
{
    public class NavigationUpdate
    {
        public string SessionId { get; set; } = string.Empty;
        public bool IsNavigationComplete { get; set; }
        public RouteStep? CurrentStep { get; set; }
        public double DistanceToNextTurn { get; set; } // 公尺
        public TimeSpan EstimatedTimeToDestination { get; set; }
        public double RemainingDistance { get; set; } // 公尺
    }
}