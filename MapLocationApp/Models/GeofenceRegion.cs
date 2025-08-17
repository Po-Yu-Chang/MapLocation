namespace MapLocationApp.Models;

public class GeofenceRegion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusMeters { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public GeofenceTransitionType TransitionType { get; set; } = GeofenceTransitionType.Both;
    
    // 用於識別這是什麼類型的地點（辦公室、客戶、等等）
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public enum GeofenceTransitionType
{
    Enter = 1,
    Exit = 2,
    Both = 3
}

public class GeofenceEvent
{
    public string GeofenceId { get; set; } = string.Empty;
    public string GeofenceName { get; set; } = string.Empty;
    public GeofenceTransitionType TransitionType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class CheckInRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string GeofenceId { get; set; } = string.Empty;
    public string GeofenceName { get; set; } = string.Empty;
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    public DateTime? CheckOutTime { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Notes { get; set; } = string.Empty;
    public CheckInType Type { get; set; } = CheckInType.Manual;
}

public enum CheckInType
{
    Manual,
    Automatic,
    GPS
}