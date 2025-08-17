using System;
using System.Threading.Tasks;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Interface for handling destination arrival events
    /// </summary>
    public interface IDestinationArrivalService
    {
        /// <summary>
        /// Checks if the user has arrived at the destination
        /// </summary>
        Task<bool> CheckArrivalAsync(AppLocation currentLocation, AppLocation destination);

        /// <summary>
        /// Handles the arrival experience when destination is reached
        /// </summary>
        Task HandleArrivalAsync(NavigationSession session);

        /// <summary>
        /// Sets the arrival threshold distance
        /// </summary>
        void SetArrivalThreshold(double meters);

        /// <summary>
        /// Event fired when arrival is detected
        /// </summary>
        event EventHandler<ArrivalEventArgs> ArrivalDetected;
    }

    /// <summary>
    /// Event arguments for arrival detection
    /// </summary>
    public class ArrivalEventArgs : EventArgs
    {
        public AppLocation CurrentLocation { get; set; } = new();
        public AppLocation Destination { get; set; } = new();
        public double DistanceToDestination { get; set; }
        public NavigationSession? Session { get; set; }
        public DateTime ArrivalTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Service for handling destination arrival experience
    /// </summary>
    public class DestinationArrivalService : IDestinationArrivalService
    {
        private const double DEFAULT_ARRIVAL_THRESHOLD = 20; // meters
        
        private double _arrivalThreshold = DEFAULT_ARRIVAL_THRESHOLD;
        private readonly ITTSService _ttsService;
        private readonly ITelegramNotificationService _telegramService;
        
        public event EventHandler<ArrivalEventArgs>? ArrivalDetected;

        public DestinationArrivalService(
            ITTSService ttsService,
            ITelegramNotificationService telegramService)
        {
            _ttsService = ttsService;
            _telegramService = telegramService;
        }

        public async Task<bool> CheckArrivalAsync(AppLocation currentLocation, AppLocation destination)
        {
            try
            {
                var distance = CalculateDistance(currentLocation, destination);
                
                if (distance <= _arrivalThreshold)
                {
                    var arrivalArgs = new ArrivalEventArgs
                    {
                        CurrentLocation = currentLocation,
                        Destination = destination,
                        DistanceToDestination = distance,
                        ArrivalTime = DateTime.Now
                    };

                    ArrivalDetected?.Invoke(this, arrivalArgs);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Check arrival error: {ex.Message}");
                return false;
            }
        }

        public async Task HandleArrivalAsync(NavigationSession session)
        {
            try
            {
                // 1. Play arrival sound and announcement
                await PlayArrivalAnnouncement();

                // 2. Trigger device vibration
                await TriggerVibration();

                // 3. Show arrival notification
                await ShowArrivalNotification(session);

                // 4. Suggest nearby services (parking, etc.)
                await SuggestNearbyServices(session);

                // 5. Save navigation history
                await SaveNavigationHistory(session);

                // 6. Send Telegram notification if configured
                await SendTelegramNotification(session);

                System.Diagnostics.Debug.WriteLine("Arrival handling completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Handle arrival error: {ex.Message}");
            }
        }

        public void SetArrivalThreshold(double meters)
        {
            _arrivalThreshold = Math.Max(5, Math.Min(meters, 100)); // Clamp between 5-100 meters
        }

        private async Task PlayArrivalAnnouncement()
        {
            try
            {
                // Play arrival sound effect (would be implemented with actual audio in MAUI)
                await SimulateAudioPlayback("arrival_sound.mp3");

                // Speak arrival message
                if (_ttsService.IsSupported)
                {
                    await _ttsService.SpeakAsync("您已到達目的地", "zh-TW");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Play arrival announcement error: {ex.Message}");
            }
        }

        private async Task TriggerVibration()
        {
            try
            {
                // In a real MAUI app, this would use Microsoft.Maui.Essentials.Vibration
                // await Vibration.VibrateAsync(TimeSpan.FromMilliseconds(500));
                
                // Simulate vibration for now
                await Task.Delay(100);
                System.Diagnostics.Debug.WriteLine("Triggered arrival vibration");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
            }
        }

        private async Task ShowArrivalNotification(NavigationSession session)
        {
            try
            {
                // In a real MAUI app, this would show a native notification
                var notification = new
                {
                    Title = "導航完成",
                    Message = $"您已到達 {session.Route?.ToAddress ?? "目的地"}",
                    Timestamp = DateTime.Now
                };

                System.Diagnostics.Debug.WriteLine($"Arrival notification: {notification.Title} - {notification.Message}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Show notification error: {ex.Message}");
            }
        }

        private async Task SuggestNearbyServices(NavigationSession session)
        {
            try
            {
                if (session.Route == null) return;

                // Simulate finding nearby parking and services
                var suggestions = new[]
                {
                    "🅿️ 附近停車場：距離100公尺",
                    "⛽ 加油站：距離200公尺", 
                    "🏪 便利商店：距離50公尺",
                    "🍽️ 餐廳：距離150公尺"
                };

                foreach (var suggestion in suggestions)
                {
                    System.Diagnostics.Debug.WriteLine($"Nearby service: {suggestion}");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Suggest nearby services error: {ex.Message}");
            }
        }

        private async Task SaveNavigationHistory(NavigationSession session)
        {
            try
            {
                if (session.Route == null) return;

                // Create navigation history record
                var historyRecord = new NavigationHistoryRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    RouteId = session.RouteId,
                    RouteName = session.Route.Name,
                    StartAddress = session.Route.FromAddress,
                    EndAddress = session.Route.ToAddress,
                    StartTime = session.StartTime,
                    EndTime = session.EndTime ?? DateTime.Now,
                    ActualDuration = (session.EndTime ?? DateTime.Now) - session.StartTime,
                    PlannedDuration = session.Route.EstimatedDuration,
                    Distance = session.Route.Distance,
                    TransportMode = session.Route.Type.ToString(),
                    CompletedSuccessfully = true
                };

                // In a real implementation, save to local storage or database
                await SaveToNavigationHistory(historyRecord);
                
                System.Diagnostics.Debug.WriteLine($"Saved navigation history: {historyRecord.RouteName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save navigation history error: {ex.Message}");
            }
        }

        private async Task SendTelegramNotification(NavigationSession session)
        {
            try
            {
                if (session.Route == null) return;

                var message = $"🏁 導航完成\n" +
                             $"目的地：{session.Route.ToAddress}\n" +
                             $"總距離：{session.Route.Distance / 1000:F1} 公里\n" +
                             $"實際時間：{FormatDuration((session.EndTime ?? DateTime.Now) - session.StartTime)}\n" +
                             $"完成時間：{DateTime.Now:yyyy-MM-dd HH:mm}";

                await _telegramService.SendMessageAsync(message);
                System.Diagnostics.Debug.WriteLine("Sent Telegram arrival notification");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Send Telegram notification error: {ex.Message}");
            }
        }

        private double CalculateDistance(AppLocation location1, AppLocation location2)
        {
            const double earthRadius = 6371000; // Earth radius in meters

            var lat1Rad = location1.Latitude * Math.PI / 180;
            var lat2Rad = location2.Latitude * Math.PI / 180;
            var deltaLatRad = (location2.Latitude - location1.Latitude) * Math.PI / 180;
            var deltaLngRad = (location2.Longitude - location1.Longitude) * Math.PI / 180;

            var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadius * c;
        }

        private async Task SimulateAudioPlayback(string audioFile)
        {
            // Simulate audio playback delay
            await Task.Delay(1000);
            System.Diagnostics.Debug.WriteLine($"Played audio: {audioFile}");
        }

        private async Task SaveToNavigationHistory(NavigationHistoryRecord record)
        {
            // In a real implementation, this would save to local database
            await Task.Delay(100);
            System.Diagnostics.Debug.WriteLine($"Saved navigation record: {record.Id}");
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return $"{(int)duration.TotalHours} 小時 {duration.Minutes} 分鐘";
            }
            else
            {
                return $"{duration.Minutes} 分鐘";
            }
        }
    }

    /// <summary>
    /// Navigation history record for tracking completed trips
    /// </summary>
    public class NavigationHistoryRecord
    {
        public string Id { get; set; } = string.Empty;
        public string RouteId { get; set; } = string.Empty;
        public string RouteName { get; set; } = string.Empty;
        public string StartAddress { get; set; } = string.Empty;
        public string EndAddress { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan ActualDuration { get; set; }
        public TimeSpan PlannedDuration { get; set; }
        public double Distance { get; set; } // meters
        public string TransportMode { get; set; } = string.Empty;
        public bool CompletedSuccessfully { get; set; }
    }
}