using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    public interface IDatabaseService
    {
        Task<LoginResult> AuthenticateUserAsync(string username, string password);
        Task<bool> CreateUserAsync(User user);
        Task<User?> GetUserByIdAsync(int userId);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<bool> UpdateUserAsync(User user);
        Task<bool> DeleteUserAsync(int userId);
        
        Task<bool> SaveCheckInRecordAsync(CheckInRecord record);
        Task<List<CheckInRecord>> GetCheckInRecordsAsync(int userId, DateTime? date = null);
        Task<CheckInRecord?> GetLatestCheckInAsync(int userId);
        Task<bool> UpdateCheckInRecordAsync(CheckInRecord record);
        Task<bool> DeleteCheckInRecordAsync(string recordId);

        // Geofence persistence
        Task<List<GeofenceRegion>> GetAllGeofencesAsync();
        Task<bool> SaveGeofenceAsync(GeofenceRegion geofence);
        Task<bool> UpdateGeofenceAsync(GeofenceRegion geofence);
        Task<bool> DeleteGeofenceAsync(string geofenceId);

        // Aliases for test compatibility
        Task<bool> SaveCheckInAsync(CheckInRecord record) => SaveCheckInRecordAsync(record);
        Task<bool> UpdateCheckInAsync(CheckInRecord record) => UpdateCheckInRecordAsync(record);
        
        /// <summary>Forces the service to re-read connection settings on next operation.</summary>
        void ResetConnection();
        Task<bool> TestConnectionAsync();
        /// <summary>Tests a connection using the provided credentials without changing saved state.</summary>
        Task<bool> TestConnectionAsync(DatabaseConfig config, string password);
        Task<bool> InitializeDatabaseAsync();
    }

    public class LoginResult
    {
        public bool Success { get; set; }
        public User? User { get; set; }
        public string? ErrorMessage { get; set; }
    }
}