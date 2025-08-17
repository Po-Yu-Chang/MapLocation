using MapLocationApp.Views;

namespace MapLocationApp;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	private async void OnMapClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//MapPage");
	}

	private async void OnCheckInClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//CheckInPage");
	}

	private async void OnPrivacyClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//PrivacyPolicyPage");
	}

	private async void OnSettingsClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//SettingsPage");
	}

	private async void OnQuickLocationClicked(object? sender, EventArgs e)
	{
		try
		{
			QuickLocationBtn.Text = "🔄 正在獲取位置...";
			QuickLocationBtn.IsEnabled = false;

			var location = await Geolocation.GetLocationAsync(new GeolocationRequest
			{
				DesiredAccuracy = GeolocationAccuracy.Medium,
				Timeout = TimeSpan.FromSeconds(10)
			});

			if (location != null)
			{
				await DisplayAlert("位置資訊", 
					$"緯度: {location.Latitude:F6}\n經度: {location.Longitude:F6}\n精確度: ±{location.Accuracy:F0}公尺", 
					"確定");
			}
			else
			{
				await DisplayAlert("錯誤", "無法獲取位置", "確定");
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("錯誤", $"位置獲取失敗: {ex.Message}", "確定");
		}
		finally
		{
			QuickLocationBtn.Text = "📍 獲取當前位置";
			QuickLocationBtn.IsEnabled = true;
		}
	}
}
