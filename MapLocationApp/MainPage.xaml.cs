using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateDashboard();
    }

    private async void UpdateDashboard()
    {
        try
        {
            var hour = DateTime.Now.Hour;
            string greetKey = hour < 5 ? "GreetingNight"
                            : hour < 12 ? "GreetingMorning"
                            : hour < 18 ? "GreetingAfternoon"
                            : "GreetingEvening";
            GreetingLabel.Text = LocalizationService.Instance.GetLocalizedString(greetKey);
            TodayDateLabel.Text = DateTime.Today.ToString("yyyy / MM / dd");

            var session = ServiceHelper.GetService<IUserSessionService>();
            var db = ServiceHelper.GetService<IDatabaseService>();
            var role = ServiceHelper.GetService<IRoleService>();

            // Force a DB refresh so role-based tile visibility is correct on first launch.
            User? user = null;
            if (session != null)
            {
                user = await session.GetCurrentUserAsync();
                if (user != null && db != null)
                {
                    try
                    {
                        var fresh = await db.GetUserByIdAsync(user.Id);
                        if (fresh != null) { user = fresh; await session.LoginAsync(fresh); }
                    }
                    catch { /* fall back to cached */ }
                }
            }

            // KPI: this month's check-in count.
            if (user != null && db != null)
            {
                var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var monthRecords = (await db.GetCheckInRecordsAsync(user.Id))
                    .Where(r => r.CheckInTime >= firstOfMonth)
                    .ToList();
                MonthCountLabel.Text = monthRecords.Count.ToString();

                var todayRecord = monthRecords.FirstOrDefault(r => r.CheckInTime.Date == DateTime.Today);
                TodayStatusLabel.Text = todayRecord == null
                    ? LocalizationService.Instance.GetLocalizedString("NotCheckedInToday")
                    : todayRecord.CheckOutTime.HasValue
                        ? string.Format(LocalizationService.Instance.GetLocalizedString("ClockedInOutAt"),
                            todayRecord.CheckInTime.ToString("HH:mm"),
                            todayRecord.CheckOutTime.Value.ToString("HH:mm"))
                        : string.Format(LocalizationService.Instance.GetLocalizedString("ClockedInAt"),
                            todayRecord.CheckInTime.ToString("HH:mm"));
            }
            else
            {
                MonthCountLabel.Text = "0";
                TodayStatusLabel.Text = LocalizationService.Instance.GetLocalizedString("NotLoggedIn");
            }

            // Admin-only tiles
            var isAdmin = role?.IsCurrentUserAdmin() == true;
            GeofencesTile.IsVisible = isAdmin;
            EditRecordsTile.IsVisible = isAdmin;
            PendingApprovalsTile.IsVisible = isAdmin;

            // Pending count badge
            if (isAdmin && db != null)
            {
                try
                {
                    var pending = await db.GetPendingUsersAsync();
                    PendingCountLabel.Text = pending.Count.ToString();
                }
                catch { PendingCountLabel.Text = "0"; }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateDashboard failed: {ex.Message}");
        }
    }

    private async void OnCheckInClicked(object? sender, EventArgs e)
        => await NavigateAsync("//CheckInPage");

    private async void OnScheduleClicked(object? sender, EventArgs e)
        => await NavigateAsync("SchedulePage");

    private async void OnLeaveClicked(object? sender, EventArgs e)
        => await NavigateAsync("LeavePage");

    private async void OnReportClicked(object? sender, EventArgs e)
        => await NavigateAsync("ReportPage");

    private async void OnProfileClicked(object? sender, EventArgs e)
        => await NavigateAsync("ProfilePage");

    private async void OnGeofencesClicked(object? sender, EventArgs e)
        => await NavigateAsync("GeofenceManagementPage");

    private async void OnEditRecordsClicked(object? sender, EventArgs e)
        => await NavigateAsync("EditCheckInPage");

    private async void OnPendingApprovalsClicked(object? sender, EventArgs e)
        => await NavigateAsync("PendingApprovalsPage");

    private async void OnSettingsClicked(object? sender, EventArgs e)
        => await NavigateAsync("//SettingsPage");

    private async Task NavigateAsync(string route)
    {
        try { await Shell.Current.GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Nav to {route} failed: {ex.Message}"); }
    }
}
