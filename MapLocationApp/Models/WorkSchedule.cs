namespace MapLocationApp.Models;

/// <summary>個人週排班：每個 weekday 可獨立設定上下班時段，或標記為休假。</summary>
public class WorkSchedule
{
    /// <summary>0=Sunday … 6=Saturday，與 DateTime.DayOfWeek 對齊。</summary>
    public int WeekDay { get; set; }

    /// <summary>true 表示此日為休假，忽略 Start/End。</summary>
    public bool IsRestDay { get; set; }

    public TimeSpan? Start { get; set; }
    public TimeSpan? End { get; set; }

    public string? Note { get; set; }
}
