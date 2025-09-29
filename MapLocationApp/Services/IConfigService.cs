using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    public interface IConfigService
    {
        Task<DatabaseConfig> GetDatabaseConfigAsync();
        Task<bool> SaveDatabaseConfigAsync(DatabaseConfig config);
        
        Task<RememberedUser?> GetRememberedUserAsync();
        Task<bool> SaveRememberedUserAsync(RememberedUser user);
        Task<bool> ClearRememberedUserAsync();
        
        Task<User?> GetCurrentUserAsync();
        Task<bool> SaveCurrentUserAsync(User user);
        Task<bool> ClearCurrentUserAsync();
        
        Task<AppSettings> GetAppSettingsAsync();
        Task<bool> SaveAppSettingsAsync(AppSettings settings);
    }
}