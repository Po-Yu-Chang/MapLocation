using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    /// <summary>
    /// 進階導航服務實作
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly ILocationService _locationService;
        private readonly IRouteService _routeService;
        private readonly ITTSService _ttsService;
        private readonly ITelegramNotificationService _telegramService;
        
        private Timer _navigationTimer;
        private NavigationState _currentState;
        private CancellationTokenSource _cancellationTokenSource;
        
        // 常數設定
        private const double ARRIVAL_THRESHOLD = 20.0; // 公尺
        private const double ROUTE_DEVIATION_THRESHOLD = 50.0; // 公尺
        private const int CONSECUTIVE_DEVIATIONS_REQUIRED = 3;
        private const int NAVIGATION_UPDATE_INTERVAL = 2000; // 毫秒
        private const double INSTRUCTION_DISTANCE_THRESHOLD = 500.0; // 公尺
        private const double NEXT_INSTRUCTION_THRESHOLD = 200.0; // 公尺
        private const double APPROACH_INSTRUCTION_THRESHOLD = 100.0; // 公尺
        
        private int _consecutiveDeviations = 0;
        private NavigationInstruction _lastSpokenInstruction;
        private DateTime _lastInstructionTime = DateTime.MinValue;
        private int _currentStepIndex = 0;
        private double _totalRouteDistance = 0;
        private double _distanceTraveled = 0;

        public NavigationState CurrentState => _currentState ?? new NavigationState();
        public bool IsNavigating => _currentState?.IsActive == true;

        // 事件
        public event EventHandler<NavigationInstruction> InstructionUpdated;
        public event EventHandler<AppLocation> LocationUpdated;
        public event EventHandler<RouteDeviationResult> RouteDeviated;
        public event EventHandler DestinationReached;
        public event EventHandler<NavigationState> StateChanged;
        public event EventHandler<Exception> NavigationError;

        public NavigationService(
            ILocationService locationService,
            IRouteService routeService,
            ITTSService ttsService,
            ITelegramNotificationService telegramService)
        {
            _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _ttsService = ttsService ?? throw new ArgumentNullException(nameof(ttsService));
            _telegramService = telegramService;
            
            _currentState = new NavigationState();
        }

        public async Task StartNavigationAsync(Route route)
        {
            try
            {
                Debug.WriteLine("NavigationService: 開始導航");
                
                if (route == null)
                    throw new ArgumentNullException(nameof(route));

                // 停止現有導航
                if (IsNavigating)
                {
                    await StopNavigationAsync();
                }

                // 初始化導航狀態
                _currentState = new NavigationState
                {
                    CurrentRoute = route,
                    IsActive = true,
                    StartTime = DateTime.Now,
                    EstimatedTimeRemaining = route.EstimatedDuration,
                    DistanceRemaining = route.Distance * 1000, // 轉換為公尺
                    RouteProgress = 0.0
                };
                
                // 重置導航變數
                _currentStepIndex = 0;
                _totalRouteDistance = route.Distance * 1000;
                _distanceTraveled = 0;

                // 取得目前位置
                var currentLocation = await _locationService.GetCurrentLocationAsync();
                if (currentLocation != null)
                {
                    _currentState.CurrentLocation = currentLocation;
                    await UpdateNavigationStateAsync(currentLocation);
                }

                // 開始位置追蹤
                StartLocationTracking();

                // 發送導航開始通知
                if (_telegramService != null)
                {
                    await _telegramService.SendRouteNotificationAsync(
                        "使用者", 
                        "導航開始", 
                        route.StartLatitude, 
                        route.StartLongitude, 
                        route.EndLatitude, 
                        route.EndLongitude);
                }

                // 播放導航開始語音
                await _ttsService.SpeakAsync("導航開始，請依照指示行駛");

                StateChanged?.Invoke(this, _currentState);
                
                Debug.WriteLine("NavigationService: 導航已成功開始");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NavigationService: 開始導航錯誤 - {ex.Message}");
                NavigationError?.Invoke(this, ex);
                throw;
            }
        }

        public async Task StopNavigationAsync()
        {
            try
            {
                Debug.WriteLine("NavigationService: 停止導航");

                // 停止位置追蹤
                StopLocationTracking();

                // 停止語音
                if (_ttsService != null)
                {
                    await _ttsService.StopSpeakingAsync();
                }

                // 發送導航結束通知
                if (_telegramService != null && _currentState?.CurrentRoute != null)
                {
                    await _telegramService.SendRouteNotificationAsync(
                        "使用者", 
                        "導航結束", 
                        _currentState.CurrentRoute.StartLatitude, 
                        _currentState.CurrentRoute.StartLongitude, 
                        _currentState.CurrentRoute.EndLatitude, 
                        _currentState.CurrentRoute.EndLongitude);
                }

                // 重置狀態
                if (_currentState != null)
                {
                    _currentState.IsActive = false;
                }

                StateChanged?.Invoke(this, _currentState);
                
                Debug.WriteLine("NavigationService: 導航已停止");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NavigationService: 停止導航錯誤 - {ex.Message}");
                NavigationError?.Invoke(this, ex);
            }
        }

        public async Task PauseNavigationAsync()
        {
            try
            {
                if (_currentState != null)
                {
                    StopLocationTracking();
                    await _ttsService.SpeakAsync("導航已暫停");
                    StateChanged?.Invoke(this, _currentState);
                }
            }
            catch (Exception ex)
            {
                NavigationError?.Invoke(this, ex);
            }
        }

        public async Task ResumeNavigationAsync()
        {
            try
            {
                if (_currentState?.CurrentRoute != null)
                {
                    StartLocationTracking();
                    await _ttsService.SpeakAsync("導航已恢復");
                    StateChanged?.Invoke(this, _currentState);
                }
            }
            catch (Exception ex)
            {
                NavigationError?.Invoke(this, ex);
            }
        }

        public async Task<RouteDeviationResult> CheckRouteDeviationAsync(AppLocation currentLocation)
        {
            try
            {
                if (_currentState?.CurrentRoute == null || currentLocation == null)
                {
                    return new RouteDeviationResult { IsDeviated = false };
                }

                // 計算到路線的最短距離（簡化版本）
                var distanceToRoute = CalculateDistanceToRoute(currentLocation, _currentState.CurrentRoute);
                
                if (distanceToRoute > ROUTE_DEVIATION_THRESHOLD)
                {
                    _consecutiveDeviations++;
                    
                    if (_consecutiveDeviations >= CONSECUTIVE_DEVIATIONS_REQUIRED)
                    {
                        var result = new RouteDeviationResult
                        {
                            IsDeviated = true,
                            DeviationDistance = distanceToRoute,
                            SuggestedAction = RouteAction.Recalculate,
                            Message = "您已偏離路線"
                        };
                        
                        RouteDeviated?.Invoke(this, result);
                        await _ttsService.SpeakAsync("您已偏離路線，正在重新規劃");
                        
                        return result;
                    }
                }
                else
                {
                    _consecutiveDeviations = 0;
                }

                return new RouteDeviationResult { IsDeviated = false };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"檢查路線偏離錯誤: {ex.Message}");
                return new RouteDeviationResult { IsDeviated = false };
            }
        }

        public async Task<Route> RecalculateRouteAsync(AppLocation currentLocation)
        {
            try
            {
                if (_currentState?.CurrentRoute == null || currentLocation == null)
                    return null;

                Debug.WriteLine("重新計算路線");
                
                var newRoute = await _routeService.CalculateRouteAsync(
                    currentLocation.Latitude,
                    currentLocation.Longitude,
                    _currentState.CurrentRoute.EndLatitude,
                    _currentState.CurrentRoute.EndLongitude,
                    RouteType.Driving);

                if (newRoute?.Success == true && newRoute.Route != null)
                {
                    _currentState.CurrentRoute = newRoute.Route;
                    _currentState.IsOffRoute = false;
                    _consecutiveDeviations = 0;
                    
                    await _ttsService.SpeakAsync("路線已重新規劃");
                    StateChanged?.Invoke(this, _currentState);
                    
                    return newRoute.Route;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"重新計算路線錯誤: {ex.Message}");
                NavigationError?.Invoke(this, ex);
            }

            return null;
        }

        public async Task UpdateNavigationStateAsync(AppLocation currentLocation)
        {
            try
            {
                if (_currentState == null || currentLocation == null)
                    return;

                _currentState.CurrentLocation = currentLocation;
                
                // 更新到達目的地的預估時間
                var estimatedArrival = DateTime.Now.Add(_currentState.EstimatedTimeRemaining);
                _currentState.EstimatedArrivalTime = estimatedArrival.ToString("HH:mm");

                // 取得下一個導航指令
                var nextInstruction = await GetNextInstructionAsync(currentLocation);
                if (nextInstruction != null)
                {
                    _currentState.NextInstruction = nextInstruction;
                    
                    // 檢查是否需要播放語音指令
                    await CheckAndPlayInstruction(nextInstruction);
                }

                // 檢查路線偏離
                var deviationResult = await CheckRouteDeviationAsync(currentLocation);
                _currentState.IsOffRoute = deviationResult.IsDeviated;

                // 檢查是否到達目的地
                if (await CheckArrivalAsync(currentLocation))
                {
                    await HandleArrival();
                    return;
                }

                LocationUpdated?.Invoke(this, currentLocation);
                StateChanged?.Invoke(this, _currentState);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新導航狀態錯誤: {ex.Message}");
                NavigationError?.Invoke(this, ex);
            }
        }

        public async Task<NavigationInstruction> GetNextInstructionAsync(AppLocation currentLocation)
        {
            try
            {
                if (_currentState?.CurrentRoute == null || currentLocation == null)
                    return null;

                var route = _currentState.CurrentRoute;
                
                // 檢查是否到達目的地
                var distanceToDestination = CalculateDistance(
                    currentLocation.Latitude, currentLocation.Longitude,
                    route.EndLatitude, route.EndLongitude);

                if (distanceToDestination <= ARRIVAL_THRESHOLD)
                {
                    return NavigationInstructions.CreateInstruction(NavigationType.Arrive, 0);
                }

                // 如果路線有步驟，使用真實的導航指令
                if (route.Steps != null && route.Steps.Any())
                {
                    return GetInstructionFromRouteSteps(currentLocation, route);
                }
                else
                {
                    // 回退到簡單的直線導航
                    var bearing = CalculateBearing(
                        currentLocation.Latitude, currentLocation.Longitude,
                        route.EndLatitude, route.EndLongitude);
                        
                    var direction = GetDirectionFromBearing(bearing);
                    var instruction = $"朝{direction}方向行駛 {FormatDistance(distanceToDestination)}";
                    
                    return new NavigationInstruction
                    {
                        Text = instruction,
                        Type = NavigationType.Continue,
                        DistanceInMeters = distanceToDestination,
                        Direction = direction
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"取得導航指令錯誤: {ex.Message}");
                return null;
            }
        }
        
        private NavigationInstruction GetInstructionFromRouteSteps(AppLocation currentLocation, Route route)
        {
            try
            {
                var steps = route.Steps.ToList();
                
                // 找到當前最接近的路線步驟
                var currentStep = FindCurrentStep(currentLocation, steps);
                if (currentStep == null)
                {
                    // 如果找不到當前步驟，使用最後一個步驟
                    currentStep = steps.Last();
                }
                
                var stepIndex = steps.IndexOf(currentStep);
                _currentStepIndex = stepIndex;
                
                // 計算到當前步驟終點的距離
                var distanceToStepEnd = CalculateDistance(
                    currentLocation.Latitude, currentLocation.Longitude,
                    currentStep.EndLatitude, currentStep.EndLongitude);
                    
                // 更新行程進度
                UpdateRouteProgress(currentLocation, route);
                
                // 檢查是否接近轉彎點
                if (distanceToStepEnd <= APPROACH_INSTRUCTION_THRESHOLD && stepIndex < steps.Count - 1)
                {
                    var nextStep = steps[stepIndex + 1];
                    var turnDirection = CalculateTurnDirection(currentStep, nextStep);
                    
                    return new NavigationInstruction
                    {
                        Text = $"在 {FormatDistance(distanceToStepEnd)} 後{GetTurnInstruction(turnDirection)}",
                        Type = GetNavigationTypeFromTurn(turnDirection),
                        DistanceInMeters = distanceToStepEnd,
                        Direction = GetDirectionFromBearing(nextStep.Bearing)
                    };
                }
                else if (distanceToStepEnd <= NEXT_INSTRUCTION_THRESHOLD && stepIndex < steps.Count - 1)
                {
                    var nextStep = steps[stepIndex + 1];
                    var turnDirection = CalculateTurnDirection(currentStep, nextStep);
                    
                    return new NavigationInstruction
                    {
                        Text = $"準備{GetTurnInstruction(turnDirection)}，距離 {FormatDistance(distanceToStepEnd)}",
                        Type = GetNavigationTypeFromTurn(turnDirection),
                        DistanceInMeters = distanceToStepEnd,
                        Direction = GetDirectionFromBearing(nextStep.Bearing)
                    };
                }
                else
                {
                    // 繼續直行指令
                    var direction = GetDirectionFromBearing(currentStep.Bearing);
                    return new NavigationInstruction
                    {
                        Text = $"繼續朝{direction}方向行駛 {FormatDistance(distanceToStepEnd)}",
                        Type = NavigationType.Continue,
                        DistanceInMeters = distanceToStepEnd,
                        Direction = direction
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"從路線步驟取得指令錯誤: {ex.Message}");
                return null;
            }
        }
        
        private RouteStep FindCurrentStep(AppLocation currentLocation, List<RouteStep> steps)
        {
            RouteStep closestStep = null;
            double minDistance = double.MaxValue;
            
            foreach (var step in steps)
            {
                var distanceToStart = CalculateDistance(
                    currentLocation.Latitude, currentLocation.Longitude,
                    step.StartLatitude, step.StartLongitude);
                    
                var distanceToEnd = CalculateDistance(
                    currentLocation.Latitude, currentLocation.Longitude,
                    step.EndLatitude, step.EndLongitude);
                    
                var minStepDistance = Math.Min(distanceToStart, distanceToEnd);
                
                if (minStepDistance < minDistance)
                {
                    minDistance = minStepDistance;
                    closestStep = step;
                }
            }
            
            return closestStep;
        }
        
        private void UpdateRouteProgress(AppLocation currentLocation, Route route)
        {
            if (route.Steps?.Any() != true || _currentStepIndex < 0)
                return;
                
            try
            {
                var steps = route.Steps.ToList();
                double progressDistance = 0;
                
                // 計算已完成步驟的距離
                for (int i = 0; i < _currentStepIndex && i < steps.Count; i++)
                {
                    progressDistance += steps[i].DistanceInMeters;
                }
                
                // 加上當前步驟中已行駛的距離
                if (_currentStepIndex < steps.Count)
                {
                    var currentStep = steps[_currentStepIndex];
                    var stepStartDistance = CalculateDistance(
                        currentLocation.Latitude, currentLocation.Longitude,
                        currentStep.StartLatitude, currentStep.StartLongitude);
                    var stepTotalDistance = currentStep.DistanceInMeters;
                    var stepProgress = Math.Max(0, stepTotalDistance - stepStartDistance);
                    progressDistance += Math.Min(stepProgress, stepTotalDistance);
                }
                
                _distanceTraveled = progressDistance;
                
                // 更新導航狀態
                if (_totalRouteDistance > 0)
                {
                    _currentState.RouteProgress = Math.Min(1.0, _distanceTraveled / _totalRouteDistance);
                    _currentState.DistanceRemaining = Math.Max(0, _totalRouteDistance - _distanceTraveled);
                    
                    // 更新預估剩餘時間
                    var avgSpeed = 50.0; // km/h 預設平均速度
                    var remainingHours = (_currentState.DistanceRemaining / 1000.0) / avgSpeed;
                    _currentState.EstimatedTimeRemaining = TimeSpan.FromHours(remainingHours);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新路線進度錯誤: {ex.Message}");
            }
        }
        
        private string CalculateTurnDirection(RouteStep currentStep, RouteStep nextStep)
        {
            var bearingDiff = nextStep.Bearing - currentStep.Bearing;
            
            // 正規化角度差異到 -180 到 180 度
            while (bearingDiff > 180) bearingDiff -= 360;
            while (bearingDiff < -180) bearingDiff += 360;
            
            if (Math.Abs(bearingDiff) < 15)
                return "直行";
            else if (bearingDiff > 15 && bearingDiff < 75)
                return "右轉";
            else if (bearingDiff >= 75 && bearingDiff <= 105)
                return "右轉";
            else if (bearingDiff > 105)
                return "迴轉";
            else if (bearingDiff < -15 && bearingDiff > -75)
                return "左轉";
            else if (bearingDiff <= -75 && bearingDiff >= -105)
                return "左轉";
            else
                return "迴轉";
        }
        
        private string GetTurnInstruction(string turnDirection)
        {
            return turnDirection switch
            {
                "左轉" => "左轉",
                "右轉" => "右轉", 
                "迴轉" => "迴轉",
                "直行" => "繼續直行",
                _ => "繼續行駛"
            };
        }
        
        private NavigationType GetNavigationTypeFromTurn(string turnDirection)
        {
            return turnDirection switch
            {
                "左轉" => NavigationType.TurnLeft,
                "右轉" => NavigationType.TurnRight,
                "迴轉" => NavigationType.UTurn,
                "直行" => NavigationType.Continue,
                _ => NavigationType.Continue
            };
        }
        
        private double CalculateBearing(double startLat, double startLng, double endLat, double endLng)
        {
            var startLatRad = ToRadians(startLat);
            var startLngRad = ToRadians(startLng);
            var endLatRad = ToRadians(endLat);
            var endLngRad = ToRadians(endLng);
            
            var dLng = endLngRad - startLngRad;
            
            var y = Math.Sin(dLng) * Math.Cos(endLatRad);
            var x = Math.Cos(startLatRad) * Math.Sin(endLatRad) -
                    Math.Sin(startLatRad) * Math.Cos(endLatRad) * Math.Cos(dLng);
            
            var bearingRad = Math.Atan2(y, x);
            var bearingDeg = ToDegrees(bearingRad);
            
            return (bearingDeg + 360) % 360;
        }
        
        private double ToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }
        
        private string GetDirectionFromBearing(double bearing)
        {
            var directions = new[] { "北", "東北", "東", "東南", "南", "西南", "西", "西北" };
            var index = (int)Math.Round(bearing / 45.0) % 8;
            return directions[index];
        }
        
        private string FormatDistance(double meters)
        {
            if (meters < 1000)
                return $"{Math.Round(meters)}公尺";
            else
                return $"{Math.Round(meters / 1000.0, 1)}公里";
        }

        public async Task<bool> CheckArrivalAsync(AppLocation currentLocation)
        {
            try
            {
                if (_currentState?.CurrentRoute == null || currentLocation == null)
                    return false;

                var distance = CalculateDistance(
                    currentLocation.Latitude, currentLocation.Longitude,
                    _currentState.CurrentRoute.EndLatitude, _currentState.CurrentRoute.EndLongitude);

                return distance <= ARRIVAL_THRESHOLD;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"檢查到達錯誤: {ex.Message}");
                return false;
            }
        }

        private void StartLocationTracking()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            
            _navigationTimer = new Timer(async _ =>
            {
                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var currentLocation = await _locationService.GetCurrentLocationAsync();
                        if (currentLocation != null)
                        {
                            await UpdateNavigationStateAsync(currentLocation);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"位置更新錯誤: {ex.Message}");
                    }
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(NAVIGATION_UPDATE_INTERVAL));
        }

        private void StopLocationTracking()
        {
            _navigationTimer?.Dispose();
            _navigationTimer = null;
            
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private async Task CheckAndPlayInstruction(NavigationInstruction instruction)
        {
            try
            {
                if (instruction == null || _ttsService == null)
                    return;

                // 避免重複播放相同指令
                var timeSinceLastInstruction = DateTime.Now - _lastInstructionTime;
                if (_lastSpokenInstruction?.Text == instruction.Text && 
                    timeSinceLastInstruction.TotalSeconds < 10)
                {
                    return;
                }

                // 播放語音指令
                if (instruction.DistanceInMeters <= INSTRUCTION_DISTANCE_THRESHOLD)
                {
                    await _ttsService.SpeakAsync(instruction.Text);
                    _lastSpokenInstruction = instruction;
                    _lastInstructionTime = DateTime.Now;
                    
                    InstructionUpdated?.Invoke(this, instruction);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"播放導航指令錯誤: {ex.Message}");
            }
        }

        private async Task HandleArrival()
        {
            try
            {
                Debug.WriteLine("到達目的地");
                
                await _ttsService.SpeakAsync("您已到達目的地");
                
                // 震動提醒（如果支援）
                try
                {
                    // 簡化版本：記錄震動事件
                    Debug.WriteLine("設備震動提醒：到達目的地");
                }
                catch
                {
                    // 忽略震動錯誤
                }

                DestinationReached?.Invoke(this, EventArgs.Empty);
                
                // 自動停止導航
                await StopNavigationAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理到達錯誤: {ex.Message}");
            }
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // 使用 Haversine 公式計算距離
            const double R = 6371000; // 地球半徑（公尺）
            
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        private double CalculateDistanceToRoute(AppLocation location, Route route)
        {
            try
            {
                if (route.Steps == null || !route.Steps.Any())
                {
                    // 如果沒有步驟，計算到直線路徑的距離
                    return CalculateDistanceToLine(
                        location.Latitude, location.Longitude,
                        route.StartLatitude, route.StartLongitude,
                        route.EndLatitude, route.EndLongitude);
                }
                
                // 計算到所有路線段的最短距離
                double minDistance = double.MaxValue;
                
                foreach (var step in route.Steps)
                {
                    var distanceToStep = CalculateDistanceToLine(
                        location.Latitude, location.Longitude,
                        step.StartLatitude, step.StartLongitude,
                        step.EndLatitude, step.EndLongitude);
                        
                    minDistance = Math.Min(minDistance, distanceToStep);
                }
                
                return minDistance;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"計算到路線距離錯誤: {ex.Message}");
                // 回退到簡單計算
                return CalculateDistance(
                    location.Latitude, location.Longitude,
                    route.EndLatitude, route.EndLongitude);
            }
        }
        
        private double CalculateDistanceToLine(double pointLat, double pointLon, 
                                               double line1Lat, double line1Lon, 
                                               double line2Lat, double line2Lon)
        {
            // 使用點到線段的最短距離公式
            const double R = 6371000; // 地球半徑（公尺）
            
            // 轉換為弧度
            var lat1 = ToRadians(pointLat);
            var lon1 = ToRadians(pointLon);
            var lat2 = ToRadians(line1Lat);
            var lon2 = ToRadians(line1Lon);
            var lat3 = ToRadians(line2Lat);
            var lon3 = ToRadians(line2Lon);
            
            // 計算線段兩端點到測試點的距離
            var d13 = Math.Acos(Math.Sin(lat1) * Math.Sin(lat2) + 
                               Math.Cos(lat1) * Math.Cos(lat2) * Math.Cos(lon2 - lon1));
            var d23 = Math.Acos(Math.Sin(lat1) * Math.Sin(lat3) + 
                               Math.Cos(lat1) * Math.Cos(lat3) * Math.Cos(lon3 - lon1));
            var d12 = Math.Acos(Math.Sin(lat2) * Math.Sin(lat3) + 
                               Math.Cos(lat2) * Math.Cos(lat3) * Math.Cos(lon3 - lon2));
            
            // 如果線段長度為零，返回點到點的距離
            if (d12 < 1e-6)
            {
                return R * d13;
            }
            
            // 計算投影點
            var A = Math.Sin(d13) * Math.Sin(d13) - Math.Sin(d23) * Math.Sin(d23) + Math.Sin(d12) * Math.Sin(d12);
            var B = 2 * Math.Sin(d12) * Math.Sin(d12);
            
            if (Math.Abs(B) < 1e-6)
            {
                return R * Math.Min(d13, d23);
            }
            
            var x = (A / B);
            
            // 檢查投影點是否在線段上
            if (x < 0)
            {
                return R * d13; // 最近點是線段起點
            }
            else if (x > 1)
            {
                return R * d23; // 最近點是線段終點
            }
            else
            {
                // 計算到投影點的距離
                var dxt = Math.Asin(Math.Sqrt(Math.Sin(d13) * Math.Sin(d13) - x * x * Math.Sin(d12) * Math.Sin(d12)));
                return R * Math.Abs(dxt);
            }
        }

        public void Dispose()
        {
            StopLocationTracking();
            _currentState = null;
        }
    }
}