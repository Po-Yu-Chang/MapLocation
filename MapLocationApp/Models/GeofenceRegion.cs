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

    /// <summary>Wi-Fi 打卡點識別（SSID）；空值代表純 GPS 打卡點。</summary>
    public string? Ssid { get; set; }

    /// <summary>選填：限定特定 BSSID（同一 SSID 多熱點時用 BSSID 鎖定）。</summary>
    public string? Bssid { get; set; }

    /// <summary>是否為 Wi-Fi 打卡點（依 Ssid 是否有值判定，不另存欄位避免不一致）。</summary>
    public bool IsWifiBased => !string.IsNullOrWhiteSpace(Ssid);
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