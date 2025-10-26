namespace MapLocationApp.Models;

/// <summary>
/// Event arguments for check-in events
/// </summary>
public class CheckInEventArgs : EventArgs
{
    /// <summary>
    /// Latitude coordinate of the check-in
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude coordinate of the check-in
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Timestamp when the check-in was recorded
    /// </summary>
    public DateTime CheckInTime { get; set; }

    /// <summary>
    /// Additional note for the check-in
    /// </summary>
    public string? Note { get; set; }
}
