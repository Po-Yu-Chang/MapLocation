using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Route action suggestions for deviation handling
    /// </summary>
    public enum RouteAction
    {
        Continue,       // Stay on current route
        Recalculate,    // Recalculate the entire route
        GetBackOnTrack, // Guide back to original route
        TakeAlternate   // Suggest alternate route
    }

    /// <summary>
    /// Result of route deviation analysis
    /// </summary>
    public class RouteDeviationResult
    {
        public bool IsDeviated { get; set; }
        public double DeviationDistance { get; set; } // meters
        public RouteAction SuggestedAction { get; set; }
        public string? Message { get; set; }
        public AppLocation? NearestRoutePoint { get; set; }
        public int? NearestStepIndex { get; set; }
    }

    /// <summary>
    /// Interface for route tracking and deviation detection
    /// </summary>
    public interface IRouteTrackingService
    {
        /// <summary>
        /// Checks if current location deviates from the planned route
        /// </summary>
        Task<RouteDeviationResult> CheckRouteDeviationAsync(AppLocation currentLocation, Route currentRoute);

        /// <summary>
        /// Finds the nearest point on the route to the current location
        /// </summary>
        AppLocation FindNearestPointOnRoute(AppLocation currentLocation, Route route);

        /// <summary>
        /// Calculates progress along the current route
        /// </summary>
        RouteProgress CalculateRouteProgress(AppLocation currentLocation, Route route);

        /// <summary>
        /// Event fired when route deviation is detected
        /// </summary>
        event EventHandler<RouteDeviationResult> RouteDeviationDetected;

        /// <summary>
        /// Resets deviation tracking state
        /// </summary>
        void ResetDeviationTracking();
    }

    /// <summary>
    /// Route progress information
    /// </summary>
    public class RouteProgress
    {
        public double CompletedDistance { get; set; } // meters
        public double RemainingDistance { get; set; } // meters
        public double ProgressPercentage { get; set; } // 0-100
        public int CurrentStepIndex { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// Route tracking service for monitoring deviation from planned routes
    /// </summary>
    public class RouteTrackingService : IRouteTrackingService
    {
        private const double ROUTE_DEVIATION_THRESHOLD = 50; // meters
        private const int CONSECUTIVE_DEVIATIONS_REQUIRED = 3;
        private const double MAJOR_DEVIATION_THRESHOLD = 200; // meters for immediate recalculation

        private int _consecutiveDeviations = 0;
        private AppLocation? _lastKnownGoodLocation;
        private DateTime _lastDeviationTime = DateTime.MinValue;

        public event EventHandler<RouteDeviationResult>? RouteDeviationDetected;

        public async Task<RouteDeviationResult> CheckRouteDeviationAsync(AppLocation currentLocation, Route currentRoute)
        {
            try
            {
                // Find the nearest point on the route
                var nearestPoint = FindNearestPointOnRoute(currentLocation, currentRoute);
                var distanceToRoute = CalculateDistance(currentLocation, nearestPoint);

                // Check for major deviation (immediate action required)
                if (distanceToRoute > MAJOR_DEVIATION_THRESHOLD)
                {
                    var majorDeviationResult = new RouteDeviationResult
                    {
                        IsDeviated = true,
                        DeviationDistance = distanceToRoute,
                        SuggestedAction = RouteAction.Recalculate,
                        Message = "Major route deviation detected",
                        NearestRoutePoint = nearestPoint
                    };

                    RouteDeviationDetected?.Invoke(this, majorDeviationResult);
                    return majorDeviationResult;
                }

                // Check for minor deviation with consecutive detection
                if (distanceToRoute > ROUTE_DEVIATION_THRESHOLD)
                {
                    _consecutiveDeviations++;
                    _lastDeviationTime = DateTime.UtcNow;

                    if (_consecutiveDeviations >= CONSECUTIVE_DEVIATIONS_REQUIRED)
                    {
                        var deviationResult = new RouteDeviationResult
                        {
                            IsDeviated = true,
                            DeviationDistance = distanceToRoute,
                            SuggestedAction = DetermineRouteAction(distanceToRoute, currentLocation, currentRoute),
                            Message = "Route deviation detected",
                            NearestRoutePoint = nearestPoint,
                            NearestStepIndex = FindNearestStepIndex(currentLocation, currentRoute)
                        };

                        RouteDeviationDetected?.Invoke(this, deviationResult);
                        return deviationResult;
                    }
                }
                else
                {
                    // Back on track
                    _consecutiveDeviations = 0;
                    _lastKnownGoodLocation = currentLocation;
                }

                return new RouteDeviationResult 
                { 
                    IsDeviated = false,
                    DeviationDistance = distanceToRoute,
                    SuggestedAction = RouteAction.Continue,
                    NearestRoutePoint = nearestPoint
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Route deviation check error: {ex.Message}");
                return new RouteDeviationResult { IsDeviated = false, SuggestedAction = RouteAction.Continue };
            }
        }

        public AppLocation FindNearestPointOnRoute(AppLocation currentLocation, Route route)
        {
            if (route.Coordinates == null || !route.Coordinates.Any())
            {
                // If no coordinates, use route endpoints
                var startDistance = CalculateDistance(currentLocation, 
                    new AppLocation { Latitude = route.StartLatitude, Longitude = route.StartLongitude });
                var endDistance = CalculateDistance(currentLocation,
                    new AppLocation { Latitude = route.EndLatitude, Longitude = route.EndLongitude });

                return startDistance < endDistance
                    ? new AppLocation { Latitude = route.StartLatitude, Longitude = route.StartLongitude }
                    : new AppLocation { Latitude = route.EndLatitude, Longitude = route.EndLongitude };
            }

            var nearestPoint = route.Coordinates.First();
            var minDistance = CalculateDistance(currentLocation, 
                new AppLocation { Latitude = nearestPoint.Latitude, Longitude = nearestPoint.Longitude });

            foreach (var coord in route.Coordinates.Skip(1))
            {
                var point = new AppLocation { Latitude = coord.Latitude, Longitude = coord.Longitude };
                var distance = CalculateDistance(currentLocation, point);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestPoint = coord;
                }
            }

            return new AppLocation 
            { 
                Latitude = nearestPoint.Latitude, 
                Longitude = nearestPoint.Longitude 
            };
        }

        public RouteProgress CalculateRouteProgress(AppLocation currentLocation, Route route)
        {
            try
            {
                var nearestStepIndex = FindNearestStepIndex(currentLocation, route);
                var completedDistance = 0.0;
                var remainingDistance = 0.0;

                // Calculate completed distance
                for (int i = 0; i < nearestStepIndex && i < route.Steps.Count; i++)
                {
                    completedDistance += route.Steps[i].Distance;
                }

                // Add partial distance to current step
                if (nearestStepIndex < route.Steps.Count)
                {
                    var currentStep = route.Steps[nearestStepIndex];
                    var stepStart = new AppLocation 
                    { 
                        Latitude = currentStep.StartLatitude, 
                        Longitude = currentStep.StartLongitude 
                    };
                    var distanceToStepStart = CalculateDistance(stepStart, currentLocation) * 1000; // Convert to meters
                    completedDistance += Math.Min(distanceToStepStart, currentStep.Distance);
                }

                // Calculate remaining distance
                for (int i = nearestStepIndex; i < route.Steps.Count; i++)
                {
                    remainingDistance += route.Steps[i].Distance;
                }

                var totalDistance = route.Distance;
                var progressPercentage = totalDistance > 0 ? (completedDistance / totalDistance) * 100 : 0;

                // Estimate remaining time based on route type
                var estimatedSpeed = GetEstimatedSpeed(route.Type);
                var remainingTimeHours = (remainingDistance / 1000.0) / estimatedSpeed;

                return new RouteProgress
                {
                    CompletedDistance = completedDistance,
                    RemainingDistance = remainingDistance,
                    ProgressPercentage = Math.Clamp(progressPercentage, 0, 100),
                    CurrentStepIndex = nearestStepIndex,
                    EstimatedTimeRemaining = TimeSpan.FromHours(remainingTimeHours)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Route progress calculation error: {ex.Message}");
                return new RouteProgress();
            }
        }

        private RouteAction DetermineRouteAction(double deviationDistance, AppLocation currentLocation, Route route)
        {
            // Logic to determine the best action based on deviation context
            if (deviationDistance > 100) // Major deviation
            {
                return RouteAction.Recalculate;
            }

            // Check if we can get back to the route easily
            var nearestPoint = FindNearestPointOnRoute(currentLocation, route);
            var timeToReturn = EstimateTimeToReturnToRoute(currentLocation, nearestPoint);

            return timeToReturn < TimeSpan.FromMinutes(2) 
                ? RouteAction.GetBackOnTrack 
                : RouteAction.Recalculate;
        }

        private int FindNearestStepIndex(AppLocation currentLocation, Route route)
        {
            if (!route.Steps.Any()) return 0;

            var nearestIndex = 0;
            var minDistance = double.MaxValue;

            for (int i = 0; i < route.Steps.Count; i++)
            {
                var step = route.Steps[i];
                var stepLocation = new AppLocation 
                { 
                    Latitude = step.StartLatitude, 
                    Longitude = step.StartLongitude 
                };
                var distance = CalculateDistance(currentLocation, stepLocation);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private double CalculateDistance(AppLocation location1, AppLocation location2)
        {
            const double earthRadius = 6371000; // Earth radius in meters

            var lat1Rad = location1.Latitude * Math.PI / 180;
            var lat2Rad = location2.Latitude * Math.PI / 180;
            var deltaLatRad = (location2.Latitude - location1.Latitude) * Math.PI / 180;
            var deltaLngRad = (location2.Longitude - location1.Longitude) * Math.PI / 180;

            var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadius * c; // Distance in meters
        }

        private double GetEstimatedSpeed(RouteType routeType)
        {
            return routeType switch
            {
                RouteType.Walking => 5.0,   // 5 km/h
                RouteType.Cycling => 15.0,  // 15 km/h
                RouteType.Driving => 50.0,  // 50 km/h
                _ => 50.0
            };
        }

        private TimeSpan EstimateTimeToReturnToRoute(AppLocation currentLocation, AppLocation routePoint)
        {
            var distance = CalculateDistance(currentLocation, routePoint);
            var walkingSpeed = 1.4; // m/s (average walking speed)
            var timeSeconds = distance / walkingSpeed;
            return TimeSpan.FromSeconds(timeSeconds);
        }

        public void ResetDeviationTracking()
        {
            _consecutiveDeviations = 0;
            _lastKnownGoodLocation = null;
            _lastDeviationTime = DateTime.MinValue;
        }
    }
}