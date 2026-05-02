using MapLocationApp.Models;
using MapLocationApp.Services;
using MapLocationApp.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MapLocationApp.Views;

public partial class GeofenceManagementPage : ContentPage
{
    private readonly IGeofenceService _geofenceService;
    private readonly ILocationService _locationService;
    private readonly ICheckInStorageService _checkInStorage;
    private readonly IWifiService _wifiService;
    private readonly ObservableCollection<GeofenceDisplay> _items = new();
    private string? _editingId;

    public GeofenceManagementPage(IGeofenceService geofenceService, ILocationService locationService, ICheckInStorageService checkInStorage, IWifiService wifiService)
    {
        InitializeComponent();
        _geofenceService = geofenceService;
        _locationService = locationService;
        _checkInStorage = checkInStorage;
        _wifiService = wifiService;
        GeofencesCollectionView.ItemsSource = _items;
        ScanWifiButton.IsEnabled = _wifiService.ScanSupported;
        ApplyLocalizedUi();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch
        {
            await Navigation.PopAsync();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Admin guard — block direct navigation by non-admin users (e.g. via route URL).
        var role = ServiceHelper.GetService<IRoleService>();
        if (role?.IsCurrentUserAdmin() != true)
        {
            await DisplayAlert(L("Error"), L("AdminRequired"), L("OK"));
            try { await Shell.Current.GoToAsync(".."); }
            catch { await Navigation.PopAsync(); }
            return;
        }

        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
        LocalizationService.Instance.CultureChanged += OnCultureChanged;
        ApplyLocalizedUi();
        await LoadGeofencesAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
    }

    private async Task LoadGeofencesAsync()
    {
        try
        {
            var all = _geofenceService.GetMonitoredGeofences();
            // GetMonitoredGeofences 是同步的，但服務內部需先載入；先呼叫一次 Get 觸發載入
            if (all.Count == 0)
            {
                await _geofenceService.GetGeofencesAsync();
                all = _geofenceService.GetMonitoredGeofences();
            }

            _items.Clear();
            foreach (var g in all.OrderBy(x => x.Name))
            {
                _items.Add(ToDisplay(g));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"載入地點失敗: {ex.Message}", "確定");
        }
    }

    private GeofenceDisplay ToDisplay(GeofenceRegion g)
    {
        var workType = g.WorkType == WorkType.Field ? L("Field") : L("Office");
        var detail = g.IsWifiBased
            ? $"📶 {g.Ssid}{(string.IsNullOrEmpty(g.Bssid) ? string.Empty : $"  •  {g.Bssid}")}"
            : $"📍 {g.Latitude:F6}, {g.Longitude:F6}  •  {L("Radius")} {g.RadiusMeters:F0}m";
        return new GeofenceDisplay
        {
            Id = g.Id,
            Name = g.Name,
            DetailText = detail,
            StatusText = $"{workType}  •  {L("Category")}: {(string.IsNullOrEmpty(g.Category) ? L("Uncategorized") : g.Category)}  •  {(g.IsActive ? $"✅ {L("Enabled")}" : $"⛔ {L("Disabled")}")}",
        };
    }

    private async void OnUseCurrentLocationClicked(object sender, EventArgs e)
    {
        try
        {
            var hasPerm = await _locationService.RequestLocationPermissionAsync();
            if (!hasPerm)
            {
                await DisplayAlert("錯誤", "未授予定位權限", "確定");
                return;
            }
            var loc = await _locationService.GetCurrentLocationAsync();
            if (loc == null)
            {
                await DisplayAlert("錯誤", "無法取得目前位置", "確定");
                return;
            }
            LatitudeEntry.Text = loc.Latitude.ToString("F6", CultureInfo.InvariantCulture);
            LongitudeEntry.Text = loc.Longitude.ToString("F6", CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"取得位置失敗: {ex.Message}", "確定");
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NameEntry.Text))
            {
                await DisplayAlert("錯誤", "請輸入名稱", "確定");
                return;
            }

            // 檢查名稱是否重複（編輯時排除自身）
            var existingNames = _geofenceService.GetMonitoredGeofences()
                .Where(g => g.Id != _editingId)
                .Select(g => g.Name.Trim().ToLower());
            if (existingNames.Contains(NameEntry.Text.Trim().ToLower()))
            {
                await DisplayAlert("錯誤", $"打卡地點「{NameEntry.Text.Trim()}」名稱已存在，請使用不同名稱。", "確定");
                return;
            }
            var isWifi = WifiTypeRadio.IsChecked;
            double lat = 0, lng = 0, radius = 0;
            string? ssid = null, bssid = null;

            if (isWifi)
            {
                ssid = SsidEntry.Text?.Trim();
                if (string.IsNullOrEmpty(ssid))
                {
                    await DisplayAlert("錯誤", L("WifiSsid"), "確定");
                    return;
                }
                bssid = string.IsNullOrWhiteSpace(BssidEntry.Text) ? null : BssidEntry.Text.Trim();
            }
            else
            {
                if (!TryParseDouble(LatitudeEntry.Text, out lat) || lat < -90 || lat > 90)
                {
                    await DisplayAlert("錯誤", "緯度格式錯誤（-90 ~ 90）", "確定");
                    return;
                }
                if (!TryParseDouble(LongitudeEntry.Text, out lng) || lng < -180 || lng > 180)
                {
                    await DisplayAlert("錯誤", "經度格式錯誤（-180 ~ 180）", "確定");
                    return;
                }
                if (!TryParseDouble(RadiusEntry.Text, out radius) || radius <= 0)
                {
                    await DisplayAlert("錯誤", "半徑必須為大於 0 的數字（公尺）", "確定");
                    return;
                }
            }

            var region = new GeofenceRegion
            {
                Id = _editingId ?? Guid.NewGuid().ToString(),
                Name = NameEntry.Text.Trim(),
                Category = CategoryEntry.Text?.Trim() ?? string.Empty,
                Description = DescriptionEntry.Text?.Trim() ?? string.Empty,
                Latitude = lat,
                Longitude = lng,
                RadiusMeters = radius,
                IsActive = IsActiveCheckBox.IsChecked,
                TransitionType = GeofenceTransitionType.Both,
                WorkType = WorkTypeFieldRadio.IsChecked ? WorkType.Field : WorkType.Office,
                Ssid = ssid,
                Bssid = bssid,
            };

            bool ok;
            if (_editingId != null)
            {
                ok = await _geofenceService.UpdateGeofenceAsync(_editingId, region);
            }
            else
            {
                ok = await _geofenceService.AddGeofenceAsync(region);
            }

            if (ok)
            {
                await DisplayAlert("完成", _editingId != null ? "已更新地點" : "已新增地點", "確定");
                ResetForm();
                await LoadGeofencesAsync();
            }
            else
            {
                await DisplayAlert("錯誤", "儲存失敗：無法寫入資料庫。\n請至「設定」頁面確認資料庫連線是否正常。", "確定");
            }
        }
        catch (ArgumentException aex)
        {
            await DisplayAlert("錯誤", aex.Message, "確定");
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"儲存失敗: {ex.Message}", "確定");
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
        {
            var g = _geofenceService.GetMonitoredGeofences().FirstOrDefault(x => x.Id == id);
            if (g == null) return;

            _editingId = g.Id;
            NameEntry.Text = g.Name;
            CategoryEntry.Text = g.Category;
            DescriptionEntry.Text = g.Description;
            LatitudeEntry.Text = g.Latitude.ToString("F6", CultureInfo.InvariantCulture);
            LongitudeEntry.Text = g.Longitude.ToString("F6", CultureInfo.InvariantCulture);
            RadiusEntry.Text = g.RadiusMeters.ToString("F0", CultureInfo.InvariantCulture);
            IsActiveCheckBox.IsChecked = g.IsActive;
            WorkTypeOfficeRadio.IsChecked = g.WorkType == WorkType.Office;
            WorkTypeFieldRadio.IsChecked = g.WorkType == WorkType.Field;

            // Wi-Fi 欄位
            SsidEntry.Text = g.Ssid ?? string.Empty;
            BssidEntry.Text = g.Bssid ?? string.Empty;
            if (g.IsWifiBased)
            {
                WifiTypeRadio.IsChecked = true;
                GpsTypeRadio.IsChecked = false;
                GpsFieldsPanel.IsVisible = false;
                WifiFieldsPanel.IsVisible = true;
            }
            else
            {
                GpsTypeRadio.IsChecked = true;
                WifiTypeRadio.IsChecked = false;
                GpsFieldsPanel.IsVisible = true;
                WifiFieldsPanel.IsVisible = false;
            }

            FormTitleLabel.Text = $"{L("Edit")}: {g.Name}";
            SaveButton.Text = L("UpdateBtn");
            CancelEditButton.IsVisible = true;
            await Task.CompletedTask;
        }
    }

    private async void OnToggleActiveClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
        {
            var ok = await _geofenceService.ToggleGeofenceActiveAsync(id);
            if (ok) await LoadGeofencesAsync();
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
        {
            var g = _geofenceService.GetMonitoredGeofences().FirstOrDefault(x => x.Id == id);
            if (g == null) return;

            // 檢查是否有進行中的打卡
            try
            {
                var todayRecords = await _checkInStorage.GetCheckInRecordsAsync(DateTime.Today);
                var hasActiveCheckIn = todayRecords.Any(r => r.GeofenceId == id && r.CheckOutTime == null);
                if (hasActiveCheckIn)
                {
                    await DisplayAlert("無法刪除", $"「{g.Name}」目前有進行中的打卡記錄，請先完成打卡再刪除此地點。", "確定");
                    return;
                }
            }
            catch { /* 查詢失敗時繼續，由使用者決定 */ }

            var confirm = await DisplayAlert("刪除確認", $"確定要刪除「{g.Name}」？", "刪除", "取消");
            if (!confirm) return;

            var ok = await _geofenceService.RemoveGeofenceAsync(id);
            if (ok)
            {
                if (_editingId == id) ResetForm();
                await LoadGeofencesAsync();
            }
            else
            {
                await DisplayAlert("錯誤", "刪除失敗", "確定");
            }
        }
    }

    private void OnLocationTypeChanged(object? sender, CheckedChangedEventArgs e)
    {
        // CheckedChanged fires for both radios in the group; only react to the activating one.
        if (sender is RadioButton rb && rb.IsChecked)
        {
            var isWifi = ReferenceEquals(rb, WifiTypeRadio);
            GpsFieldsPanel.IsVisible = !isWifi;
            WifiFieldsPanel.IsVisible = isWifi;
        }
    }

    private async void OnUseCurrentWifiClicked(object sender, EventArgs e)
    {
        try
        {
            var hasPerm = await _locationService.RequestLocationPermissionAsync();
            if (!hasPerm)
            {
                await DisplayAlert(L("Error"), L("WifiPermissionRequired"), L("OK"));
                return;
            }

            var current = await _wifiService.GetCurrentAsync();
            if (current == null)
            {
                await DisplayAlert(L("Error"), L("WifiNotConnected"), L("OK"));
                return;
            }

            SsidEntry.Text = current.Ssid;
            BssidEntry.Text = current.Bssid ?? string.Empty;
        }
        catch (Exception ex)
        {
            await DisplayAlert(L("Error"), ex.Message, L("OK"));
        }
    }

    private async void OnScanWifiClicked(object sender, EventArgs e)
    {
        try
        {
            if (!_wifiService.ScanSupported)
            {
                await DisplayAlert(L("Error"), L("WifiScanNotSupported"), L("OK"));
                return;
            }

            var hasPerm = await _locationService.RequestLocationPermissionAsync();
            if (!hasPerm)
            {
                await DisplayAlert(L("Error"), L("WifiPermissionRequired"), L("OK"));
                return;
            }

            var scan = await _wifiService.ScanAsync();
            if (scan == null || scan.Count == 0)
            {
                await DisplayAlert(L("Error"), L("WifiNotConnected"), L("OK"));
                return;
            }

            // Strongest signal first; ScanResult.Level / Rssi is dBm (more negative = weaker).
            var sorted = scan.OrderByDescending(w => w.SignalDbm ?? int.MinValue).ToList();
            var labels = sorted.Select(w => string.IsNullOrEmpty(w.Bssid) ? w.Ssid : $"{w.Ssid}  ({w.Bssid})").ToArray();
            var picked = await DisplayActionSheet(L("SelectWifi"), L("Cancel"), null, labels);
            if (string.IsNullOrEmpty(picked) || picked == L("Cancel")) return;

            var idx = Array.IndexOf(labels, picked);
            if (idx < 0) return;
            var w = sorted[idx];
            SsidEntry.Text = w.Ssid;
            BssidEntry.Text = w.Bssid ?? string.Empty;
        }
        catch (Exception ex)
        {
            await DisplayAlert(L("Error"), ex.Message, L("OK"));
        }
    }

    private void OnClearFormClicked(object sender, EventArgs e) => ResetForm();

    private void OnCancelEditClicked(object sender, EventArgs e) => ResetForm();

    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadGeofencesAsync();

    private void ResetForm()
    {
        _editingId = null;
        NameEntry.Text = string.Empty;
        CategoryEntry.Text = string.Empty;
        DescriptionEntry.Text = string.Empty;
        LatitudeEntry.Text = string.Empty;
        LongitudeEntry.Text = string.Empty;
        RadiusEntry.Text = "100";
        SsidEntry.Text = string.Empty;
        BssidEntry.Text = string.Empty;
        IsActiveCheckBox.IsChecked = true;
        WorkTypeOfficeRadio.IsChecked = true;
        WorkTypeFieldRadio.IsChecked = false;
        GpsTypeRadio.IsChecked = true;
        WifiTypeRadio.IsChecked = false;
        GpsFieldsPanel.IsVisible = true;
        WifiFieldsPanel.IsVisible = false;
        ApplyLocalizedUi();
        CancelEditButton.IsVisible = false;
    }

    private static string L(string key) => LocalizationService.Instance.GetLocalizedString(key);

    private void OnCultureChanged(CultureInfo _)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            ApplyLocalizedUi();
            await LoadGeofencesAsync();
        });
    }

    private void ApplyLocalizedUi()
    {
        if (_editingId == null)
        {
            FormTitleLabel.Text = L("AddLocation");
            SaveButton.Text = L("AddBtn");
        }
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    public class GeofenceDisplay
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
    }
}
