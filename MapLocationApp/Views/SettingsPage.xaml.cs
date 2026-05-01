using MapLocationApp.Models;
using MapLocationApp.Services;
using MapLocationApp.Services.Interfaces;
using System.Globalization;

namespace MapLocationApp.Views;

public partial class SettingsPage : ContentPage
{
    private readonly LocalizationService _localizationService;
    private readonly ITelegramNotificationService _telegramService;
    private readonly IConfigService _configService;
    private readonly IDatabaseService _databaseService;
    private readonly ISecureConfigService _secureConfig;

    public SettingsPage()
    {
        InitializeComponent();
        _localizationService = LocalizationService.Instance;
        _telegramService = ServiceHelper.GetService<ITelegramNotificationService>();
        _configService = ServiceHelper.GetService<IConfigService>();
        _databaseService = ServiceHelper.GetService<IDatabaseService>();
        _secureConfig = ServiceHelper.GetService<ISecureConfigService>();
        LoadCurrentSettings();
        LoadM5Settings();
        _ = LoadDatabaseSettingsAsync();
    }

    private const string StrictGeofenceKey = "CheckIn.StrictGeofence";

    private void LoadM5Settings()
    {
        StrictGeofenceSwitch.Toggled -= OnStrictGeofenceToggled;
        StrictGeofenceSwitch.IsToggled = Preferences.Default.Get(StrictGeofenceKey, false);
        StrictGeofenceSwitch.Toggled += OnStrictGeofenceToggled;

        var reminder = ServiceHelper.GetService<IReminderService>();
        ClockReminderSwitch.Toggled -= OnClockReminderToggled;
        ClockReminderSwitch.IsToggled = reminder?.RemindersEnabled ?? false;
        ClockReminderSwitch.Toggled += OnClockReminderToggled;
    }

    private void OnStrictGeofenceToggled(object? sender, ToggledEventArgs e)
    {
        Preferences.Default.Set(StrictGeofenceKey, e.Value);
    }

