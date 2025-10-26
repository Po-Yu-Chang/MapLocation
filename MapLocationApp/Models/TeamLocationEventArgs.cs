namespace MapLocationApp.Models;

/// <summary>
/// Event arguments for team location sharing events
/// </summary>
public class TeamLocationEventArgs : EventArgs
{
    /// <summary>
    /// Name of the team
    /// </summary>
    public string TeamName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the user who shared the location
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Latitude coordinate
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude coordinate
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Timestamp of the location update
    /// </summary>
    public DateTime UpdateTime { get; set; }
}
