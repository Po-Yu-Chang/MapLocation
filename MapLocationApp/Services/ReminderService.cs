using MapLocationApp.Models;
using Plugin.LocalNotification;

namespace MapLocationApp.Services;

public interface IReminderService
{
    Task<bool> RequestPermissionAsync();
    Task ScheduleClockReminderAsync(User user);
    Task CancelAllRemindersAsync();
    bool RemindersEnabled { get; set; }
}

public class ReminderService : IReminderService
{
    private const string EnabledKey = "Reminders.Enabled";
    private const int ClockInId = 1001;
    private const int ClockOutId = 1002;

    public bool RemindersEnabled
    {
        get => Preferences.Default.Get(EnabledKey, true);
        set => Preferences.Default.Set(EnabledKey, value);
    }

    public async Task<bool> RequestPermissionAsync()
    {
        var status = await LocalNotificationCenter.Current.RequestNotificationPermission();
        return status;
    }

    public async Task ScheduleClockReminderAsync(User user)
    {
        // Always cancel before scheduling so toggling settings yields predictable state.
        await CancelAllRemindersAsync();
        if (!RemindersEnabled) return;

        var start = user.WorkHoursStart ?? TimeSpan.FromHours(9);
        var end = user.WorkHoursEnd ?? TimeSpan.FromHours(18);

        // Schedule the next occurrence; the OS handles repeat via NotificationRepeat.Daily.
        await Schedule(ClockInId, "Clock-in reminder", $"上班時間 {Format(start)}，記得打卡", NextOccurrence(start));
        await Schedule(ClockOutId, "Clock-out reminder", $"下班時間 {Format(end)}，記得打卡", NextOccurrence(end));
    }

    public Task CancelAllRemindersAsync()
    {
        LocalNotificationCenter.Current.Cancel(ClockInId, ClockOutId);
        return Task.CompletedTask;
    }

    private static async Task Schedule(int id, string title, string body, DateTime when)
    {
        var req = new NotificationRequest
        {
            NotificationId = id,
            Title = title,
            Description = body,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = when,
                NotifyRepeatInterval = TimeSpan.FromDays(1),
            },
        };
        await LocalNotificationCenter.Current.Show(req);
    }

    private static DateTime NextOccurrence(TimeSpan time)
    {
        var today = DateTime.Today.Add(time);
        return today > DateTime.Now ? today : today.AddDays(1);
    }

    private static string Format(TimeSpan t) => $"{t.Hours:00}:{t.Minutes:00}";
}
