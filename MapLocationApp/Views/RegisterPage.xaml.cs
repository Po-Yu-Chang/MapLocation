using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp.Views;

public partial class RegisterPage : ContentPage
{
    private readonly IDatabaseService _databaseService;

    public RegisterPage(IDatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        try
        {
            SubmitButton.IsEnabled = false;

            var username = UsernameEntry.Text?.Trim() ?? string.Empty;
            var password = PasswordEntry.Text ?? string.Empty;
            var confirm = ConfirmPasswordEntry.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username))
            {
                await DisplayAlert(L("Error"), L("UsernameRequired"), L("OK"));
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert(L("Error"), L("PasswordRequired"), L("OK"));
                return;
            }

            if (password.Length < 6)
            {
                await DisplayAlert(L("Error"), L("PasswordTooShort"), L("OK"));
                return;
            }

            if (password != confirm)
            {
                await DisplayAlert(L("Error"), L("PasswordMismatch"), L("OK"));
                return;
            }

            var existing = await _databaseService.GetUserByUsernameAsync(username);
            if (existing != null)
            {
                await DisplayAlert(L("Error"), L("UsernameTaken"), L("OK"));
                return;
            }

            var newUser = new User
            {
                Username = username,
                Password = password,
                FullName = TrimOrNull(FullNameEntry.Text),
                Email = TrimOrNull(EmailEntry.Text),
                Department = TrimOrNull(DepartmentEntry.Text),
                Position = TrimOrNull(PositionEntry.Text),
                Role = UserRole.Employee,
                // Pending approval — admin must activate via PendingApprovalsPage before login works.
                IsActive = false,
                MustChangePassword = false,
            };

            var ok = await _databaseService.CreateUserAsync(newUser);
            if (!ok)
            {
                await DisplayAlert(L("Error"), L("RegisterFailed"), L("OK"));
                return;
            }

            await DisplayAlert(L("Success"), L("RegisterPendingApproval"), L("OK"));
            await NavigateBackToLogin();
        }
        catch (Exception ex)
        {
            await DisplayAlert(L("Error"), ex.Message, L("OK"));
        }
        finally
        {
            SubmitButton.IsEnabled = true;
        }
    }

    private async void OnBackToLoginClicked(object sender, EventArgs e)
    {
        await NavigateBackToLogin();
    }

    private async Task NavigateBackToLogin()
    {
        try { await Shell.Current.GoToAsync("//LoginPage"); }
        catch { await Navigation.PopAsync(); }
    }

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string L(string key) => LocalizationService.Instance.GetLocalizedString(key);
}
