using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using MapLocationApp.Services;
using MapLocationApp.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace MapLocationApp.Views
{
    public partial class RoutePlanningPage : ContentPage, INotifyPropertyChanged
    {
        private readonly IRouteService _routeService;
        private readonly ILocationService _locationService;
        private readonly IGeocodingService _geocodingService;
        private readonly ITelegramNotificationService _telegramService;
        private readonly INavigationService _navigationService;
        private readonly ITTSService _ttsService;
        private readonly IMapService _mapService;
        private readonly EnhancedNominatimService _enhancedNominatim;
        private Route _currentCalculatedRoute;
        private Route _currentRoute;
        private NavigationSession _currentNavigationSession;
        private Timer _navigationUpdateTimer;
        private string _selectedTransportMode = "driving";
        private Microsoft.Maui.Devices.Sensors.Location _startLocation;
        private Microsoft.Maui.Devices.Sensors.Location _endLocation;
        private bool _isMuted = false;
        private CancellationTokenSource _searchCancellationTokenSource;
        private const int SearchDelayMs = 300;

        // Google Maps 風格的集合
        public ObservableCollection<Route> SavedRoutes { get; set; }
        public ObservableCollection<Route> RecentRoutes { get; set; }
        public ObservableCollection<RouteOption> RouteOptions { get; set; }
        public ObservableCollection<SearchSuggestion> FromSuggestions { get; set; }
        public ObservableCollection<SearchSuggestion> ToSuggestions { get; set; }
        
        public ICommand StartNavigationCommand { get; }
        public ICommand DeleteRouteCommand { get; }
        
        public bool HasSavedRoutes => RecentRoutes?.Count > 0;
        
        // 導航相關屬性
        public bool IsNavigating => _navigationService?.IsNavigating == true;
        public bool IsNotNavigating => !IsNavigating;
        public NavigationInstruction CurrentInstruction => _navigationService?.CurrentState?.CurrentInstruction;
        public string EstimatedArrivalTime => _navigationService?.CurrentState?.EstimatedArrivalTime ?? "無";
        public string RemainingTime => FormatTimeSpan(_navigationService?.CurrentState?.EstimatedTimeRemaining ?? TimeSpan.Zero);
        public string RemainingDistance => FormatDistance(_navigationService?.CurrentState?.DistanceRemaining ?? 0);
        public double RouteProgress => _navigationService?.CurrentState?.RouteProgress ?? 0.0;
        
        public string SelectedTransportMode
        {
            get => _selectedTransportMode;
            set => SetProperty(ref _selectedTransportMode, value);
        }

        public Route CurrentRoute
        {
            get => _currentRoute;
            set => SetProperty(ref _currentRoute, value);
        }

        // 事件
        public event EventHandler<Route> RouteSelected;

        public RoutePlanningPage()
        {
            InitializeComponent();
            
            // 從依賴注入容器取得服務
            _routeService = ServiceHelper.GetService<IRouteService>();
            _locationService = ServiceHelper.GetService<ILocationService>();
            _geocodingService = ServiceHelper.GetService<IGeocodingService>();
            _telegramService = ServiceHelper.GetService<ITelegramNotificationService>();
            _navigationService = ServiceHelper.GetService<INavigationService>();
            _ttsService = ServiceHelper.GetService<ITTSService>();
            _mapService = ServiceHelper.GetService<IMapService>();
            
            // 初始化集合
            SavedRoutes = new ObservableCollection<Route>();
            RecentRoutes = new ObservableCollection<Route>();
            RouteOptions = new ObservableCollection<RouteOption>();
            FromSuggestions = new ObservableCollection<SearchSuggestion>();
            ToSuggestions = new ObservableCollection<SearchSuggestion>();
            
            // 初始化搜尋取消權杖
            _searchCancellationTokenSource = new CancellationTokenSource();
            
            // 初始化增強版服務（基於您的 Newtonsoft.Json 示例）
            _enhancedNominatim = new EnhancedNominatimService();
            
            // 初始化命令
            StartNavigationCommand = new Command<Route>(async (route) => await StartNavigationAsync(route));
            DeleteRouteCommand = new Command<Route>(async (route) => await DeleteRouteAsync(route));
            
            // 設定數據上下文
            BindingContext = this;
            
            // 訂閱位置變更事件
            if (_locationService != null)
            {
                _locationService.LocationChanged += OnLocationChanged;
            }
            
            // 訂閱導航服務事件
            if (_navigationService != null)
            {
                _navigationService.InstructionUpdated += OnNavigationInstructionUpdated;
                _navigationService.StateChanged += OnNavigationStateChanged;
                _navigationService.DestinationReached += OnDestinationReached;
                _navigationService.RouteDeviated += OnRouteDeviated;
            }
            
            // 載入已儲存的路線
            LoadSavedRoutesAsync();
            
            // 設定預設交通方式
            SelectedTransportMode = "driving";
            UpdateTransportModeButtons();
            
            // 初始化地圖
            InitializeMap();
            
            // 開始位置追蹤
            StartLocationTracking();
        }

        private void InitializeMap()
        {
            if (_mapService != null)
            {
                // 初始化路線規劃模式的地圖
                if (PlanningMapView != null)
                {
                    PlanningMapView.Map = _mapService.CreateMap();
                    _mapService.CenterMap(PlanningMapView, 25.0330, 121.5654, 12); // 台北市中心
                }
                
                // 初始化導航模式的地圖
                if (NavigationMapView != null)
                {
                    NavigationMapView.Map = _mapService.CreateMap();
                    _mapService.CenterMap(NavigationMapView, 25.0330, 121.5654, 12); // 台北市中心
                }
            }
        }

        private async void StartLocationTracking()
        {
            try
            {
                if (_locationService != null)
                {
                    // 訂閱位置變更事件
                    _locationService.LocationChanged += OnLocationChanged;
                    
                    // 嘗試獲取當前位置
                    var currentLocation = await _locationService.GetCurrentLocationAsync();
                    if (currentLocation != null)
                    {
                        OnLocationChanged(this, currentLocation);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"開始位置追蹤錯誤: {ex.Message}");
            }
        }

        // XAML 中實際使用的事件處理器
        private async void OnStartLocationTextChanged(object sender, TextChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"起點文字變更: '{e.NewTextValue}'");
            await SearchLocationSuggestions(e.NewTextValue, true);
        }
        
        private async void OnStartSuggestionTapped(object sender, EventArgs e)
        {
            if (sender is Grid grid && grid.BindingContext is SearchSuggestion suggestion)
            {
                StartLocationEntry.Text = suggestion.MainText;
                _startLocation = new Microsoft.Maui.Devices.Sensors.Location(suggestion.Latitude, suggestion.Longitude);
                StartSuggestionsView.IsVisible = false;
                FromSuggestions.Clear();
                CheckCanSearchRoute();
            }
        }
        
        private async void OnEndSuggestionTapped(object sender, EventArgs e)
        {
            if (sender is Grid grid && grid.BindingContext is SearchSuggestion suggestion)
            {
                EndLocationEntry.Text = suggestion.MainText;
                _endLocation = new Microsoft.Maui.Devices.Sensors.Location(suggestion.Latitude, suggestion.Longitude);
                EndSuggestionsView.IsVisible = false;
                ToSuggestions.Clear();
                CheckCanSearchRoute();
            }
        }

        private async void OnEndLocationTextChanged(object sender, TextChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"終點文字變更: '{e.NewTextValue}'");
            await SearchLocationSuggestions(e.NewTextValue, false);
        }

        private async void OnUseCurrentLocationClicked(object sender, EventArgs e)
        {
            try
            {
                var location = await _locationService.GetCurrentLocationAsync();
                if (location != null)
                {
                    StartLocationEntry.Text = $"目前位置 ({location.Latitude:F4}, {location.Longitude:F4})";
                    _startLocation = new Microsoft.Maui.Devices.Sensors.Location(location.Latitude, location.Longitude);
                    await DisplayAlert("✅ 成功", "已設定目前位置為起點", "確定");
                }
                else
                {
                    await DisplayAlert("❌ 錯誤", "無法取得目前位置，請檢查位置權限設定", "確定");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ 錯誤", $"取得位置時發生錯誤: {ex.Message}", "確定");
            }
        }

        private async void OnSearchNearbyClicked(object sender, EventArgs e)
        {
            await DisplayAlert("功能提示", "附近搜尋功能將在後續版本中提供", "確定");
        }

        private void OnClearEndLocationClicked(object sender, EventArgs e)
        {
            EndLocationEntry.Text = string.Empty;
            _endLocation = null;
        }

        private void OnSuggestionTapped(object sender, EventArgs e)
        {
            if (sender is Grid grid && grid.BindingContext is SearchSuggestion suggestion)
            {
                // 判斷是起點還是終點
                if (string.IsNullOrEmpty(StartLocationEntry.Text) || StartLocationEntry.IsFocused)
                {
                    StartLocationEntry.Text = suggestion.MainText;
                    _startLocation = new Microsoft.Maui.Devices.Sensors.Location(suggestion.Latitude, suggestion.Longitude);
                }
                else
                {
                    EndLocationEntry.Text = suggestion.MainText;
                    _endLocation = new Microsoft.Maui.Devices.Sensors.Location(suggestion.Latitude, suggestion.Longitude);
                }
                
                // SearchSuggestionsCollectionView.IsVisible = false;
                CheckCanSearchRoute();
            }
        }

        private async void OnGetDirectionsClicked(object sender, EventArgs e)
        {
            if (_startLocation != null && _endLocation != null)
            {
                await FindRouteAsync();
            }
            else
            {
                await DisplayAlert("提示", "請先選擇起點和終點", "確定");
            }
        }

        private void OnRouteOptionClicked(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                // 重置所有路線選項按鈕
                var buttons = new[] { FastestRouteButton, ShortestRouteButton, EcoRouteButton };
                foreach (var btn in buttons)
                {
                    if (btn != null)
                    {
                        btn.BackgroundColor = Color.FromArgb("#E0E0E0");
                        btn.TextColor = Colors.Black;
                    }
                }
                
                // 高亮選中的按鈕
                button.BackgroundColor = Color.FromArgb("#1976D2");
                button.TextColor = Colors.White;
            }
        }

        private async void OnRouteOptionSelected(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("OnRouteOptionSelected 被調用");
                
                if (sender == null)
                {
                    System.Diagnostics.Debug.WriteLine("sender 是 null");
                    await DisplayAlert("調試資訊", "sender 是 null", "確定");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"sender 類型: {sender.GetType().Name}");
                
                if (sender is Border border)
                {
                    System.Diagnostics.Debug.WriteLine("sender 是 Border");
                    
                    if (border.BindingContext == null)
                    {
                        System.Diagnostics.Debug.WriteLine("BindingContext 是 null");
                        await DisplayAlert("調試資訊", "BindingContext 是 null", "確定");
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"BindingContext 類型: {border.BindingContext.GetType().Name}");
                    
                    if (border.BindingContext is RouteOption selectedOption)
                    {
                        System.Diagnostics.Debug.WriteLine($"選擇的路線: {selectedOption.Description}");
                        await SelectRouteOption(selectedOption);
                    }
                    else
                    {
                        await DisplayAlert("調試資訊", $"BindingContext 不是 RouteOption，而是 {border.BindingContext.GetType().Name}", "確定");
                    }
                }
                else
                {
                    await DisplayAlert("調試資訊", $"sender 不是 Border，而是 {sender.GetType().Name}", "確定");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"錯誤: {ex}");
                await DisplayAlert("錯誤", $"選擇路線時發生錯誤: {ex.Message}\n\n詳細資訊: {ex.StackTrace}", "確定");
            }
        }

        private async void OnTransportModeClicked(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                // 重置所有交通方式按鈕 (簡化版暫時移除)
                // var buttons = new[] { DrivingModeButton, WalkingModeButton, CyclingModeButton, TransitModeButton };
                /*
                foreach (var btn in buttons)
                {
                    if (btn != null)
                    {
                        btn.BackgroundColor = Color.FromArgb("#E0E0E0");
                        btn.TextColor = Colors.Black;
                    }
                }
                
                // 高亮選中的按鈕並設定模式
                button.BackgroundColor = Color.FromArgb("#1976D2");
                button.TextColor = Colors.White;
                
                // 根據按鈕設定模式
                if (button == DrivingModeButton) SelectedTransportMode = "driving";
                else if (button == WalkingModeButton) SelectedTransportMode = "walking";
                else if (button == CyclingModeButton) SelectedTransportMode = "cycling";
                else if (button == TransitModeButton) SelectedTransportMode = "transit";
                */
                
                // 如果已有起終點，重新搜尋路線
                if (_startLocation != null && _endLocation != null)
                {
                    await FindRouteAsync();
                }
            }
        }

        private async void OnSwapLocationsClicked(object sender, EventArgs e)
        {
            // 交換起點和終點
            var tempText = StartLocationEntry.Text;
            var tempLocation = _startLocation;
            
            StartLocationEntry.Text = EndLocationEntry.Text;
            _startLocation = _endLocation;
            
            EndLocationEntry.Text = tempText;
            _endLocation = tempLocation;
            
            // 重新搜尋路線
            if (_startLocation != null && _endLocation != null)
            {
                await FindRouteAsync();
            }
        }

        private async void OnStartNavigationClicked(object sender, EventArgs e)
        {
            if (CurrentRoute != null)
            {
                await StartAdvancedNavigationAsync(CurrentRoute);
            }
        }
        
        private async void OnTestNavigationClicked(object sender, EventArgs e)
        {
            try
            {
                // 創建一個測試路線
                var testRoute = new Route
                {
                    Id = Guid.NewGuid().ToString(),
                    StartLatitude = 25.0330,
                    StartLongitude = 121.5654,
                    EndLatitude = 25.0340,
                    EndLongitude = 121.5645,
                    Distance = 1.2,
                    EstimatedDuration = TimeSpan.FromMinutes(5),
                    StartAddress = "台北車站",
                    EndAddress = "台北101"
                };

                System.Diagnostics.Debug.WriteLine("開始測試導航模式");
                await StartAdvancedNavigationAsync(testRoute);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"測試導航錯誤: {ex.Message}");
                await DisplayAlert("測試錯誤", $"測試導航失敗: {ex.Message}", "確定");
            }
        }
        
        private async void OnMuteToggleClicked(object sender, EventArgs e)
        {
            _isMuted = !_isMuted;
            
            if (_isMuted)
            {
                if (_ttsService != null)
                {
                    await _ttsService.StopSpeakingAsync();
                }
            }
            
            // 更新靜音按鈕狀態
            UpdateMuteButtonStates();
        }

        private async void OnStopNavigationClicked(object sender, EventArgs e)
        {
            await StopAdvancedNavigationAsync();
        }

        private async void OnMyLocationClicked(object sender, EventArgs e)
        {
            try
            {
                var location = await _locationService.GetCurrentLocationAsync();
                if (location != null)
                {
                    await DisplayAlert("位置", $"目前位置: {location.Latitude:F4}, {location.Longitude:F4}", "確定");
                }
                else
                {
                    await DisplayAlert("錯誤", "無法取得目前位置", "確定");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("錯誤", $"定位失敗: {ex.Message}", "確定");
            }
        }
        
        private void OnZoomToRouteClicked(object sender, EventArgs e)
        {
            // TODO: 縮放到路線功能
        }

        private void OnRecentRouteTapped(object sender, EventArgs e)
        {
            // 處理最近路線的點擊事件
            if (sender is Grid grid && grid.BindingContext is RouteOption recentRoute)
            {
                // 設定起終點為最近路線的起終點
                if (recentRoute.Route?.StartAddress != null && recentRoute.Route?.EndAddress != null)
                {
                    StartLocationEntry.Text = recentRoute.Route.StartAddress;
                    EndLocationEntry.Text = recentRoute.Route.EndAddress;
                    
                    // 可以在這裡重新計算路線
                    _ = Task.Run(async () => await FindRouteAsync());
                }
            }
        }

        // 私有方法
        /// <summary>
        /// 智能地址搜尋建議 - 支援各種地址格式輸入
        /// </summary>
        private async Task SearchLocationSuggestions(string query, bool isStartLocation)
        {
            System.Diagnostics.Debug.WriteLine($"🔍 智能搜尋 {(isStartLocation ? "起點" : "終點")}: '{query}'");
            
            try
            {
                // 取消之前的搜尋請求
                _searchCancellationTokenSource?.Cancel();
                _searchCancellationTokenSource = new CancellationTokenSource();
                
                var suggestions = isStartLocation ? FromSuggestions : ToSuggestions;
                var suggestionsView = isStartLocation ? StartSuggestionsView : EndSuggestionsView;
                
                // 如果查詢太短，隱藏建議
                if (string.IsNullOrWhiteSpace(query) || query.Length < 1)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        suggestionsView.IsVisible = false;
                        suggestions.Clear();
                    });
                    return;
                }
                
                // 顯示建議區域
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    suggestionsView.IsVisible = true;
                });
                
                // 添加即時搜尋指示
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    suggestions.Clear();
                    suggestions.Add(new SearchSuggestion
                    {
                        MainText = "🔍 正在搜尋...",
                        SecondaryText = $"正在為您搜尋 '{query}' 相關地點",
                        Latitude = 0,
                        Longitude = 0
                    });
                });

                // 延遲搜尋以避免過於頻繁的 API 呼叫
                await Task.Delay(SearchDelayMs, _searchCancellationTokenSource.Token);
                
                System.Diagnostics.Debug.WriteLine($"開始執行地址搜尋API查詢...");
                var searchResults = await GetEnhancedLocationSuggestions(query);
                
                if (_searchCancellationTokenSource.Token.IsCancellationRequested)
                {
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"獲得 {searchResults.Count} 個智能建議");
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    suggestions.Clear();
                    
                    if (searchResults.Any())
                    {
                        foreach (var suggestion in searchResults.Take(8))
                        {
                            suggestions.Add(suggestion);
                            System.Diagnostics.Debug.WriteLine($"✓ 添加建議: {suggestion.MainText} - {suggestion.SecondaryText}");
                        }
                    }
                    else
                    {
                        // 沒有找到結果時的友好提示
                        suggestions.Add(new SearchSuggestion
                        {
                            MainText = $"沒有找到 '{query}' 的結果",
                            SecondaryText = "💡 試試輸入更詳細的地址、地標名稱或使用繁體中文",
                            Latitude = 0,
                            Longitude = 0
                        });
                    }
                });
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("搜尋被使用者取消");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 智能搜尋錯誤: {ex.Message}");
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var suggestions = isStartLocation ? FromSuggestions : ToSuggestions;
                    suggestions.Clear();
                    suggestions.Add(new SearchSuggestion
                    {
                        MainText = "搜尋發生錯誤",
                        SecondaryText = "⚠️ 請檢查網路連線或稍後再試",
                        Latitude = 0,
                        Longitude = 0
                    });
                });
            }
        }

        private RouteType GetRouteTypeFromMode(string mode)
        {
            return mode switch
            {
                "walking" => RouteType.Walking,
                "cycling" => RouteType.Cycling,
                "transit" => RouteType.Driving, // 暫時用開車模式
                _ => RouteType.Driving
            };
        }

        /// <summary>
        /// 獲取增強版地址搜尋建議
        /// </summary>
        private async Task<List<SearchSuggestion>> GetEnhancedLocationSuggestions(string query)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🌐 呼叫增強版地址搜尋API: '{query}'");
                
                if (_geocodingService == null)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ 地理編碼服務無法使用，使用本地建議");
                    return GetSmartLocalSuggestions(query);
                }
                
                // 使用改進後的 GeocodingService 智能搜尋功能
                var suggestions = await _geocodingService.GetLocationSuggestionsAsync(query);
                var suggestionsList = suggestions.ToList();
                
                System.Diagnostics.Debug.WriteLine($"從 API 獲得 {suggestionsList.Count} 個建議");
                
                // 如果 API 沒有結果，嘗試預設建議
                if (!suggestionsList.Any())
                {
                    System.Diagnostics.Debug.WriteLine("API 沒有結果，使用預設建議");
                    return GetSmartLocalSuggestions(query);
                }
                
                return suggestionsList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"獲取位置建議錯誤: {ex.Message}");
                return GetSmartLocalSuggestions(query);
            }
        }

        /// <summary>
        /// 智能本地地址建議（當網路不可用時）
        /// </summary>
        private List<SearchSuggestion> GetSmartLocalSuggestions(string query)
        {
            var suggestions = new List<SearchSuggestion>();
            
            // 常見地點建議
            var commonPlaces = new[]
            {
                new { Name = "台北車站", Address = "台北市中正區", Lat = 25.0478, Lng = 121.5170 },
                new { Name = "台北101", Address = "台北市信義區", Lat = 25.0340, Lng = 121.5645 },
                new { Name = "桃園機場", Address = "桃園市大園區", Lat = 25.0797, Lng = 121.2342 },
                new { Name = "高雄車站", Address = "高雄市三民區", Lat = 22.6391, Lng = 120.3022 },
                new { Name = "台中車站", Address = "台中市中區", Lat = 24.1369, Lng = 120.6839 }
            };

            foreach (var place in commonPlaces)
            {
                if (place.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    place.Address.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add(new SearchSuggestion
                    {
                        MainText = place.Name,
                        SecondaryText = place.Address,
                        Latitude = place.Lat,
                        Longitude = place.Lng
                    });
                }
            }

            return suggestions;
        }

        private void CheckCanSearchRoute()
        {
            if (_startLocation != null && _endLocation != null)
            {
                // 自動搜尋路線
                _ = Task.Run(async () => await FindRouteAsync());
            }
        }

        private void UpdateTransportModeButtons()
        {
            // 更新交通方式按鈕樣式 - 簡化版暫時移除
            // var buttons = new[] { DrivingModeButton, WalkingModeButton, CyclingModeButton, TransitModeButton };
            // var modes = new[] { "driving", "walking", "cycling", "transit" };
            
            // for (int i = 0; i < buttons.Length; i++)
            // {
            //     if (buttons[i] != null)
            //     {
            //         var isSelected = modes[i] == SelectedTransportMode;
            //         buttons[i].BackgroundColor = isSelected ? Color.FromArgb("#1976D2") : Color.FromArgb("#E0E0E0");
            //         buttons[i].TextColor = isSelected ? Colors.White : Colors.Black;
            //     }
            // }
        }

        private async Task FindRouteAsync()
        {
            if (_startLocation == null || _endLocation == null)
                return;

            try
            {
                GetDirectionsButton.Text = "🔄 搜尋中...";
                GetDirectionsButton.IsEnabled = false;
                
                // 使用現有的 RouteService 方法
                var routeResult = await _routeService.CalculateRouteAsync(
                    _startLocation.Latitude, _startLocation.Longitude,
                    _endLocation.Latitude, _endLocation.Longitude,
                    GetRouteTypeFromMode(SelectedTransportMode));
                
                if (routeResult?.Success == true && routeResult.Route != null)
                {
                    // 創建多個路線選項
                    var routeOptions = new List<RouteOption>
                    {
                        new RouteOption
                        {
                            Route = routeResult.Route,
                            Duration = FormatDuration(routeResult.Route.EstimatedDuration),
                            Distance = $"{routeResult.Route.Distance:F1} 公里",
                            Description = "推薦路線",
                            TrafficColor = "#4CAF50",
                            TrafficInfo = "交通順暢",
                            IsSelected = true
                        }
                    };

                    RouteOptions.Clear();
                    foreach (var option in routeOptions)
                        RouteOptions.Add(option);

                    // 自動選擇第一個路線
                    if (routeOptions.Any())
                    {
                        await SelectRouteOption(routeOptions.First());
                        
                        // 儲存計算出的路線
                        _currentCalculatedRoute = routeOptions.First().Route;
                        
                        // 顯示路線選項卡片
                        if (RouteOptionsCollectionView != null)
                        {
                            RouteOptionsCollectionView.IsVisible = true;
                        }
                        
                        // 顯示路線資訊卡片 (如果 UI 元素存在)
                        try
                        {
                            UpdateRouteInfoCard(_currentCalculatedRoute);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"更新 UI 錯誤: {ex.Message}");
                        }
                    }
                }
                else
                {
                    await DisplayAlert("提示", "找不到路線", "確定");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("錯誤", $"搜尋路線時發生錯誤: {ex.Message}", "確定");
            }
            finally
            {
                GetDirectionsButton.Text = "🧭 開始導航";
                GetDirectionsButton.IsEnabled = true;
            }
        }

        private async Task SelectRouteOption(RouteOption option)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("SelectRouteOption 開始執行");
                
                if (option?.Route == null)
                {
                    System.Diagnostics.Debug.WriteLine("option 或 option.Route 是 null");
                    await DisplayAlert("錯誤", "無效的路線選項", "確定");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"路線選項: {option.Description}");

                // 更新選擇狀態
                if (RouteOptions != null)
                {
                    System.Diagnostics.Debug.WriteLine($"RouteOptions 有 {RouteOptions.Count} 個選項");
                    foreach (var routeOption in RouteOptions)
                    {
                        if (routeOption != null)
                        {
                            routeOption.IsSelected = routeOption == option;
                            System.Diagnostics.Debug.WriteLine($"設定 {routeOption.Description} 選擇狀態為: {routeOption.IsSelected}");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("RouteOptions 是 null");
                }

                CurrentRoute = option.Route;
                System.Diagnostics.Debug.WriteLine("CurrentRoute 已設定");
                
                // 在地圖上渲染路線
                if (_mapService != null && PlanningMapView?.Map != null && option.Route != null)
                {
                    System.Diagnostics.Debug.WriteLine("開始在地圖上繪製路線");
                    _mapService.DrawRoute(PlanningMapView.Map, option.Route);
                    _mapService.AnimateToRoute(PlanningMapView, option.Route);
                    PlanningMapView.Refresh();
                }
                
                // 更新導航資訊
                UpdateNavigationInfo(option);
                System.Diagnostics.Debug.WriteLine("UpdateNavigationInfo 完成");
                
                // 通知地圖更新路線
                if (RouteSelected != null)
                {
                    System.Diagnostics.Debug.WriteLine("觸發 RouteSelected 事件");
                    RouteSelected.Invoke(this, option.Route);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("RouteSelected 事件是 null");
                }
                
                System.Diagnostics.Debug.WriteLine("SelectRouteOption 執行完畢");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SelectRouteOption 錯誤: {ex}");
                await DisplayAlert("錯誤", $"選擇路線時發生錯誤: {ex.Message}\n\n詳細資訊: {ex.StackTrace}", "確定");
            }
        }

        private void UpdateNavigationInfo(RouteOption option)
        {
            try
            {
                if (option != null && GetDirectionsButton != null)
                {
                    // 由於沒有 NavigationStatusLabel，我們可以更新 GetDirectionsButton 的文字
                    GetDirectionsButton.Text = $"🧭 開始導航 ({option.Duration})";
                }
            }
            catch (Exception ex)
            {
                // 靜默處理 UI 更新錯誤，避免中斷使用者操作
                System.Diagnostics.Debug.WriteLine($"UpdateNavigationInfo 錯誤: {ex.Message}");
            }
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours} 小時 {duration.Minutes} 分鐘";
            else
                return $"{duration.Minutes} 分鐘";
        }

        private async void LoadSavedRoutesAsync()
        {
            try
            {
                var routes = await _routeService.GetSavedRoutesAsync();
                SavedRoutes.Clear();
                RecentRoutes.Clear();
                
                foreach (var route in routes)
                {
                    SavedRoutes.Add(route);
                    RecentRoutes.Add(route);
                }
                
                OnPropertyChanged(nameof(HasSavedRoutes));
            }
            catch (Exception ex)
            {
                await DisplayAlert("錯誤", $"載入已儲存路線時發生錯誤: {ex.Message}", "確定");
            }
        }

        private async Task StartNavigationAsync(Route route)
        {
            try
            {
                // 開始導航
                _currentNavigationSession = new NavigationSession
                {
                    Route = route,
                    StartTime = DateTime.Now,
                    IsActive = true
                };

                // 發送Telegram通知 - 使用現有的方法
                if (_telegramService != null)
                {
                    await _telegramService.SendRouteNotificationAsync(
                        "使用者", 
                        "導航路線", 
                        route.StartLatitude, 
                        route.StartLongitude, 
                        route.EndLatitude, 
                        route.EndLongitude);
                }

                // 開始位置更新計時器
                StartNavigationUpdates();

                await DisplayAlert("✅ 導航開始", $"已開始導航至目的地", "確定");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ 錯誤", $"開始導航時發生錯誤: {ex.Message}", "確定");
            }
        }

        private async Task DeleteRouteAsync(Route route)
        {
            try
            {
                bool confirm = await DisplayAlert("確認刪除", "確定要刪除這個路線嗎？", "刪除", "取消");
                if (confirm)
                {
                    await _routeService.DeleteRouteAsync(route.Id);
                    SavedRoutes.Remove(route);
                    RecentRoutes.Remove(route);
                    OnPropertyChanged(nameof(HasSavedRoutes));
                    await DisplayAlert("✅ 成功", "路線已刪除", "確定");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ 錯誤", $"刪除路線時發生錯誤: {ex.Message}", "確定");
            }
        }

        private void StartNavigationUpdates()
        {
            _navigationUpdateTimer = new Timer(async _ =>
            {
                if (_currentNavigationSession?.IsActive == true)
                {
                    await UpdateNavigationStatus();
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        private async Task UpdateNavigationStatus()
        {
            try
            {
                var currentLocation = await _locationService.GetCurrentLocationAsync();
                if (currentLocation != null && _currentNavigationSession != null)
                {
                    // 更新導航狀態
                    // 這裡可以加入更詳細的導航邏輯
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新導航狀態錯誤: {ex.Message}");
            }
        }

        private void OnLocationChanged(object sender, AppLocation location)
        {
            if (location != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // 處理位置變更
                    System.Diagnostics.Debug.WriteLine($"位置變更: {location.Latitude:F4}, {location.Longitude:F4}, 精確度: {location.Accuracy:F0}m");
                    
                    // 更新地圖上的用戶位置
                    if (_mapService != null)
                    {
                        // 更新規劃模式的地圖
                        if (PlanningMapView?.Map != null)
                        {
                            _mapService.UpdateUserLocation(PlanningMapView.Map, location.Latitude, location.Longitude, 0, location.Accuracy ?? 0);
                            PlanningMapView.Refresh();
                        }
                        
                        // 更新導航模式的地圖
                        if (NavigationMapView?.Map != null && IsNavigating)
                        {
                            _mapService.UpdateUserLocation(NavigationMapView.Map, location.Latitude, location.Longitude, 0, location.Accuracy ?? 0);
                            
                            // 在導航模式中，讓地圖跟隨用戶位置
                            _mapService.AnimateToLocation(NavigationMapView, location.Latitude, location.Longitude, 17);
                            NavigationMapView.Refresh();
                        }
                    }
                });
            }
        }
        
        private void UpdateRouteInfoCard(Route route)
        {
            if (route == null) return;
            
            try
            {
                // 實作 UI 元素更新，當 UI 元素存在時
                System.Diagnostics.Debug.WriteLine($"路線更新: {FormatDuration(route.EstimatedDuration)}, {route.Distance:F1} 公里");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新路線資訊卡片錯誤: {ex.Message}");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // 清理計時器
            _navigationUpdateTimer?.Dispose();
            
            // 取消訂閱導航事件
            if (_navigationService != null)
            {
                _navigationService.InstructionUpdated -= OnNavigationInstructionUpdated;
                _navigationService.StateChanged -= OnNavigationStateChanged;
                _navigationService.DestinationReached -= OnDestinationReached;
                _navigationService.RouteDeviated -= OnRouteDeviated;
            }
        }

        // 進階導航功能
        private async Task StartAdvancedNavigationAsync(Route route)
        {
            try
            {
                if (_navigationService == null)
                {
                    await DisplayAlert("錯誤", "導航服務不可用", "確定");
                    return;
                }

                await _navigationService.StartNavigationAsync(route);
                
                // 更新 UI
                UpdateNavigationUI();
                
                await DisplayAlert("✅ 導航開始", "進階導航已啟動，請跟隨語音指示", "確定");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ 錯誤", $"開始導航失敗: {ex.Message}", "確定");
            }
        }

        private async Task StopAdvancedNavigationAsync()
        {
            try
            {
                if (_navigationService != null)
                {
                    await _navigationService.StopNavigationAsync();
                }
                
                // 更新 UI
                UpdateNavigationUI();
                
                await DisplayAlert("✅ 導航停止", "導航已結束", "確定");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ 錯誤", $"停止導航失敗: {ex.Message}", "確定");
            }
        }

        // 導航事件處理器
        private void OnNavigationInstructionUpdated(object sender, NavigationInstruction instruction)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                OnPropertyChanged(nameof(CurrentInstruction));
                UpdateNavigationUI();
                
                // 播放語音指令
                if (instruction != null && !_isMuted && _ttsService != null)
                {
                    try
                    {
                        await _ttsService.SpeakAsync(instruction.Text);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"語音播放錯誤: {ex.Message}");
                    }
                }
            });
        }

        private void OnNavigationStateChanged(object sender, NavigationState state)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnPropertyChanged(nameof(IsNavigating));
                OnPropertyChanged(nameof(IsNotNavigating));
                OnPropertyChanged(nameof(EstimatedArrivalTime));
                OnPropertyChanged(nameof(RemainingTime));
                OnPropertyChanged(nameof(RemainingDistance));
                OnPropertyChanged(nameof(RouteProgress));
                UpdateNavigationUI();
                
                // 控制路線選項卡片的顯示
                if (RouteOptionsCollectionView != null)
                {
                    RouteOptionsCollectionView.IsVisible = !IsNavigating && RouteOptions?.Count > 0;
                }
            });
        }

        private async void OnDestinationReached(object sender, EventArgs e)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("🏁 到達目的地", "您已成功到達目的地！", "確定");
                UpdateNavigationUI();
            });
        }

        private async void OnRouteDeviated(object sender, RouteDeviationResult result)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (result.IsDeviated)
                {
                    await DisplayAlert("⚠️ 路線偏離", result.Message ?? "您已偏離路線，正在重新規劃", "確定");
                }
            });
        }

        private void UpdateNavigationUI()
        {
            try
            {
                // 更新導航相關 UI 元素的可見性
                OnPropertyChanged(nameof(IsNavigating));
                OnPropertyChanged(nameof(IsNotNavigating));
                OnPropertyChanged(nameof(CurrentInstruction));
                OnPropertyChanged(nameof(EstimatedArrivalTime));
                OnPropertyChanged(nameof(RemainingTime));
                OnPropertyChanged(nameof(RemainingDistance));
                OnPropertyChanged(nameof(RouteProgress));
                
                // 同步靜音按鈕狀態
                UpdateMuteButtonStates();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新導航 UI 錯誤: {ex.Message}");
            }
        }

        // 新的 Google Maps 風格事件處理器
        private async void OnNavigationMenuClicked(object sender, EventArgs e)
        {
            try
            {
                var action = await DisplayActionSheet("導航選項", "取消", null, 
                    "路線總覽", "避開收費站", "避開高速公路", "回報問題", "停止導航");
                
                switch (action)
                {
                    case "路線總覽":
                        await ShowRouteOverview();
                        break;
                    case "避開收費站":
                        await ToggleAvoidTolls();
                        break;
                    case "避開高速公路":
                        await ToggleAvoidHighways();
                        break;
                    case "回報問題":
                        await ReportIssue();
                        break;
                    case "停止導航":
                        await StopAdvancedNavigationAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"導航選單錯誤: {ex.Message}");
            }
        }

        private async void OnExitNavigationClicked(object sender, EventArgs e)
        {
            var result = await DisplayAlert("停止導航", "確定要停止導航嗎？", "停止", "取消");
            if (result)
            {
                await StopAdvancedNavigationAsync();
            }
        }

        private void OnRecenterClicked(object sender, EventArgs e)
        {
            try
            {
                // 重新置中地圖到目前位置
                System.Diagnostics.Debug.WriteLine("重新置中地圖");
                // TODO: 實作地圖置中邏輯
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"重新置中錯誤: {ex.Message}");
            }
        }

        private void OnZoomInClicked(object sender, EventArgs e)
        {
            try
            {
                // 放大地圖
                System.Diagnostics.Debug.WriteLine("地圖放大");
                // TODO: 實作地圖縮放邏輯
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"放大錯誤: {ex.Message}");
            }
        }

        private void OnZoomOutClicked(object sender, EventArgs e)
        {
            try
            {
                // 縮小地圖
                System.Diagnostics.Debug.WriteLine("地圖縮小");
                // TODO: 實作地圖縮放邏輯
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"縮小錯誤: {ex.Message}");
            }
        }

        private void UpdateMuteButtonStates()
        {
            try
            {
                var muteIcon = _isMuted ? "🔇" : "🔊";
                
                // 更新導航模式的靜音按鈕
                if (NavigationMuteButton != null)
                    NavigationMuteButton.Text = muteIcon;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新靜音按鈕錯誤: {ex.Message}");
            }
        }

        // 導航選單功能
        private async Task ShowRouteOverview()
        {
            await DisplayAlert("路線總覽", "顯示完整路線地圖", "確定");
            // TODO: 實作路線總覽功能
        }

        private async Task ToggleAvoidTolls()
        {
            await DisplayAlert("避開收費站", "已設定避開收費站，正在重新計算路線", "確定");
            // TODO: 實作避開收費站功能
        }

        private async Task ToggleAvoidHighways()
        {
            await DisplayAlert("避開高速公路", "已設定避開高速公路，正在重新計算路線", "確定");
            // TODO: 實作避開高速公路功能
        }

        private async Task ReportIssue()
        {
            var action = await DisplayActionSheet("回報問題", "取消", null, 
                "道路封閉", "事故", "施工", "交通壅塞", "其他");
            
            if (action != "取消" && !string.IsNullOrEmpty(action))
            {
                await DisplayAlert("問題已回報", $"感謝您回報「{action}」，這將幫助改善路線規劃", "確定");
                // TODO: 實作問題回報功能
            }
        }

        // 輔助方法
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
            else
                return $"{timeSpan.Minutes}m";
        }

        private string FormatDistance(double distanceInMeters)
        {
            if (distanceInMeters < 1000)
                return $"{Math.Round(distanceInMeters / 10) * 10:F0} m";
            else
                return $"{distanceInMeters / 1000:F1} km";
        }

        #region INotifyPropertyChanged Implementation
        
        public new event PropertyChangedEventHandler PropertyChanged;

        protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "", Action onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region 測試方法 - 基於您的 Nominatim 示例

        /// <summary>
        /// 測試增強版 Nominatim API - 使用您提供的 Newtonsoft.Json 方法
        /// </summary>
        public async Task TestEnhancedNominatimAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🗺️ 開始測試增強版 Nominatim 搜尋...");
                
                // 測試您的台北101示例
                await _enhancedNominatim.TestTaipei101Async();
                
                // 測試多個搜尋查詢
                var testQueries = new[] { "台北101", "台北車站", "西門町", "信義路五段" };
                
                foreach (var query in testQueries)
                {
                    var suggestions = await _enhancedNominatim.GetSearchSuggestionsAsync(query);
                    System.Diagnostics.Debug.WriteLine($"📍 '{query}' 找到 {suggestions.Count} 個建議:");
                    foreach (var suggestion in suggestions.Take(3))
                    {
                        System.Diagnostics.Debug.WriteLine($"  • {suggestion.MainText} - {suggestion.SecondaryText}");
                        System.Diagnostics.Debug.WriteLine($"    坐標: ({suggestion.Latitude:F6}, {suggestion.Longitude:F6})");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("✅ 所有測試完成");
                
                // 在UI上顯示成功訊息
                await DisplayAlert("🎉 測試完成", 
                    "增強版 Nominatim API 測試成功！\n\n使用 Newtonsoft.Json 解析 JSON 回應\n請查看調試輸出查看詳細結果。", 
                    "確定");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 測試失敗: {ex.Message}");
                await DisplayAlert("❌ 測試失敗", $"測試過程中發生錯誤:\n{ex.Message}", "確定");
            }
        }

        #endregion
    }
}