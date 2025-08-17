using System.Text.Json;
using System.Text.Json.Serialization;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    public interface IRouteService
    {
        Task<RouteResult> CalculateRouteAsync(double startLat, double startLng, double endLat, double endLng, RouteType routeType = RouteType.Driving);
        Task<List<RouteStep>> GetRouteStepsAsync(string routeId);
        Task<bool> SaveRouteAsync(Route route);
        Task<List<Route>> GetSavedRoutesAsync();
        Task<bool> DeleteRouteAsync(string routeId);
        Task<NavigationSession> StartNavigationAsync(string routeId);
        Task<NavigationUpdate> GetNavigationUpdateAsync(string sessionId, double currentLat, double currentLng);
        Task EndNavigationAsync(string sessionId);
    }

    public class RouteService : IRouteService
    {
        private readonly HttpClient _httpClient;
        private readonly string _routesFile;
        private readonly Dictionary<string, NavigationSession> _activeSessions;

        public RouteService()
        {
            _httpClient = new HttpClient();
            _routesFile = Path.Combine(FileSystem.AppDataDirectory, "saved_routes.json");
            _activeSessions = new Dictionary<string, NavigationSession>();
        }

        public async Task<RouteResult> CalculateRouteAsync(double startLat, double startLng, double endLat, double endLng, RouteType routeType = RouteType.Driving)
        {
            try
            {
                // 使用 OpenRouteService API (需要註冊 API 金鑰)
                var profile = routeType switch
                {
                    RouteType.Walking => "foot-walking",
                    RouteType.Cycling => "cycling-regular",
                    RouteType.Driving => "driving-car",
                    _ => "driving-car"
                };

                // 如果無法取得線上路線，使用簡單的直線路線
                var fallbackRoute = CreateFallbackRoute(startLat, startLng, endLat, endLng, routeType);
                
                return new RouteResult
                {
                    Success = true,
                    Route = fallbackRoute,
                    ErrorMessage = null
                };
            }
            catch (Exception ex)
            {
                return new RouteResult
                {
                    Success = false,
                    Route = null,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<List<RouteStep>> GetRouteStepsAsync(string routeId)
        {
            try
            {
                var routes = await GetSavedRoutesAsync();
                var route = routes.FirstOrDefault(r => r.Id == routeId);
                return route?.Steps ?? new List<RouteStep>();
            }
            catch
            {
                return new List<RouteStep>();
            }
        }

        public async Task<bool> SaveRouteAsync(Route route)
        {
            try
            {
                var routes = await GetSavedRoutesAsync();
                
                // 檢查是否已存在相同 ID 的路線
                var existingRoute = routes.FirstOrDefault(r => r.Id == route.Id);
                if (existingRoute != null)
                {
                    routes.Remove(existingRoute);
                }
                
                routes.Add(route);
                
                var json = JsonSerializer.Serialize(routes, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_routesFile, json);
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save route error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Route>> GetSavedRoutesAsync()
        {
            try
            {
                if (!File.Exists(_routesFile))
                    return new List<Route>();

                var json = await File.ReadAllTextAsync(_routesFile);
                return JsonSerializer.Deserialize<List<Route>>(json) ?? new List<Route>();
            }
            catch
            {
                return new List<Route>();
            }
        }

        public async Task<bool> DeleteRouteAsync(string routeId)
        {
            try
            {
                var routes = await GetSavedRoutesAsync();
                var route = routes.FirstOrDefault(r => r.Id == routeId);
                
                if (route != null)
                {
                    routes.Remove(route);
                    var json = JsonSerializer.Serialize(routes, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(_routesFile, json);
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<NavigationSession> StartNavigationAsync(string routeId)
        {
            try
            {
                var routes = await GetSavedRoutesAsync();
                var route = routes.FirstOrDefault(r => r.Id == routeId);
                
                if (route == null)
                    return null;

                var session = new NavigationSession
                {
                    Id = Guid.NewGuid().ToString(),
                    RouteId = routeId,
                    Route = route,
                    StartTime = DateTime.Now,
                    CurrentStepIndex = 0,
                    IsActive = true
                };

                _activeSessions[session.Id] = session;
                return session;
            }
            catch
            {
                return null;
            }
        }

        public async Task<NavigationUpdate> GetNavigationUpdateAsync(string sessionId, double currentLat, double currentLng)
        {
            try
            {
                if (!_activeSessions.TryGetValue(sessionId, out var session))
                    return null;

                var currentStep = session.Route.Steps[session.CurrentStepIndex];
                var distanceToStep = CalculateDistance(currentLat, currentLng, 
                    currentStep.EndLatitude, currentStep.EndLongitude);

                // 如果距離步驟終點小於 20 公尺，移動到下一個步驟
                if (distanceToStep < 0.02) // 約 20 公尺
                {
                    session.CurrentStepIndex++;
                    if (session.CurrentStepIndex >= session.Route.Steps.Count)
                    {
                        // 導航完成
                        session.IsActive = false;
                        return new NavigationUpdate
                        {
                            SessionId = sessionId,
                            IsNavigationComplete = true,
                            CurrentStep = null,
                            DistanceToNextTurn = 0,
                            EstimatedTimeToDestination = TimeSpan.Zero,
                            RemainingDistance = 0
                        };
                    }
                    currentStep = session.Route.Steps[session.CurrentStepIndex];
                }

                var totalRemainingDistance = CalculateRemainingDistance(session, currentLat, currentLng);
                var estimatedTime = CalculateEstimatedTime(totalRemainingDistance, session.Route.Type);

                return new NavigationUpdate
                {
                    SessionId = sessionId,
                    IsNavigationComplete = false,
                    CurrentStep = currentStep,
                    DistanceToNextTurn = distanceToStep * 1000, // 轉換為公尺
                    EstimatedTimeToDestination = estimatedTime,
                    RemainingDistance = totalRemainingDistance * 1000 // 轉換為公尺
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task EndNavigationAsync(string sessionId)
        {
            try
            {
                if (_activeSessions.TryGetValue(sessionId, out var session))
                {
                    session.IsActive = false;
                    session.EndTime = DateTime.Now;
                    _activeSessions.Remove(sessionId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"End navigation error: {ex.Message}");
            }
        }

        private Route CreateFallbackRoute(double startLat, double startLng, double endLat, double endLng, RouteType routeType)
        {
            var distance = CalculateDistance(startLat, startLng, endLat, endLng);
            var duration = CalculateEstimatedTime(distance, routeType);

            var route = new Route
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Route to destination",
                StartLatitude = startLat,
                StartLongitude = startLng,
                EndLatitude = endLat,
                EndLongitude = endLng,
                Type = routeType,
                Distance = distance * 1000, // 轉換為公尺
                EstimatedDuration = duration,
                CreatedDate = DateTime.Now,
                Steps = new List<RouteStep>
                {
                    new RouteStep
                    {
                        Index = 0,
                        Instruction = "前往目的地",
                        StartLatitude = startLat,
                        StartLongitude = startLng,
                        EndLatitude = endLat,
                        EndLongitude = endLng,
                        Distance = distance * 1000,
                        Duration = duration,
                        Type = StepType.Straight
                    }
                },
                Coordinates = new List<RouteCoordinate>
                {
                    new RouteCoordinate { Latitude = startLat, Longitude = startLng },
                    new RouteCoordinate { Latitude = endLat, Longitude = endLng }
                }
            };

            return route;
        }

        private double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            const double earthRadius = 6371; // 地球半徑（公里）
            
            var lat1Rad = lat1 * Math.PI / 180;
            var lat2Rad = lat2 * Math.PI / 180;
            var deltaLatRad = (lat2 - lat1) * Math.PI / 180;
            var deltaLngRad = (lng2 - lng1) * Math.PI / 180;

            var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);
            
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return earthRadius * c;
        }

        private TimeSpan CalculateEstimatedTime(double distanceKm, RouteType routeType)
        {
            var speedKmh = routeType switch
            {
                RouteType.Walking => 5.0,
                RouteType.Cycling => 15.0,
                RouteType.Driving => 50.0,
                _ => 50.0
            };

            var hours = distanceKm / speedKmh;
            return TimeSpan.FromHours(hours);
        }

        private double CalculateRemainingDistance(NavigationSession session, double currentLat, double currentLng)
        {
            var totalDistance = 0.0;
            
            // 計算到當前步驟終點的距離
            var currentStep = session.Route.Steps[session.CurrentStepIndex];
            totalDistance += CalculateDistance(currentLat, currentLng, 
                currentStep.EndLatitude, currentStep.EndLongitude);

            // 加上剩餘步驟的距離
            for (int i = session.CurrentStepIndex + 1; i < session.Route.Steps.Count; i++)
            {
                totalDistance += session.Route.Steps[i].Distance / 1000.0; // 轉換為公里
            }

            return totalDistance;
        }
    }

    // 資料模型
    public class RouteResult
    {
        public bool Success { get; set; }
        public Route Route { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class Route
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        public double EndLatitude { get; set; }
        public double EndLongitude { get; set; }
        public string? StartAddress { get; set; } // 新增：起點地址
        public string? EndAddress { get; set; } // 新增：終點地址
        public RouteType Type { get; set; }
        public double Distance { get; set; } // 公尺
        public double DistanceInMeters => Distance; // 方便存取的屬性
        public TimeSpan EstimatedDuration { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<RouteStep> Steps { get; set; } = new();
        public List<RouteCoordinate> Coordinates { get; set; } = new();
        
        // Google Maps 風格的顯示屬性
        public string FromAddress => StartAddress ?? $"{StartLatitude:F6}, {StartLongitude:F6}";
        public string ToAddress => EndAddress ?? $"{EndLatitude:F6}, {EndLongitude:F6}";
        public string Duration => EstimatedDuration.TotalHours >= 1 
            ? $"{EstimatedDuration.Hours}小時{EstimatedDuration.Minutes}分鐘"
            : $"{EstimatedDuration.Minutes}分鐘";
    }

    public class RouteStep
    {
        public int Index { get; set; }
        public string Instruction { get; set; }
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        public double EndLatitude { get; set; }
        public double EndLongitude { get; set; }
        public double Distance { get; set; } // 公尺
        public TimeSpan Duration { get; set; }
        public StepType Type { get; set; }
    }

    public class RouteCoordinate
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class NavigationUpdate
    {
        public string SessionId { get; set; }
        public bool IsNavigationComplete { get; set; }
        public RouteStep CurrentStep { get; set; }
        public double DistanceToNextTurn { get; set; } // 公尺
        public TimeSpan EstimatedTimeToDestination { get; set; }
        public double RemainingDistance { get; set; } // 公尺
    }

    public enum RouteType
    {
        Driving,
        Walking,
        Cycling
    }

    public enum StepType
    {
        Straight,
        TurnLeft,
        TurnRight,
        UTurn,
        RoundaboutEnter,
        RoundaboutExit
    }
}