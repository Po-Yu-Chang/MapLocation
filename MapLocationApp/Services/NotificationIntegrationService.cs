using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    public interface INotificationIntegrationService
    {
        Task InitializeAsync();
        void Dispose();
    }

    public class NotificationIntegrationService : INotificationIntegrationService, IDisposable
    {
        private readonly IGeofenceService _geofenceService;
        private readonly ITeamLocationService _teamLocationService;
        private readonly ITelegramNotificationService _telegramService;
        private readonly CheckInStorageService _checkInService;
        private bool _isInitialized = false;

        public NotificationIntegrationService(
            IGeofenceService geofenceService,
            ITeamLocationService teamLocationService,
            ITelegramNotificationService telegramService,
            CheckInStorageService checkInService)
        {
            _geofenceService = geofenceService;
            _teamLocationService = teamLocationService;
            _telegramService = telegramService;
            _checkInService = checkInService;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                // 訂閱地理圍欄事件
                _geofenceService.GeofenceEntered += OnGeofenceEntered;
                _geofenceService.GeofenceExited += OnGeofenceExited;

                // 訂閱團隊位置事件
                _teamLocationService.LocationShared += OnTeamLocationShared;

                // 訂閱打卡事件
                _checkInService.CheckInRecorded += OnCheckInRecorded;

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("通知整合服務已初始化");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知整合服務初始化錯誤: {ex.Message}");
            }
        }

        private async void OnGeofenceEntered(object? sender, GeofenceEvent e)
        {
            try
            {
                if (await _telegramService.IsConfiguredAsync())
                {
                    var userName = Preferences.Get("UserName", "使用者");
                    await _telegramService.SendGeofenceNotificationAsync(
                        userName, 
                        e.GeofenceName, 
                        true
                    );
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"地理圍欄進入通知錯誤: {ex.Message}");
            }
        }

        private async void OnGeofenceExited(object? sender, GeofenceEvent e)
        {
            try
            {
                if (await _telegramService.IsConfiguredAsync())
                {
                    var userName = Preferences.Get("UserName", "使用者");
                    await _telegramService.SendGeofenceNotificationAsync(
                        userName, 
                        e.GeofenceName, 
                        false
                    );
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"地理圍欄離開通知錯誤: {ex.Message}");
            }
        }

        private async void OnTeamLocationShared(object? sender, TeamLocationEventArgs e)
        {
            try
            {
                if (await _telegramService.IsConfiguredAsync())
                {
                    await _telegramService.SendTeamLocationUpdateAsync(
                        e.TeamName,
                        e.UserName,
                        e.Latitude,
                        e.Longitude
                    );
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"團隊位置分享通知錯誤: {ex.Message}");
            }
        }

        private async void OnCheckInRecorded(object? sender, CheckInEventArgs e)
        {
            try
            {
                if (await _telegramService.IsConfiguredAsync())
                {
                    var userName = Preferences.Get("UserName", "使用者");
                    await _telegramService.SendCheckInNotificationAsync(
                        userName,
                        e.Latitude,
                        e.Longitude,
                        e.CheckInTime
                    );
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打卡通知錯誤: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isInitialized)
            {
                _geofenceService.GeofenceEntered -= OnGeofenceEntered;
                _geofenceService.GeofenceExited -= OnGeofenceExited;
                _teamLocationService.LocationShared -= OnTeamLocationShared;
                _checkInService.CheckInRecorded -= OnCheckInRecorded;
                _isInitialized = false;
            }
        }
    }

    // 事件參數類別
    public class CheckInEventArgs : EventArgs
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime CheckInTime { get; set; }
        public string? Note { get; set; }
    }

    public class TeamLocationEventArgs : EventArgs
    {
        public string TeamName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime UpdateTime { get; set; }
    }
}