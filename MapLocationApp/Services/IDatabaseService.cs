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

        // Aliases for test compatibility
        Task<bool> SaveCheckInAsync(CheckInRecord record) => SaveCheckInRecordAsync(record);
        Task<bool> UpdateCheckInAsync(CheckInRecord record) => UpdateCheckInRecordAsync(record);
        
        Task<bool> TestConnectionAsync();
        Task<bool> InitializeDatabaseAsync();
    }

    public class LoginResult
    {
        public bool Success { get; set; }
        public User? User { get; set; }
        public string? ErrorMessage { get; set; }
    }
}