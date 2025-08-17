using MapLocationApp.Services;

namespace MapLocationApp.Models
{
    public class NavigationSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RouteId { get; set; }
        public Route Route { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int CurrentStepIndex { get; set; }
        public bool IsActive { get; set; }
        public AppLocation? CurrentLocation { get; set; }
        public double? DistanceRemaining { get; set; }
        public TimeSpan? TimeRemaining { get; set; }
        public string? CurrentInstruction { get; set; }
    }
}