namespace MapLocationApp.Services;

public interface ILocationService
{
    Task<AppLocation?> GetCurrentLocationAsync();
    Task<bool> RequestLocationPermissionAsync();
    Task<bool> IsLocationEnabledAsync();
    event EventHandler<AppLocation> LocationChanged;
    Task StartLocationUpdatesAsync();
    Task StopLocationUpdatesAsync();
}

public class AppLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Accuracy { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double? Altitude { get; set; }
    public double? Speed { get; set; }
    public double? Course { get; set; }
    public double? Heading { get; set; }
}