    private async void OnClockReminderToggled(object? sender, ToggledEventArgs e)
    {
        try
        {
            var reminder = ServiceHelper.GetService<IReminderService>();
            if (reminder == null) return;

            if (e.Value)
            {
                var granted = await reminder.RequestPermissionAsync();
                if (!granted)
                {
                    ClockReminderSwitch.Toggled -= OnClockReminderToggled;
                    ClockReminderSwitch.IsToggled = false;
                    ClockReminderSwitch.Toggled += OnClockReminderToggled;
                    return;
                }
                reminder.RemindersEnabled = true;
                var session = ServiceHelper.GetService<IUserSessionService>();
                var user = session?.CurrentUser ?? await (session?.GetCurrentUserAsync() ?? Task.FromResult<User?>(null));
                if (user != null)
                    await reminder.ScheduleClockReminderAsync(user);
            }
            else
            {
                reminder.RemindersEnabled = false;
                await reminder.CancelAllRemindersAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnClockReminderToggled failed: {ex.Message}");
        }
    }

    private void LoadCurrentSettings()
    {
        var currentCulture = CultureInfo.CurrentUICulture.Name;

        // Unsubscribe before setting index to avoid spurious alerts on init
        LanguagePicker.SelectedIndexChanged -= OnLanguageChanged;
        LanguagePicker.SelectedIndex = currentCulture switch
        {
            "zh-TW" => 0,
            "zh-CN" => 1,
            "en-US" => 2,
            "th-TH" => 3,
            "th" => 3,
            _ => 0
        };
        LanguagePicker.SelectedIndexChanged += OnLanguageChanged;

        var savedTheme = Preferences.Get("AppTheme", (int)AppTheme.Unspecified);
        ThemePicker.SelectedIndexChanged -= OnThemeChanged;
        ThemePicker.SelectedIndex = savedTheme switch
        {
            (int)AppTheme.Light => 0,
            (int)AppTheme.Dark => 1,
            _ => 2
        };
        ThemePicker.SelectedIndexChanged += OnThemeChanged;

        HighAccuracySwitch.IsToggled = Preferences.Get("HighAccuracy", true);
        BackgroundLocationSwitch.IsToggled = Preferences.Get("BackgroundLocation", false);
        GeofenceDetectionSwitch.IsToggled = Preferences.Get("GeofenceDetection", true);
        CheckInNotificationSwitch.IsToggled = Preferences.Get("CheckInNotification", true);
        GeofenceNotificationSwitch.IsToggled = Preferences.Get("GeofenceNotification", true);
        TeamNotificationSwitch.IsToggled = Preferences.Get("TeamNotification", true);

        _ = LoadTelegramSettingsAsync();
    }

    private async Task LoadTelegramSettingsAsync()
    {
        try
        {
            var isConfigured = await _telegramService.IsConfiguredAsync();

            // Unsubscribe to avoid triggering OnTelegramEnabledToggled (which clears storage) during init
            TelegramEnabledSwitch.Toggled -= OnTelegramEnabledToggled;
            TelegramEnabledSwitch.IsToggled = isConfigured;
            TelegramEnabledSwitch.Toggled += OnTelegramEnabledToggled;

            TelegramConfigLayout.IsVisible = isConfigured;

            if (isConfigured)
            {
                string token;
                try { token = await SecureStorage.Default.GetAsync("TelegramBotToken") ?? string.Empty; }
                catch { token = Preferences.Get("TelegramBotToken", string.Empty); }
                if (string.IsNullOrEmpty(token))
                    token = Preferences.Get("TelegramBotToken", string.Empty);

                TelegramBotTokenEntry.Text = token;
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
            3 => "th-TH",
            _ => "zh-TW"
        };

        try
        {
            _localizationService.SetCulture(selectedCulture);
            Preferences.Set("AppLanguage", selectedCulture);

            if (Application.Current?.Windows.FirstOrDefault() is Window window)
            {
                window.Page = new AppShell();
                await Shell.Current.GoToAsync("//SettingsPage");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"語言切換錯誤: {ex.Message}");
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
            // 停用 Telegram 通知，清除所有儲存的 token
            TelegramBotTokenEntry.Text = string.Empty;
            TelegramChatIdEntry.Text = string.Empty;
            try { SecureStorage.Default.Remove("TelegramBotToken"); } catch { }
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

    private async Task LoadDatabaseSettingsAsync()
    {
        try
        {
            var config = await _configService.GetDatabaseConfigAsync();
            DbHostEntry.Text = config.Host;
            DbPortEntry.Text = config.Port.ToString();
            DbNameEntry.Text = config.DatabaseName;
            DbUsernameEntry.Text = config.Username;
            // Load password from SecureStorage; fall back to DatabaseConfig.Password for migration
            try
            {
                var securePassword = await _secureConfig.GetDatabasePasswordAsync();
                DbPasswordEntry.Text = securePassword ?? config.Password;
            }
            catch
            {
                DbPasswordEntry.Text = config.Password;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"載入資料庫設定失敗: {ex.Message}");
        }
    }

    private async void OnDbTestClicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(DbHostEntry.Text) ||
                string.IsNullOrWhiteSpace(DbNameEntry.Text) ||
                string.IsNullOrWhiteSpace(DbUsernameEntry.Text))
            {
                await DisplayAlert("❌ 輸入錯誤", "請填寫主機、資料庫名稱與使用者名稱。", "確定");
                return;
            }

            if (!int.TryParse(DbPortEntry.Text, out var port) || port < 1 || port > 65535)
            {
                await DisplayAlert("❌ 輸入錯誤", "Port 必須介於 1 到 65535 之間。", "確定");
                return;
            }

            DbTestButton.IsEnabled = false;
            DbTestButton.Text = "🔄 測試中...";
            DbStatusLabel.Text = "📡 狀態: 測試連線中...";
            DbStatusLabel.TextColor = Colors.Gray;

            var testConfig = new DatabaseConfig
            {
                Host = DbHostEntry.Text.Trim(),
                Port = port,
                DatabaseName = DbNameEntry.Text.Trim(),
                Username = DbUsernameEntry.Text.Trim()
            };
            var testPassword = DbPasswordEntry.Text ?? string.Empty;

            var success = await _databaseService.TestConnectionAsync(testConfig, testPassword);
            if (success)
            {
                DbStatusLabel.Text = "📡 狀態: 連線成功 ✅";
                DbStatusLabel.TextColor = Colors.Green;
                await DisplayAlert("✅ 成功", "資料庫連線測試成功！", "確定");
            }
            else
            {
                DbStatusLabel.Text = "📡 狀態: 連線失敗 ❌";
                DbStatusLabel.TextColor = Colors.Red;
                await DisplayAlert("❌ 失敗", "無法連線至資料庫，請確認設定是否正確。", "確定");
            }
        }
        catch (Exception ex)
        {
            DbStatusLabel.Text = "📡 狀態: 錯誤 ❌";
            DbStatusLabel.TextColor = Colors.Red;
            await DisplayAlert("❌ 錯誤", $"測試失敗: {ex.Message}", "確定");
        }
        finally
        {
            DbTestButton.IsEnabled = true;
            DbTestButton.Text = "🔌 測試連線";
        }
    }

    private async void OnDbSaveClicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(DbHostEntry.Text) ||
                string.IsNullOrWhiteSpace(DbNameEntry.Text) ||
                string.IsNullOrWhiteSpace(DbUsernameEntry.Text))
            {
                await DisplayAlert("❌ 輸入錯誤", "請填寫主機、資料庫名稱與使用者名稱。", "確定");
                return;
            }

