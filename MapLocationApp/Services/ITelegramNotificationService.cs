using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MapLocationApp.Services
{
    public interface ITelegramNotificationService
    {
        Task<bool> InitializeAsync(string botToken, string chatId);
        Task<bool> SendMessageAsync(string message);
        Task<bool> SendLocationAsync(double latitude, double longitude, string locationName = null);
        Task<bool> SendCheckInNotificationAsync(string userName, double latitude, double longitude, DateTime checkInTime);
        Task<bool> SendGeofenceNotificationAsync(string userName, string geofenceName, bool isEntering);
        Task<bool> SendTeamLocationUpdateAsync(string teamName, string userName, double latitude, double longitude);
        Task<bool> SendRouteNotificationAsync(string userName, string routeName, double startLat, double startLng, double endLat, double endLng);
        Task<bool> IsConfiguredAsync();
        Task<bool> TestConnectionAsync();
    }
}