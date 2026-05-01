using MapLocationApp.Models;
using SQLite;
using System.Text.Json;

namespace MapLocationApp.Services
{
    public class SqliteConfigService : IConfigService
    {
        private readonly string _dbPath;
        private SQLiteAsyncConnection? _database;

        public SqliteConfigService()
        {
            _dbPath = Path.Combine(FileSystem.AppDataDirectory, "config.db3");
        }

        private async Task<SQLiteAsyncConnection> GetDatabaseAsync()
        {
            if (_database == null)
            {
                _database = new SQLiteAsyncConnection(_dbPath);
                await _database.CreateTableAsync<ConfigItem>();
            }
            return _database;
        }

        public async Task<DatabaseConfig> GetDatabaseConfigAsync()
        {
            try
            {
                var db = await GetDatabaseAsync();
                var configItem = await db.Table<ConfigItem>()
                    .Where(x => x.Key == "DatabaseConfig")
                    .FirstOrDefaultAsync();

                if (configItem != null && !string.IsNullOrEmpty(configItem.Value))
                {
                    var config = JsonSerializer.Deserialize<DatabaseConfig>(configItem.Value);
                    if (config != null)
                        return config;
                }

                return new DatabaseConfig
                {
                    Host = "127.0.0.1",
                    Port = 3306,
                    DatabaseName = "MapLocation",
                    Username = "root",
                    Password = ""
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取得資料庫設定失敗: {ex.Message}");
                return new DatabaseConfig
                {
                    Host = "127.0.0.1",
                    Port = 3306,
                    DatabaseName = "MapLocation",
                    Username = "root",
                    Password = ""
                };
            }
        }

        public async Task<bool> SaveDatabaseConfigAsync(DatabaseConfig config)
        {
            try
            {
                var db = await GetDatabaseAsync();
                var json = JsonSerializer.Serialize(config);
                
                var configItem = await db.Table<ConfigItem>()
                    .Where(x => x.Key == "DatabaseConfig")
                    .FirstOrDefaultAsync();

                if (configItem == null)
                {
                    configItem = new ConfigItem
                    {
                        Key = "DatabaseConfig",
                        Value = json,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    await db.InsertAsync(configItem);
                }
                else
                {
                    configItem.Value = json;
                    configItem.UpdatedAt = DateTime.Now;
                    await db.UpdateAsync(configItem);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"儲存資料庫設定失敗: {ex.Message}");
                return false;
            }
        }

        public async Task<RememberedUser?> GetRememberedUserAsync()
        {
            try
            {
                var db = await GetDatabaseAsync();
                var configItem = await db.Table<ConfigItem>()
                    .Where(x => x.Key == "RememberedUser")
                    .FirstOrDefaultAsync();

                if (configItem != null && !string.IsNullOrEmpty(configItem.Value))
                {
                    return JsonSerializer.Deserialize<RememberedUser>(configItem.Value);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取得記住的使用者失敗: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SaveRememberedUserAsync(RememberedUser user)
        {
            try
            {
                var db = await GetDatabaseAsync();
                var json = JsonSerializer.Serialize(user);
                
                var configItem = await db.Table<ConfigItem>()
                    .Where(x => x.Key == "RememberedUser")
                    .FirstOrDefaultAsync();

                if (configItem == null)
                {
                    configItem = new ConfigItem
                    {
                        Key = "RememberedUser",
                        Value = json,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    await db.InsertAsync(configItem);
                }
                else
                {
                    configItem.Value = json;
                    configItem.UpdatedAt = DateTime.Now;
                    await db.UpdateAsync(configItem);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"儲存記住的使用者失敗: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ClearRememberedUserAsync()
        {
            try
            {
                var db = await GetDatabaseAsync();
                var configItem = await db.Table<ConfigItem>()
                    .Where(x => x.Key == "RememberedUser")
                    .FirstOrDefaultAsync();

                if (configItem != null)
                {
                    await db.DeleteAsync(configItem);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除記住的使用者失敗: {ex.Message}");
                return false;
            }
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            try
            {
                var db = await GetDatabaseAsync();
                var configItem = await db.Table<ConfigItem>()
                    .Where(x => x.Key == "CurrentUser")
                    .FirstOrDefaultAsync();

                if (configItem != null && !string.IsNullOrEmpty(configItem.Value))
                {
                    return JsonSerializer.Deserialize<User>(configItem.Value);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取得當前使用者失敗: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SaveCurrentUserAsync(User user)
        {
            try
            {
                var db = await GetDatabaseAsync();
                var json = JsonSerializer.Serialize(user);
                
                var configItem = await db.Table<ConfigItem>()
                    .Where(x => x.Key == "CurrentUser")
                    .FirstOrDefaultAsync();

                if (configItem == null)
                {
                    configItem = new ConfigItem
                    {
                        Key = "CurrentUser",
                        Value = json,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    await db.InsertAsync(configItem);
                }
                else
                {
                    configItem.Value = json;
                    configItem.UpdatedAt = DateTime.Now;
                    await db.UpdateAsync(configItem);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"儲存當前使用者失敗: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ClearCurrentUserAsync()
        {
            try
            {
                var db = await GetDatabaseAsync();
                var configItem = await db.Table<ConfigItem>()
                    .Where(x => x.Key == "CurrentUser")
                    .FirstOrDefaultAsync();

                if (configItem != null)
                {
                    await db.DeleteAsync(configItem);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除當前使用者失敗: {ex.Message}");
                return false;
            }
        }

        public async Task<AppSettings> GetAppSettingsAsync()
        {
            try
            {
                var db = await GetDatabaseAsync();
                var configItem = await db.Table<ConfigItem>()
                    .Where(x => x.Key == "AppSettings")
                    .FirstOrDefaultAsync();

                if (configItem != null && !string.IsNullOrEmpty(configItem.Value))
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(configItem.Value);
                    if (settings != null)
                        return settings;
                }

                return new AppSettings
                {
                    EnableNotifications = true,
                    EnableGpsTracking = true,
                    AutoCheckOut = false,
                    CheckOutReminderMinutes = 480,
                    Language = "zh-TW",
                    Theme = "Auto"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取得應用程式設定失敗: {ex.Message}");
                return new AppSettings
                {
                    EnableNotifications = true,
                    EnableGpsTracking = true,
                    AutoCheckOut = false,
                    CheckOutReminderMinutes = 480,
                    Language = "zh-TW",
                    Theme = "Auto"
                };
            }
        }

        public async Task<bool> SaveAppSettingsAsync(AppSettings settings)
        {
            try
            {
                var db = await GetDatabaseAsync();
                var json = JsonSerializer.Serialize(settings);
                
                var configItem = await db.Table<ConfigItem>()
                    .Where(x => x.Key == "AppSettings")
                    .FirstOrDefaultAsync();

                if (configItem == null)
                {
                    configItem = new ConfigItem
                    {
                        Key = "AppSettings",
                        Value = json,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    await db.InsertAsync(configItem);
                }
                else
                {
                    configItem.Value = json;
                    configItem.UpdatedAt = DateTime.Now;
                    await db.UpdateAsync(configItem);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"儲存應用程式設定失敗: {ex.Message}");
                return false;
            }
        }
    }

    [Table("ConfigItems")]
    public class ConfigItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [Unique]
        public string Key { get; set; } = string.Empty;
        
        public string Value { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
    }
}