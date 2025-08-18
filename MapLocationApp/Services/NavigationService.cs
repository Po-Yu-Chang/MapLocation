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
        
        private int _consecutiveDeviations = 0;
        private NavigationInstruction _lastSpokenInstruction;
        private DateTime _lastInstructionTime = DateTime.MinValue;

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

                // 簡化版本：生成基本的導航指令
                var distanceToDestination = CalculateDistance(
                    currentLocation.Latitude, currentLocation.Longitude,
                    _currentState.CurrentRoute.EndLatitude, _currentState.CurrentRoute.EndLongitude);

                if (distanceToDestination <= ARRIVAL_THRESHOLD)
                {
                    return NavigationInstructions.CreateInstruction(NavigationType.Arrive, 0);
                }
                else if (distanceToDestination <= 500)
                {
                    return NavigationInstructions.CreateInstruction(NavigationType.Continue, distanceToDestination);
                }
                else
                {
                    return NavigationInstructions.CreateInstruction(NavigationType.Continue, distanceToDestination);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"取得導航指令錯誤: {ex.Message}");
                return null;
            }
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
            // 簡化版本：計算到目的地的距離作為路線距離
            // 實際實作應該計算到路線路徑的最短距離
            return CalculateDistance(
                location.Latitude, location.Longitude,
                route.EndLatitude, route.EndLongitude);
        }

        public void Dispose()
        {
            StopLocationTracking();
            _currentState = null;
        }
    }
}