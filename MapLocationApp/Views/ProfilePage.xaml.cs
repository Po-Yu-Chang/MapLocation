using System.Globalization;
using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp.Views;

public partial class ProfilePage : ContentPage
{
    private readonly IUserSessionService _session;
    private readonly IDatabaseService _databaseService;

    public ProfilePage(IUserSessionService session, IDatabaseService databaseService)
    {
        InitializeComponent();
        _session = session;
        _databaseService = databaseService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
        LocalizationService.Instance.CultureChanged += OnCultureChanged;
        await LoadProfileAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
    }

    private void OnCultureChanged(CultureInfo _) => MainThread.BeginInvokeOnMainThread(() => { });

    private async Task LoadProfileAsync()
    {
        var user = await _session.GetCurrentUserAsync();
        if (user == null)
        {
            await DisplayAlert(L("Error"), L("NotLoggedIn"), L("OK"));
            await Shell.Current.GoToAsync("..");
            return;
        }

        FullNameEntry.Text = user.FullName ?? string.Empty;
        EmailEntry.Text = user.Email ?? string.Empty;
        PhoneEntry.Text = user.PhoneNumber ?? string.Empty;
        DepartmentEntry.Text = user.Department ?? string.Empty;
        PositionEntry.Text = user.Position ?? string.Empty;

        WorkStartPicker.Time = user.WorkHoursStart ?? TimeSpan.FromHours(9);
        WorkEndPicker.Time = user.WorkHoursEnd ?? TimeSpan.FromHours(18);

        if (!string.IsNullOrEmpty(user.AvatarPath) && File.Exists(user.AvatarPath))
        {
            AvatarImage.Source = ImageSource.FromFile(user.AvatarPath);
        }
        else
        {
            AvatarImage.Source = null;
        }
    }

    private async void OnChangeAvatarClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await MediaPicker.PickPhotoAsync();
            if (result == null) return;

            // Copy to AppDataDirectory so it survives app updates and source-deletes.
            var destDir = Path.Combine(FileSystem.AppDataDirectory, "avatars");
            Directory.CreateDirectory(destDir);
            var ext = Path.GetExtension(result.FileName);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var destPath = Path.Combine(destDir, $"avatar_{Guid.NewGuid():N}{ext}");

            using (var src = await result.OpenReadAsync())
            using (var dst = File.Create(destPath))
            {
                await src.CopyToAsync(dst);
            }

            AvatarImage.Source = ImageSource.FromFile(destPath);

            var user = _session.CurrentUser;
            if (user != null)
            {
                // Stash on the in-memory user; persisted only on Save.
                user.AvatarPath = destPath;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(L("Error"), ex.Message, L("OK"));
        }
    }

    private void OnResetWorkHoursClicked(object sender, EventArgs e)
    {
        WorkStartPicker.Time = TimeSpan.FromHours(9);
        WorkEndPicker.Time = TimeSpan.FromHours(18);
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var user = _session.CurrentUser;
            if (user == null)
            {
                await DisplayAlert(L("Error"), L("NotLoggedIn"), L("OK"));
                return;
            }

            user.FullName = TrimOrNull(FullNameEntry.Text);
            user.Email = TrimOrNull(EmailEntry.Text);
            user.PhoneNumber = TrimOrNull(PhoneEntry.Text);
            user.Department = TrimOrNull(DepartmentEntry.Text);
            user.Position = TrimOrNull(PositionEntry.Text);

            user.WorkHoursStart = WorkStartPicker.Time;
            user.WorkHoursEnd = WorkEndPicker.Time;

            var ok = await _databaseService.UpdateUserAsync(user);
            if (ok)
            {
                await _session.LoginAsync(user); // refresh cached session
                await DisplayAlert(L("Success"), L("ProfileUpdated"), L("OK"));
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

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string L(string key) => LocalizationService.Instance.GetLocalizedString(key);
}
