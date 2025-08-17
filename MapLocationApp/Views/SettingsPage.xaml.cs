using MapLocationApp.Services;
using System.Globalization;

namespace MapLocationApp.Views;

public partial class SettingsPage : ContentPage
{
    private readonly LocalizationService _localizationService;

    public SettingsPage()
    {
        InitializeComponent();
        _localizationService = LocalizationService.Instance;
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        // 載入當前語言設定
        var currentCulture = CultureInfo.CurrentUICulture.Name;
        LanguagePicker.SelectedIndex = currentCulture switch
        {
            "zh-TW" => 0,
            "zh-CN" => 1,
            "en-US" => 2,
            "ja-JP" => 3,
            "ko-KR" => 4,
            _ => 0
        };

        // 載入當前主題設定
        var currentTheme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        ThemePicker.SelectedIndex = currentTheme switch
        {
            AppTheme.Light => 0,
            AppTheme.Dark => 1,
            _ => 2
        };

        // 載入其他設定 (從 Preferences 讀取)
        HighAccuracySwitch.IsToggled = Preferences.Get("HighAccuracy", true);
        BackgroundLocationSwitch.IsToggled = Preferences.Get("BackgroundLocation", false);
        GeofenceDetectionSwitch.IsToggled = Preferences.Get("GeofenceDetection", true);
        CheckInNotificationSwitch.IsToggled = Preferences.Get("CheckInNotification", true);
        GeofenceNotificationSwitch.IsToggled = Preferences.Get("GeofenceNotification", true);
        TeamNotificationSwitch.IsToggled = Preferences.Get("TeamNotification", true);
    }

    private async void OnLanguageChanged(object sender, EventArgs e)
    {
        if (LanguagePicker.SelectedIndex == -1) return;

        var selectedCulture = LanguagePicker.SelectedIndex switch
        {
            0 => "zh-TW",
            1 => "zh-CN",
            2 => "en-US",
            3 => "ja-JP",
            4 => "ko-KR",
            _ => "zh-TW"
        };

        try
        {
            _localizationService.SetCulture(selectedCulture);
            Preferences.Set("AppLanguage", selectedCulture);
            
            await DisplayAlert("語言設定", "語言已更改，請重新啟動應用程式以完全生效。", "確定");
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"無法更改語言: {ex.Message}", "確定");
        }
    }

    private async void OnThemeChanged(object sender, EventArgs e)
    {
        if (ThemePicker.SelectedIndex == -1) return;

        var selectedTheme = ThemePicker.SelectedIndex switch
        {
            0 => AppTheme.Light,
            1 => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };

        try
        {
            if (Application.Current != null)
            {
                Application.Current.UserAppTheme = selectedTheme;
                Preferences.Set("AppTheme", (int)selectedTheme);
                
                var themeName = selectedTheme switch
                {
                    AppTheme.Light => "淺色模式",
                    AppTheme.Dark => "深色模式",
                    _ => "自動模式"
                };

                await DisplayAlert("主題設定", $"已切換到{themeName}", "確定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"無法更改主題: {ex.Message}", "確定");
        }
    }

    private void OnHighAccuracyToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("HighAccuracy", e.Value);
    }

    private void OnBackgroundLocationToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("BackgroundLocation", e.Value);
    }

    private void OnGeofenceDetectionToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("GeofenceDetection", e.Value);
    }

    private void OnCheckInNotificationToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("CheckInNotification", e.Value);
    }

    private void OnGeofenceNotificationToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("GeofenceNotification", e.Value);
    }

    private void OnTeamNotificationToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("TeamNotification", e.Value);
    }

    private async void OnExportDataClicked(object sender, EventArgs e)
    {
        try
        {
            // 實作資料匯出功能
            await DisplayAlert("資料匯出", "資料匯出功能開發中...", "確定");
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"匯出失敗: {ex.Message}", "確定");
        }
    }

    private async void OnImportDataClicked(object sender, EventArgs e)
    {
        try
        {
            // 實作資料匯入功能
            await DisplayAlert("資料匯入", "資料匯入功能開發中...", "確定");
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"匯入失敗: {ex.Message}", "確定");
        }
    }

    private async void OnClearCacheClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await DisplayAlert("清除快取", "確定要清除所有快取資料嗎？此操作無法復原。", "確定", "取消");
            
            if (result)
            {
                // 清除快取邏輯
                var cacheDirectory = Path.Combine(FileSystem.CacheDirectory);
                if (Directory.Exists(cacheDirectory))
                {
                    Directory.Delete(cacheDirectory, true);
                    Directory.CreateDirectory(cacheDirectory);
                }

                await DisplayAlert("成功", "快取已清除", "確定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"清除失敗: {ex.Message}", "確定");
        }
    }

    private async void OnPrivacyPolicyClicked(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("//PrivacyPolicy");
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"無法開啟隱私政策: {ex.Message}", "確定");
        }
    }
}