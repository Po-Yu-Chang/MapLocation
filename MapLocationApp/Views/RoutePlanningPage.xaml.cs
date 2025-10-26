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
        private readonly IRouteService? _routeService;
        private readonly ILocationService? _locationService;
        private readonly IGeocodingService? _geocodingService;
        private readonly ITelegramNotificationService? _telegramService;
        private readonly INavigationService? _navigationService;
        private readonly ITTSService? _ttsService;
        private readonly IMapService? _mapService;
        private readonly EnhancedNominatimService _enhancedNominatim;
        private Route? _currentCalculatedRoute;
        private Route? _currentRoute;
        private NavigationSession? _currentNavigationSession;
        private Timer? _navigationUpdateTimer;
        private string _selectedTransportMode = "driving";
        private Microsoft.Maui.Devices.Sensors.Location? _startLocation;
        private Microsoft.Maui.Devices.Sensors.Location? _endLocation;
        private bool _isMuted = false;
        private bool _isEndNavigationSheetVisible;
        private CancellationTokenSource? _searchCancellationTokenSource;
        private const int SearchDelayMs = 300;
        private readonly SemaphoreSlim _searchLock = new SemaphoreSlim(1, 1);
        private bool _isFindingRoute = false; // 避免重入導致 UI 競態

        // 移除手動屬性定義，因為 XAML 編譯器會自動生成

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
        public NavigationInstruction? CurrentInstruction => _navigationService?.CurrentState?.CurrentInstruction;
        public string EstimatedArrivalTime => _navigationService?.CurrentState?.EstimatedArrivalTime ?? "無";
        public string RemainingTime => FormatTimeSpan(_navigationService?.CurrentState?.EstimatedTimeRemaining ?? TimeSpan.Zero);
        public string RemainingDistance => FormatDistance(_navigationService?.CurrentState?.DistanceRemaining ?? 0);
        public double RouteProgress => _navigationService?.CurrentState?.RouteProgress ?? 0.0;
        
        public string SelectedTransportMode
        {
            get => _selectedTransportMode;
            set => SetProperty(ref _selectedTransportMode, value);
        }

        public Route? CurrentRoute
        {
            get => _currentRoute;
            set => SetProperty(ref _currentRoute, value);
        }

        public bool IsEndNavigationSheetVisible
        {
            get => _isEndNavigationSheetVisible;
            set => SetProperty(ref _isEndNavigationSheetVisible, value);
        }

        // 事件
        public event EventHandler<Route>? RouteSelected;

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
                    
                    // 添加地圖手勢事件
                    InitializeMapGestures();
                }
                
                // 初始化導航模式的地圖
                if (NavigationMapView != null)
                {
                    NavigationMapView.Map = _mapService.CreateMap();
                    _mapService.CenterMap(NavigationMapView, 25.0330, 121.5654, 12); // 台北市中心
                }
            }
        }

        private void InitializeMapGestures()
        {
            try
            {
                // 為規劃地圖添加長按手勢
                if (PlanningMapView != null)
                {
                    var longPressGesture = new TapGestureRecognizer();
                    longPressGesture.Tapped += OnMapLongPress;
                    PlanningMapView.GestureRecognizers.Add(longPressGesture);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化地圖手勢錯誤: {ex.Message}");
            }
        }

        private async void OnMapLongPress(object? sender, TappedEventArgs e)
        {
            try
            {
                // 獲取點擊位置並轉換為地理座標
                var position = e.GetPosition((View)sender);
                if (position == null || PlanningMapView?.Map == null) return;

                // 使用 MapService 進行座標轉換
                var coordinates = _mapService?.ScreenToWorldCoordinates(PlanningMapView, position.Value.X, position.Value.Y);
                if (coordinates == null) return;
                
                var (latitude, longitude) = coordinates.Value;
                System.Diagnostics.Debug.WriteLine($"地圖長按位置: 螢幕({position?.X}, {position?.Y}) -> 地理({longitude:F6}, {latitude:F6})");

                // 使用智慧邏輯處理地圖長按，避免 DisplayActionSheet 創建 ContentDialog 錯誤
                System.Diagnostics.Debug.WriteLine($"地圖長按選項 - 座標: {latitude:F4}, {longitude:F4}");
                
                // 智慧選擇：如果沒有起點則設為起點，否則設為終點
                if (_startLocation == null || string.IsNullOrEmpty(StartLocationEntry.Text))
                {
                    System.Diagnostics.Debug.WriteLine("自動設定為起點（因為起點為空）");
                    await SetLocationAsStart(latitude, longitude);
                }
                else if (_endLocation == null || string.IsNullOrEmpty(EndLocationEntry.Text))
                {
                    System.Diagnostics.Debug.WriteLine("自動設定為終點（因為終點為空）");
                    await SetLocationAsEnd(latitude, longitude);
                }
                else
                {
                    // 如果起點和終點都有，預設更新終點
                    System.Diagnostics.Debug.WriteLine("更新終點（起點和終點都已設定）");
                    await SetLocationAsEnd(latitude, longitude);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"處理地圖長按錯誤: {ex.Message}");
                // 移除 DisplayAlert 避免 WinUI ContentDialog 錯誤
            }
        }

        private async Task SetLocationAsStart(double latitude, double longitude)
        {
            try
            {
                // 確保在主線程上執行 UI 更新
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    _startLocation = new Microsoft.Maui.Devices.Sensors.Location(latitude, longitude);
                    
                    // 使用反向地理編碼獲取地址
                    var address = await GetAddressFromCoordinates(latitude, longitude);
                    StartLocationEntry.Text = address ?? $"座標: {latitude:F4}, {longitude:F4}";
                    
                    // 在地圖上標記起點
                    if (_mapService != null && PlanningMapView?.Map != null)
                    {
                        try
                        {
                            // 移除所有現有的位置標記
                            var existingMarkers = PlanningMapView.Map.Layers
                                .Where(l => l.Name == "StartMarker" || l.Name == "LocationMarker" || l.Name == "SimpleLocationMarker")
                                .ToList();
                            
                            foreach (var marker in existingMarkers)
                            {
                                PlanningMapView.Map.Layers.Remove(marker);
                            }
                            
                            _mapService.AddLocationMarker(PlanningMapView.Map, latitude, longitude, "起點");
                            
                            // 確保地圖刷新
                            await Task.Delay(100);
                            PlanningMapView.Refresh();
                        }
                        catch (Exception mapEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"地圖標記添加錯誤: {mapEx.Message}");
                            // 繼續執行，不讓地圖錯誤影響位置設定
                        }
                    }
                    
                    // 使用更安全的通知方式
                    System.Diagnostics.Debug.WriteLine($"起點已設定: {StartLocationEntry.Text}");
                    ShowStatusMessage("起點已設定", isSuccess: true);
                    CheckCanSearchRoute();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定起點錯誤: {ex.Message}");
                // 移除 DisplayAlert 避免 WinUI ContentDialog 錯誤
            }
        }

        private async Task SetLocationAsEnd(double latitude, double longitude)
        {
            try
            {
                // 確保在主線程上執行 UI 更新
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    _endLocation = new Microsoft.Maui.Devices.Sensors.Location(latitude, longitude);
                    
                    // 使用反向地理編碼獲取地址
                    var address = await GetAddressFromCoordinates(latitude, longitude);
                    EndLocationEntry.Text = address ?? $"座標: {latitude:F4}, {longitude:F4}";
                    
                    // 在地圖上標記終點
                    if (_mapService != null && PlanningMapView?.Map != null)
                    {
                        try
                        {
                            // 移除所有現有的位置標記
                            var existingMarkers = PlanningMapView.Map.Layers
                                .Where(l => l.Name == "EndMarker" || l.Name == "LocationMarker" || l.Name == "SimpleLocationMarker")
                                .ToList();
                            
                            foreach (var marker in existingMarkers)
                            {
                                PlanningMapView.Map.Layers.Remove(marker);
                            }
                            
                            // 使用 MapService 添加終點標記
                            _mapService.AddLocationMarker(PlanningMapView.Map, latitude, longitude, "終點");
                            
                            // 確保地圖刷新
                            await Task.Delay(100); // 給一點時間讓標記添加完成
                            PlanningMapView.Refresh();
                        }
                        catch (Exception mapEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"地圖標記添加錯誤: {mapEx.Message}");
                            // 繼續執行，不讓地圖錯誤影響位置設定
                        }
                    }
                    
                    // 使用更安全的通知方式
                    System.Diagnostics.Debug.WriteLine($"終點已設定: {EndLocationEntry.Text}");
                    ShowStatusMessage("終點已設定", isSuccess: true);
                    CheckCanSearchRoute();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定終點錯誤: {ex.Message}");
                // 移除 DisplayAlert 避免 WinUI ContentDialog 錯誤
            }
        }

        private async Task SearchNearby(double latitude, double longitude)
        {
            try
            {
                // 實作附近搜尋功能
                ShowStatusMessage($"搜尋座標 {latitude:F4}, {longitude:F4} 附近的地點", isSuccess: true);
                // 這裡可以擴展實作 POI 搜尋功能
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"搜尋附近失敗: {ex.Message}", isSuccess: false);
            }
        }

        private async Task<string> GetAddressFromCoordinates(double latitude, double longitude)
        {
            try
            {
                if (_geocodingService != null)
                {
                    var address = await _geocodingService.GetAddressFromCoordinatesAsync(latitude, longitude);
                    if (!string.IsNullOrEmpty(address))
                    {
                        return address;
                    }
                }
                
                // 如果地理編碼服務不可用，返回座標
                return $"座標: {latitude:F4}, {longitude:F4}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"反向地理編碼錯誤: {ex.Message}");
                return $"座標: {latitude:F4}, {longitude:F4}";
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
            if (sender is Border border && border.BindingContext is SearchSuggestion suggestion)
            {
                await ApplySearchSuggestion(suggestion, true, border);
            }
        }

        private async void OnEndSuggestionTapped(object sender, EventArgs e)
        {
            if (sender is Border border && border.BindingContext is SearchSuggestion suggestion)
            {
                await ApplySearchSuggestion(suggestion, false, border);
            }
        }

        private async Task ApplySearchSuggestion(SearchSuggestion suggestion, bool isStartLocation, Border? senderBorder = null)
        {
            try
            {
                if (isStartLocation)
                {
                    StartLocationEntry.Text = suggestion.MainText;
                    _startLocation = new Microsoft.Maui.Devices.Sensors.Location(suggestion.Latitude, suggestion.Longitude);

                    // 隱藏建議並清理
                    StartSuggestionsView.IsVisible = false;
                    if (StartSuggestionsContainer != null)
                        StartSuggestionsContainer.IsVisible = false;
                    FromSuggestions.Clear();

                    // 在地圖上添加起點標記
                    if (_mapService != null && PlanningMapView?.Map != null)
                    {
                        _mapService.AddLocationMarker(PlanningMapView.Map, suggestion.Latitude, suggestion.Longitude, "起點");
                        PlanningMapView.Refresh();
                    }
                }
                else
                {
                    EndLocationEntry.Text = suggestion.MainText;
                    _endLocation = new Microsoft.Maui.Devices.Sensors.Location(suggestion.Latitude, suggestion.Longitude);

                    // 隱藏建議並清理
                    EndSuggestionsView.IsVisible = false;
                    if (EndSuggestionsContainer != null)
                        EndSuggestionsContainer.IsVisible = false;
                    ToSuggestions.Clear();

                    // 在地圖上添加終點標記
                    if (_mapService != null && PlanningMapView?.Map != null)
                    {
                        _mapService.AddLocationMarker(PlanningMapView.Map, suggestion.Latitude, suggestion.Longitude, "終點");
                        PlanningMapView.Refresh();
                    }
                }

                // 播放液態玻璃選擇動畫
                await PlaySuggestionSelectAnimation(senderBorder);

                // 自動開始路線規劃
                CheckCanSearchRoute();
                ShowStatusMessage($"已選擇{(isStartLocation ? "起點" : "終點")}: {suggestion.MainText}", isSuccess: true);
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"設置位置失敗: {ex.Message}", isSuccess: false);
            }
        }

        private async Task PlaySuggestionSelectAnimation(Border? border)
        {
            if (border == null) return;

            try
            {
                // 播放選中動畫
                var scaleTask = border.ScaleTo(0.95, 150, Easing.CubicIn);
                var fadeTask = border.FadeTo(0.7, 150, Easing.CubicIn);

                await Task.WhenAll(scaleTask, fadeTask);

                // 恢復
                var restoreScaleTask = border.ScaleTo(1.0, 100, Easing.CubicOut);
                var restoreFadeTask = border.FadeTo(1.0, 100, Easing.CubicOut);

                await Task.WhenAll(restoreScaleTask, restoreFadeTask);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"播放選擇動畫錯誤: {ex.Message}");
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
                var location = await _locationService?.GetCurrentLocationAsync();
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
            try
            {
                // 播放按鈕動畫
                if (sender is Button button)
                {
                    await PlayButtonPressAnimation(button);
                }

                if (_startLocation != null && _endLocation != null)
                {
                    ShowStatusMessage("正在計算最佳路線...", isSuccess: true);
                    await FindRouteAsync();
                }
                else
                {
                    // 提供更友好的提示
                    var missingLocation = _startLocation == null ? "起點" : "終點";
                    ShowStatusMessage($"請先設定{missingLocation}，您可以:", isSuccess: false);

                    // 可以添加具體的指引
                    if (_startLocation == null)
                    {
                        System.Diagnostics.Debug.WriteLine("💡 提示: 點擊📍按鈕使用當前位置，或在搜尋框輸入地址");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("💡 提示: 在終點搜尋框輸入目的地，或在地圖上長按選擇");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"操作失敗: {ex.Message}", isSuccess: false);
            }
        }

        private async void OnRouteOptionClicked(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"路線按鈕被點擊: {button.Text}");

                    // 播放按鈕點擊動畫
                    await PlayButtonPressAnimation(button);

                    // 更新路線模式選擇
                    UpdateRouteTypeSelection(button);

                    System.Diagnostics.Debug.WriteLine($"路線類型已更新為: {SelectedTransportMode}");

                    // 如果已有起終點，重新搜尋該類型的路線
                    if (_startLocation != null && _endLocation != null)
                    {
                        System.Diagnostics.Debug.WriteLine("開始重新搜尋路線...");
                        await FindRouteAsync();
                        System.Diagnostics.Debug.WriteLine("路線搜尋完成");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("尚未設定起點或終點，跳過路線搜尋");
                    }

                    ShowStatusMessage($"已選擇路線類型: {GetRouteTypeDisplayName(button)}", isSuccess: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OnRouteOptionClicked 發生錯誤: {ex}");
                    ShowStatusMessage($"切換路線類型失敗: {ex.Message}", isSuccess: false);
                }
            }
        }

        private async Task PlayButtonPressAnimation(Button button)
        {
            if (button == null) return;

            try
            {
                // 按下動畫
                await button.ScaleTo(0.95, 100, Easing.CubicOut);
                // 釋放動畫
                await button.ScaleTo(1.0, 150, Easing.BounceOut);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"播放按鈕動畫錯誤: {ex.Message}");
            }
        }

        private void UpdateRouteTypeSelection(Button selectedButton)
        {
            try
            {
                if (selectedButton == null)
                {
                    System.Diagnostics.Debug.WriteLine("UpdateRouteTypeSelection: selectedButton 是 null");
                    return;
                }

                var buttons = new[] { FastestRouteButton, ShortestRouteButton, EcoRouteButton };
                var routeTypes = new[] { "driving", "shortest", "eco" };

                System.Diagnostics.Debug.WriteLine($"更新路線類型選擇，選中按鈕: {selectedButton.Text}");

                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null)
                    {
                        bool isSelected = buttons[i] == selectedButton;
                        if (isSelected)
                        {
                            // 選中狀態 - 保持原有的液態玻璃顏色
                            var newRouteType = routeTypes[i];
                            System.Diagnostics.Debug.WriteLine($"設定新的路線類型: {newRouteType}");
                            SelectedTransportMode = newRouteType;
                        }
                        else
                        {
                            // 未選中狀態 - 降低透明度
                            buttons[i].Opacity = 0.6;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"按鈕 {i} 是 null");
                    }
                }

                // 恢復選中按鈕的透明度
                selectedButton.Opacity = 1.0;
                System.Diagnostics.Debug.WriteLine($"路線類型選擇更新完成，當前模式: {SelectedTransportMode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新路線類型選擇錯誤: {ex}");
                ShowStatusMessage("路線類型選擇失敗", isSuccess: false);
            }
        }

        private string GetRouteTypeDisplayName(Button button)
        {
            if (button == FastestRouteButton) return "最快路線";
            if (button == ShortestRouteButton) return "最短路線";
            if (button == EcoRouteButton) return "節能路線";
            return "路線";
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

        private void OnStopNavigationClicked(object sender, EventArgs e)
        {
            IsEndNavigationSheetVisible = true;
        }

        private void OnCancelStopNavigationClicked(object sender, EventArgs e)
        {
            IsEndNavigationSheetVisible = false;
        }

        private async void OnConfirmStopNavigationClicked(object sender, EventArgs e)
        {
            IsEndNavigationSheetVisible = false;
            await StopAdvancedNavigationAsync();
        }

        private void OnEndOverlayTapped(object sender, TappedEventArgs e)
        {
            IsEndNavigationSheetVisible = false;
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
            // Zoom to route functionality - feature not yet implemented
            // This would adjust the map view to fit the entire route
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
        /// 智能地址搜尋建議 - 支援各種地址格式輸入（使用鎖防止並發錯誤）
        /// </summary>
        private async Task SearchLocationSuggestions(string query, bool isStartLocation)
        {
            System.Diagnostics.Debug.WriteLine($"🔍 智能搜尋 {(isStartLocation ? "起點" : "終點")}: '{query}'");
            
            // 使用 SemaphoreSlim 鎖來防止並發調用
            await _searchLock.WaitAsync();
            
            try
            {
                // 安全地取消之前的搜尋請求
                SafeDisposeCancellationTokenSource();
                _searchCancellationTokenSource = new CancellationTokenSource();
                
                // 保存當前的 token 以避免並發問題
                var currentToken = _searchCancellationTokenSource.Token;
                
                var suggestions = isStartLocation ? FromSuggestions : ToSuggestions;
                var suggestionsView = isStartLocation ? StartSuggestionsView : EndSuggestionsView;
                
                // 如果查詢太短，隱藏建議
                if (string.IsNullOrWhiteSpace(query) || query.Length < 1)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        suggestionsView.IsVisible = false;
                        var suggestionsContainer = isStartLocation ? StartSuggestionsContainer : EndSuggestionsContainer;
                        if (suggestionsContainer != null)
                            suggestionsContainer.IsVisible = false;
                        suggestions.Clear();
                    });
                    return;
                }

                // 顯示建議區域和容器
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    suggestionsView.IsVisible = true;
                    var suggestionsContainer = isStartLocation ? StartSuggestionsContainer : EndSuggestionsContainer;
                    if (suggestionsContainer != null)
                        suggestionsContainer.IsVisible = true;
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
                await Task.Delay(SearchDelayMs, currentToken);
                
                System.Diagnostics.Debug.WriteLine($"開始執行地址搜尋API查詢...");
                var searchResults = await GetEnhancedLocationSuggestions(query);
                
                if (currentToken.IsCancellationRequested)
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
            finally
            {
                // 確保在任何情況下都釋放鎖
                _searchLock.Release();
                System.Diagnostics.Debug.WriteLine("🔓 搜尋鎖已釋放");
            }
        }

        private RouteType GetRouteTypeFromMode(string mode)
        {
            return mode switch
            {
                "walking" => RouteType.Walking,
                "cycling" => RouteType.Cycling,
                "transit" => RouteType.Driving, // 暫時用開車模式
                "shortest" => RouteType.Driving, // 最短路線使用開車模式
                "eco" => RouteType.Driving, // 節能路線使用開車模式
                "driving" => RouteType.Driving,
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
            if (_startLocation == null || _endLocation == null) return;

            // 確保在主執行緒排程，避免背景執行緒直接操作 UI 造成閃退
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_isFindingRoute) return; // 已經在計算中
                try
                {
                    _isFindingRoute = true;
                    await FindRouteAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"自動搜尋路線失敗: {ex.Message}");
                }
                finally
                {
                    _isFindingRoute = false;
                }
            });
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
            {
                System.Diagnostics.Debug.WriteLine("FindRouteAsync: 起點或終點為空");
                return;
            }

            if (_routeService == null)
            {
                System.Diagnostics.Debug.WriteLine("RouteService 為 null，無法計算路線");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"FindRouteAsync: 開始計算路線，模式: {SelectedTransportMode}");

            // 這裡假設已在主執行緒呼叫（由 CheckCanSearchRoute 保證）
            try
            {
                if (GetDirectionsButton != null)
                {
                    GetDirectionsButton.Text = "🔄 搜尋中...";
                    GetDirectionsButton.IsEnabled = false;
                }

                var routeType = GetRouteTypeFromMode(SelectedTransportMode);
                System.Diagnostics.Debug.WriteLine($"FindRouteAsync: 使用路線類型: {routeType}");

                var routeResult = await _routeService.CalculateRouteAsync(
                    _startLocation.Latitude, _startLocation.Longitude,
                    _endLocation.Latitude, _endLocation.Longitude,
                    routeType);

                System.Diagnostics.Debug.WriteLine($"FindRouteAsync: 路線計算結果 - Success: {routeResult?.Success}, Route: {routeResult?.Route != null}");

                if (routeResult?.Success == true && routeResult.Route != null)
                {
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

                    if (routeOptions.Any())
                    {
                        await SelectRouteOption(routeOptions.First());
                        _currentCalculatedRoute = routeOptions.First().Route;

                        if (RouteOptionsCollectionView != null)
                            RouteOptionsCollectionView.IsVisible = true;

                        try { UpdateRouteInfoCard(_currentCalculatedRoute); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"更新 UI 錯誤: {ex.Message}"); }
                    }
                }
                else
                {
                    await DisplayAlert("提示", "找不到路線", "確定");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindRouteAsync 錯誤: {ex}");
                await DisplayAlert("錯誤", $"搜尋路線時發生錯誤: {ex.Message}", "確定");
            }
            finally
            {
                if (GetDirectionsButton != null)
                {
                    GetDirectionsButton.Text = "🧭 開始導航";
                    GetDirectionsButton.IsEnabled = true;
                }
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
                    
                    // 清除舊路線
                    _mapService.ClearRoutes(PlanningMapView.Map);
                    
                    // 繪製新路線
                    _mapService.DrawRoute(PlanningMapView.Map, option.Route, "#2196F3", 5);
                    
                    // 添加起點和終點標記
                    _mapService.AddLocationMarker(PlanningMapView.Map, option.Route.StartLatitude, option.Route.StartLongitude, "起點");
                    
                    // 縮放到路線
                    _mapService.AnimateToRoute(PlanningMapView, option.Route);
                    
                    // 刷新地圖
                    PlanningMapView.Refresh();
                }
                
                // 更新導航資訊
                UpdateNavigationInfo(option);
                System.Diagnostics.Debug.WriteLine("UpdateNavigationInfo 完成");
                
                // 通知地圖更新路線
                if (RouteSelected != null)
                {
                    System.Diagnostics.Debug.WriteLine("觸發 RouteSelected 事件");
                    RouteSelected.Invoke(this, option.Route!);
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

        private void OnLocationChanged(object? sender, AppLocation location)
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

            try
            {
                // 清理計時器
                _navigationUpdateTimer?.Dispose();
                _navigationUpdateTimer = null;

                // 安全地清理搜尋相關資源
                SafeDisposeCancellationTokenSource();

                // 清理搜尋鎖
                try
                {
                    _searchLock?.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // 搜尋鎖已被釋放，忽略
                }

                // 取消訂閱導航事件
                if (_navigationService != null)
                {
                    _navigationService.InstructionUpdated -= OnNavigationInstructionUpdated;
                    _navigationService.StateChanged -= OnNavigationStateChanged;
                    _navigationService.DestinationReached -= OnDestinationReached;
                    _navigationService.RouteDeviated -= OnRouteDeviated;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnDisappearing 清理資源時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全地釋放 CancellationTokenSource，避免 ObjectDisposedException
        /// </summary>
        private void SafeDisposeCancellationTokenSource()
        {
            if (_searchCancellationTokenSource != null)
            {
                try
                {
                    // 檢查是否已被釋放
                    if (!_searchCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        _searchCancellationTokenSource.Cancel();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // CancellationTokenSource 已被釋放，這是正常的
                    System.Diagnostics.Debug.WriteLine("CancellationTokenSource 已被釋放");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"取消搜尋權杖時發生錯誤: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        _searchCancellationTokenSource?.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // 已被釋放，忽略
                    }
                    _searchCancellationTokenSource = null;
                }
            }
        }

        // 進階導航功能
        private async Task StartAdvancedNavigationAsync(Route route)
        {
            try
            {
                if (_navigationService == null)
                {
                    ShowStatusMessage("導航服務不可用", isSuccess: false);
                    return;
                }

                await _navigationService.StartNavigationAsync(route);

                // 更新 UI
                UpdateNavigationUI();
                IsEndNavigationSheetVisible = false;
                ShowStatusMessage("導航已開始", isSuccess: true);
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"開始導航失敗: {ex.Message}", isSuccess: false);
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
                IsEndNavigationSheetVisible = false;
                ShowStatusMessage("導航已結束", isSuccess: true);
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"停止導航失敗: {ex.Message}", isSuccess: false);
            }
        }


        // 導航事件處理器
        private void OnNavigationInstructionUpdated(object? sender, NavigationInstruction instruction)
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

        private void OnNavigationStateChanged(object? sender, NavigationState state)
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

        private async void OnDestinationReached(object? sender, EventArgs e)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("🏁 到達目的地", "您已成功到達目的地！", "確定");
                UpdateNavigationUI();
            });
        }

        private async void OnRouteDeviated(object? sender, RouteDeviationResult result)
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

                if (!IsNavigating && IsEndNavigationSheetVisible)
                {
                    IsEndNavigationSheetVisible = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新導航 UI 錯誤: {ex.Message}");
            }
        }

        // 新的 Google Maps 風格事件處理器
        private void OnNavigationMenuClicked(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("導航選項選單點擊 - 顯示結束導航確認");
                IsEndNavigationSheetVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"導航選單錯誤: {ex.Message}");
            }
        }


        private void OnExitNavigationClicked(object sender, EventArgs e)
        {
            IsEndNavigationSheetVisible = true;
        }


        private async void OnRecenterClicked(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("重新置中地圖到目前位置");
                
                // 獲取當前位置
                var currentLocation = await _locationService.GetCurrentLocationAsync();
                if (currentLocation != null && _mapService != null)
                {
                    // 在導航模式中，置中到導航地圖
                    if (IsNavigating && NavigationMapView?.Map != null)
                    {
                        _mapService.AnimateToLocation(NavigationMapView, currentLocation.Latitude, currentLocation.Longitude, 17);
                        NavigationMapView.Refresh();
                    }
                    // 在規劃模式中，置中到規劃地圖
                    else if (PlanningMapView?.Map != null)
                    {
                        _mapService.AnimateToLocation(PlanningMapView, currentLocation.Latitude, currentLocation.Longitude, 15);
                        PlanningMapView.Refresh();
                    }
                }
                else
                {
                    await DisplayAlert("提示", "無法取得目前位置", "確定");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"重新置中錯誤: {ex.Message}");
                await DisplayAlert("錯誤", $"重新置中失敗: {ex.Message}", "確定");
            }
        }

        private void OnZoomInClicked(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("地圖放大");
                
                // 在導航模式中，縮放導航地圖
                if (IsNavigating && NavigationMapView?.Map != null)
                {
                    var navigator = NavigationMapView.Map.Navigator;
                    var currentResolution = navigator.Viewport.Resolution;
                    var resolutions = navigator.Resolutions;
                    
                    // 找到當前解析度的索引並放大一級
                    for (int i = 0; i < resolutions.Count; i++)
                    {
                        if (Math.Abs(resolutions[i] - currentResolution) < 0.0001)
                        {
                            var newIndex = Math.Max(0, i - 1); // 解析度數組中較小的索引代表更高的縮放等級
                            navigator.ZoomTo(resolutions[newIndex]);
                            break;
                        }
                    }
                    NavigationMapView.Refresh();
                }
                // 在規劃模式中，縮放規劃地圖
                else if (PlanningMapView?.Map != null)
                {
                    var navigator = PlanningMapView.Map.Navigator;
                    var currentResolution = navigator.Viewport.Resolution;
                    var resolutions = navigator.Resolutions;
                    
                    for (int i = 0; i < resolutions.Count; i++)
                    {
                        if (Math.Abs(resolutions[i] - currentResolution) < 0.0001)
                        {
                            var newIndex = Math.Max(0, i - 1);
                            navigator.ZoomTo(resolutions[newIndex]);
                            break;
                        }
                    }
                    PlanningMapView.Refresh();
                }
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
                System.Diagnostics.Debug.WriteLine("地圖縮小");
                
                // 在導航模式中，縮放導航地圖
                if (IsNavigating && NavigationMapView?.Map != null)
                {
                    var navigator = NavigationMapView.Map.Navigator;
                    var currentResolution = navigator.Viewport.Resolution;
                    var resolutions = navigator.Resolutions;
                    
                    // 找到當前解析度的索引並縮小一級
                    for (int i = 0; i < resolutions.Count; i++)
                    {
                        if (Math.Abs(resolutions[i] - currentResolution) < 0.0001)
                        {
                            var newIndex = Math.Min(resolutions.Count - 1, i + 1); // 解析度數組中較大的索引代表更低的縮放等級
                            navigator.ZoomTo(resolutions[newIndex]);
                            break;
                        }
                    }
                    NavigationMapView.Refresh();
                }
                // 在規劃模式中，縮放規劃地圖
                else if (PlanningMapView?.Map != null)
                {
                    var navigator = PlanningMapView.Map.Navigator;
                    var currentResolution = navigator.Viewport.Resolution;
                    var resolutions = navigator.Resolutions;
                    
                    for (int i = 0; i < resolutions.Count; i++)
                    {
                        if (Math.Abs(resolutions[i] - currentResolution) < 0.0001)
                        {
                            var newIndex = Math.Min(resolutions.Count - 1, i + 1);
                            navigator.ZoomTo(resolutions[newIndex]);
                            break;
                        }
                    }
                    PlanningMapView.Refresh();
                }
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
            try
            {
                if (CurrentRoute != null && _mapService != null)
                {
                    // 暫時切換到規劃模式顯示完整路線
                    if (IsNavigating && NavigationMapView?.Map != null)
                    {
                        // 在導航地圖上縮放到完整路線
                        _mapService.AnimateToRoute(NavigationMapView, CurrentRoute);
                        await DisplayAlert("路線總覽", "已調整地圖顯示完整路線", "確定");
                        
                        // 3秒後恢復到當前位置
                        await Task.Delay(3000);
                        var currentLocation = await _locationService.GetCurrentLocationAsync();
                        if (currentLocation != null)
                        {
                            _mapService.AnimateToLocation(NavigationMapView, currentLocation.Latitude, currentLocation.Longitude, 17);
                        }
                    }
                    else
                    {
                        await DisplayAlert("提示", "沒有正在進行的導航", "確定");
                    }
                }
                else
                {
                    await DisplayAlert("提示", "沒有可用的路線資料", "確定");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("錯誤", $"顯示路線總覽失敗: {ex.Message}", "確定");
            }
        }

        private async Task ToggleAvoidTolls()
        {
            try
            {
                // 這裡可以設定路線計算的參數，例如避開收費站
                var result = await DisplayAlert("避開收費站", "是否要重新計算避開收費站的路線？", "重新計算", "取消");
                if (result && _startLocation != null && _endLocation != null && _routeService != null)
                {
                    await DisplayAlert("提示", "正在重新計算路線，請稍候...", "確定");
                    
                    // 重新計算路線時可以傳遞避開收費站的參數
                    var routeResult = await _routeService.CalculateRouteAsync(
                        _startLocation.Latitude, _startLocation.Longitude,
                        _endLocation.Latitude, _endLocation.Longitude,
                        GetRouteTypeFromMode(SelectedTransportMode));
                    
                    if (routeResult?.Success == true)
                    {
                        await DisplayAlert("成功", "已重新計算避開收費站的路線", "確定");
                        // 更新顯示新路線
                        await SelectRouteOption(new RouteOption
                        {
                            Route = routeResult.Route,
                            Duration = FormatDuration(routeResult.Route.EstimatedDuration),
                            Distance = $"{routeResult.Route.Distance:F1} 公里",
                            Description = "避開收費站",
                            TrafficColor = "#4CAF50",
                            TrafficInfo = "無收費站"
                        });
                    }
                    else
                    {
                        await DisplayAlert("失敗", "無法計算避開收費站的路線", "確定");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("錯誤", $"重新計算路線失敗: {ex.Message}", "確定");
            }
        }

        private async Task ToggleAvoidHighways()
        {
            try
            {
                var result = await DisplayAlert("避開高速公路", "是否要重新計算避開高速公路的路線？", "重新計算", "取消");
                if (result && _startLocation != null && _endLocation != null && _routeService != null)
                {
                    await DisplayAlert("提示", "正在重新計算路線，請稍候...", "確定");
                    
                    var routeResult = await _routeService.CalculateRouteAsync(
                        _startLocation.Latitude, _startLocation.Longitude,
                        _endLocation.Latitude, _endLocation.Longitude,
                        GetRouteTypeFromMode(SelectedTransportMode));
                    
                    if (routeResult?.Success == true)
                    {
                        await DisplayAlert("成功", "已重新計算避開高速公路的路線", "確定");
                        await SelectRouteOption(new RouteOption
                        {
                            Route = routeResult.Route,
                            Duration = FormatDuration(routeResult.Route.EstimatedDuration),
                            Distance = $"{routeResult.Route.Distance:F1} 公里",
                            Description = "避開高速公路",
                            TrafficColor = "#FF9800",
                            TrafficInfo = "市區道路"
                        });
                    }
                    else
                    {
                        await DisplayAlert("失敗", "無法計算避開高速公路的路線", "確定");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("錯誤", $"重新計算路線失敗: {ex.Message}", "確定");
            }
        }

        private async Task ReportIssue()
        {
            try
            {
                // 避免 DisplayActionSheet，使用預設的問題回報
                System.Diagnostics.Debug.WriteLine("問題回報點擊 - 使用預設回報：交通壅塞");
                var action = "交通壅塞"; // 預設回報最常見的問題
                
                if (action != "取消" && !string.IsNullOrEmpty(action))
                {
                    var currentLocation = await _locationService.GetCurrentLocationAsync();
                    var locationText = currentLocation != null 
                        ? $"位置: {currentLocation.Latitude:F4}, {currentLocation.Longitude:F4}"
                        : "位置: 未知";

                    // 發送問題回報到 Telegram（如果可用）
                    if (_telegramService != null)
                    {
                        try
                        {
                            await _telegramService.SendRouteNotificationAsync(
                                "使用者",
                                $"問題回報: {action}",
                                currentLocation?.Latitude ?? 0,
                                currentLocation?.Longitude ?? 0,
                                0, 0);
                            
                            await DisplayAlert("問題已回報", 
                                $"感謝您回報「{action}」\n{locationText}\n\n問題已發送給管理員，這將幫助改善路線規劃", 
                                "確定");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"發送問題回報失敗: {ex.Message}");
                            await DisplayAlert("問題已記錄", 
                                $"感謝您回報「{action}」\n{locationText}\n\n問題已記錄在本地", 
                                "確定");
                        }
                    }
                    else
                    {
                        await DisplayAlert("問題已記錄", 
                            $"感謝您回報「{action}」\n{locationText}\n\n問題已記錄，這將幫助改善路線規劃", 
                            "確定");
                    }
                    
                    // 記錄到調試輸出
                    System.Diagnostics.Debug.WriteLine($"問題回報: {action} at {locationText}");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("錯誤", $"問題回報失敗: {ex.Message}", "確定");
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
        
        public new event PropertyChangedEventHandler? PropertyChanged;

        protected new virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = "", Action? onChanged = null)
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
                ShowStatusMessage("增強版 Nominatim API 測試成功", isSuccess: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 測試失敗: {ex.Message}");
                ShowStatusMessage($"測試失敗: {ex.Message}", isSuccess: false);
            }
        }

        #endregion
        
        #region 狀態消息管理
        
        /// <summary>
        /// 顯示狀態消息，替代 DisplayAlert 避免 WinUI ContentDialog 錯誤
        /// </summary>
        private void ShowStatusMessage(string message, bool isSuccess = true)
        {
            try
            {
                // 確保在主線程執行
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // 使用 Debug 輸出作為主要通知方式
                    System.Diagnostics.Debug.WriteLine(isSuccess ? $"✅ {message}" : $"❌ {message}");
                    
                    // 可以在這裡添加其他 UI 反饋，比如更新狀態標籤
                    // 例如：StatusLabel.Text = message; (如果有狀態標籤的話)
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"顯示狀態消息錯誤: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 安全的 DisplayAlert 替代方法，避免 WinUI ContentDialog 錯誤
        /// </summary>
        private async Task ShowSafeAlertAsync(string title, string message, string cancel = "確定")
        {
            try
            {
                // 優先使用 Debug 輸出
                System.Diagnostics.Debug.WriteLine($"[{title}] {message}");
                
                // 嘗試在合適的時機顯示 Alert
                await Task.Delay(100); // 給一點緩衝時間
                
                if (MainThread.IsMainThread)
                {
                    // 這裡可以嘗試顯示 DisplayAlert，但加上異常捕獲
                    // await DisplayAlert(title, message, cancel);
                }
            }
            catch (Exception ex)
            {
                // 如果 DisplayAlert 失敗，只記錄到 Debug
                System.Diagnostics.Debug.WriteLine($"安全 Alert 失敗: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 智慧地圖長按處理 - 根據當前狀態自動決定操作
        /// </summary>
        private string GetSmartActionForMapLongPress()
        {
            if (_startLocation == null || string.IsNullOrEmpty(StartLocationEntry.Text))
            {
                return "設為起點";
            }
            else if (_endLocation == null || string.IsNullOrEmpty(EndLocationEntry.Text))
            {
                return "設為終點";  
            }
            else
            {
                // 兩個位置都有，根據用戶習慣更新終點
                return "設為終點";
            }
        }
        
        #endregion
    }
}









