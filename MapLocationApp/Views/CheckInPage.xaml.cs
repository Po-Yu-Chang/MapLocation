using MapLocationApp.Models;
using MapLocationApp.Services;
using System.Collections.ObjectModel;

namespace MapLocationApp.Views;

public partial class CheckInPage : ContentPage
{
    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private readonly IGeocodingService _geocodingService;
    private readonly ICheckInStorageService _checkInStorageService;
    
    private AppLocation? _currentLocation;
    private List<GeofenceRegion> _allGeofences = new();
    private readonly ObservableCollection<GeofenceWithDistance> _nearbyGeofences = new();
    private readonly ObservableCollection<CheckInRecordDisplay> _todayRecords = new();
    private CheckInRecord? _currentCheckIn;

    private readonly IDatabaseService _databaseService;
    private readonly IConfigService _configService;
    private readonly IUserSessionService _userSessionService;
    private User? _currentUser;

    public CheckInPage(ILocationService locationService, IGeofenceService geofenceService, IGeocodingService geocodingService, ICheckInStorageService checkInStorageService, IDatabaseService databaseService, IConfigService configService)
    {
        InitializeComponent();
        
        _locationService = locationService;
        _geofenceService = geofenceService;
        _geocodingService = geocodingService;
        _checkInStorageService = checkInStorageService;
        _databaseService = databaseService;
        _configService = configService;
        _userSessionService = ServiceHelper.GetService<IUserSessionService>();
        
        NearbyGeofencesCollectionView.ItemsSource = _nearbyGeofences;
        TodayRecordsCollectionView.ItemsSource = _todayRecords;
        
        // 訂閱用戶會話事件
        _userSessionService.UserLoggedIn += OnUserLoggedIn;
        _userSessionService.UserLoggedOut += OnUserLoggedOut;
        
        InitializeAsync();
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
                UserNameLabel.Text = "未登入";
                UserDepartmentLabel.Text = "請先登入以使用打卡功能";
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
            CurrentLocationLabel.Text = "正在獲取位置...";
            
            var hasPermission = await _locationService.RequestLocationPermissionAsync();
            if (!hasPermission)
            {
                CurrentLocationLabel.Text = "位置權限未授予";
                return;
            }

            _currentLocation = await _locationService.GetCurrentLocationAsync();
            
            if (_currentLocation != null)
            {
                // 顯示座標
                var coordinateText = $"{_currentLocation.Latitude:F6}, {_currentLocation.Longitude:F6}";
                AccuracyLabel.Text = $"精確度: ±{_currentLocation.Accuracy:F0} 公尺";
                
                // 開始地址解析
                CurrentLocationLabel.Text = "正在解析地址...";
                
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
                CurrentLocationLabel.Text = "無法獲取位置";
                AccuracyLabel.Text = "請檢查GPS設定";
            }
        }
        catch (Exception ex)
        {
            CurrentLocationLabel.Text = "位置獲取失敗";
            AccuracyLabel.Text = ex.Message;
        }
    }

    private async Task LoadGeofences()
    {
        try
        {
            _allGeofences = await _geofenceService.GetGeofencesAsync();
            
            // 更新選擇器
            var geofenceNames = _allGeofences.Select(g => g.Name).ToList();
            geofenceNames.Insert(0, "選擇打卡地點");
            GeofencePicker.ItemsSource = geofenceNames;
            GeofencePicker.SelectedIndex = 0;
            
            UpdateNearbyGeofences();
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"載入地理圍欄失敗: {ex.Message}", "確定");
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
        // 清空現有記錄
        _todayRecords.Clear();
        
        try
        {
            var today = DateTime.Today;
            var records = await _checkInStorageService.GetCheckInRecordsAsync(today);
            
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
            
            var errorMessage = new CheckInRecordDisplay
            {
                GeofenceName = "載入記錄時發生錯誤",
                CheckInTimeText = "",
                Notes = ex.Message,
                DurationText = ""
            };
            _todayRecords.Add(errorMessage);
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

    private void OnGeofenceSelected(object sender, EventArgs e)
    {
        if (GeofencePicker.SelectedIndex > 0)
        {
            CheckInButton.IsEnabled = true;
        }
        else
        {
            CheckInButton.IsEnabled = false;
        }
    }

    private async void OnCheckInClicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentLocation == null)
            {
                await DisplayAlert("錯誤", "無法獲取目前位置", "確定");
                return;
            }

            if (GeofencePicker.SelectedIndex <= 0)
            {
                await DisplayAlert("錯誤", "請選擇打卡地點", "確定");
                return;
            }

            var selectedGeofence = _allGeofences[GeofencePicker.SelectedIndex - 1];
            
            // 檢查是否在地理圍欄範圍內
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

            // 建立打卡記錄
            _currentCheckIn = new CheckInRecord
            {
                Id = Guid.NewGuid().ToString(),
                UserId = _currentUser?.Id.ToString() ?? "0",
                GeofenceId = selectedGeofence.Id,
                GeofenceName = selectedGeofence.Name,
                CheckInTime = DateTime.Now,
                Latitude = _currentLocation.Latitude,
                Longitude = _currentLocation.Longitude,
                Notes = NotesEntry.Text ?? string.Empty,
                Type = CheckInType.Manual
            };

            // 保存打卡記錄到資料庫和本地存儲
            var saveSuccess = false;
            
            if (_currentUser != null)
            {
                saveSuccess = await _databaseService.SaveCheckInRecordAsync(_currentCheckIn);
            }
            
            // 如果資料庫保存失敗，仍嘗試保存到本地
            if (!saveSuccess)
            {
                saveSuccess = await _checkInStorageService.SaveCheckInRecordAsync(_currentCheckIn);
            }
            
            if (saveSuccess)
            {
                await DisplayAlert("打卡成功", 
                    $"已在 {selectedGeofence.Name} 打卡\n時間: {DateTime.Now:HH:mm}", 
                    "確定");
                    
                CheckInButton.IsEnabled = false;
                CheckOutButton.IsEnabled = true;
                
                // 重新載入今日記錄
                _ = Task.Run(async () => await LoadTodayRecords());
            }
            else
            {
                await DisplayAlert("錯誤", "打卡記錄保存失敗", "確定");
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
            
            var duration = _currentCheckIn.CheckOutTime.Value - _currentCheckIn.CheckInTime;
            
            await DisplayAlert("打卡下班", 
                $"工作時間: {duration.Hours}小時{duration.Minutes}分鐘", 
                "確定");

            CheckInButton.IsEnabled = true;
            CheckOutButton.IsEnabled = false;
            
            _currentCheckIn = null;
            
            // 重新載入今日記錄
            _ = Task.Run(async () => await LoadTodayRecords());
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"打卡下班失敗: {ex.Message}", "確定");
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        RefreshButton.IsEnabled = false;
        RefreshButton.Text = "重新整理中...";
        
        try
        {
            await LoadCurrentLocation();
            await LoadGeofences();
            await LoadTodayRecords();
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            RefreshButton.Text = "重新整理";
        }
    }
    
    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await DisplayAlert("確認登出", "您確定要登出嗎？", "確定", "取消");
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
    
    private async void OnGoToLoginClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//LoginPage");
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