using MapLocationApp.Models;
using MapLocationApp.Services;
using MapLocationApp.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MapLocationApp.Views;

public partial class CheckInPage : ContentPage
{
    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private readonly IGeocodingService _geocodingService;
    private readonly ICheckInStorageService _checkInStorageService;
    private readonly IMapService _mapService;
    private readonly IWifiService _wifiService;
    
    private AppLocation? _currentLocation;
    private List<GeofenceRegion> _allGeofences = new();
    private readonly ObservableCollection<GeofenceWithDistance> _nearbyGeofences = new();
    private readonly ObservableCollection<CheckInRecordDisplay> _todayRecords = new();
    private CheckInRecord? _currentCheckIn;
    private bool _isMapInitialized;

    private readonly IDatabaseService _databaseService;
    private readonly IConfigService _configService;
    private readonly IUserSessionService _userSessionService;
    private User? _currentUser;

    public CheckInPage(ILocationService locationService, IGeofenceService geofenceService, IGeocodingService geocodingService, ICheckInStorageService checkInStorageService, IDatabaseService databaseService, IConfigService configService, IMapService mapService, IWifiService wifiService)
    {
        InitializeComponent();

        _locationService = locationService;
        _geofenceService = geofenceService;
        _geocodingService = geocodingService;
        _checkInStorageService = checkInStorageService;
        _databaseService = databaseService;
        _configService = configService;
        _mapService = mapService;
        _wifiService = wifiService;
        _userSessionService = ServiceHelper.GetService<IUserSessionService>();
        
        NearbyGeofencesCollectionView.ItemsSource = _nearbyGeofences;
        TodayRecordsCollectionView.ItemsSource = _todayRecords;
        
        // 訂閱用戶會話事件
        _userSessionService.UserLoggedIn += OnUserLoggedIn;
        _userSessionService.UserLoggedOut += OnUserLoggedOut;
        
        InitializeAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
        LocalizationService.Instance.CultureChanged += OnCultureChanged;
        ApplyLocalizedUi();
        // 從其他頁面（如登入或圍欄管理）返回時重新同步使用者狀態與地點清單
        try
        {
            await LoadCurrentUser();
            UpdateUI();
            await LoadGeofences();
            await LoadTodayRecords();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnAppearing 載入失敗: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
    }

    private async void InitializeAsync()
    {
        await LoadCurrentUser();
        await LoadCurrentLocation();
        await LoadGeofences();
        await LoadTodayRecords();
        UpdateUI();
    }

    private async Task LoadCurrentUser()
    {
        try
        {
            _currentUser = await _userSessionService.GetCurrentUserAsync();
            UpdateUserDisplay();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"載入當前用戶失敗: {ex.Message}");
        }
    }
    
