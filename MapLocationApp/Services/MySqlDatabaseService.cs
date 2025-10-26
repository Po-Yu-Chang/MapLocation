using MapLocationApp.Models;
using MapLocationApp.Services;
using MapLocationApp.Services.Interfaces;
using MySql.Data.MySqlClient;
using System.Security.Cryptography;
using System.Text;

namespace MapLocationApp.Services
{
    public class MySqlDatabaseService : IDatabaseService
    {
        private readonly IConfigService _configService;
        private readonly ISecureConfigService _secureConfig;
        private string? _connectionString;
        private bool _isInitialized;

        public MySqlDatabaseService(IConfigService configService, ISecureConfigService secureConfig)
        {
            _configService = configService;
            _secureConfig = secureConfig;
            _isInitialized = false;
        }

        private async Task InitializeConnectionStringAsync()
        {
            if (_isInitialized && !string.IsNullOrEmpty(_connectionString))
                return;

            try
            {
                var config = await _configService.GetDatabaseConfigAsync();
                var password = await _secureConfig.GetDatabasePasswordAsync();

                if (string.IsNullOrEmpty(password))
                {
                    System.Diagnostics.Debug.WriteLine("資料庫密碼未在 SecureStorage 中設定。請透過設定頁面配置密碼。");
                    throw new InvalidOperationException("Database password not configured in SecureStorage. Please configure via settings.");
                }

                // T014: Enable SSL/TLS for MySQL connections
                _connectionString = $"Server={config.Host};Port={config.Port};Database={config.DatabaseName};Uid={config.Username};Pwd={password};SslMode=Required;Connection Timeout=30;Command Timeout=60;Default Command Timeout=60;";
                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("資料庫連線字串已成功初始化 (使用 SSL/TLS)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化資料庫連線字串失敗: {ex.Message}");
                _isInitialized = false;
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await InitializeConnectionStringAsync();

                if (string.IsNullOrEmpty(_connectionString))
                    return false;

                using var connection = new MySqlConnection(_connectionString);
                
               
                
                // 測試連線
                await connection.OpenAsync();
                
                // 執行簡單查詢測試
                using var command = new MySqlCommand("SELECT 1", connection);
                command.CommandTimeout = 10;
                var result = await command.ExecuteScalarAsync();
                
                return result != null;
            }
            catch (MySqlException mysqlEx)
            {
                System.Diagnostics.Debug.WriteLine($"MySQL 連線錯誤: {mysqlEx.Message} (錯誤碼: {mysqlEx.Number})");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"資料庫連線測試失敗: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> InitializeDatabaseAsync()
        {
            try
            {
                await InitializeConnectionStringAsync();

                if (string.IsNullOrEmpty(_connectionString))
                    return false;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var createUsersTable = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        Username VARCHAR(50) UNIQUE NOT NULL,
                        PasswordHash VARCHAR(255) NOT NULL,
                        Email VARCHAR(100),
                        FullName VARCHAR(100),
                        Department VARCHAR(50),
                        Position VARCHAR(50),
                        IsActive BOOLEAN DEFAULT TRUE,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                        LastLoginAt DATETIME
                    )";

                var createCheckInRecordsTable = @"
                    CREATE TABLE IF NOT EXISTS CheckInRecords (
                        Id VARCHAR(36) PRIMARY KEY,
                        UserId INT NOT NULL,
                        GeofenceId VARCHAR(36),
                        GeofenceName VARCHAR(100),
                        CheckInTime DATETIME NOT NULL,
                        CheckOutTime DATETIME,
                        Latitude DOUBLE NOT NULL,
                        Longitude DOUBLE NOT NULL,
                        Notes TEXT,
                        Type ENUM('Manual', 'Automatic') DEFAULT 'Manual',
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                        INDEX idx_user_date (UserId, CheckInTime),
                        INDEX idx_geofence (GeofenceId)
                    )";

                using var command1 = new MySqlCommand(createUsersTable, connection);
                await command1.ExecuteNonQueryAsync();

                using var command2 = new MySqlCommand(createCheckInRecordsTable, connection);
                await command2.ExecuteNonQueryAsync();

                await CreateDefaultAdminUser(connection);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化資料庫失敗: {ex.Message}");
                return false;
            }
        }

        private async Task CreateDefaultAdminUser(MySqlConnection connection)
        {
            try
            {
                var checkUserQuery = "SELECT COUNT(*) FROM users WHERE username = @username";
                using var checkCommand = new MySqlCommand(checkUserQuery, connection);
                checkCommand.Parameters.AddWithValue("@username", "admin");
                
                var userCount = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                
                if (userCount == 0)
                {
                    var adminUser = new User
                    {
                        Username = "admin",
                        Password = "admin123",
                        Email = "admin@maplocation.com",
                        FullName = "系統管理員",
                        Department = "IT",
                        Position = "管理員",
                        IsActive = true
                    };
                    
                    await CreateUserInternalAsync(connection, adminUser);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"建立預設管理員帳號失敗: {ex.Message}");
            }
        }

        public async Task<LoginResult> AuthenticateUserAsync(string username, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(_connectionString))
                {
                    return new LoginResult 
                    { 
                        Success = false, 
                        ErrorMessage = "資料庫連線未設定" 
                    };
                }

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT id, username, password, email, full_name, department, position, is_active, created_at, last_login_at
                    FROM users 
                    WHERE username = @username AND is_active = TRUE";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);

                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    var storedPasswordHash = reader["password"].ToString();
                    var inputPasswordHash = password;

                    if (storedPasswordHash == inputPasswordHash)
                    {
                        var user = new User
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            Username = reader["username"].ToString() ?? string.Empty,
                            Email = reader["email"] as string,
                            FullName = reader["full_name"] as string,
                            Department = reader["department"] as string,
                            Position = reader["position"] as string,
                            IsActive = Convert.ToBoolean(reader["is_active"]),
                            CreatedAt = Convert.ToDateTime(reader["created_at"]),
                            LastLoginAt = reader["last_login_at"] as DateTime?
                        };

                        reader.Close();
                        await UpdateLastLoginAsync(connection, user.Id);

                        return new LoginResult 
                        { 
                            Success = true, 
                            User = user 
                        };
                    }
                }

