using System.Collections.ObjectModel;
using System.Globalization;
using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp.Views;

public partial class LeavePage : ContentPage
{
    private readonly IDatabaseService _databaseService;
    private readonly IUserSessionService _session;
    private readonly ObservableCollection<LeaveDisplay> _items = new();
    private string? _editingId;
    private static readonly LeaveType[] AllTypes =
    {
        LeaveType.Annual, LeaveType.Personal, LeaveType.Sick,
        LeaveType.Family, LeaveType.Official, LeaveType.Other
    };

    public LeavePage(IDatabaseService databaseService, IUserSessionService session)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _session = session;
        LeavesCollectionView.ItemsSource = _items;
        RebuildTypePicker();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
        LocalizationService.Instance.CultureChanged += OnCultureChanged;
        ResetForm();
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
    }

    private void OnCultureChanged(CultureInfo _) => MainThread.BeginInvokeOnMainThread(() =>
    {
        RebuildTypePicker();
        RebuildItemTexts();
    });

    private void RebuildTypePicker()
    {
        var keep = TypePicker.SelectedIndex;
        TypePicker.ItemsSource = AllTypes.Select(LocalizeType).ToList();
        TypePicker.SelectedIndex = keep < 0 ? 1 : keep; // default Personal
    }

    private void RebuildItemTexts()
    {
        // Cheap: re-pull from DB to refresh localized text on culture change.
        _ = LoadAsync();
    }

    private static string LocalizeType(LeaveType t) => t switch
    {
        LeaveType.Annual => L("LeaveAnnual"),
        LeaveType.Personal => L("LeavePersonal"),
        LeaveType.Sick => L("LeaveSick"),
        LeaveType.Family => L("LeaveFamily"),
        LeaveType.Official => L("LeaveOfficial"),
        _ => L("LeaveOther"),
    };

    private async Task LoadAsync()
    {
        var user = await _session.GetCurrentUserAsync();
        if (user == null)
        {
            await DisplayAlert(L("Error"), L("NotLoggedIn"), L("OK"));
            return;
        }

        var records = await _databaseService.GetLeaveRecordsAsync(user.Id);
        _items.Clear();
        foreach (var r in records)
        {
            _items.Add(new LeaveDisplay
            {
                Id = r.Id,
                HeaderText = $"{LocalizeType(r.Type)}  ·  {r.StartDate:yyyy-MM-dd} ~ {r.EndDate:yyyy-MM-dd}  ({r.Days} {L("LeaveDays")})",
                DetailText = string.IsNullOrEmpty(r.Note) ? "—" : r.Note,
            });
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var user = await _session.GetCurrentUserAsync();
            if (user == null) return;

            if (EndDatePicker.Date < StartDatePicker.Date)
            {
                await DisplayAlert(L("Error"), L("EndDateBeforeStart"), L("OK"));
                return;
            }

            var type = AllTypes[Math.Max(0, TypePicker.SelectedIndex)];
            LeaveRecord record;
            bool ok;
            if (_editingId != null)
            {
                var existing = (await _databaseService.GetLeaveRecordsAsync(user.Id))
                    .FirstOrDefault(x => x.Id == _editingId);
                if (existing == null) return;
                record = existing;
                record.Type = type;
                record.StartDate = StartDatePicker.Date;
                record.EndDate = EndDatePicker.Date;
                record.Note = NoteEditor.Text;
                ok = await _databaseService.UpdateLeaveRecordAsync(record);
            }
            else
            {
                record = new LeaveRecord
                {
                    UserId = user.Id.ToString(),
                    Type = type,
                    StartDate = StartDatePicker.Date,
                    EndDate = EndDatePicker.Date,
                    Note = NoteEditor.Text,
                };
                ok = await _databaseService.SaveLeaveRecordAsync(record);
            }

            if (ok)
            {
                ResetForm();
                await LoadAsync();
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

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
        {
            var user = await _session.GetCurrentUserAsync();
            if (user == null) return;
            var record = (await _databaseService.GetLeaveRecordsAsync(user.Id)).FirstOrDefault(x => x.Id == id);
            if (record == null) return;

            _editingId = id;
            FormTitleLabel.Text = L("Edit");
            SaveButton.Text = L("UpdateBtn");
            CancelEditButton.IsVisible = true;
            TypePicker.SelectedIndex = Array.IndexOf(AllTypes, record.Type);
            StartDatePicker.Date = record.StartDate;
            EndDatePicker.Date = record.EndDate;
            NoteEditor.Text = record.Note ?? string.Empty;
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
        {
            var confirm = await DisplayAlert(L("Delete"), L("DeleteRecordConfirm"), L("Delete"), L("Cancel"));
            if (!confirm) return;
            var ok = await _databaseService.DeleteLeaveRecordAsync(id);
            if (ok)
            {
                if (_editingId == id) ResetForm();
                await LoadAsync();
            }
        }
    }

    private void OnCancelClicked(object sender, EventArgs e) => ResetForm();

    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadAsync();

    private void ResetForm()
    {
        _editingId = null;
        FormTitleLabel.Text = L("AddBtn");
        SaveButton.Text = L("AddBtn");
        CancelEditButton.IsVisible = false;
        TypePicker.SelectedIndex = 1;
        StartDatePicker.Date = DateTime.Today;
        EndDatePicker.Date = DateTime.Today;
        NoteEditor.Text = string.Empty;
    }

    private static string L(string key) => LocalizationService.Instance.GetLocalizedString(key);

    private class LeaveDisplay
    {
        public string Id { get; set; } = string.Empty;
        public string HeaderText { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
    }
}
