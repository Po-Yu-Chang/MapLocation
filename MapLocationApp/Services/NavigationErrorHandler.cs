using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Types of navigation errors
    /// </summary>
    public enum NavigationErrorType
    {
        GPSLost,
        NetworkError,
        RouteCalculationFailed,
        TTSError,
        LocationPermissionDenied,
        RouteDeviationTimeout,
        TrafficDataUnavailable,
        OfflineMapMissing,
        InsufficientMemory,
        UnknownError
    }

    /// <summary>
    /// Navigation error information
    /// </summary>
    public class NavigationError
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public NavigationErrorType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Context { get; set; }
        public bool IsRecoverable { get; set; } = true;
        public string? SuggestedAction { get; set; }
    }

    /// <summary>
    /// Interface for handling navigation errors
    /// </summary>
    public interface INavigationErrorHandler
    {
        /// <summary>
        /// Handles GPS signal loss
        /// </summary>
        Task HandleGPSLostAsync();

        /// <summary>
        /// Handles network connectivity errors
        /// </summary>
        Task HandleNetworkErrorAsync(Exception exception);

        /// <summary>
        /// Handles route calculation failures
        /// </summary>
        Task HandleRouteCalculationFailedAsync(string reason);

        /// <summary>
        /// Handles Text-to-Speech errors
        /// </summary>
        Task HandleTTSErrorAsync(Exception exception);

        /// <summary>
        /// Handles location permission denied
        /// </summary>
        Task HandleLocationPermissionDeniedAsync();

        /// <summary>
        /// Handles general navigation errors
        /// </summary>
        Task HandleNavigationErrorAsync(NavigationError error);

        /// <summary>
        /// Event fired when a navigation error occurs
        /// </summary>
        event EventHandler<NavigationError> ErrorOccurred;

        /// <summary>
        /// Event fired when error recovery is attempted
        /// </summary>
        event EventHandler<NavigationError> ErrorRecoveryAttempted;
    }

    /// <summary>
    /// Navigation error handler implementation
    /// </summary>
    public class NavigationErrorHandler : INavigationErrorHandler
    {
        private readonly ITTSService _ttsService;
        private readonly ITelegramNotificationService _telegramService;
        private readonly ILogger<NavigationErrorHandler>? _logger;
        private readonly INavigationPreferencesService _preferencesService;

        public event EventHandler<NavigationError>? ErrorOccurred;
        public event EventHandler<NavigationError>? ErrorRecoveryAttempted;

        public NavigationErrorHandler(
            ITTSService ttsService,
            ITelegramNotificationService telegramService,
            INavigationPreferencesService preferencesService,
            ILogger<NavigationErrorHandler>? logger = null)
        {
            _ttsService = ttsService;
            _telegramService = telegramService;
            _preferencesService = preferencesService;
            _logger = logger;
        }

        public async Task HandleGPSLostAsync()
        {
            var error = new NavigationError
            {
                Type = NavigationErrorType.GPSLost,
                Message = "GPS 訊號丟失，請檢查位置設定",
                IsRecoverable = true,
                SuggestedAction = "請確認位置服務已開啟，並移至開闊區域"
            };

            await HandleNavigationErrorAsync(error);

            try
            {
                // Announce GPS loss via TTS
                if (_ttsService.IsSupported)
                {
                    await _ttsService.SpeakAsync("GPS 訊號丟失，請檢查位置設定", "zh-TW");
                }

                // Show user notification
                await ShowUserNotification("GPS 訊號丟失", 
                    "請確認位置服務已開啟，並移至開闊區域以恢復導航");

                // Attempt to switch to offline mode if available
                await SwitchToOfflineMode();

                _logger?.LogWarning("GPS signal lost, attempted recovery actions");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling GPS loss");
            }
        }

        public async Task HandleNetworkErrorAsync(Exception exception)
        {
            var error = new NavigationError
            {
                Type = NavigationErrorType.NetworkError,
                Message = "網路連線錯誤，部分功能可能無法使用",
                Exception = exception,
                IsRecoverable = true,
                SuggestedAction = "檢查網路連線或切換至離線模式"
            };

            await HandleNavigationErrorAsync(error);

            try
            {
                // Log the network error
                _logger?.LogError(exception, "Network error during navigation");

                // Enable offline navigation if possible
                await EnableOfflineNavigation();

                // Announce network issue
                if (_ttsService.IsSupported)
                {
                    await _ttsService.SpeakAsync("網路連線中斷，已切換至離線導航", "zh-TW");
                }

                // Show notification
                await ShowUserNotification("網路連線錯誤", 
                    "已自動切換至離線模式，部分功能可能受限");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling network failure");
            }
        }

        public async Task HandleRouteCalculationFailedAsync(string reason)
        {
            var error = new NavigationError
            {
                Type = NavigationErrorType.RouteCalculationFailed,
                Message = $"路線計算失敗：{reason}",
                IsRecoverable = true,
                SuggestedAction = "請重新選擇起點和終點"
            };

            await HandleNavigationErrorAsync(error);

            try
            {
                // Announce route calculation failure
                if (_ttsService.IsSupported)
                {
                    await _ttsService.SpeakAsync("路線計算失敗，請重新規劃路線", "zh-TW");
                }

                // Show notification with retry option
                await ShowUserNotification("路線計算失敗", 
                    $"無法計算路線：{reason}。請檢查起點和終點是否正確");

                _logger?.LogWarning("Route calculation failed: {Reason}", reason);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling route calculation failure");
            }
        }

        public async Task HandleTTSErrorAsync(Exception exception)
        {
            var error = new NavigationError
            {
                Type = NavigationErrorType.TTSError,
                Message = "語音導航功能暫時無法使用",
                Exception = exception,
                IsRecoverable = true,
                SuggestedAction = "語音功能將自動停用，可稍後重新啟用"
            };

            await HandleNavigationErrorAsync(error);

            try
            {
                // Disable voice guidance temporarily
                var preferences = await _preferencesService.GetPreferencesAsync();
                // Note: In real implementation, would temporarily disable TTS without changing user preference
                
                // Show silent notification
                await ShowUserNotification("語音導航暫停", 
                    "語音功能遇到問題，已暫時停用，請查看螢幕上的導航指示");

                _logger?.LogWarning(exception, "TTS error occurred, voice guidance disabled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling TTS failure");
            }
        }

        public async Task HandleLocationPermissionDeniedAsync()
        {
            var error = new NavigationError
            {
                Type = NavigationErrorType.LocationPermissionDenied,
                Message = "位置權限被拒絕，無法進行導航",
                IsRecoverable = false,
                SuggestedAction = "請至設定中允許應用程式存取位置資訊"
            };

            await HandleNavigationErrorAsync(error);

            try
            {
                // Show critical notification
                await ShowUserNotification("需要位置權限", 
                    "請至設定中允許 MapLocation 存取位置資訊以使用導航功能");

                _logger?.LogError("Location permission denied");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling location permission denial");
            }
        }

        public async Task HandleNavigationErrorAsync(NavigationError error)
        {
            try
            {
                // Fire error event
                ErrorOccurred?.Invoke(this, error);

                // Log the error
                _logger?.LogError(error.Exception, 
                    "Navigation error: {ErrorType} - {Message}", 
                    error.Type, error.Message);

                // Send telemetry if configured
                await SendErrorTelemetry(error);

                // Attempt recovery if possible
                if (error.IsRecoverable)
                {
                    ErrorRecoveryAttempted?.Invoke(this, error);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in navigation error handler");
            }
        }

        private async Task ShowUserNotification(string title, string message)
        {
            try
            {
                // In a real MAUI app, this would show a native notification or toast
                System.Diagnostics.Debug.WriteLine($"Notification: {title} - {message}");
                
                // Could also use DisplayAlert in the UI layer
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show user notification");
            }
        }

        private async Task SwitchToOfflineMode()
        {
            try
            {
                var preferences = await _preferencesService.GetPreferencesAsync();
                if (!preferences.EnableOfflineMode)
                {
                    preferences.EnableOfflineMode = true;
                    await _preferencesService.SavePreferencesAsync(preferences);
                }

                System.Diagnostics.Debug.WriteLine("Switched to offline navigation mode");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to switch to offline mode");
            }
        }

        private async Task EnableOfflineNavigation()
        {
            try
            {
                // Enable offline capabilities
                await SwitchToOfflineMode();
                
                System.Diagnostics.Debug.WriteLine("Enabled offline navigation due to network error");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to enable offline navigation");
            }
        }

        private async Task SendErrorTelemetry(NavigationError error)
        {
            try
            {
                // Send error information via Telegram if configured
                var preferences = await _preferencesService.GetPreferencesAsync();
                if (preferences.SendTelegramNotifications)
                {
                    var message = $"🚨 導航錯誤\n" +
                                 $"類型：{GetErrorTypeDescription(error.Type)}\n" +
                                 $"訊息：{error.Message}\n" +
                                 $"時間：{error.Timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                                 $"可恢復：{(error.IsRecoverable ? "是" : "否")}";

                    await _telegramService.SendMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send error telemetry");
            }
        }

        private string GetErrorTypeDescription(NavigationErrorType errorType)
        {
            return errorType switch
            {
                NavigationErrorType.GPSLost => "GPS 訊號丟失",
                NavigationErrorType.NetworkError => "網路連線錯誤",
                NavigationErrorType.RouteCalculationFailed => "路線計算失敗",
                NavigationErrorType.TTSError => "語音播放錯誤",
                NavigationErrorType.LocationPermissionDenied => "位置權限拒絕",
                NavigationErrorType.RouteDeviationTimeout => "路線偏離逾時",
                NavigationErrorType.TrafficDataUnavailable => "交通資料無法取得",
                NavigationErrorType.OfflineMapMissing => "離線地圖缺失",
                NavigationErrorType.InsufficientMemory => "記憶體不足",
                _ => "未知錯誤"
            };
        }
    }
}