    private void UpdateUserDisplay()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_currentUser != null)
            {
                UserNameLabel.Text = _currentUser.DisplayName;
                UserDepartmentLabel.Text = _currentUser.DepartmentPosition;
                LogoutButton.IsVisible = true;
            }
            else
            {
                UserNameLabel.Text = L("NotLoggedIn");
                UserDepartmentLabel.Text = L("PleaseLoginToUse");
                LogoutButton.IsVisible = false;
            }
        });
    }
    
    private void UpdateUI()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            bool isLoggedIn = _currentUser != null;
            CheckInOperationsPanel.IsEnabled = isLoggedIn;
            LoginRequiredPanel.IsVisible = !isLoggedIn;
        });
    }
    
    private void OnUserLoggedIn(object? sender, User user)
    {
        _currentUser = user;
        UpdateUserDisplay();
        UpdateUI();
        _ = LoadTodayRecords(); // 重新載入記錄
    }
    
    private void OnUserLoggedOut(object? sender, EventArgs e)
    {
        _currentUser = null;
        UpdateUserDisplay();
        UpdateUI();
        _todayRecords.Clear(); // 清空記錄
    }

    private async Task LoadCurrentLocation()
    {
        try
        {
            CurrentLocationLabel.Text = L("GettingLocation");
            
            var hasPermission = await _locationService.RequestLocationPermissionAsync();
            if (!hasPermission)
            {
                CurrentLocationLabel.Text = L("LocationPermissionDenied");
                return;
            }

            _currentLocation = await _locationService.GetCurrentLocationAsync();
            
            if (_currentLocation != null)
            {
                // 顯示座標
                var coordinateText = $"{_currentLocation.Latitude:F6}, {_currentLocation.Longitude:F6}";
                AccuracyLabel.Text = $"{L("LocationAccuracy")}: ±{_currentLocation.Accuracy:F0} m";

                // 更新地圖圖釘
                UpdateLocationMap(_currentLocation.Latitude, _currentLocation.Longitude);
                
                // 開始地址解析
                CurrentLocationLabel.Text = L("ResolvingAddress");
                
                try
                {
                    var address = await _geocodingService.GetAddressFromCoordinatesAsync(
                        _currentLocation.Latitude, 
                        _currentLocation.Longitude);
                    
                    CurrentLocationLabel.Text = address;
                }
                catch (Exception geocodingEx)
                {
                    System.Diagnostics.Debug.WriteLine($"地址解析失敗: {geocodingEx.Message}");
                    CurrentLocationLabel.Text = coordinateText; // 回退到座標顯示
                }
                
                UpdateNearbyGeofences();
            }
            else
            {
                CurrentLocationLabel.Text = L("UnableToGetLocation");
                AccuracyLabel.Text = L("CheckGpsSettings");
            }
        }
        catch (Exception ex)
        {
            CurrentLocationLabel.Text = L("LocationFailed");
            AccuracyLabel.Text = ex.Message;
        }
    }

    private void UpdateLocationMap(double latitude, double longitude)
    {
        try
        {
            if (!_isMapInitialized)
            {
                LocationMapControl.Map = _mapService.CreateMap();
                _isMapInitialized = true;
            }

            _mapService.AddLocationMarker(LocationMapControl.Map, latitude, longitude, L("MyLocation"));
            // Draw geofence radius circles if geofences are already loaded
            if (_allGeofences.Count > 0)
                _mapService.AddGeofenceLayer(LocationMapControl.Map, _allGeofences);
            _mapService.CenterMap(LocationMapControl, latitude, longitude, 16);
            MapPlaceholderLabel.IsVisible = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"更新位置地圖失敗: {ex.Message}");
        }
    }

    private async Task LoadGeofences()
    {
        // Show loading indicator
        GeofenceLoadingIndicator.IsVisible = true;
        GeofenceLoadingIndicator.IsRunning = true;
        GeofenceLoadingLabel.IsVisible = true;
        GeofencePicker.IsEnabled = false;

        try
        {
            _allGeofences = await _geofenceService.GetGeofencesAsync();
            
            // Draw geofence circles on map if map is already initialized
            if (_isMapInitialized && LocationMapControl.Map != null)
                _mapService.AddGeofenceLayer(LocationMapControl.Map, _allGeofences);

            // 更新選擇器
            var geofenceNames = _allGeofences.Select(g => g.Name).ToList();
            geofenceNames.Insert(0, L("SelectCheckInLocation"));
            GeofencePicker.ItemsSource = geofenceNames;
            GeofencePicker.SelectedIndex = 0;
            
            UpdateNearbyGeofences();
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"載入地理圍欄失敗: {ex.Message}", "確定");
        }
        finally
        {
            GeofenceLoadingIndicator.IsRunning = false;
            GeofenceLoadingIndicator.IsVisible = false;
            GeofenceLoadingLabel.IsVisible = false;
            GeofencePicker.IsEnabled = true;
        }
    }

    private void UpdateNearbyGeofences()
    {
        if (_currentLocation == null) return;

        _nearbyGeofences.Clear();

        foreach (var geofence in _allGeofences)
        {
            var distance = _geofenceService.CalculateDistance(
                _currentLocation.Latitude, _currentLocation.Longitude,
                geofence.Latitude, geofence.Longitude);

            var geofenceWithDistance = new GeofenceWithDistance
            {
                Geofence = geofence,
                Distance = distance,
                Name = geofence.Name,
                Description = geofence.Description,
                DistanceText = distance < 1000 ? $"{distance:F0}m" : $"{distance / 1000:F1}km"
            };

            _nearbyGeofences.Add(geofenceWithDistance);
        }

        // 按距離排序
        var sortedGeofences = _nearbyGeofences.OrderBy(g => g.Distance).ToList();
        _nearbyGeofences.Clear();
        foreach (var geofence in sortedGeofences)
        {
            _nearbyGeofences.Add(geofence);
        }
    }

    private async Task LoadTodayRecords()
    {
        // 先備份舊資料，載入失敗時保留
        var backup = _todayRecords.ToList();
        _todayRecords.Clear();
        
        try
        {
            var today = DateTime.Today;
            var records = await _checkInStorageService.GetCheckInRecordsAsync(today);

            // 還原進行中的打卡狀態（修復：App 重啟後仍可下班打卡）
            var openRecord = records
                .Where(r => r.CheckOutTime == null)
                .OrderByDescending(r => r.CheckInTime)
                .FirstOrDefault();
            _currentCheckIn = openRecord;
            UpdateCheckInOutButtons();

            foreach (var record in records)
            {
                // 解析地址
                var address = "未知位置";
                try
                {
                    address = await _geocodingService.GetAddressFromCoordinatesAsync(
                        record.Latitude, record.Longitude);
                }
                catch
                {
                    address = $"位置 ({record.Latitude:F4}, {record.Longitude:F4})";
                }
                
                var displayRecord = new CheckInRecordDisplay
                {
                    GeofenceName = record.GeofenceName ?? address,
                    CheckInTimeText = record.CheckInTime.ToString("HH:mm"),
                    Notes = record.Notes ?? "",
                    DurationText = CalculateDuration(record.CheckInTime)
                };
                
                _todayRecords.Add(displayRecord);
            }
            
            // 如果沒有記錄，顯示提示訊息
            if (!_todayRecords.Any())
            {
                var noRecordMessage = new CheckInRecordDisplay
                {
                    GeofenceName = "今日尚無打卡記錄",
                    CheckInTimeText = "",
                    Notes = "請選擇地點進行打卡",
                    DurationText = ""
                };
                _todayRecords.Add(noRecordMessage);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"載入今日記錄失敗: {ex.Message}");
            // 載入失敗時恢復舊資料，不讓畫面空白
            if (backup.Count > 0)
            {
                foreach (var item in backup)
                    _todayRecords.Add(item);
            }
            else
            {
                _todayRecords.Add(new CheckInRecordDisplay
                {
                    GeofenceName = "載入記錄時發生錯誤",
                    CheckInTimeText = "",
                    Notes = ex.Message,
                    DurationText = ""
                });
            }
        }
    }
    
    private string CalculateDuration(DateTime checkInTime)
    {
        var duration = DateTime.Now - checkInTime;
        
        if (duration.TotalDays >= 1)
        {
            return $"{duration.Days}天前";
        }
        else if (duration.TotalHours >= 1)
        {
            return $"{duration.Hours}小時{duration.Minutes}分鐘前";
        }
        else
        {
            return $"{duration.Minutes}分鐘前";
        }
    }

    private void UpdateCheckInOutButtons()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            bool hasOpenCheckIn = _currentCheckIn != null && _currentCheckIn.CheckOutTime == null;
            CheckInButton.IsEnabled = !hasOpenCheckIn && GeofencePicker.SelectedIndex > 0;
            CheckOutButton.IsEnabled = hasOpenCheckIn;
        });
    }

    private void OnGeofenceSelected(object sender, EventArgs e)
    {
        bool hasOpenCheckIn = _currentCheckIn != null && _currentCheckIn.CheckOutTime == null;
        CheckInButton.IsEnabled = !hasOpenCheckIn && GeofencePicker.SelectedIndex > 0;
    }

    private async void OnCheckInClicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentUser == null)
            {
                await DisplayAlert("錯誤", "請先登入再進行打卡", "確定");
                return;
            }

            // 每次打卡前同步今日記錄，避免記憶體狀態與 DB 不一致
            await LoadTodayRecords();

            // 修復：阻止重複上班打卡（已有未結束的打卡記錄）
            if (_currentCheckIn != null && _currentCheckIn.CheckOutTime == null)
            {
                await DisplayAlert("錯誤",
                    $"您已於 {_currentCheckIn.CheckInTime:HH:mm} 在「{_currentCheckIn.GeofenceName}」打卡，請先打卡下班",
                    "確定");
                return;
            }

            if (GeofencePicker.SelectedIndex <= 0)
            {
                await DisplayAlert("錯誤", "請選擇打卡地點", "確定");
                return;
            }

            var selectedGeofence = _allGeofences[GeofencePicker.SelectedIndex - 1];

            // GPS 打卡點需要當前位置 + 圍欄判定；Wi-Fi 打卡點改用 SSID/BSSID 比對。
            CheckInMethod method;
            double recLat = 0, recLng = 0;

            if (selectedGeofence.IsWifiBased)
            {
                var wifi = await _wifiService.GetCurrentAsync();
                if (wifi == null)
                {
                    await DisplayAlert("錯誤", L("WifiNotConnected"), "確定");
                    return;
                }

                if (!_geofenceService.MatchesWifi(selectedGeofence, wifi.Ssid, wifi.Bssid))
                {
                    var msg = string.IsNullOrEmpty(selectedGeofence.Bssid)
                        ? $"目前連線到 {wifi.Ssid}，與打卡點「{selectedGeofence.Name}」(SSID: {selectedGeofence.Ssid}) 不符。確定要打卡嗎？"
                        : $"目前連線到 {wifi.Ssid} ({wifi.Bssid})，與打卡點「{selectedGeofence.Name}」綁定的熱點不符。確定要打卡嗎？";
                    var ok = await DisplayAlert("Wi-Fi 不符", msg, "確定", "取消");
                    if (!ok) return;
                }

                method = CheckInMethod.Wifi;
                if (_currentLocation != null)
                {
                    recLat = _currentLocation.Latitude;
                    recLng = _currentLocation.Longitude;
                }
            }
            else
            {
                if (_currentLocation == null)
                {
                    await DisplayAlert("錯誤", "無法獲取目前位置", "確定");
                    return;
                }

                // 位置超過 5 分鐘則強制重新取得
                if ((DateTime.UtcNow - _currentLocation.Timestamp).TotalMinutes > 5)
                {
                    var reload = await DisplayAlert("位置已過時",
                        "目前位置資料超過 5 分鐘，建議重新取得後再打卡。要繼續使用舊位置打卡嗎？",
                        "重新取得位置", "繼續打卡");
                    if (!reload)
                    {
                        await LoadCurrentLocation();
                        if (_currentLocation == null)
                        {
                            await DisplayAlert("錯誤", "重新取得位置失敗", "確定");
                            return;
                        }
                    }
                }

                var isInside = await _geofenceService.IsInsideGeofenceAsync(
                    _currentLocation.Latitude, _currentLocation.Longitude, selectedGeofence);

                if (!isInside)
                {
                    var distance = _geofenceService.CalculateDistance(
                        _currentLocation.Latitude, _currentLocation.Longitude,
                        selectedGeofence.Latitude, selectedGeofence.Longitude);

                    var result = await DisplayAlert("位置確認",
                        $"您距離 {selectedGeofence.Name} 還有 {distance:F0} 公尺，確定要打卡嗎？",
                        "確定", "取消");

                    if (!result) return;
                }

                method = CheckInMethod.GPS;
                recLat = _currentLocation.Latitude;
                recLng = _currentLocation.Longitude;
            }

            // 建立打卡記錄
            _currentCheckIn = new CheckInRecord
            {
                Id = Guid.NewGuid().ToString(),
                UserId = _currentUser.Id.ToString(),
                GeofenceId = selectedGeofence.Id,
                GeofenceName = selectedGeofence.Name,
                CheckInTime = DateTime.Now,
                Latitude = recLat,
                Longitude = recLng,
                Notes = (NotesEntry.Text ?? string.Empty).Trim().Length > 500
                    ? (NotesEntry.Text ?? string.Empty).Trim()[..500]
                    : (NotesEntry.Text ?? string.Empty).Trim(),
                Type = CheckInType.Manual,
                Method = method,
                WorkType = selectedGeofence.WorkType
            };

            // 保存打卡記錄：先嘗試資料庫，再保存到本地（離線備援）
            var dbSuccess = await _databaseService.SaveCheckInRecordAsync(_currentCheckIn);
            var localSuccess = !dbSuccess && await _checkInStorageService.SaveCheckInRecordAsync(_currentCheckIn);
            var saveSuccess = dbSuccess || localSuccess;
            
            if (saveSuccess)
            {
                string successMsg = dbSuccess
                    ? $"已在 {selectedGeofence.Name} 打卡\n時間: {DateTime.Now:HH:mm}"
                    : $"已在 {selectedGeofence.Name} 打卡（⚠️ 離線暫存）\n時間: {DateTime.Now:HH:mm}\n資料庫無法連線，記錄已暫存本機，請稍後同步。";
                await DisplayAlert(dbSuccess ? "打卡成功" : "打卡成功（離線模式）", successMsg, "確定");

                NotesEntry.Text = string.Empty;
                UpdateCheckInOutButtons();

                // 重新載入今日記錄（在 UI thread 執行）
                await LoadTodayRecords();
            }
            else
            {
                _currentCheckIn = null;
                await DisplayAlert("錯誤", "打卡記錄保存失敗，請確認資料庫連線設定後再試。", "確定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"打卡失敗: {ex.Message}", "確定");
        }
    }

    private async void OnCheckOutClicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentCheckIn == null)
            {
                await DisplayAlert("錯誤", "沒有進行中的打卡記錄", "確定");
                return;
            }

            _currentCheckIn.CheckOutTime = DateTime.Now;

            // 修復：將下班時間實際寫回資料庫與本地存儲
            var dbSuccess = await _databaseService.UpdateCheckInRecordAsync(_currentCheckIn);
            var localSuccess = await _checkInStorageService.SaveCheckInRecordAsync(_currentCheckIn);

            if (!dbSuccess && !localSuccess)
            {
                // 還原狀態，避免畫面與儲存不一致
                _currentCheckIn.CheckOutTime = null;
                await DisplayAlert("錯誤", "下班打卡記錄保存失敗", "確定");
                return;
            }

            var duration = _currentCheckIn.CheckOutTime!.Value - _currentCheckIn.CheckInTime;

            await DisplayAlert("打卡下班",
                $"工作時間: {duration.Hours}小時{duration.Minutes}分鐘",
                "確定");

            _currentCheckIn = null;
            UpdateCheckInOutButtons();

            // 重新載入今日記錄（在 UI thread 執行）
            await LoadTodayRecords();
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"打卡下班失敗: {ex.Message}", "確定");
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        RefreshButton.IsEnabled = false;
        RefreshButton.Text = L("Refreshing");
        
        try
        {
            await LoadCurrentLocation();
            await LoadGeofences();
            await LoadTodayRecords();
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            RefreshButton.Text = L("Refresh");
        }
    }
    
    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await DisplayAlert(L("ConfirmLogout"), L("LogoutConfirmMessage"), L("OK"), L("Cancel"));
            if (result)
            {
                await _userSessionService.LogoutAsync();
                await Shell.Current.GoToAsync("//LoginPage");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"登出失敗: {ex.Message}", "確定");
        }
    }

    private static string L(string key) => LocalizationService.Instance.GetLocalizedString(key);

    private void OnCultureChanged(CultureInfo _)
    {
        MainThread.BeginInvokeOnMainThread(ApplyLocalizedUi);
    }

    private void ApplyLocalizedUi()
    {
        if (RefreshButton.IsEnabled)
            RefreshButton.Text = L("Refresh");

        if (_currentUser == null)
            UpdateUserDisplay();

        if (_currentLocation == null && string.IsNullOrWhiteSpace(CurrentLocationLabel.Text))
            CurrentLocationLabel.Text = L("GettingLocation");
    }
    
    private async void OnGoToLoginClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//LoginPage");
    }

    private async void OnManageGeofencesClicked(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("GeofenceManagementPage");
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"無法開啟管理頁面: {ex.Message}", "確定");
        }
    }

    private async void OnManageRecordsClicked(object sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("EditCheckInPage"); }
        catch (Exception ex) { await DisplayAlert("錯誤", ex.Message, "確定"); }
    }

    private async void OnProfileClicked(object sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("ProfilePage"); }
        catch (Exception ex) { await DisplayAlert("錯誤", ex.Message, "確定"); }
    }

    private async void OnScheduleClicked(object sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("SchedulePage"); }
        catch (Exception ex) { await DisplayAlert("錯誤", ex.Message, "確定"); }
    }

    private async void OnLeaveClicked(object sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("LeavePage"); }
        catch (Exception ex) { await DisplayAlert("錯誤", ex.Message, "確定"); }
    }

    private async void OnReportClicked(object sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("ReportPage"); }
        catch (Exception ex) { await DisplayAlert("錯誤", ex.Message, "確定"); }
    }
}

// 輔助類別
public class GeofenceWithDistance
{
    public GeofenceRegion Geofence { get; set; } = new();
    public double Distance { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DistanceText { get; set; } = string.Empty;
}

public class CheckInRecordDisplay
{
    public string GeofenceName { get; set; } = string.Empty;
    public string CheckInTimeText { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;
}
