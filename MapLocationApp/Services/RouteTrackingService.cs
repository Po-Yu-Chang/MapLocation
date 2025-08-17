using System;
using System.Linq;
using System.Threading.Tasks;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Service for tracking route progress and detecting deviations
    /// </summary>
    public interface IRouteTrackingService
    {
        /// <summary>
        /// Checks if current location has deviated from the route
        /// </summary>
        Task<RouteDeviationResult> CheckRouteDeviationAsync(AppLocation currentLocation, Route currentRoute, int currentStepIndex);

        /// <summary>
        /// Finds the nearest point on the route to the current location
        /// </summary>
        RoutePoint FindNearestPointOnRoute(AppLocation currentLocation, Route route);

        /// <summary>
        /// Calculates progress along the current route
        /// </summary>
        RouteProgress CalculateRouteProgress(AppLocation currentLocation, Route route, int currentStepIndex);
    }

    /// <summary>
    /// Implementation of route tracking service
    /// </summary>
    public class RouteTrackingService : IRouteTrackingService
    {
        private const double ROUTE_DEVIATION_THRESHOLD = 50; // meters
        private const int CONSECUTIVE_DEVIATIONS_REQUIRED = 3;
        private const double EARTH_RADIUS_KM = 6371;

        private int _consecutiveDeviations = 0;

        public async Task<RouteDeviationResult> CheckRouteDeviationAsync(
            AppLocation currentLocation, 
            Route currentRoute, 
            int currentStepIndex)
        {
            if (currentLocation == null || currentRoute == null || currentRoute.Steps == null)
            {
                return new RouteDeviationResult { IsDeviated = false };
            }

            try
            {
                // Find the nearest point on the current route
                var nearestPoint = FindNearestPointOnRoute(currentLocation, currentRoute);
                var distanceToRoute = CalculateDistance(
                    currentLocation.Latitude, currentLocation.Longitude,
                    nearestPoint.Latitude, nearestPoint.Longitude);

                if (distanceToRoute > ROUTE_DEVIATION_THRESHOLD)
                {
                    _consecutiveDeviations++;

                    if (_consecutiveDeviations >= CONSECUTIVE_DEVIATIONS_REQUIRED)
                    {
                        return new RouteDeviationResult
                        {
                            IsDeviated = true,
                            DeviationDistance = distanceToRoute,
                            SuggestedAction = RouteAction.Recalculate,
                            NearestPoint = nearestPoint,
                            Message = $"偏離路線 {distanceToRoute:F0} 公尺，建議重新規劃路線"
                        };
                    }
                    else
                    {
                        return new RouteDeviationResult
                        {
                            IsDeviated = false,
                            DeviationDistance = distanceToRoute,
                            SuggestedAction = RouteAction.ContinueMonitoring,
                            Message = "偏離路線，正在監控中..."
                        };
                    }
                }
                else
                {
                    _consecutiveDeviations = 0;
                    return new RouteDeviationResult { IsDeviated = false };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Route deviation check error: {ex.Message}");
                return new RouteDeviationResult { IsDeviated = false };
            }
        }

        public RoutePoint FindNearestPointOnRoute(AppLocation currentLocation, Route route)
        {
            if (route?.Coordinates == null || !route.Coordinates.Any())
            {
                // Fallback to route endpoints
                return new RoutePoint
                {
                    Latitude = route?.EndLatitude ?? currentLocation.Latitude,
                    Longitude = route?.EndLongitude ?? currentLocation.Longitude,
                    Index = 0
                };
            }

            double minDistance = double.MaxValue;
            RoutePoint nearestPoint = null;

            for (int i = 0; i < route.Coordinates.Count; i++)
            {
                var coord = route.Coordinates[i];
                var distance = CalculateDistance(
                    currentLocation.Latitude, currentLocation.Longitude,
                    coord.Latitude, coord.Longitude);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestPoint = new RoutePoint
                    {
                        Latitude = coord.Latitude,
                        Longitude = coord.Longitude,
                        Index = i,
                        DistanceFromCurrent = distance
                    };
                }
            }

            return nearestPoint ?? new RoutePoint
            {
                Latitude = currentLocation.Latitude,
                Longitude = currentLocation.Longitude,
                Index = 0
            };
        }

        public RouteProgress CalculateRouteProgress(AppLocation currentLocation, Route route, int currentStepIndex)
        {
            if (route?.Steps == null || currentStepIndex >= route.Steps.Count)
            {
                return new RouteProgress
                {
                    PercentageComplete = 100,
                    DistanceRemainingMeters = 0,
                    EstimatedTimeRemaining = TimeSpan.Zero
                };
            }

            double totalDistance = route.Distance;
            double remainingDistance = 0;

            // Calculate distance to end of current step
            var currentStep = route.Steps[currentStepIndex];
            remainingDistance += CalculateDistance(
                currentLocation.Latitude, currentLocation.Longitude,
                currentStep.EndLatitude, currentStep.EndLongitude) * 1000; // Convert to meters

            // Add distance from remaining steps
            for (int i = currentStepIndex + 1; i < route.Steps.Count; i++)
            {
                remainingDistance += route.Steps[i].Distance;
            }

            var percentageComplete = Math.Max(0, Math.Min(100, 
                ((totalDistance - remainingDistance) / totalDistance) * 100));

            // Estimate remaining time based on average speed
            var averageSpeedKmh = GetAverageSpeed(route.Type);
            var remainingTimeHours = (remainingDistance / 1000.0) / averageSpeedKmh;

            return new RouteProgress
            {
                PercentageComplete = percentageComplete,
                DistanceRemainingMeters = remainingDistance,
                EstimatedTimeRemaining = TimeSpan.FromHours(remainingTimeHours)
            };
        }

        private double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            var lat1Rad = lat1 * Math.PI / 180;
            var lat2Rad = lat2 * Math.PI / 180;
            var deltaLatRad = (lat2 - lat1) * Math.PI / 180;
            var deltaLngRad = (lng2 - lng1) * Math.PI / 180;

            var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EARTH_RADIUS_KM * c * 1000; // Return in meters
        }

        private double GetAverageSpeed(RouteType routeType)
        {
            return routeType switch
            {
                RouteType.Walking => 5.0,   // km/h
                RouteType.Cycling => 15.0,  // km/h
                RouteType.Driving => 50.0,  // km/h
                _ => 50.0
            };
        }
    }

    /// <summary>
    /// Result of route deviation check
    /// </summary>
    public class RouteDeviationResult
    {
        public bool IsDeviated { get; set; }
        public double DeviationDistance { get; set; }
        public RouteAction SuggestedAction { get; set; }
        public RoutePoint NearestPoint { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Represents a point on the route
    /// </summary>
    public class RoutePoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Index { get; set; }
        public double DistanceFromCurrent { get; set; }
    }

    /// <summary>
    /// Progress information along a route
    /// </summary>
    public class RouteProgress
    {
        public double PercentageComplete { get; set; }
        public double DistanceRemainingMeters { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// Suggested actions for route deviations
    /// </summary>
    public enum RouteAction
    {
        Continue,
        ContinueMonitoring,
        Recalculate,
        StopNavigation
    }
}