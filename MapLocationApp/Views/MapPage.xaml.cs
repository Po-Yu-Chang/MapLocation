using MapLocationApp.Models;
using MapLocationApp.Services;
using Microsoft.Maui.Controls;
using System.Globalization;

namespace MapLocationApp.Views;

public partial class MapPage : ContentPage
{
    private readonly IMapService _mapService;
    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    
    private AppLocation? _currentLocation;
    private bool _isLocationTracking = false;

    public MapPage(IMapService mapService, ILocationService locationService, IGeofenceService geofenceService)
    {
        InitializeComponent();
        
        _mapService = mapService;
        _locationService = locationService;
        _geofenceService = geofenceService;
        
        InitializeMap();
        ApplyLocalizedUi();
        InitializeServices();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
        LocalizationService.Instance.CultureChanged += OnCultureChanged;
        ApplyLocalizedUi();
    }

    private void InitializeMap()
    {
        // 初始化地圖
        MapControl.Map = _mapService.CreateMap();
        
        // 設定預設中心點（台北市）
        _mapService.CenterMap(MapControl, 25.0330, 121.5654, 12);
        
        // 初始化圖磚供應商選擇器
        var providers = _mapService.GetAvailableProviders();
        TileProviderPicker.ItemsSource = providers.Select(p => p.Name).ToList();
        TileProviderPicker.SelectedIndex = 0;
    }

    private async void InitializeServices()
    {
        // 訂閱位置變更事件
        _locationService.LocationChanged += OnLocationChanged;
        
        // 訂閱地理圍欄事件
        _geofenceService.GeofenceEntered += OnGeofenceEntered;
        _geofenceService.GeofenceExited += OnGeofenceExited;
        
        // 載入地理圍欄
        await LoadGeofences();
        
        // 檢查並請求位置權限
        var hasPermission = await _locationService.RequestLocationPermissionAsync();
        if (!hasPermission)
        {
            await DisplayAlert(L("LocationPermission"), L("LocationPermissionDenied"), L("OK"));
        }
    }

    private async Task LoadGeofences()
    {
        var geofences = await _geofenceService.GetGeofencesAsync();
        _mapService.AddGeofenceLayer(MapControl.Map, geofences);
        MapControl.Refresh();
    }

    private async void OnLocationButtonClicked(object sender, EventArgs e)
    {
        if (!_isLocationTracking)
        {
            // 開始位置追蹤
            StatusLabel.Text = L("LocationPermissionChecking");
            
            // 先檢查權限
            var hasPermission = await _locationService.RequestLocationPermissionAsync();
            if (!hasPermission)
            {
                StatusLabel.Text = L("LocationPermissionDenied");
                await DisplayAlert("權限需求", 
                    "無法取得位置權限。請在系統設定中允許此應用程式存取位置資訊。\n\n" +
                    "Windows 設定路徑：設定 > 隱私權與安全性 > 位置", 
                    "確定");
                return;
            }
            
            StatusLabel.Text = L("GettingLocation");
            
            var location = await _locationService.GetCurrentLocationAsync();
            if (location != null)
            {
                _currentLocation = location;
                _mapService.AddLocationMarker(MapControl.Map, location.Latitude, location.Longitude, L("MyLocation"));
                _mapService.CenterMap(MapControl, location.Latitude, location.Longitude, 15);
                
                LocationLabel.Text = $"{L("LocationLabel")}: {location.Latitude:F6}, {location.Longitude:F6} ({L("LocationAccuracy")}: {location.Accuracy:F0}m)";
                StatusLabel.Text = $"{L("LocationUpdated")} - {location.Timestamp:HH:mm:ss}";
                
                await _locationService.StartLocationUpdatesAsync();
                await _geofenceService.StartMonitoringAsync();
                
                LocateActionButton.Text = L("StopTracking");
                _isLocationTracking = true;
            }
            else
            {
                StatusLabel.Text = L("UnableToGetLocation");
                
                // 提供更詳細的錯誤資訊和解決建議
                var errorMessage = "無法獲取您的位置。可能的原因：\n\n" +
                    "1. GPS 功能未啟用\n" +
                    "2. 位置權限未授權\n" +
                    "3. 裝置不支援定位服務\n" +
                    "4. 需要更好的 GPS 信號（請移動到戶外）\n\n" +
                    "請檢查系統設定並重試。";
                
                await DisplayAlert("位置服務錯誤", errorMessage, "確定");
            }
        }
        else
        {
            // 停止位置追蹤
            await _locationService.StopLocationUpdatesAsync();
            await _geofenceService.StopMonitoringAsync();
            
            LocateActionButton.Text = L("MyLocation");
            _isLocationTracking = false;
            StatusLabel.Text = L("TrackingStopped");
        }
    }

