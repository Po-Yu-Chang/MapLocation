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
                System.Diagnostics.Debug.WriteLine($"計算路線: ({startLat}, {startLng}) -> ({endLat}, {endLng}), 類型: {routeType}");
                
                // 首先嘗試使用真實路線 API
                var realRoute = await GetRealRouteAsync(startLat, startLng, endLat, endLng, routeType);
                if (realRoute != null)
                {
                    System.Diagnostics.Debug.WriteLine("成功獲取真實路線");
                    return new RouteResult
                    {
                        Success = true,
                        Route = realRoute,
                        ErrorMessage = null
                    };
                }

                System.Diagnostics.Debug.WriteLine("真實路線獲取失敗，使用後備路線");
                
                // 如果無法取得線上路線，使用簡單的直線路線
                var fallbackRoute = CreateFallbackRoute(startLat, startLng, endLat, endLng, routeType);
                
                return new RouteResult
                {
                    Success = true,
                    Route = fallbackRoute,
                    ErrorMessage = "使用後備路線規劃"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"路線計算錯誤: {ex.Message}");
                return new RouteResult
                {
                    Success = false,
                    Route = null,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<Route> GetRealRouteAsync(double startLat, double startLng, double endLat, double endLng, RouteType routeType)
        {
            try
            {
                // 使用 OpenRouteService API
                var profile = routeType switch
                {
                    RouteType.Walking => "foot-walking",
                    RouteType.Cycling => "cycling-regular", 
                    RouteType.Driving => "driving-car",
                    _ => "driving-car"
                };

                // 構建 API URL
                var apiUrl = $"https://api.openrouteservice.org/v2/directions/{profile}?" +
                           $"api_key=YOUR_API_KEY&" +
                           $"start={startLng},{startLat}&" +
                           $"end={endLng},{endLat}&" +
                           "format=json";

                System.Diagnostics.Debug.WriteLine($"呼叫 OpenRouteService API: {apiUrl}");

                // 暫時模擬 API 呼叫失敗，使用後備路線
                // 實際部署時需要註冊 OpenRouteService API Key
                return null;

                /* 實際 API 呼叫程式碼 (需要 API Key)
                var response = await _httpClient.GetStringAsync(apiUrl);
                var jsonDoc = JsonDocument.Parse(response);
                
                if (jsonDoc.RootElement.TryGetProperty("features", out var features) && 
                    features.GetArrayLength() > 0)
                {
                    var feature = features[0];
                    var properties = feature.GetProperty("properties");
                    var geometry = feature.GetProperty("geometry");
                    
                    var route = new Route
                    {
                        Id = Guid.NewGuid().ToString(),
                        StartLatitude = startLat,
                        StartLongitude = startLng,
                        EndLatitude = endLat,
                        EndLongitude = endLng,
                        Distance = properties.GetProperty("summary").GetProperty("distance").GetDouble() / 1000.0, // 轉換為公里
                        EstimatedDuration = TimeSpan.FromSeconds(properties.GetProperty("summary").GetProperty("duration").GetDouble()),
                        CreatedAt = DateTime.Now,
                        StartAddress = $"起點 ({startLat:F4}, {startLng:F4})",
                        EndAddress = $"終點 ({endLat:F4}, {endLng:F4})"
                    };

                    // 解析路線步驟
                    if (properties.TryGetProperty("segments", out var segments))
                    {
                        route.Steps = ParseRouteSteps(segments, geometry);
                    }

                    return route;
                }
                */
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"真實路線 API 錯誤: {ex.Message}");
            }

            return null;
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
                StartLatitude = startLat,
                StartLongitude = startLng,
                EndLatitude = endLat,
                EndLongitude = endLng,
                Distance = distance, // 保持公里單位，與現有程式碼一致
                EstimatedDuration = duration,
                CreatedAt = DateTime.Now,
                StartAddress = $"起點 ({startLat:F4}, {startLng:F4})",
                EndAddress = $"終點 ({endLat:F4}, {endLng:F4})",
                Steps = new List<RouteStep>
                {
                    new RouteStep
                    {
                        Index = 0,
                        Instruction = "直行前往目的地",
                        StartLatitude = startLat,
                        StartLongitude = startLng,
                        EndLatitude = endLat,
                        EndLongitude = endLng,
                        Distance = distance * 1000, // 步驟距離用公尺
                        Duration = duration,
                        Type = StepType.Straight
                    }
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
}