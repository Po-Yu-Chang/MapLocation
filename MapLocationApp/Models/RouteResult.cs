namespace MapLocationApp.Models
{
    public class RouteResult
    {
        public bool Success { get; set; }
        public Route? Route { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public enum RouteType
    {
        Driving,
        Walking,
        Cycling,
        Transit
    }
}