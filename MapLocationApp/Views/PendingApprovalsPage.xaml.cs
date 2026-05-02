using System.Collections.ObjectModel;
using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp.Views;

public partial class PendingApprovalsPage : ContentPage
{
    private readonly IDatabaseService _databaseService;
    private readonly ObservableCollection<PendingUserDisplay> _items = new();

    public PendingApprovalsPage(IDatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        UsersCollectionView.ItemsSource = _items;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Admin guard.
        var role = ServiceHelper.GetService<IRoleService>();
        if (role?.IsCurrentUserAdmin() != true)
        {
            await DisplayAlert(L("Error"), L("AdminRequired"), L("OK"));
            try { await Shell.Current.GoToAsync(".."); }
            catch { await Navigation.PopAsync(); }
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var users = await _databaseService.GetPendingUsersAsync();
            _items.Clear();
            foreach (var u in users)
            {
                _items.Add(new PendingUserDisplay
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayInfo = BuildDisplayInfo(u),
                    RegisteredAt = string.Format(L("RegisteredAt"), u.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")),
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(L("Error"), ex.Message, L("OK"));
        }
    }

    private static string BuildDisplayInfo(User u)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(u.FullName)) parts.Add(u.FullName);
        if (!string.IsNullOrEmpty(u.Email)) parts.Add(u.Email);
        var dept = u.DepartmentPosition;
        if (!string.IsNullOrEmpty(dept)) parts.Add(dept);
        return parts.Count == 0 ? "—" : string.Join(" · ", parts);
    }

    private async void OnApproveClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not int id) return;
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item == null) return;

        var confirm = await DisplayAlert(L("ApproveBtn"),
            string.Format(L("ApproveConfirm"), item.Username), L("ApproveBtn"), L("Cancel"));
        if (!confirm) return;

        var ok = await _databaseService.ApproveUserAsync(id);
        if (ok)
        {
            await DisplayAlert(L("Success"), string.Format(L("UserApproved"), item.Username), L("OK"));
            await LoadAsync();
        }
        else
        {
            await DisplayAlert(L("Error"), L("CheckInError"), L("OK"));
        }
    }

    private async void OnRejectClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not int id) return;
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item == null) return;

        var confirm = await DisplayAlert(L("RejectBtn"),
            string.Format(L("RejectConfirm"), item.Username), L("RejectBtn"), L("Cancel"));
        if (!confirm) return;

        var ok = await _databaseService.RejectUserAsync(id);
        if (ok)
        {
            await DisplayAlert(L("Success"), string.Format(L("UserRejected"), item.Username), L("OK"));
            await LoadAsync();
        }
        else
        {
            await DisplayAlert(L("Error"), L("CheckInError"), L("OK"));
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadAsync();

    private static string L(string key) => LocalizationService.Instance.GetLocalizedString(key);

    private class PendingUserDisplay
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayInfo { get; set; } = string.Empty;
        public string RegisteredAt { get; set; } = string.Empty;
    }
}
