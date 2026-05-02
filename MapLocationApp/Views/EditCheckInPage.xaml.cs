using System.Collections.ObjectModel;
using System.Globalization;
using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp.Views;

public partial class EditCheckInPage : ContentPage
{
    private readonly IDatabaseService _databaseService;
    private readonly IUserSessionService _session;
    private readonly IGeofenceService _geofenceService;
    private readonly ObservableCollection<RecordDisplay> _items = new();
    private List<GeofenceRegion> _geofences = new();
    private string? _editingId;
    private DateTime? _filterDate;

    public EditCheckInPage(IDatabaseService databaseService, IUserSessionService session, IGeofenceService geofenceService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _session = session;
        _geofenceService = geofenceService;
        RecordsCollectionView.ItemsSource = _items;
        FilterDatePicker.Date = DateTime.Today;
        _filterDate = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Editing past records / back-fill is admin-only — protect against direct navigation.
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
        ResetForm();
        await LoadGeofencesAsync();
        await LoadRecordsAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
    }

    private void OnCultureChanged(CultureInfo _) => MainThread.BeginInvokeOnMainThread(() => { });

    private async Task LoadGeofencesAsync()
    {
        try
        {
            _geofences = (await _geofenceService.GetGeofencesAsync()).ToList();
            GeofencePicker.ItemsSource = new[] { L("NoGeofence") }
                .Concat(_geofences.Select(g => g.Name))
                .ToList();
            GeofencePicker.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"載入打卡點失敗: {ex.Message}");
        }
    }

    private async Task LoadRecordsAsync()
    {
        try
        {
            var user = await _session.GetCurrentUserAsync();
            if (user == null)
            {
                await DisplayAlert(L("Error"), L("NotLoggedIn"), L("OK"));
                return;
            }

            var records = await _databaseService.GetCheckInRecordsAsync(user.Id, _filterDate);

            _items.Clear();
            foreach (var r in records)
            {
                _items.Add(ToDisplay(r));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(L("Error"), ex.Message, L("OK"));
        }
    }

    private RecordDisplay ToDisplay(CheckInRecord r)
    {
        var loc = string.IsNullOrEmpty(r.GeofenceName) ? L("NoGeofence") : r.GeofenceName;
        var workType = r.WorkType == WorkType.Field ? L("Field") : L("Office");
        var method = r.Method switch
        {
            CheckInMethod.Wifi => L("MethodWifi"),
            CheckInMethod.Manual => L("MethodManual"),
            _ => L("MethodGPS"),
        };
        var checkOut = r.CheckOutTime.HasValue ? r.CheckOutTime.Value.ToLocalTime().ToString("HH:mm") : "—";
        var edited = r.EditedAt.HasValue ? string.Format(L("EditedAt"), r.EditedAt.Value.ToLocalTime().ToString("g")) : string.Empty;

        return new RecordDisplay
        {
            Id = r.Id,
            HeaderText = $"{r.CheckInTime.ToLocalTime():yyyy-MM-dd HH:mm} → {checkOut}  ·  {loc}",
            DetailText = string.IsNullOrEmpty(r.Notes) ? "—" : r.Notes,
            TagsText = $"{workType}  ·  {method}{(string.IsNullOrEmpty(edited) ? string.Empty : "  ·  " + edited)}",
            // Yellow accent for field, navy for office — visual classification at a glance.
            AccentColor = r.WorkType == WorkType.Field ? Color.FromArgb("#FACC15") : Color.FromArgb("#1E3A8A"),
        };
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
        {
            var user = await _session.GetCurrentUserAsync();
            if (user == null) return;
            var records = await _databaseService.GetCheckInRecordsAsync(user.Id);
            var r = records.FirstOrDefault(x => x.Id == id);
            if (r == null) return;

            _editingId = r.Id;
            FormTitleLabel.Text = L("EditCheckInTitle");
            SaveButton.Text = L("UpdateBtn");
            CancelEditButton.IsVisible = true;

            var idx = string.IsNullOrEmpty(r.GeofenceId)
                ? 0
                : Math.Max(0, _geofences.FindIndex(g => g.Id == r.GeofenceId) + 1);
            GeofencePicker.SelectedIndex = idx;
            WorkTypeOfficeRadio.IsChecked = r.WorkType == WorkType.Office;
            WorkTypeFieldRadio.IsChecked = r.WorkType == WorkType.Field;
            CheckInDatePicker.Date = r.CheckInTime.ToLocalTime().Date;
            CheckInTimePicker.Time = r.CheckInTime.ToLocalTime().TimeOfDay;
            if (r.CheckOutTime.HasValue)
            {
                CheckOutDatePicker.Date = r.CheckOutTime.Value.ToLocalTime().Date;
                CheckOutTimePicker.Time = r.CheckOutTime.Value.ToLocalTime().TimeOfDay;
            }
            else
            {
                CheckOutDatePicker.Date = DateTime.Today;
                CheckOutTimePicker.Time = TimeSpan.Zero;
            }
            NotesEditor.Text = r.Notes ?? string.Empty;
            EditReasonEntry.Text = r.EditReason ?? string.Empty;
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var user = await _session.GetCurrentUserAsync();
            if (user == null)
            {
                await DisplayAlert(L("Error"), L("NotLoggedIn"), L("OK"));
                return;
            }

            var checkInTime = CheckInDatePicker.Date.Add(CheckInTimePicker.Time);
            DateTime? checkOutTime = null;
            // Treat 00:00 of any date as "no checkout" — combined with the explicit clear button.
            if (CheckOutTimePicker.Time != TimeSpan.Zero || CheckOutDatePicker.Date != DateTime.Today)
            {
                var combined = CheckOutDatePicker.Date.Add(CheckOutTimePicker.Time);
                if (combined > checkInTime) checkOutTime = combined;
            }

            GeofenceRegion? selectedGeofence = null;
            if (GeofencePicker.SelectedIndex > 0)
            {
                selectedGeofence = _geofences[GeofencePicker.SelectedIndex - 1];
            }

            CheckInRecord record;
            if (_editingId != null)
            {
                var existing = (await _databaseService.GetCheckInRecordsAsync(user.Id))
                    .FirstOrDefault(x => x.Id == _editingId);
                if (existing == null)
                {
                    await DisplayAlert(L("Error"), L("CheckInError"), L("OK"));
                    return;
                }
                record = existing;
            }
            else
            {
                record = new CheckInRecord
                {
                    UserId = user.Id.ToString(),
                    Latitude = selectedGeofence?.Latitude ?? 0,
                    Longitude = selectedGeofence?.Longitude ?? 0,
                    Method = CheckInMethod.Manual,
                };
            }

            record.GeofenceId = selectedGeofence?.Id ?? string.Empty;
            record.GeofenceName = selectedGeofence?.Name ?? string.Empty;
            record.CheckInTime = checkInTime;
            record.CheckOutTime = checkOutTime;
            record.Notes = NotesEditor.Text ?? string.Empty;
            record.WorkType = WorkTypeFieldRadio.IsChecked ? WorkType.Field : WorkType.Office;
            record.Type = CheckInType.Manual;
            // Track edit history when user supplies a reason or this is an in-place edit.
            if (_editingId != null || !string.IsNullOrWhiteSpace(EditReasonEntry.Text))
            {
                record.EditedAt = DateTime.UtcNow;
                record.EditReason = EditReasonEntry.Text;
            }

            bool ok;
            if (_editingId != null)
            {
                ok = await _databaseService.UpdateCheckInRecordAsync(record);
            }
            else
            {
                ok = await _databaseService.SaveCheckInRecordAsync(record);
            }

            if (ok)
            {
                await DisplayAlert(L("Success"), _editingId != null ? L("UpdateBtn") : L("AddBtn"), L("OK"));
                ResetForm();
                await LoadRecordsAsync();
            }
            else
            {
                await DisplayAlert(L("Error"), L("CheckInError"), L("OK"));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(L("Error"), ex.Message, L("OK"));
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
        {
            var confirm = await DisplayAlert(L("DeleteRecord"), L("DeleteRecordConfirm"), L("Delete"), L("Cancel"));
            if (!confirm) return;
            var ok = await _databaseService.DeleteCheckInRecordAsync(id);
            if (ok)
            {
                if (_editingId == id) ResetForm();
                await LoadRecordsAsync();
            }
            else
            {
                await DisplayAlert(L("Error"), L("CheckInError"), L("OK"));
            }
        }
    }

    private void OnClearCheckOutClicked(object sender, EventArgs e)
    {
        CheckOutDatePicker.Date = DateTime.Today;
        CheckOutTimePicker.Time = TimeSpan.Zero;
    }

    private void OnCancelEditClicked(object sender, EventArgs e) => ResetForm();

    private async void OnFilterDateChanged(object? sender, DateChangedEventArgs e)
    {
        _filterDate = e.NewDate;
        await LoadRecordsAsync();
    }

    private async void OnAllDatesClicked(object sender, EventArgs e)
    {
        _filterDate = null;
        await LoadRecordsAsync();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadGeofencesAsync();
        await LoadRecordsAsync();
    }

    private void ResetForm()
    {
        _editingId = null;
        FormTitleLabel.Text = L("ManualCheckInTitle");
        SaveButton.Text = L("AddBtn");
        CancelEditButton.IsVisible = false;
        if (GeofencePicker.ItemsSource != null) GeofencePicker.SelectedIndex = 0;
        WorkTypeOfficeRadio.IsChecked = true;
        WorkTypeFieldRadio.IsChecked = false;
        var now = DateTime.Now;
        CheckInDatePicker.Date = now.Date;
        CheckInTimePicker.Time = new TimeSpan(now.Hour, now.Minute, 0);
        CheckOutDatePicker.Date = DateTime.Today;
        CheckOutTimePicker.Time = TimeSpan.Zero;
        NotesEditor.Text = string.Empty;
        EditReasonEntry.Text = string.Empty;
    }

    private static string L(string key) => LocalizationService.Instance.GetLocalizedString(key);

    private class RecordDisplay
    {
        public string Id { get; set; } = string.Empty;
        public string HeaderText { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
        public string TagsText { get; set; } = string.Empty;
        public Color AccentColor { get; set; } = Colors.Transparent;
    }
}
