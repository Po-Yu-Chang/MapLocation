using MapLocationApp.Models;
using MapLocationApp.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MapLocationApp.Views;

public partial class GeofenceManagementPage : ContentPage
{
    private readonly IGeofenceService _geofenceService;
    private readonly ILocationService _locationService;
    private readonly ICheckInStorageService _checkInStorage;
    private readonly ObservableCollection<GeofenceDisplay> _items = new();
    private string? _editingId;

    public GeofenceManagementPage(IGeofenceService geofenceService, ILocationService locationService, ICheckInStorageService checkInStorage)
    {
        InitializeComponent();
        _geofenceService = geofenceService;
        _locationService = locationService;
        _checkInStorage = checkInStorage;
        GeofencesCollectionView.ItemsSource = _items;
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

    private GeofenceDisplay ToDisplay(GeofenceRegion g) => new()
    {
        Id = g.Id,
        Name = g.Name,
        DetailText = $"📍 {g.Latitude:F6}, {g.Longitude:F6}  •  {L("Radius")} {g.RadiusMeters:F0}m",
        StatusText = $"{L("Category")}: {(string.IsNullOrEmpty(g.Category) ? L("Uncategorized") : g.Category)}  •  {(g.IsActive ? $"✅ {L("Enabled")}" : $"⛔ {L("Disabled")}")}",
    };

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
            if (!TryParseDouble(LatitudeEntry.Text, out var lat) || lat < -90 || lat > 90)
            {
                await DisplayAlert("錯誤", "緯度格式錯誤（-90 ~ 90）", "確定");
                return;
            }
            if (!TryParseDouble(LongitudeEntry.Text, out var lng) || lng < -180 || lng > 180)
            {
                await DisplayAlert("錯誤", "經度格式錯誤（-180 ~ 180）", "確定");
                return;
            }
            if (!TryParseDouble(RadiusEntry.Text, out var radius) || radius <= 0)
            {
                await DisplayAlert("錯誤", "半徑必須為大於 0 的數字（公尺）", "確定");
                return;
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

            FormTitleLabel.Text = $"{L("Edit")}: {g.Name}";
            SaveButton.Text = $"💾 {L("UpdateBtn")}";
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
        RadiusEntry.Text = string.Empty;
        IsActiveCheckBox.IsChecked = true;
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
            SaveButton.Text = $"💾 {L("AddBtn")}";
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
