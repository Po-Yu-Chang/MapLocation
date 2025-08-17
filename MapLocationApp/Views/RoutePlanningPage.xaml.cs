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
        private Route _currentCalculatedRoute;
        private Route _currentRoute;
        private NavigationSession _currentNavigationSession;
        private Timer _navigationUpdateTimer;
        private string _selectedTransportMode = "driving";
        private Microsoft.Maui.Devices.Sensors.Location _startLocation;
        private Microsoft.Maui.Devices.Sensors.Location _endLocation;

        // Google Maps 風格的集合
        public ObservableCollection<Route> SavedRoutes { get; set; }
        public ObservableCollection<Route> RecentRoutes { get; set; }
        public ObservableCollection<RouteOption> RouteOptions { get; set; }
        public ObservableCollection<SearchSuggestion> FromSuggestions { get; set; }
        public ObservableCollection<SearchSuggestion> ToSuggestions { get; set; }
        
        public ICommand StartNavigationCommand { get; }
        public ICommand DeleteRouteCommand { get; }
        
        public bool HasSavedRoutes => RecentRoutes?.Count > 0;
        
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
            
            // 初始化集合
            SavedRoutes = new ObservableCollection<Route>();
            RecentRoutes = new ObservableCollection<Route>();
            RouteOptions = new ObservableCollection<RouteOption>();
            FromSuggestions = new ObservableCollection<SearchSuggestion>();
            ToSuggestions = new ObservableCollection<SearchSuggestion>();
            
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
            
            // 載入已儲存的路線
            LoadSavedRoutesAsync();
            
            // 設定預設交通方式
            SelectedTransportMode = "driving";
            UpdateTransportModeButtons();
        }

        // XAML 中實際使用的事件處理器
        private async void OnStartLocationTextChanged(object sender, TextChangedEventArgs e)
        {
            await SearchLocationSuggestions(e.NewTextValue, true);
        }

        private async void OnEndLocationTextChanged(object sender, TextChangedEventArgs e)
        {
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
                await StartNavigationAsync(CurrentRoute);
            }
        }

        private async void OnStopNavigationClicked(object sender, EventArgs e)
        {
            try
            {
                // 停止導航計時器
                if (_navigationUpdateTimer != null)
                {
                    _navigationUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _navigationUpdateTimer.Dispose();
                    _navigationUpdateTimer = null;
                }

                // 清除導航會話
                _currentNavigationSession = null;

                // 發送導航結束通知
                var telegramService = ServiceHelper.GetService<ITelegramNotificationService>();
                if (telegramService != null && _startLocation != null && _endLocation != null)
                {
                    await telegramService.SendRouteNotificationAsync("使用者", "導航已結束", 
                        _startLocation.Latitude, _startLocation.Longitude, 
                        _endLocation.Latitude, _endLocation.Longitude);
                }

                // 更新 UI 狀態
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // 清除導航狀態相關 UI
                    OnPropertyChanged("NavigationStatus");
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("錯誤", $"停止導航時發生錯誤: {ex.Message}", "確定");
            }
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
        private async Task SearchLocationSuggestions(string query, bool isStartLocation)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                // SearchSuggestionsCollectionView.IsVisible = false;
                return;
            }

            try
            {
                var suggestions = await GetLocationSuggestions(query);
                
                // 更新搜尋建議的 ItemsSource
                // SearchSuggestionsCollectionView.ItemsSource = suggestions;
                // SearchSuggestionsCollectionView.IsVisible = suggestions.Any();
            }
            catch (Exception ex)
            {
                await DisplayAlert("錯誤", $"搜尋地點時發生錯誤: {ex.Message}", "確定");
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

        private async Task<List<SearchSuggestion>> GetLocationSuggestions(string query)
        {
            try
            {
                // 使用 GeocodingService 的搜尋建議功能
                var suggestions = await _geocodingService.GetLocationSuggestionsAsync(query);
                return suggestions.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"獲取位置建議錯誤: {ex.Message}");
                return GetDefaultSuggestions(query);
            }
        }

        private List<SearchSuggestion> GetDefaultSuggestions(string query)
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
                if (route == null)
                {
                    await DisplayAlert("❌ 錯誤", "路線資料無效", "確定");
                    return;
                }

                // 使用新的 NavigationService 開始導航
                _currentNavigationSession = await _navigationService.StartNavigationAsync(route);

                // 訂閱導航事件
                _navigationService.InstructionUpdated += OnNavigationInstructionUpdated;
                _navigationService.LocationUpdated += OnNavigationLocationUpdated;
                _navigationService.RouteDeviationDetected += OnRouteDeviationDetected;
                _navigationService.DestinationArrived += OnDestinationArrived;
                _navigationService.NavigationCompleted += OnNavigationCompleted;

                await DisplayAlert("✅ 導航開始", 
                    $"已開始導航至 {route.ToAddress}\n將會提供語音指引", "確定");

                System.Diagnostics.Debug.WriteLine($"Enhanced navigation started for route: {route.Name}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ 錯誤", $"開始導航時發生錯誤: {ex.Message}", "確定");
                System.Diagnostics.Debug.WriteLine($"Start navigation error: {ex}");
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
                    System.Diagnostics.Debug.WriteLine($"位置變更: {location.Latitude:F4}, {location.Longitude:F4}");
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
                _navigationService.LocationUpdated -= OnNavigationLocationUpdated;
                _navigationService.RouteDeviationDetected -= OnRouteDeviationDetected;
                _navigationService.DestinationArrived -= OnDestinationArrived;
                _navigationService.NavigationCompleted -= OnNavigationCompleted;
            }
        }

        #region Navigation Event Handlers

        private void OnNavigationInstructionUpdated(object sender, NavigationInstruction instruction)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation instruction: {instruction.Text} - {instruction.Distance}");
                    
                    // Update UI with navigation instruction
                    // This would update navigation UI elements if they exist
                    DisplayAlert("🗣️ 導航指示", instruction.Text, "確定");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation instruction UI update error: {ex.Message}");
                }
            });
        }

        private void OnNavigationLocationUpdated(object sender, AppLocation location)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation location update: {location.Latitude:F6}, {location.Longitude:F6}");
                    
                    // Update location-based UI elements
                    // This would update map position and navigation progress
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation location UI update error: {ex.Message}");
                }
            });
        }

        private void OnRouteDeviationDetected(object sender, RouteDeviationEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Route deviation detected: {e.Message}");
                    
                    if (e.RequiresRecalculation)
                    {
                        await DisplayAlert("🔄 路線重新規劃", e.Message, "確定");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Route deviation UI update error: {ex.Message}");
                }
            });
        }

        private void OnDestinationArrived(object sender, DestinationArrivalEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var totalTime = e.TotalNavigationTime.TotalMinutes;
                    await DisplayAlert("🎉 到達目的地", 
                        $"您已成功到達目的地！\n導航時間：{totalTime:F0} 分鐘", "完成");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Destination arrival UI update error: {ex.Message}");
                }
            });
        }

        private void OnNavigationCompleted(object sender, NavigationCompletedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation completed: {e.Reason}");
                    
                    // Reset navigation state
                    _currentNavigationSession = null;
                    
                    // Update UI to reflect navigation completion
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation completion UI update error: {ex.Message}");
                }
            });
        }

        #endregion

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
    }
}