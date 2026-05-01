namespace MapLocationApp.Models;

public enum LeaveType
{
    Annual = 0,    // 特休
    Personal = 1,  // 事假
    Sick = 2,      // 病假
    Family = 3,    // 家事假
    Official = 4,  // 公假
    Other = 5
}

public class LeaveRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public LeaveType Type { get; set; } = LeaveType.Personal;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>請假天數（含起訖兩日）。半天請假進階功能可後續加 IsHalfDay。</summary>
    public int Days => Math.Max(1, (int)(EndDate.Date - StartDate.Date).TotalDays + 1);
}
