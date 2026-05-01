using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp.Views;

public partial class SchedulePage : ContentPage
{
    private readonly IDatabaseService _databaseService;
    private readonly IUserSessionService _session;
    private readonly ObservableCollection<DayItem> _days = new();

    public SchedulePage(IDatabaseService databaseService, IUserSessionService session)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _session = session;
        DaysCollectionView.ItemsSource = _days;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
        LocalizationService.Instance.CultureChanged += OnCultureChanged;
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
    }

    private void OnCultureChanged(CultureInfo _) => MainThread.BeginInvokeOnMainThread(RebuildDayLabels);

    private void RebuildDayLabels()
    {
        foreach (var d in _days) d.RefreshLabel();
    }

    private async Task LoadAsync()
    {
        var user = await _session.GetCurrentUserAsync();
        if (user == null)
        {
            await DisplayAlert(L("Error"), L("NotLoggedIn"), L("OK"));
            return;
        }

        var existing = await _databaseService.GetWorkScheduleAsync(user.Id);
        var byDay = existing.ToDictionary(s => s.WeekDay);

        _days.Clear();
        // Iterate Mon..Sun for display order; persist actual DayOfWeek (0..6, Sun=0).
        int[] order = { 1, 2, 3, 4, 5, 6, 0 };
        foreach (var w in order)
        {
            byDay.TryGetValue(w, out var s);
            _days.Add(new DayItem
            {
                WeekDay = w,
                IsRestDay = s?.IsRestDay ?? (w == 0 || w == 6),
                Start = s?.Start ?? user.WorkHoursStart ?? TimeSpan.FromHours(9),
                End = s?.End ?? user.WorkHoursEnd ?? TimeSpan.FromHours(18),
            });
        }
    }

    private void OnRestDayToggled(object? sender, CheckedChangedEventArgs e)
    {
        // Bound IsRestDay already updates via two-way binding; recompute IsWorkDay-driven enable state.
        if (sender is CheckBox cb && cb.BindingContext is DayItem d)
        {
            d.OnRestDayChanged();
        }
    }

    private void OnApplyDefaultClicked(object sender, EventArgs e)
    {
        var user = _session.CurrentUser;
        var start = user?.WorkHoursStart ?? TimeSpan.FromHours(9);
        var end = user?.WorkHoursEnd ?? TimeSpan.FromHours(18);
        foreach (var d in _days)
        {
            if (!d.IsRestDay)
            {
                d.Start = start;
                d.End = end;
            }
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var user = await _session.GetCurrentUserAsync();
        if (user == null) return;

        var schedules = _days.Select(d => new WorkSchedule
        {
            WeekDay = d.WeekDay,
            IsRestDay = d.IsRestDay,
            Start = d.IsRestDay ? null : d.Start,
            End = d.IsRestDay ? null : d.End,
        });

        var ok = await _databaseService.SaveWorkScheduleAsync(user.Id, schedules);
        await DisplayAlert(ok ? L("Success") : L("Error"),
            ok ? L("ProfileUpdated") : L("CheckInError"), L("OK"));
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync(".."); }
        catch { await Navigation.PopAsync(); }
    }

    private static string L(string key) => LocalizationService.Instance.GetLocalizedString(key);

    public class DayItem : INotifyPropertyChanged
    {
        public int WeekDay { get; set; }
        public string DayLabel => LocalizationService.Instance.GetLocalizedString($"WeekDay{WeekDay}");

        private bool _isRestDay;
        public bool IsRestDay
        {
            get => _isRestDay;
            set { _isRestDay = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsWorkDay)); }
        }

        public bool IsWorkDay => !IsRestDay;

        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }

        public void RefreshLabel() => OnPropertyChanged(nameof(DayLabel));
        public void OnRestDayChanged() => OnPropertyChanged(nameof(IsWorkDay));

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