            if (!int.TryParse(DbPortEntry.Text, out var port) || port < 1 || port > 65535)
            {
                await DisplayAlert("❌ 輸入錯誤", "Port 必須介於 1 到 65535 之間。", "確定");
                return;
            }

            DbSaveButton.IsEnabled = false;
            DbSaveButton.Text = "💾 儲存中...";

            // Save password to SecureStorage (not plaintext in SQLite)
            var password = DbPasswordEntry.Text ?? string.Empty;
            try
            {
                await _secureConfig.SetDatabasePasswordAsync(password);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SecureStorage 儲存失敗，回退至 DatabaseConfig: {ex.Message}");
            }

            // Save config without password (password is in SecureStorage)
            var config = new DatabaseConfig
            {
                Host = DbHostEntry.Text.Trim(),
                Port = port,
                DatabaseName = DbNameEntry.Text.Trim(),
                Username = DbUsernameEntry.Text.Trim(),
                Password = string.Empty
            };

            var saved = await _configService.SaveDatabaseConfigAsync(config);
            if (saved)
            {
                // Reset cached connection so next DB operation uses the new settings immediately
                _databaseService.ResetConnection();
                // Reset geofence cache so it reloads from new DB connection
                ServiceHelper.GetService<IGeofenceService>()?.ResetCache();
                DbStatusLabel.Text = "📡 狀態: 設定已儲存並套用 ✅";
                DbStatusLabel.TextColor = Colors.Green;
                await DisplayAlert("✅ 成功", "資料庫設定已儲存，下次操作即生效。", "確定");
            }
            else
            {
                DbStatusLabel.Text = "📡 狀態: 儲存失敗 ❌";
                DbStatusLabel.TextColor = Colors.Red;
                await DisplayAlert("❌ 失敗", "儲存資料庫設定失敗。", "確定");
            }
        }
        catch (Exception ex)
        {
            DbStatusLabel.Text = "📡 狀態: 錯誤 ❌";
            DbStatusLabel.TextColor = Colors.Red;
            await DisplayAlert("❌ 錯誤", $"儲存失敗: {ex.Message}", "確定");
        }
        finally
        {
            DbSaveButton.IsEnabled = true;
            DbSaveButton.Text = "💾 儲存設定";
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
