using MapLocationApp.Services;
using System.Globalization;

namespace MapLocationApp.Views;

public partial class SettingsPage : ContentPage
{
    private readonly LocalizationService _localizationService;
    private readonly ITelegramNotificationService _telegramService;

    public SettingsPage()
    {
        InitializeComponent();
        _localizationService = LocalizationService.Instance;
        _telegramService = ServiceHelper.GetService<ITelegramNotificationService>();
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

        // 載入當前主題設定（從 Preferences 讀取，而不是直接從 Application）
        var savedTheme = Preferences.Get("AppTheme", (int)AppTheme.Unspecified);
        ThemePicker.SelectedIndex = savedTheme switch
        {
            (int)AppTheme.Light => 0,
            (int)AppTheme.Dark => 1,
            _ => 2
        };

        // 載入其他設定 (從 Preferences 讀取)
        HighAccuracySwitch.IsToggled = Preferences.Get("HighAccuracy", true);
        BackgroundLocationSwitch.IsToggled = Preferences.Get("BackgroundLocation", false);
        GeofenceDetectionSwitch.IsToggled = Preferences.Get("GeofenceDetection", true);
        CheckInNotificationSwitch.IsToggled = Preferences.Get("CheckInNotification", true);
        GeofenceNotificationSwitch.IsToggled = Preferences.Get("GeofenceNotification", true);
        TeamNotificationSwitch.IsToggled = Preferences.Get("TeamNotification", true);

        // 載入 Telegram 設定
        LoadTelegramSettings();
    }

    private async void LoadTelegramSettings()
    {
        try
        {
            var isConfigured = await _telegramService.IsConfiguredAsync();
            TelegramEnabledSwitch.IsToggled = isConfigured;
            TelegramConfigLayout.IsVisible = isConfigured;

            if (isConfigured)
            {
                TelegramBotTokenEntry.Text = Preferences.Get("TelegramBotToken", string.Empty);
                TelegramChatIdEntry.Text = Preferences.Get("TelegramChatId", string.Empty);
                TelegramStatusLabel.Text = "📡 狀態: 已設定並啟用";
                TelegramStatusLabel.TextColor = Colors.Green;
            }
            else
            {
                TelegramStatusLabel.Text = "📡 狀態: 未設定";
                TelegramStatusLabel.TextColor = Colors.Gray;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"載入 Telegram 設定錯誤: {ex.Message}");
        }
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
            // 只儲存設定，不立即套用主題
            Preferences.Set("AppTheme", (int)selectedTheme);
            
            var themeName = selectedTheme switch
            {
                AppTheme.Light => "淺色模式",
                AppTheme.Dark => "深色模式",
                _ => "自動模式（跟隨系統）"
            };

            // 延遲套用主題變更，避免 UI 閃爍
            await Task.Delay(100);
            
            if (Application.Current != null)
            {
                Application.Current.UserAppTheme = selectedTheme;
            }

            await DisplayAlert("主題設定", $"已切換到{themeName}", "確定");
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

    private void OnTelegramEnabledToggled(object sender, ToggledEventArgs e)
    {
        TelegramConfigLayout.IsVisible = e.Value;
        
        if (!e.Value)
        {
            // 停用 Telegram 通知
            TelegramBotTokenEntry.Text = string.Empty;
            TelegramChatIdEntry.Text = string.Empty;
            Preferences.Remove("TelegramBotToken");
            Preferences.Remove("TelegramChatId");
            TelegramStatusLabel.Text = "📡 狀態: 已停用";
            TelegramStatusLabel.TextColor = Colors.Gray;
        }
    }

    private async void OnTelegramTestClicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(TelegramBotTokenEntry.Text) || 
                string.IsNullOrWhiteSpace(TelegramChatIdEntry.Text))
            {
                await DisplayAlert("❌ 輸入錯誤", "請填入 Bot Token 和 Chat ID", "確定");
                return;
            }

            TelegramTestButton.IsEnabled = false;
            TelegramTestButton.Text = "🔄 測試中...";

            var success = await _telegramService.InitializeAsync(
                TelegramBotTokenEntry.Text.Trim(),
                TelegramChatIdEntry.Text.Trim()
            );

            if (success)
            {
                // 發送測試訊息
                await _telegramService.SendMessageAsync("🧪 <b>MapLocation 測試訊息</b>\n\nTelegram 通知設定成功！");
                
                TelegramStatusLabel.Text = "📡 狀態: 連線成功 ✅";
                TelegramStatusLabel.TextColor = Colors.Green;
                await DisplayAlert("✅ 成功", "Telegram 連線測試成功！已發送測試訊息。", "確定");
            }
            else
            {
                TelegramStatusLabel.Text = "📡 狀態: 連線失敗 ❌";
                TelegramStatusLabel.TextColor = Colors.Red;
                await DisplayAlert("❌ 失敗", "Telegram 連線測試失敗，請檢查 Bot Token 和 Chat ID 是否正確。", "確定");
            }
        }
        catch (Exception ex)
        {
            TelegramStatusLabel.Text = "📡 狀態: 錯誤 ❌";
            TelegramStatusLabel.TextColor = Colors.Red;
            await DisplayAlert("❌ 錯誤", $"測試連線時發生錯誤: {ex.Message}", "確定");
        }
        finally
        {
            TelegramTestButton.IsEnabled = true;
            TelegramTestButton.Text = "🧪 測試連線";
        }
    }

    private async void OnTelegramSaveClicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(TelegramBotTokenEntry.Text) || 
                string.IsNullOrWhiteSpace(TelegramChatIdEntry.Text))
            {
                await DisplayAlert("❌ 輸入錯誤", "請填入 Bot Token 和 Chat ID", "確定");
                return;
            }

            TelegramSaveButton.IsEnabled = false;
            TelegramSaveButton.Text = "💾 儲存中...";

            var success = await _telegramService.InitializeAsync(
                TelegramBotTokenEntry.Text.Trim(),
                TelegramChatIdEntry.Text.Trim()
            );

            if (success)
            {
                TelegramStatusLabel.Text = "📡 狀態: 已儲存並啟用 ✅";
                TelegramStatusLabel.TextColor = Colors.Green;
                await DisplayAlert("✅ 成功", "Telegram 設定已成功儲存！", "確定");
            }
            else
            {
                TelegramStatusLabel.Text = "📡 狀態: 儲存失敗 ❌";
                TelegramStatusLabel.TextColor = Colors.Red;
                await DisplayAlert("❌ 失敗", "Telegram 設定儲存失敗，請檢查設定是否正確。", "確定");
            }
        }
        catch (Exception ex)
        {
            TelegramStatusLabel.Text = "📡 狀態: 錯誤 ❌";
            TelegramStatusLabel.TextColor = Colors.Red;
            await DisplayAlert("❌ 錯誤", $"儲存設定時發生錯誤: {ex.Message}", "確定");
        }
        finally
        {
            TelegramSaveButton.IsEnabled = true;
            TelegramSaveButton.Text = "💾 儲存設定";
        }
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