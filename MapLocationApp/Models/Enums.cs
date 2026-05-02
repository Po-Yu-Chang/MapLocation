namespace MapLocationApp.Models;

/// <summary>內勤/外勤分類，用於打卡點與打卡紀錄分流報表。</summary>
public enum WorkType
{
    Office = 0,
    Field = 1
}

/// <summary>實際定位技術（與 CheckInType 的「觸發方式」分開）。</summary>
public enum CheckInMethod
{
    GPS = 0,
    Wifi = 1,
    Manual = 2
}

/// <summary>使用者角色 — 管理員 / 員工。預設新建帳號為 Employee。</summary>
public enum UserRole
{
    Employee = 0,
    Admin = 1
}
