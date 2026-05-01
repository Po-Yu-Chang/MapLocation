using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MapLocationApp.Services
{
    public class TelegramNotificationService : ITelegramNotificationService
    {
        private readonly ILogger<TelegramNotificationService>? _logger;
        private readonly IConfiguration? _configuration;
        private readonly HttpClient _httpClient;
        private string _botToken = "";
        private string _chatId = "";
        private bool _isConfigured;

        public TelegramNotificationService(ILogger<TelegramNotificationService>? logger = null, IConfiguration? configuration = null)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _isConfigured = false;
            
            // 從設定檔案載入設定
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                // 優先從 appsettings.json 載入
                if (_configuration != null)
                {
                    _botToken = _configuration["Telegram:BotToken"] ?? "";
                    _chatId = _configuration["Telegram:ChatId"] ?? "";
                }

                // 如果 appsettings.json 沒有設定，則從安全儲存載入 token，從 Preferences 載入 chatId
                if (string.IsNullOrEmpty(_botToken))
                {
                    // SecureStorage is async-only; fall back to Preferences key migration on first run
                    _botToken = Microsoft.Maui.Storage.Preferences.Get("TelegramBotToken", "");
                }

                if (string.IsNullOrEmpty(_chatId))
                {
                    _chatId = Microsoft.Maui.Storage.Preferences.Get("TelegramChatId", "");
                }

                _isConfigured = !string.IsNullOrEmpty(_botToken) && !string.IsNullOrEmpty(_chatId);

                _logger?.LogInformation($"載入 Telegram 設定: Token={(!string.IsNullOrEmpty(_botToken) ? "已設定" : "未設定")}, ChatId={(!string.IsNullOrEmpty(_chatId) ? "已設定" : "未設定")}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "載入 Telegram 設定時發生錯誤");
            }
        }

        public async Task<bool> InitializeAsync(string botToken, string chatId)
        {
            try
            {
                _botToken = botToken?.Trim() ?? "";
                _chatId = chatId?.Trim() ?? "";

                // Bot token 存 SecureStorage（加密），ChatId 存 Preferences（非敏感）
                try
                {
                    await SecureStorage.Default.SetAsync("TelegramBotToken", _botToken);
                    Microsoft.Maui.Storage.Preferences.Remove("TelegramBotToken"); // 清除舊的明文儲存
                }
                catch
                {
                    // SecureStorage 不可用時降級到 Preferences
                    Microsoft.Maui.Storage.Preferences.Set("TelegramBotToken", _botToken);
                }
                Microsoft.Maui.Storage.Preferences.Set("TelegramChatId", _chatId);

                if (string.IsNullOrEmpty(_botToken))
                {
                    _isConfigured = false;
                    return false;
                }

                // 測試連線 - 呼叫 getMe API
                var isValid = await TestConnectionAsync();
                _isConfigured = isValid && !string.IsNullOrEmpty(_chatId);
                
                _logger?.LogInformation($"Telegram 初始化結果: {(_isConfigured ? "成功" : "失敗")}");
                
                return _isConfigured;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "初始化 Telegram 服務時發生錯誤");
                _isConfigured = false;
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_botToken))
                {
                    return false;
                }

                // 呼叫 getMe API 來測試連線
                var response = await _httpClient.GetStringAsync($"https://api.telegram.org/bot{_botToken}/getMe");
                var responseJson = JsonDocument.Parse(response);
                
                if (responseJson.RootElement.GetProperty("ok").GetBoolean())
                {
                    var botInfo = responseJson.RootElement.GetProperty("result");
                    var botName = botInfo.GetProperty("username").GetString();
                    _logger?.LogInformation($"Telegram Bot 連線成功: @{botName}");
                    return true;
                }
                else
                {
                    _logger?.LogError("Telegram Bot 連線失敗");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "測試 Telegram 連線時發生錯誤");
                return false;
            }
        }

        public async Task<bool> SendMessageAsync(string message)
        {
            try
            {
                if (!_isConfigured)
                {
                    _logger?.LogWarning("Telegram 服務未設定，無法發送訊息");
                    return false;
                }

                if (string.IsNullOrEmpty(_chatId))
                {
                    _logger?.LogWarning("Chat ID 未設定，無法發送訊息");
                    return false;
                }

                // 準備 HTTP 請求資料
                var requestData = new
                {
                    chat_id = _chatId,
                    text = message,
                    parse_mode = "HTML"
                };

                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 發送 HTTP POST 請求到 Telegram API
                var response = await _httpClient.PostAsync($"https://api.telegram.org/bot{_botToken}/sendMessage", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger?.LogInformation("Telegram 訊息發送成功");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogError($"發送 Telegram 訊息失敗: {response.StatusCode}, {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "發送 Telegram 訊息時發生錯誤");
                return false;
            }
        }

        public async Task<bool> SendLocationAsync(double latitude, double longitude, string locationName = null!)
        {
            try
            {
                if (!_isConfigured)
                {
                    _logger?.LogWarning("Telegram 服務未設定，無法發送位置");
                    return false;
                }

                if (string.IsNullOrEmpty(_chatId))
                {
                    _logger?.LogWarning("Chat ID 未設定，無法發送位置");
                    return false;
                }

                // 準備位置資料
                var requestData = new
                {
                    chat_id = _chatId,
                    latitude = latitude,
                    longitude = longitude
                };

                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 發送位置到 Telegram API
                var response = await _httpClient.PostAsync($"https://api.telegram.org/bot{_botToken}/sendLocation", content);
                
                bool locationSent = response.IsSuccessStatusCode;

                // 如果有位置名稱，另外發送說明文字
                if (locationSent && !string.IsNullOrEmpty(locationName))
                {
                    await SendMessageAsync($"📍 <b>位置:</b> {locationName}");
                }

                if (locationSent)
                {
                    _logger?.LogInformation("Telegram 位置發送成功");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogError($"發送 Telegram 位置失敗: {response.StatusCode}, {errorContent}");
                }

                return locationSent;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "發送 Telegram 位置時發生錯誤");
                return false;
            }
        }

        public async Task<bool> SendCheckInNotificationAsync(string userName, double latitude, double longitude, DateTime checkInTime)
        {
            try
            {
                var message = $"📍 <b>簽到通知</b>\n\n" +
                             $"👤 使用者: {userName}\n" +
                             $"📅 時間: {checkInTime:yyyy-MM-dd HH:mm:ss}\n" +
                             $"📍 座標: {latitude:F6}, {longitude:F6}";

                // 先發送訊息，再發送位置
                var messageResult = await SendMessageAsync(message);
                var locationResult = await SendLocationAsync(latitude, longitude);

                return messageResult && locationResult;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "發送簽到通知時發生錯誤");
                return false;
            }
        }

        public async Task<bool> SendGeofenceNotificationAsync(string userName, string geofenceName, bool isEntering)
        {
            try
            {
                var action = isEntering ? "進入" : "離開";
                var icon = isEntering ? "🚪➡️" : "🚪⬅️";

                var message = $"{icon} <b>地理圍欄通知</b>\n\n" +
                             $"👤 使用者: {userName}\n" +
                             $"🏷️ 圍欄: {geofenceName}\n" +
                             $"🎯 動作: {action}\n" +
                             $"📅 時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                return await SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "發送地理圍欄通知時發生錯誤");
                return false;
            }
        }

        public async Task<bool> SendTeamLocationUpdateAsync(string teamName, string userName, double latitude, double longitude)
        {
            try
            {
                var message = $"👥 <b>團隊位置更新</b>\n\n" +
                             $"👥 團隊: {teamName}\n" +
                             $"👤 成員: {userName}\n" +
                             $"📅 時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                             $"📍 座標: {latitude:F6}, {longitude:F6}";

                // 先發送訊息，再發送位置
                var messageResult = await SendMessageAsync(message);
                var locationResult = await SendLocationAsync(latitude, longitude);

                return messageResult && locationResult;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "發送團隊位置更新時發生錯誤");
                return false;
            }
        }

        public async Task<bool> SendRouteNotificationAsync(string userName, string routeName, double startLat, double startLng, double endLat, double endLng)
        {
            try
            {
                var message = $"🛣️ <b>路線規劃通知</b>\n\n" +
                             $"👤 使用者: {userName}\n" +
                             $"🏷️ 路線: {routeName}\n" +
                             $"🚩 起點: {startLat:F6}, {startLng:F6}\n" +
                             $"🏁 終點: {endLat:F6}, {endLng:F6}\n" +
                             $"📅 時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                return await SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "發送路線通知時發生錯誤");
                return false;
            }
        }

        public async Task<bool> IsConfiguredAsync()
        {
            return await Task.FromResult(_isConfigured && !string.IsNullOrEmpty(_botToken) && !string.IsNullOrEmpty(_chatId));
        }

        public string GetCurrentChatId()
        {
            return _chatId ?? "";
        }

        public string GetCurrentBotToken()
        {
            return _botToken ?? "";
        }

        private async Task SaveConfigurationAsync()
        {
            try
            {
                // Bot token 存 SecureStorage，ChatId 存 Preferences
                try
                {
                    await SecureStorage.Default.SetAsync("TelegramBotToken", _botToken);
                    Microsoft.Maui.Storage.Preferences.Remove("TelegramBotToken");
                }
                catch
                {
                    Microsoft.Maui.Storage.Preferences.Set("TelegramBotToken", _botToken);
                }
                Microsoft.Maui.Storage.Preferences.Set("TelegramChatId", _chatId);
                _logger?.LogInformation("Telegram 設定已儲存");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "儲存 Telegram 設定時發生錯誤");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}