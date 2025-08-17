using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Service for handling destination arrival and related features
    /// </summary>
    public interface IDestinationArrivalService
    {
        /// <summary>
        /// Handles arrival at destination with comprehensive features
        /// </summary>
        Task HandleArrivalAsync(AppLocation currentLocation, AppLocation destination, NavigationSession session);

        /// <summary>
        /// Suggests nearby parking options
        /// </summary>
        Task<List<ParkingOption>> SuggestParkingAsync(AppLocation destination);

        /// <summary>
        /// Saves navigation history for analytics
        /// </summary>
        Task SaveNavigationHistoryAsync(NavigationSession session);

        /// <summary>
        /// Gets navigation statistics
        /// </summary>
        Task<NavigationStatistics> GetNavigationStatisticsAsync();
    }

    /// <summary>
    /// Destination arrival service implementation
    /// </summary>
    public class DestinationArrivalService : IDestinationArrivalService
    {
        private readonly ITTSService _ttsService;
        private readonly ITelegramNotificationService _telegramService;
        private readonly string _navigationHistoryFile;

        public DestinationArrivalService(
            ITTSService ttsService = null,
            ITelegramNotificationService telegramService = null)
        {
            _ttsService = ttsService;
            _telegramService = telegramService;
            _navigationHistoryFile = Path.Combine(FileSystem.AppDataDirectory, "navigation_history.json");
        }

        public async Task HandleArrivalAsync(AppLocation currentLocation, AppLocation destination, NavigationSession session)
        {
            try
            {
                // Play arrival sound effect
                await PlayArrivalSoundAsync();

                // Vibrate device
                await VibrateDeviceAsync(TimeSpan.FromMilliseconds(1000));

                // Voice announcement
                if (_ttsService != null)
                {
                    await _ttsService.SpeakAsync("您已到達目的地，導航結束。感謝您的使用！", "zh-TW");
                }

                // Send Telegram notification
                if (_telegramService != null)
                {
                    var totalTime = DateTime.Now - session.StartTime;
                    await _telegramService.SendDestinationArrivedNotificationAsync(
                        $"目的地：{session.Route?.ToAddress ?? "未知位置"}\n" +
                        $"導航時間：{totalTime.TotalMinutes:F0} 分鐘\n" +
                        $"距離：{session.Route?.Distance / 1000:F1} 公里");
                }

                // Save navigation history
                await SaveNavigationHistoryAsync(session);

                // Show parking suggestions if available
                var parkingOptions = await SuggestParkingAsync(destination);
                if (parkingOptions?.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Found {parkingOptions.Count} parking options nearby");
                }

                System.Diagnostics.Debug.WriteLine("Destination arrival handling completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling destination arrival: {ex.Message}");
            }
        }

        public async Task<List<ParkingOption>> SuggestParkingAsync(AppLocation destination)
        {
            try
            {
                // Mock implementation - in a real app, this would query parking APIs
                var parkingOptions = new List<ParkingOption>
                {
                    new ParkingOption
                    {
                        Name = "附近停車場 A",
                        Latitude = destination.Latitude + 0.001,
                        Longitude = destination.Longitude + 0.001,
                        Distance = 100, // meters
                        Type = ParkingType.PublicParking,
                        HourlyRate = 30,
                        IsAvailable = true,
                        TotalSpaces = 50,
                        AvailableSpaces = 12
                    },
                    new ParkingOption
                    {
                        Name = "路邊停車格",
                        Latitude = destination.Latitude + 0.0005,
                        Longitude = destination.Longitude - 0.0005,
                        Distance = 80,
                        Type = ParkingType.StreetParking,
                        HourlyRate = 20,
                        IsAvailable = true,
                        TotalSpaces = 10,
                        AvailableSpaces = 3
                    }
                };

                await Task.Delay(100); // Simulate API call
                return parkingOptions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting parking suggestions: {ex.Message}");
                return new List<ParkingOption>();
            }
        }

        public async Task SaveNavigationHistoryAsync(NavigationSession session)
        {
            try
            {
                var historyEntry = new NavigationHistoryEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    StartTime = session.StartTime,
                    EndTime = session.EndTime ?? DateTime.Now,
                    Route = session.Route,
                    WasCompleted = true,
                    TotalDistance = session.Route?.Distance ?? 0,
                    AverageSpeed = CalculateAverageSpeed(session),
                    UsedVoiceGuidance = true // This could be from preferences
                };

                // Load existing history
                var history = await LoadNavigationHistoryAsync();
                history.Add(historyEntry);

                // Keep only last 100 entries
                if (history.Count > 100)
                {
                    history.RemoveRange(0, history.Count - 100);
                }

                // Save to file
                var json = System.Text.Json.JsonSerializer.Serialize(history, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_navigationHistoryFile, json);

                System.Diagnostics.Debug.WriteLine($"Navigation history saved: {historyEntry.Id}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving navigation history: {ex.Message}");
            }
        }

        public async Task<NavigationStatistics> GetNavigationStatisticsAsync()
        {
            try
            {
                var history = await LoadNavigationHistoryAsync();
                
                if (history.Count == 0)
                {
                    return new NavigationStatistics();
                }

                var completedTrips = history.Where(h => h.WasCompleted).ToList();
                var totalDistance = completedTrips.Sum(h => h.TotalDistance);
                var totalTime = completedTrips.Sum(h => (h.EndTime - h.StartTime).TotalMinutes);

                return new NavigationStatistics
                {
                    TotalTrips = completedTrips.Count,
                    TotalDistanceKm = totalDistance / 1000.0,
                    TotalTimeMinutes = totalTime,
                    AverageSpeedKmh = totalDistance > 0 ? (totalDistance / 1000.0) / (totalTime / 60.0) : 0,
                    MostUsedRouteType = GetMostUsedRouteType(completedTrips),
                    VoiceGuidanceUsagePercent = GetVoiceGuidanceUsage(completedTrips)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting navigation statistics: {ex.Message}");
                return new NavigationStatistics();
            }
        }

        private async Task PlayArrivalSoundAsync()
        {
            try
            {
                // In a real implementation, play a custom arrival sound
                // For now, we'll just log it
                await Task.Delay(100);
                System.Diagnostics.Debug.WriteLine("Playing arrival sound effect");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing arrival sound: {ex.Message}");
            }
        }

        private async Task VibrateDeviceAsync(TimeSpan duration)
        {
            try
            {
                Vibration.Default.Vibrate(duration);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
            }
        }

        private async Task<List<NavigationHistoryEntry>> LoadNavigationHistoryAsync()
        {
            try
            {
                if (File.Exists(_navigationHistoryFile))
                {
                    var json = await File.ReadAllTextAsync(_navigationHistoryFile);
                    return System.Text.Json.JsonSerializer.Deserialize<List<NavigationHistoryEntry>>(json) ?? new List<NavigationHistoryEntry>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading navigation history: {ex.Message}");
            }

            return new List<NavigationHistoryEntry>();
        }

        private double CalculateAverageSpeed(NavigationSession session)
        {
            if (session.Route == null || session.EndTime == null)
                return 0;

            var totalTimeHours = (session.EndTime.Value - session.StartTime).TotalHours;
            var totalDistanceKm = session.Route.Distance / 1000.0;

            return totalTimeHours > 0 ? totalDistanceKm / totalTimeHours : 0;
        }

        private RouteType GetMostUsedRouteType(List<NavigationHistoryEntry> history)
        {
            if (history.Count == 0)
                return RouteType.Driving;

            return history
                .GroupBy(h => h.Route?.Type ?? RouteType.Driving)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;
        }

        private double GetVoiceGuidanceUsage(List<NavigationHistoryEntry> history)
        {
            if (history.Count == 0)
                return 0;

            var voiceGuidanceCount = history.Count(h => h.UsedVoiceGuidance);
            return (double)voiceGuidanceCount / history.Count * 100;
        }
    }
}

