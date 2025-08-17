using System;
using System.Threading.Tasks;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Central navigation service interface for managing navigation sessions
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Starts navigation for the specified route
        /// </summary>
        /// <param name="route">Route to navigate</param>
        /// <returns>Navigation session</returns>
        Task<NavigationSession> StartNavigationAsync(Route route);

        /// <summary>
        /// Stops the current navigation session
        /// </summary>
        /// <returns>Task representing the operation</returns>
        Task StopNavigationAsync();

        /// <summary>
        /// Gets the current navigation status
        /// </summary>
        /// <returns>Current navigation status</returns>
        Task<NavigationStatus> GetCurrentStatusAsync();

        /// <summary>
        /// Updates the current location and gets navigation instructions
        /// </summary>
        /// <param name="currentLocation">Current GPS location</param>
        /// <returns>Updated navigation instruction</returns>
        Task<NavigationInstruction> UpdateLocationAsync(AppLocation currentLocation);

        /// <summary>
        /// Gets whether navigation is currently active
        /// </summary>
        bool IsNavigating { get; }

        /// <summary>
        /// Current active navigation session
        /// </summary>
        NavigationSession CurrentSession { get; }

        /// <summary>
        /// Current navigation instruction
        /// </summary>
        NavigationInstruction CurrentInstruction { get; }

        /// <summary>
        /// User navigation preferences
        /// </summary>
        NavigationPreferences Preferences { get; set; }

        /// <summary>
        /// Event fired when navigation instruction is updated
        /// </summary>
        event EventHandler<NavigationInstruction> InstructionUpdated;

        /// <summary>
        /// Event fired when location is updated during navigation
        /// </summary>
        event EventHandler<AppLocation> LocationUpdated;

        /// <summary>
        /// Event fired when route deviation is detected
        /// </summary>
        event EventHandler<RouteDeviationEventArgs> RouteDeviationDetected;

        /// <summary>
        /// Event fired when arriving at destination
        /// </summary>
        event EventHandler<DestinationArrivalEventArgs> DestinationArrived;

        /// <summary>
        /// Event fired when navigation is completed
        /// </summary>
        event EventHandler<NavigationCompletedEventArgs> NavigationCompleted;
    }

    /// <summary>
    /// Navigation status information
    /// </summary>
    public class NavigationStatus
    {
        public bool IsActive { get; set; }
        public Route CurrentRoute { get; set; }
        public AppLocation CurrentLocation { get; set; }
        public NavigationInstruction NextInstruction { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public double DistanceRemainingMeters { get; set; }
        public int CurrentStepIndex { get; set; }
        public DateTime StartTime { get; set; }
        public string SessionId { get; set; }
    }

    /// <summary>
    /// Event arguments for route deviation detection
    /// </summary>
    public class RouteDeviationEventArgs : EventArgs
    {
        public double DeviationDistanceMeters { get; set; }
        public AppLocation CurrentLocation { get; set; }
        public bool RequiresRecalculation { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Event arguments for destination arrival
    /// </summary>
    public class DestinationArrivalEventArgs : EventArgs
    {
        public AppLocation DestinationLocation { get; set; }
        public double DistanceToDestinationMeters { get; set; }
        public NavigationSession Session { get; set; }
        public TimeSpan TotalNavigationTime { get; set; }
    }

    /// <summary>
    /// Event arguments for navigation completion
    /// </summary>
    public class NavigationCompletedEventArgs : EventArgs
    {
        public NavigationSession Session { get; set; }
        public bool WasSuccessful { get; set; }
        public string Reason { get; set; }
        public TimeSpan TotalTime { get; set; }
        public double TotalDistanceMeters { get; set; }
    }
}