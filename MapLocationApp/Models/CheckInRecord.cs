namespace MapLocationApp.Models;

/// <summary>
/// Represents a check-in record with XSS protection for user-generated content.
/// T013: Implements input validation and sanitization for Notes property
/// </summary>
public class CheckInRecord
{
    /// <summary>
    /// Unique identifier for the check-in record
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User ID who created this check-in
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Geofence ID associated with this check-in
    /// </summary>
    public string GeofenceId { get; set; } = string.Empty;

    /// <summary>
    /// Geofence name associated with this check-in
    /// </summary>
    public string GeofenceName { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the check-in was recorded
    /// </summary>
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional check-out time
    /// </summary>
    public DateTime? CheckOutTime { get; set; }

    /// <summary>
    /// Latitude coordinate of the check-in
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude coordinate of the check-in
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Name of the location (for reporting purposes)
    /// </summary>
    public string? LocationName { get; set; }

    private string _notes = string.Empty;

    /// <summary>
    /// User notes with XSS protection and length validation.
    /// T013: Maximum 500 characters, HTML/script tags removed, encoded for safety
    /// </summary>
    public string Notes
    {
        get => _notes;
        set => _notes = SanitizeNotes(value);
    }

    /// <summary>
    /// Type of check-in
    /// </summary>
    public CheckInType Type { get; set; } = CheckInType.Manual;

    /// <summary>
    /// Whether this check-in has been synced to the database
    /// </summary>
    public bool IsSynced { get; set; }

    /// <summary>定位方式（GPS/Wifi/Manual），與 Type 的觸發語意分開。</summary>
    public CheckInMethod Method { get; set; } = CheckInMethod.GPS;

    /// <summary>內勤/外勤分類，由所屬 Geofence 帶入；手動打卡時可由使用者選擇。</summary>
    public WorkType WorkType { get; set; } = WorkType.Office;

    /// <summary>最後編輯時間；null 表示未被編輯過。</summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>編輯/補打卡原因（user-supplied，套用相同 Notes XSS 過濾）。</summary>
    public string? EditReason
    {
        get => _editReason;
        set => _editReason = string.IsNullOrEmpty(value) ? null : SanitizeShortText(value);
    }
    private string? _editReason;

    private static string SanitizeShortText(string input)
    {
        var s = System.Text.RegularExpressions.Regex.Replace(input,
            @"<script[^>]*>.*?</script>|<[^>]+>", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[<>""'`]", string.Empty);
        if (s.Length > 200) s = s.Substring(0, 200);
        return s.Trim();
    }

    /// <summary>
    /// Sanitizes user input to prevent XSS attacks
    /// T013: Removes HTML/script tags, limits length, encodes special characters
    /// </summary>
    /// <param name="input">Raw user input</param>
    /// <returns>Sanitized safe string</returns>
    private static string SanitizeNotes(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // T013: Step 1 - Remove HTML/script tags (XSS prevention)
        var sanitized = System.Text.RegularExpressions.Regex.Replace(
            input,
            @"<script[^>]*>.*?</script>|<[^>]+>",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
        );

        // T013: Step 2 - Remove potentially dangerous characters
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"[<>""'`]",
            string.Empty
        );

        // T013: Step 3 - Limit length to 500 characters
        if (sanitized.Length > 500)
        {
            sanitized = sanitized.Substring(0, 500);
        }

        // T013: Step 4 - Trim whitespace
        return sanitized.Trim();
    }
}
