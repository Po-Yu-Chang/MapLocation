using MapLocationApp.Models;
using MapLocationApp.Services;
using Microsoft.Maui.Devices;

namespace MapLocationApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateDashboard();

        // Subtle entry animation: hero pin gentle bounce on first paint.
        if (HeroPinHost != null)
        {
            HeroPinHost.Scale = 0.5;
            HeroPinHost.Opacity = 0;
            await Task.WhenAll(
                HeroPinHost.ScaleTo(1.0, 500, Easing.SpringOut),
                HeroPinHost.FadeTo(1.0, 300, Easing.CubicOut)
            );
        }
    }

    /// <summary>
    /// Pop-the-bubble feedback for tile taps — quick haptic burst + slight scale animation.
    /// HapticFeedback.Default is a no-op on Windows (no hardware), so this safely ships cross-platform.
    /// </summary>
    private async Task PopAsync(View tile)
    {
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch { /* unsupported / disabled */ }

        // Visual "press" — squish then bounce back. Timing tuned to feel like a real button press.
        await tile.ScaleTo(0.94, 80, Easing.CubicIn);
        await tile.ScaleTo(1.0, 120, Easing.SpringOut);
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
        => await PopAndNavigate(sender, "//CheckInPage");

    private async void OnScheduleClicked(object? sender, EventArgs e)
        => await PopAndNavigate(sender, "SchedulePage");

    private async void OnLeaveClicked(object? sender, EventArgs e)
        => await PopAndNavigate(sender, "LeavePage");

    private async void OnReportClicked(object? sender, EventArgs e)
        => await PopAndNavigate(sender, "ReportPage");

    private async void OnProfileClicked(object? sender, EventArgs e)
        => await PopAndNavigate(sender, "ProfilePage");

    private async void OnGeofencesClicked(object? sender, EventArgs e)
        => await PopAndNavigate(sender, "GeofenceManagementPage");

    private async void OnEditRecordsClicked(object? sender, EventArgs e)
        => await PopAndNavigate(sender, "EditCheckInPage");

    private async void OnPendingApprovalsClicked(object? sender, EventArgs e)
        => await PopAndNavigate(sender, "PendingApprovalsPage");

    private async void OnSettingsClicked(object? sender, EventArgs e)
        => await PopAndNavigate(sender, "//SettingsPage");

    /// <summary>
    /// "Pop the bubble" tap response — find the parent tile (Border) of the tapped Grid,
    /// run the pop animation + haptic, then navigate.
    /// </summary>
    private async Task PopAndNavigate(object? sender, string route)
    {
        // Walk up to the parent Border (tile root). Falls back to the inner Grid if not found.
        View? tile = sender as View;
        while (tile?.Parent is View parent && tile is not Border)
        {
            tile = parent;
        }
        if (tile != null)
        {
            await PopAsync(tile);
        }
        await NavigateAsync(route);
    }

    private async Task NavigateAsync(string route)
    {
        try { await Shell.Current.GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Nav to {route} failed: {ex.Message}"); }
    }
}