namespace MapLocationApp.Models
{
    /// <summary>
    /// Parking option information
    /// </summary>
    public class ParkingOption
    {
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Distance { get; set; } // meters from destination
        public ParkingType Type { get; set; }
        public decimal HourlyRate { get; set; }
        public bool IsAvailable { get; set; }
        public int TotalSpaces { get; set; }
        public int AvailableSpaces { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsRecommended { get; set; }
    }

    /// <summary>
    /// Types of parking
    /// </summary>
    public enum ParkingType
    {
        PublicParking,
        StreetParking,
        PrivateParking,
        FreeParking,
        PaidParking
    }

    /// <summary>
    /// Navigation history entry
    /// </summary>
    public class NavigationHistoryEntry
    {
        public string Id { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Route Route { get; set; }
        public bool WasCompleted { get; set; }
        public double TotalDistance { get; set; } // meters
        public double AverageSpeed { get; set; } // km/h
        public bool UsedVoiceGuidance { get; set; }
    }

    /// <summary>
    /// Navigation usage statistics
    /// </summary>
    public class NavigationStatistics
    {
        public int TotalTrips { get; set; }
        public double TotalDistanceKm { get; set; }
        public double TotalTimeMinutes { get; set; }
        public double AverageSpeedKmh { get; set; }
        public RouteType MostUsedRouteType { get; set; }
        public double VoiceGuidanceUsagePercent { get; set; }
    }
}