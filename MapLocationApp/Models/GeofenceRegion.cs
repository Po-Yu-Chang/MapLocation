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

    /// <summary>內勤/外勤分類；打卡時帶入 CheckInRecord 用於報表分流。</summary>
    public WorkType WorkType { get; set; } = WorkType.Office;
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

public enum CheckInType
{
    Manual,
    Automatic,
    GPS
}