                return new LoginResult 
                { 
                    Success = false, 
                    ErrorMessage = "帳號或密碼錯誤" 
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"使用者認證失敗: {ex.Message}");
                return new LoginResult 
                { 
                    Success = false, 
                    ErrorMessage = "登入過程發生錯誤" 
                };
            }
        }

        private async Task UpdateLastLoginAsync(MySqlConnection connection, int userId)
        {
            try
            {
                var updateQuery = "UPDATE users SET last_login_at = NOW() WHERE id = @userId";
                using var command = new MySqlCommand(updateQuery, connection);
                command.Parameters.AddWithValue("@userId", userId);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新最後登入時間失敗: {ex.Message}");
            }
        }

        public async Task<bool> CreateUserAsync(User user)
        {
            try
            {
                if (string.IsNullOrEmpty(_connectionString))
                    return false;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                return await CreateUserInternalAsync(connection, user);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"建立使用者失敗: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CreateUserInternalAsync(MySqlConnection connection, User user)
        {
            var query = @"
                INSERT INTO users (username, password, email, full_name, department, position, is_active)
                VALUES (@username, @password, @email, @fullName, @department, @position, @isActive)";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@password", HashPassword(user.Password));
            command.Parameters.AddWithValue("@email", user.Email);
            command.Parameters.AddWithValue("@fullName", user.FullName);
            command.Parameters.AddWithValue("@department", user.Department);
            command.Parameters.AddWithValue("@position", user.Position);
            command.Parameters.AddWithValue("@isActive", user.IsActive);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            try
            {
                if (string.IsNullOrEmpty(_connectionString))
                    return null;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT id, username, email, full_name, department, position, is_active, created_at, last_login_at
                    FROM users 
                    WHERE id = @userId";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@userId", userId);

                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    return new User
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        Username = reader["username"].ToString(),
                        Email = reader["email"] as string,
                        FullName = reader["full_name"] as string,
                        Department = reader["department"] as string,
                        Position = reader["position"] as string,
                        IsActive = Convert.ToBoolean(reader["is_active"]),
                        CreatedAt = Convert.ToDateTime(reader["created_at"]),
                        LastLoginAt = reader["last_login_at"] as DateTime?
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取得使用者資料失敗: {ex.Message}");
                return null;
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            try
            {
                if (string.IsNullOrEmpty(_connectionString))
                    return null;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT id, username, email, full_name, department, position, is_active, created_at, last_login_at
                    FROM users 
                    WHERE username = @username";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);

                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    return new User
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        Username = reader["username"].ToString(),
                        Email = reader["email"] as string,
                        FullName = reader["full_name"] as string,
                        Department = reader["department"] as string,
                        Position = reader["position"] as string,
                        IsActive = Convert.ToBoolean(reader["is_active"]),
                        CreatedAt = Convert.ToDateTime(reader["created_at"]),
                        LastLoginAt = reader["last_login_at"] as DateTime?
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取得使用者資料失敗: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {
                if (string.IsNullOrEmpty(_connectionString))
                    return false;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    UPDATE users 
                    SET email = @email, full_name = @fullName, department = @department, 
                        position = @position, is_active = @isActive
                    WHERE id = @id";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@email", user.Email);
                command.Parameters.AddWithValue("@fullName", user.FullName);
                command.Parameters.AddWithValue("@department", user.Department);
                command.Parameters.AddWithValue("@position", user.Position);
                command.Parameters.AddWithValue("@isActive", user.IsActive);
                command.Parameters.AddWithValue("@id", user.Id);

                var result = await command.ExecuteNonQueryAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新使用者資料失敗: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            try
            {
                if (string.IsNullOrEmpty(_connectionString))
                    return false;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = "UPDATE users SET is_active = FALSE WHERE id = @userId";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@userId", userId);

                var result = await command.ExecuteNonQueryAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刪除使用者失敗: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SaveCheckInRecordAsync(CheckInRecord record)
        {
            try
            {
                if (string.IsNullOrEmpty(_connectionString))
                    return false;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    INSERT INTO CheckInRecords 
                    (Id, UserId, GeofenceId, GeofenceName, CheckInTime, CheckOutTime, Latitude, Longitude, Notes, Type)
                    VALUES 
                    (@id, @userId, @geofenceId, @geofenceName, @checkInTime, @checkOutTime, @latitude, @longitude, @notes, @type)";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@id", record.Id);
                command.Parameters.AddWithValue("@userId", int.TryParse(record.UserId, out int userId) ? userId : 0);
                command.Parameters.AddWithValue("@geofenceId", record.GeofenceId);
                command.Parameters.AddWithValue("@geofenceName", record.GeofenceName);
                command.Parameters.AddWithValue("@checkInTime", record.CheckInTime);
                command.Parameters.AddWithValue("@checkOutTime", record.CheckOutTime);
                command.Parameters.AddWithValue("@latitude", record.Latitude);
                command.Parameters.AddWithValue("@longitude", record.Longitude);
                command.Parameters.AddWithValue("@notes", record.Notes);
                command.Parameters.AddWithValue("@type", record.Type.ToString());

                var result = await command.ExecuteNonQueryAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存打卡記錄失敗: {ex.Message}");
                return false;
            }
        }

        public async Task<List<CheckInRecord>> GetCheckInRecordsAsync(int userId, DateTime? date = null)
        {
            var records = new List<CheckInRecord>();
            
            try
            {
                if (string.IsNullOrEmpty(_connectionString))
                    return records;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT Id, UserId, GeofenceId, GeofenceName, CheckInTime, CheckOutTime, 
                           Latitude, Longitude, Notes, Type, CreatedAt
                    FROM CheckInRecords 
                    WHERE UserId = @userId";

                if (date.HasValue)
                {
                    query += " AND DATE(CheckInTime) = @date";
                }

                query += " ORDER BY CheckInTime DESC";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@userId", userId);
                if (date.HasValue)
                {
                    command.Parameters.AddWithValue("@date", date.Value.Date);
                }

                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    var record = new CheckInRecord
                    {
                        Id = reader["Id"].ToString() ?? string.Empty,
                        UserId = reader["UserId"].ToString() ?? string.Empty,
                        GeofenceId = reader["GeofenceId"] as string ?? string.Empty,
                        GeofenceName = reader["GeofenceName"] as string ?? string.Empty,
                        CheckInTime = Convert.ToDateTime(reader["CheckInTime"]),
                        CheckOutTime = reader["CheckOutTime"] as DateTime?,
                        Latitude = Convert.ToDouble(reader["Latitude"]),
                        Longitude = Convert.ToDouble(reader["Longitude"]),
                        Notes = reader["Notes"] as string ?? string.Empty,
                        Type = Enum.Parse<CheckInType>(reader["Type"].ToString() ?? "Manual")
                    };
                    
                    records.Add(record);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取得打卡記錄失敗: {ex.Message}");
            }

            return records;
        }

        public async Task<CheckInRecord?> GetLatestCheckInAsync(int userId)
        {
            try
            {
                if (string.IsNullOrEmpty(_connectionString))
                    return null;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT Id, UserId, GeofenceId, GeofenceName, CheckInTime, CheckOutTime, 
                           Latitude, Longitude, Notes, Type
                    FROM CheckInRecords 
                    WHERE UserId = @userId 
                    ORDER BY CheckInTime DESC 
                    LIMIT 1";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@userId", userId);

                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    return new CheckInRecord
                    {
                        Id = reader["Id"].ToString() ?? string.Empty,
                        UserId = reader["UserId"].ToString() ?? string.Empty,
                        GeofenceId = reader["GeofenceId"] as string ?? string.Empty,
                        GeofenceName = reader["GeofenceName"] as string ?? string.Empty,
                        CheckInTime = Convert.ToDateTime(reader["CheckInTime"]),
                        CheckOutTime = reader["CheckOutTime"] as DateTime?,
                        Latitude = Convert.ToDouble(reader["Latitude"]),
                        Longitude = Convert.ToDouble(reader["Longitude"]),
                        Notes = reader["Notes"] as string ?? string.Empty,
                        Type = Enum.Parse<CheckInType>(reader["Type"].ToString() ?? "Manual")
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取得最新打卡記錄失敗: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UpdateCheckInRecordAsync(CheckInRecord record)
        {
            try
            {
                if (string.IsNullOrEmpty(_connectionString))
                    return false;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    UPDATE CheckInRecords 
                    SET CheckOutTime = @checkOutTime, Notes = @notes
                    WHERE Id = @id";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@checkOutTime", record.CheckOutTime);
                command.Parameters.AddWithValue("@notes", record.Notes);
                command.Parameters.AddWithValue("@id", record.Id);

                var result = await command.ExecuteNonQueryAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新打卡記錄失敗: {ex.Message}");
                return false;
            }
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "MapLocationSalt"));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}