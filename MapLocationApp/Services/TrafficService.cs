using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Interface for traffic information services
    /// </summary>
    public interface ITrafficService
    {
        /// <summary>
        /// Gets traffic information for a specific location
        /// </summary>
        Task<TrafficInfo> GetTrafficInfoAsync(double latitude, double longitude);

        /// <summary>
        /// Gets traffic information for a route
        /// </summary>
        Task<RouteTrafficInfo> GetRouteTrafficAsync(Route route);

        /// <summary>
        /// Checks if traffic data is available for the region
        /// </summary>
        bool IsTrafficDataAvailable(double latitude, double longitude);
    }

    /// <summary>
    /// Traffic service implementation supporting multiple providers
    /// </summary>
    public class TrafficService : ITrafficService
    {
        private readonly HttpClient _httpClient;
        private readonly string _googleApiKey;
        private readonly string _hereApiKey;

        public TrafficService(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            // In a real implementation, these would be loaded from secure configuration
            _googleApiKey = GetConfigValue("GoogleMapsApiKey");
            _hereApiKey = GetConfigValue("HereApiKey");
        }

        public async Task<TrafficInfo> GetTrafficInfoAsync(double latitude, double longitude)
        {
            try
            {
                // Try Google Maps first, then fallback to HERE
                var trafficInfo = await GetGoogleTrafficInfoAsync(latitude, longitude);
                
                if (trafficInfo == null)
                {
                    trafficInfo = await GetHereTrafficInfoAsync(latitude, longitude);
                }

                // If no traffic data available, return default
                return trafficInfo ?? new TrafficInfo
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Condition = TrafficCondition.Unknown,
                    Speed = 0,
                    Timestamp = DateTime.Now,
                    Source = "None"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Traffic info error: {ex.Message}");
                return new TrafficInfo
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Condition = TrafficCondition.Unknown,
                    Speed = 0,
                    Timestamp = DateTime.Now,
                    Source = "Error"
                };
            }
        }

        public async Task<RouteTrafficInfo> GetRouteTrafficAsync(Route route)
        {
            if (route?.Coordinates == null || route.Coordinates.Count == 0)
            {
                return new RouteTrafficInfo
                {
                    RouteId = route?.Id,
                    OverallCondition = TrafficCondition.Unknown,
                    EstimatedDelayMinutes = 0,
                    TrafficSegments = new List<TrafficSegment>()
                };
            }

            try
            {
                var trafficSegments = new List<TrafficSegment>();
                var totalDelay = 0;
                var conditionCounts = new Dictionary<TrafficCondition, int>();

                // Sample traffic info along the route
                for (int i = 0; i < route.Coordinates.Count; i += Math.Max(1, route.Coordinates.Count / 10))
                {
                    var coord = route.Coordinates[i];
                    var trafficInfo = await GetTrafficInfoAsync(coord.Latitude, coord.Longitude);
                    
                    var segment = new TrafficSegment
                    {
                        StartLatitude = coord.Latitude,
                        StartLongitude = coord.Longitude,
                        Condition = trafficInfo.Condition,
                        Speed = trafficInfo.Speed,
                        DelayMinutes = CalculateSegmentDelay(trafficInfo)
                    };

                    trafficSegments.Add(segment);
                    totalDelay += segment.DelayMinutes;

                    // Count conditions for overall assessment
                    if (!conditionCounts.ContainsKey(trafficInfo.Condition))
                        conditionCounts[trafficInfo.Condition] = 0;
                    conditionCounts[trafficInfo.Condition]++;
                }

                // Determine overall condition
                var overallCondition = DetermineOverallTrafficCondition(conditionCounts);

                return new RouteTrafficInfo
                {
                    RouteId = route.Id,
                    OverallCondition = overallCondition,
                    EstimatedDelayMinutes = totalDelay,
                    TrafficSegments = trafficSegments,
                    LastUpdated = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Route traffic error: {ex.Message}");
                return new RouteTrafficInfo
                {
                    RouteId = route?.Id,
                    OverallCondition = TrafficCondition.Unknown,
                    EstimatedDelayMinutes = 0,
                    TrafficSegments = new List<TrafficSegment>()
                };
            }
        }

        public bool IsTrafficDataAvailable(double latitude, double longitude)
        {
            // Check if coordinates are in supported regions
            // For now, assume traffic data is available for major regions
            
            // Taiwan region check
            if (latitude >= 21.8 && latitude <= 25.4 && longitude >= 119.3 && longitude <= 122.1)
                return true;

            // Other major regions can be added here
            return false;
        }

        private async Task<TrafficInfo> GetGoogleTrafficInfoAsync(double latitude, double longitude)
        {
            if (string.IsNullOrEmpty(_googleApiKey))
                return null;

            try
            {
                // Google Maps Roads API for traffic data
                var requestUri = $"https://roads.googleapis.com/v1/speedLimits?" +
                               $"path={latitude},{longitude}&key={_googleApiKey}";

                var response = await _httpClient.GetAsync(requestUri);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return ParseGoogleTrafficResponse(content, latitude, longitude);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Google traffic API error: {ex.Message}");
            }

            return null;
        }

        private async Task<TrafficInfo> GetHereTrafficInfoAsync(double latitude, double longitude)
        {
            if (string.IsNullOrEmpty(_hereApiKey))
                return null;

            try
            {
                // HERE Traffic API
                var requestUri = $"https://traffic.ls.hereapi.com/traffic/6.3/flow.json?" +
                               $"prox={latitude},{longitude},1000&responseattributes=sh,fc&" +
                               $"apikey={_hereApiKey}";

                var response = await _httpClient.GetAsync(requestUri);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return ParseHereTrafficResponse(content, latitude, longitude);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HERE traffic API error: {ex.Message}");
            }

            return null;
        }

        private TrafficInfo ParseGoogleTrafficResponse(string responseContent, double lat, double lng)
        {
            try
            {
                // Mock implementation - in reality, parse the actual API response
                return new TrafficInfo
                {
                    Latitude = lat,
                    Longitude = lng,
                    Condition = TrafficCondition.Light,
                    Speed = 45, // km/h
                    Timestamp = DateTime.Now,
                    Source = "Google"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Google traffic parsing error: {ex.Message}");
                return null;
            }
        }

        private TrafficInfo ParseHereTrafficResponse(string responseContent, double lat, double lng)
        {
            try
            {
                // Mock implementation - in reality, parse the actual API response
                return new TrafficInfo
                {
                    Latitude = lat,
                    Longitude = lng,
                    Condition = TrafficCondition.Moderate,
                    Speed = 35, // km/h
                    Timestamp = DateTime.Now,
                    Source = "HERE"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HERE traffic parsing error: {ex.Message}");
                return null;
            }
        }

        private int CalculateSegmentDelay(TrafficInfo trafficInfo)
        {
            return trafficInfo.Condition switch
            {
                TrafficCondition.Heavy => 5,
                TrafficCondition.Moderate => 2,
                TrafficCondition.Light => 0,
                _ => 0
            };
        }

        private TrafficCondition DetermineOverallTrafficCondition(Dictionary<TrafficCondition, int> conditionCounts)
        {
            var totalSegments = 0;
            foreach (var count in conditionCounts.Values)
                totalSegments += count;

            if (totalSegments == 0)
                return TrafficCondition.Unknown;

            // If more than 30% heavy traffic, overall is heavy
            if (conditionCounts.GetValueOrDefault(TrafficCondition.Heavy, 0) > totalSegments * 0.3)
                return TrafficCondition.Heavy;

            // If more than 50% moderate or heavy, overall is moderate
            var moderateOrHeavy = conditionCounts.GetValueOrDefault(TrafficCondition.Moderate, 0) +
                                 conditionCounts.GetValueOrDefault(TrafficCondition.Heavy, 0);
            if (moderateOrHeavy > totalSegments * 0.5)
                return TrafficCondition.Moderate;

            return TrafficCondition.Light;
        }

        private string GetConfigValue(string key)
        {
            // In a real implementation, load from secure configuration
            // For now, return empty to indicate no API key configured
            return string.Empty;
        }
    }
}

namespace MapLocationApp.Models
{
    /// <summary>
    /// Traffic information for a specific location
    /// </summary>
    public class TrafficInfo
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public TrafficCondition Condition { get; set; }
        public double Speed { get; set; } // Current speed in km/h
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>
    /// Traffic information for an entire route
    /// </summary>
    public class RouteTrafficInfo
    {
        public string RouteId { get; set; } = string.Empty;
        public TrafficCondition OverallCondition { get; set; }
        public int EstimatedDelayMinutes { get; set; }
        public List<TrafficSegment> TrafficSegments { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Traffic information for a route segment
    /// </summary>
    public class TrafficSegment
    {
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        public TrafficCondition Condition { get; set; }
        public double Speed { get; set; }
        public int DelayMinutes { get; set; }
    }

    /// <summary>
    /// Traffic conditions
    /// </summary>
    public enum TrafficCondition
    {
        Unknown,
        Light,
        Moderate,
        Heavy,
        Stopped
    }
}