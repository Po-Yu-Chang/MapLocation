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
                var fallbackRoute = await CreateFallbackRoute(startLat, startLng, endLat, endLng, routeType);
                
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

        private async Task<Route> CreateFallbackRoute(double startLat, double startLng, double endLat, double endLng, RouteType routeType)
        {
            var distance = CalculateDistance(startLat, startLng, endLat, endLng);
            var duration = CalculateEstimatedTime(distance, routeType);

            // 創建更真實的多步驟路線
            var steps = CreateRealisticRouteSteps(startLat, startLng, endLat, endLng, distance, routeType);

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
                StartAddress = await GetAddressFromCoordinates(startLat, startLng) ?? $"起點 ({startLat:F4}, {startLng:F4})",
                EndAddress = await GetAddressFromCoordinates(endLat, endLng) ?? $"終點 ({endLat:F4}, {endLng:F4})",
                Steps = steps
            };

            return route;
        }

        private List<RouteStep> CreateRealisticRouteSteps(double startLat, double startLng, double endLat, double endLng, double totalDistance, RouteType routeType)
        {
            var steps = new List<RouteStep>();
            
            // 計算方位角和距離
            var bearing = CalculateBearing(startLat, startLng, endLat, endLng);
            var distanceKm = totalDistance;
            
            // 根據距離創建適當的步驟數量
            int stepCount = Math.Max(2, Math.Min(8, (int)(distanceKm * 2))); // 每0.5km一個步驟，最少2步最多8步
            
            for (int i = 0; i < stepCount; i++)
            {
                double ratio = (double)i / (stepCount - 1);
                var stepStartLat = startLat + (endLat - startLat) * ratio;
                var stepStartLng = startLng + (endLng - startLng) * ratio;
                
                double nextRatio = (double)(i + 1) / (stepCount - 1);
                var stepEndLat = startLat + (endLat - startLat) * nextRatio;
                var stepEndLng = startLng + (endLng - startLng) * nextRatio;
                
                var stepDistance = CalculateDistance(stepStartLat, stepStartLng, stepEndLat, stepEndLng);
                var stepDuration = CalculateEstimatedTime(stepDistance, routeType);
                
                string instruction;
                StepType stepType;
                
                if (i == 0)
                {
                    instruction = GetStartInstruction(bearing, routeType);
                    stepType = StepType.Start;
                }
                else if (i == stepCount - 1)
                {
                    instruction = "到達目的地";
                    stepType = StepType.Arrive;
                }
                else
                {
                    instruction = GetContinueInstruction(bearing, stepDistance);
                    stepType = StepType.Straight;
                    
                    // 隨機添加一些轉彎指示使路線更真實
                    if (i % 3 == 1 && stepCount > 3)
                    {
                        var random = new Random();
                        if (random.NextDouble() > 0.7) // 30% 機率轉彎
                        {
                            stepType = random.NextDouble() > 0.5 ? StepType.TurnRight : StepType.TurnLeft;
                            instruction = stepType == StepType.TurnRight ? 
                                $"向右轉，然後繼續行駛 {stepDistance*1000:F0} 公尺" : 
                                $"向左轉，然後繼續行駛 {stepDistance*1000:F0} 公尺";
                        }
                    }
                }
                
                // 計算步驟的方位角
                var stepBearing = CalculateBearing(stepStartLat, stepStartLng, stepEndLat, stepEndLng);
                
                steps.Add(new RouteStep
                {
                    Index = i,
                    Instruction = instruction,
                    StartLatitude = stepStartLat,
                    StartLongitude = stepStartLng,
                    EndLatitude = stepEndLat,
                    EndLongitude = stepEndLng,
                    Distance = stepDistance * 1000, // 步驟距離用公尺
                    Duration = stepDuration,
                    Type = stepType,
                    Bearing = stepBearing
                });
            }
            
            return steps;
        }

        private async Task<string> GetAddressFromCoordinates(double latitude, double longitude)
        {
            try
            {
                // 使用簡單的地理編碼服務或返回座標
                // 在實際應用中，可以調用 Nominatim 或 Google Geocoding API
                return null; // 暫時返回 null，讓調用方使用座標
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"地址查詢錯誤: {ex.Message}");
                return null;
            }
        }

        private string GetStartInstruction(double bearing, RouteType routeType)
        {
            var direction = GetDirectionFromBearing(bearing);
            var vehicle = routeType switch
            {
                RouteType.Walking => "步行",
                RouteType.Cycling => "騎行",
                RouteType.Driving => "駕駛",
                _ => "前往"
            };
            
            return $"開始{vehicle}，朝{direction}方向前進";
        }

        private string GetContinueInstruction(double bearing, double distanceKm)
        {
            var direction = GetDirectionFromBearing(bearing);
            return $"繼續朝{direction}方向行駛 {distanceKm*1000:F0} 公尺";
        }

        private string GetDirectionFromBearing(double bearing)
        {
            bearing = (bearing + 360) % 360; // 確保角度為正數
            
            if (bearing < 22.5 || bearing >= 337.5) return "北";
            if (bearing < 67.5) return "東北";
            if (bearing < 112.5) return "東";
            if (bearing < 157.5) return "東南";
            if (bearing < 202.5) return "南";
            if (bearing < 247.5) return "西南";
            if (bearing < 292.5) return "西";
            return "西北";
        }

        private double CalculateBearing(double startLat, double startLng, double endLat, double endLng)
        {
            var startLatRad = ToRadians(startLat);
            var startLngRad = ToRadians(startLng);
            var endLatRad = ToRadians(endLat);
            var endLngRad = ToRadians(endLng);
            
            var deltaLng = endLngRad - startLngRad;
            
            var y = Math.Sin(deltaLng) * Math.Cos(endLatRad);
            var x = Math.Cos(startLatRad) * Math.Sin(endLatRad) - 
                    Math.Sin(startLatRad) * Math.Cos(endLatRad) * Math.Cos(deltaLng);
            
            var bearing = Math.Atan2(y, x);
            return (ToDegrees(bearing) + 360) % 360;
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

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private static double ToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }
    }
}