    private void OnTileProviderChanged(object sender, EventArgs e)
    {
        if (TileProviderPicker.SelectedIndex >= 0)
        {
            var providers = _mapService.GetAvailableProviders();
            var selectedProvider = providers[TileProviderPicker.SelectedIndex];
            
            _mapService.SwitchTileProvider(MapControl, selectedProvider);
            AttributionLabel.Text = selectedProvider.Attribution;
            
            StatusLabel.Text = $"已切換至 {selectedProvider.Name}";
        }
    }

    private async void OnGeofenceButtonClicked(object sender, EventArgs e)
    {
        var geofences = await _geofenceService.GetGeofencesAsync();
        var geofenceNames = string.Join(", ", geofences.Select(g => g.Name));
        
        await DisplayAlert("地理圍欄", 
            $"目前監控 {geofences.Count} 個地理圍欄:\n{geofenceNames}", 
            "確定");
    }

    private void OnLocationChanged(object? sender, AppLocation location)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentLocation = location;
            _mapService.AddLocationMarker(MapControl.Map, location.Latitude, location.Longitude, L("MyLocation"));
            LocationLabel.Text = $"{L("LocationLabel")}: {location.Latitude:F6}, {location.Longitude:F6}";
            StatusLabel.Text = $"{L("LocationUpdated")} ({location.Accuracy:F0}m)";
            MapControl.Refresh();
        });
    }

    private void OnGeofenceEntered(object? sender, GeofenceEvent geofenceEvent)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            StatusLabel.Text = $"進入: {geofenceEvent.GeofenceName}";
            
            // 顯示通知
            await DisplayAlert("地理圍欄", 
                $"您已進入 {geofenceEvent.GeofenceName}", 
                "確定");
        });
    }

    private void OnGeofenceExited(object? sender, GeofenceEvent geofenceEvent)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            StatusLabel.Text = $"離開: {geofenceEvent.GeofenceName}";
            
            // 顯示通知
            await DisplayAlert("地理圍欄", 
                $"您已離開 {geofenceEvent.GeofenceName}", 
                "確定");
        });
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
        
        // 停止位置追蹤以節省電池
        if (_isLocationTracking)
        {
            await _locationService.StopLocationUpdatesAsync();
            await _geofenceService.StopMonitoringAsync();
        }
    }

    // 新增縮放控制方法
    private void OnZoomInClicked(object sender, EventArgs e)
    {
        try
        {
            var currentZoom = MapControl.Map.Navigator.Viewport.Resolution;
            MapControl.Map.Navigator.ZoomIn();
            StatusLabel.Text = L("ZoomInMap");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"{L("ZoomFailed")}: {ex.Message}";
        }
    }

    private void OnZoomOutClicked(object sender, EventArgs e)
    {
        try
        {
            var currentZoom = MapControl.Map.Navigator.Viewport.Resolution;
            MapControl.Map.Navigator.ZoomOut();
            StatusLabel.Text = L("ZoomOutMap");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"{L("ZoomFailed")}: {ex.Message}";
        }
    }

    private static string L(string key) => LocalizationService.Instance.GetLocalizedString(key);

    private void OnCultureChanged(CultureInfo _)
    {
        MainThread.BeginInvokeOnMainThread(ApplyLocalizedUi);
    }

    private void ApplyLocalizedUi()
    {
        LocateActionButton.Text = _isLocationTracking ? L("StopTracking") : L("MyLocation");
        GeofenceButton.Text = L("GeofenceLabel");
        TileProviderPicker.Title = L("SelectMapType");

        if (_currentLocation == null)
        {
            LocationLabel.Text = L("LocationLabel");
            AccuracyLabel.Text = L("LocationAccuracy");
            StatusLabel.Text = L("MapReady");
            return;
        }

        LocationLabel.Text = $"{L("LocationLabel")}: {_currentLocation.Latitude:F6}, {_currentLocation.Longitude:F6}";
        AccuracyLabel.Text = $"{L("LocationAccuracy")}: {_currentLocation.Accuracy:F0}m";
        StatusLabel.Text = _isLocationTracking ? L("LocationUpdated") : L("TrackingStopped");
    }
}
