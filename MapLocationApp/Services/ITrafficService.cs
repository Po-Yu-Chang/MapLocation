using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Traffic condition severity levels
    /// </summary>
    public enum TrafficCondition
    {
        Unknown,    // No data available
        Free,       // Free flowing traffic
        Light,      // Light traffic
        Moderate,   // Moderate traffic
        Heavy,      // Heavy traffic
        Severe      // Severe congestion
    }

    /// <summary>
    /// Traffic information for a specific location or route segment
    /// </summary>
    public class TrafficInfo
    {
        public TrafficCondition Condition { get; set; }
        public string Description { get; set; } = string.Empty;
        public double AverageSpeed { get; set; } // km/h
        public double FreeFlowSpeed { get; set; } // km/h
        public TimeSpan Delay { get; set; }
        public DateTime LastUpdated { get; set; }
        public string? IncidentDescription { get; set; }
        
        /// <summary>
        /// Color representation for UI display
        /// </summary>
        public string Color => Condition switch
        {
            TrafficCondition.Free => "#4CAF50",     // Green
            TrafficCondition.Light => "#8BC34A",   // Light Green
            TrafficCondition.Moderate => "#FF9800", // Orange
            TrafficCondition.Heavy => "#FF5722",    // Deep Orange
            TrafficCondition.Severe => "#F44336",   // Red
            _ => "#9E9E9E"                          // Grey
        };

        /// <summary>
        /// Localized description in Traditional Chinese
        /// </summary>
        public string LocalizedDescription => Condition switch
        {
            TrafficCondition.Free => "順暢",
            TrafficCondition.Light => "車流量少",
            TrafficCondition.Moderate => "車流量正常",
            TrafficCondition.Heavy => "車流量大",
            TrafficCondition.Severe => "嚴重擁塞",
            _ => "無資料"
        };
    }

    /// <summary>
    /// Route traffic analysis with segment-by-segment breakdown
    /// </summary>
    public class RouteTrafficAnalysis
    {
        public TrafficCondition OverallCondition { get; set; }
        public TimeSpan TotalDelay { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public TimeSpan FreeFlowDuration { get; set; }
        public List<TrafficSegment> Segments { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public bool HasIncidents { get; set; }
        public List<TrafficIncident> Incidents { get; set; } = new();
    }

    /// <summary>
    /// Traffic information for a specific route segment
    /// </summary>
    public class TrafficSegment
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        public double EndLatitude { get; set; }
        public double EndLongitude { get; set; }
        public TrafficInfo TrafficInfo { get; set; } = new();
        public double SegmentLength { get; set; } // meters
    }

    /// <summary>
    /// Traffic incident information
    /// </summary>
    public class TrafficIncident
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "accident", "construction", "closure", etc.
        public string Description { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TrafficCondition Severity { get; set; }
        public TimeSpan EstimatedDelay { get; set; }
        
        public string LocalizedType => Type.ToLower() switch
        {
            "accident" => "事故",
            "construction" => "道路施工",
            "closure" => "道路封閉",
            "weather" => "天氣影響",
            "event" => "活動管制",
            _ => "交通狀況"
        };
    }

    /// <summary>
    /// Interface for traffic information services
    /// </summary>
    public interface ITrafficService
    {
        /// <summary>
        /// Gets current traffic information for a specific location
        /// </summary>
        Task<TrafficInfo> GetTrafficInfoAsync(double latitude, double longitude);

        /// <summary>
        /// Gets traffic analysis for an entire route
        /// </summary>
        Task<RouteTrafficAnalysis> GetRouteTrafficAsync(Route route);

        /// <summary>
        /// Gets traffic incidents in a specific area
        /// </summary>
        Task<List<TrafficIncident>> GetTrafficIncidentsAsync(double centerLat, double centerLng, double radiusKm);

        /// <summary>
        /// Checks if traffic data is available for the current location
        /// </summary>
        Task<bool> IsTrafficDataAvailableAsync(double latitude, double longitude);

        /// <summary>
        /// Event fired when traffic conditions change significantly
        /// </summary>
        event EventHandler<TrafficInfo> TrafficConditionChanged;
    }

    /// <summary>
    /// Google Maps Traffic API implementation
    /// </summary>
    public class GoogleTrafficService : ITrafficService
    {
        private const string API_BASE_URL = "https://maps.googleapis.com/maps/api/directions/json";
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly Dictionary<string, TrafficInfo> _trafficCache = new();
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

        public event EventHandler<TrafficInfo>? TrafficConditionChanged;

        public GoogleTrafficService(string apiKey)
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
        }

        public async Task<TrafficInfo> GetTrafficInfoAsync(double latitude, double longitude)
        {
            try
            {
                // Check cache first
                var cacheKey = $"{latitude:F4},{longitude:F4}";
                if (_trafficCache.TryGetValue(cacheKey, out var cachedInfo) && 
                    DateTime.UtcNow - cachedInfo.LastUpdated < _cacheExpiry)
                {
                    return cachedInfo;
                }

                // For demo purposes, simulate traffic data
                var trafficInfo = await SimulateTrafficDataAsync(latitude, longitude);
                
                // Cache the result
                _trafficCache[cacheKey] = trafficInfo;
                
                return trafficInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Traffic info error: {ex.Message}");
                return new TrafficInfo 
                { 
                    Condition = TrafficCondition.Unknown, 
                    Description = "無法取得交通資訊",
                    LastUpdated = DateTime.UtcNow
                };
            }
        }

        public async Task<RouteTrafficAnalysis> GetRouteTrafficAsync(Route route)
        {
            try
            {
                var analysis = new RouteTrafficAnalysis
                {
                    LastUpdated = DateTime.UtcNow,
                    FreeFlowDuration = route.EstimatedDuration
                };

                // Simulate route traffic analysis
                var overallCondition = await SimulateRouteTrafficAsync(route);
                analysis.OverallCondition = overallCondition;

                // Calculate delays based on traffic condition
                var delayMultiplier = overallCondition switch
                {
                    TrafficCondition.Free => 1.0,
                    TrafficCondition.Light => 1.1,
                    TrafficCondition.Moderate => 1.3,
                    TrafficCondition.Heavy => 1.6,
                    TrafficCondition.Severe => 2.0,
                    _ => 1.0
                };

                analysis.EstimatedDuration = TimeSpan.FromTicks((long)(route.EstimatedDuration.Ticks * delayMultiplier));
                analysis.TotalDelay = analysis.EstimatedDuration - analysis.FreeFlowDuration;

                return analysis;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Route traffic analysis error: {ex.Message}");
                return new RouteTrafficAnalysis
                {
                    OverallCondition = TrafficCondition.Unknown,
                    EstimatedDuration = route.EstimatedDuration,
                    FreeFlowDuration = route.EstimatedDuration,
                    LastUpdated = DateTime.UtcNow
                };
            }
        }

        public async Task<List<TrafficIncident>> GetTrafficIncidentsAsync(double centerLat, double centerLng, double radiusKm)
        {
            try
            {
                // Simulate traffic incidents
                var incidents = new List<TrafficIncident>();
                
                // Add some simulated incidents for demonstration
                var random = new Random();
                if (random.NextDouble() < 0.3) // 30% chance of incidents
                {
                    incidents.Add(new TrafficIncident
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "construction",
                        Description = "道路施工，請改道行駛",
                        Latitude = centerLat + (random.NextDouble() - 0.5) * 0.01,
                        Longitude = centerLng + (random.NextDouble() - 0.5) * 0.01,
                        StartTime = DateTime.UtcNow.AddHours(-random.Next(1, 24)),
                        Severity = TrafficCondition.Moderate,
                        EstimatedDelay = TimeSpan.FromMinutes(random.Next(5, 20))
                    });
                }

                return incidents;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Traffic incidents error: {ex.Message}");
                return new List<TrafficIncident>();
            }
        }

        public async Task<bool> IsTrafficDataAvailableAsync(double latitude, double longitude)
        {
            // For demo purposes, assume traffic data is available in major cities
            // In a real implementation, this would check API availability
            await Task.Delay(100);
            return true;
        }

        private async Task<TrafficInfo> SimulateTrafficDataAsync(double latitude, double longitude)
        {
            await Task.Delay(200); // Simulate API call delay

            var random = new Random();
            var conditions = Enum.GetValues<TrafficCondition>().Where(c => c != TrafficCondition.Unknown).ToArray();
            var condition = conditions[random.Next(conditions.Length)];

            return new TrafficInfo
            {
                Condition = condition,
                Description = GetTrafficDescription(condition),
                AverageSpeed = condition switch
                {
                    TrafficCondition.Free => random.Next(50, 70),
                    TrafficCondition.Light => random.Next(40, 60),
                    TrafficCondition.Moderate => random.Next(25, 45),
                    TrafficCondition.Heavy => random.Next(10, 30),
                    TrafficCondition.Severe => random.Next(5, 15),
                    _ => 50
                },
                FreeFlowSpeed = 60,
                Delay = condition switch
                {
                    TrafficCondition.Free => TimeSpan.Zero,
                    TrafficCondition.Light => TimeSpan.FromMinutes(random.Next(1, 3)),
                    TrafficCondition.Moderate => TimeSpan.FromMinutes(random.Next(3, 8)),
                    TrafficCondition.Heavy => TimeSpan.FromMinutes(random.Next(8, 15)),
                    TrafficCondition.Severe => TimeSpan.FromMinutes(random.Next(15, 30)),
                    _ => TimeSpan.Zero
                },
                LastUpdated = DateTime.UtcNow
            };
        }

        private async Task<TrafficCondition> SimulateRouteTrafficAsync(Route route)
        {
            await Task.Delay(300); // Simulate API call delay

            // Simulate traffic based on time of day
            var hour = DateTime.Now.Hour;
            var random = new Random();

            return hour switch
            {
                >= 7 and <= 9 => random.NextDouble() < 0.7 ? TrafficCondition.Heavy : TrafficCondition.Moderate,  // Morning rush
                >= 17 and <= 19 => random.NextDouble() < 0.7 ? TrafficCondition.Heavy : TrafficCondition.Moderate, // Evening rush
                >= 11 and <= 14 => random.NextDouble() < 0.5 ? TrafficCondition.Moderate : TrafficCondition.Light, // Lunch time
                >= 22 or <= 5 => TrafficCondition.Free, // Night time
                _ => random.NextDouble() < 0.6 ? TrafficCondition.Light : TrafficCondition.Free // Other times
            };
        }

        private string GetTrafficDescription(TrafficCondition condition)
        {
            return condition switch
            {
                TrafficCondition.Free => "交通順暢",
                TrafficCondition.Light => "車流量較少",
                TrafficCondition.Moderate => "車流量正常",
                TrafficCondition.Heavy => "交通繁忙",
                TrafficCondition.Severe => "交通嚴重擁塞",
                _ => "無交通資料"
            };
        }
    }
}