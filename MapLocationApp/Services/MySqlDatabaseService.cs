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
                    if (!string.IsNullOrEmpty(config?.Password))
                    {
                        System.Diagnostics.Debug.WriteLine("SecureStorage 未設定密碼，回退使用 DatabaseConfig.Password 並寫入 SecureStorage。");
                        password = config.Password;
                        try
                        {
                            await _secureConfig.SetDatabasePasswordAsync(password);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"寫入 SecureStorage 失敗（忽略，繼續使用回退密碼）: {ex.Message}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("資料庫密碼未在 SecureStorage 或 DatabaseConfig 中設定。請透過設定頁面配置密碼。");
                        throw new InvalidOperationException("Database password not configured. Please configure via settings.");
                    }
                }

                // T014: Enable SSL/TLS for MySQL connections
                _connectionString = $"Server={config.Host};Port={config.Port};Database={config.DatabaseName};Uid={config.Username};Pwd={password};SslMode=Preferred;Connection Timeout=30;Command Timeout=60;Default Command Timeout=60;";
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

        public void ResetConnection()
        {
            _isInitialized = false;
            _connectionString = null;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await InitializeConnectionStringAsync();

                if (string.IsNullOrEmpty(_connectionString))
                    return false;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                
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

        public async Task<bool> TestConnectionAsync(DatabaseConfig config, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(config.Host) || string.IsNullOrWhiteSpace(config.DatabaseName) ||
                    string.IsNullOrWhiteSpace(config.Username))
                    return false;

                var connStr = $"Server={config.Host};Port={config.Port};Database={config.DatabaseName};Uid={config.Username};Pwd={password};SslMode=Preferred;Connection Timeout=10;Command Timeout=10;";
                using var connection = new MySqlConnection(connStr);
                await connection.OpenAsync();

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
                        MustChangePassword BOOLEAN DEFAULT FALSE,
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

                var createGeofenceRegionsTable = @"
                    CREATE TABLE IF NOT EXISTS geofence_regions (
                        id VARCHAR(36) PRIMARY KEY,
                        name VARCHAR(100) NOT NULL,
                        latitude DOUBLE NOT NULL,
                        longitude DOUBLE NOT NULL,
                        radius_meters DOUBLE NOT NULL,
                        is_active BOOLEAN DEFAULT TRUE,
                        created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                        transition_type TINYINT DEFAULT 3,
                        category VARCHAR(50) DEFAULT '',
                        description TEXT
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

                using var command3 = new MySqlCommand(createGeofenceRegionsTable, connection);
                await command3.ExecuteNonQueryAsync();

                await CreateDefaultAdminUser(connection);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化資料庫失敗: {ex.Message}");
                throw new InvalidOperationException($"資料庫初始化失敗：{ex.Message}", ex);
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
                        IsActive = true,
                        MustChangePassword = true  // Force password change on first login
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
                await InitializeConnectionStringAsync();

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
                    SELECT id, username, password, email, full_name, department, position, is_active,
                           COALESCE(must_change_password, FALSE) as must_change_password,
                           created_at, last_login_at
                    FROM users 
                    WHERE username = @username AND is_active = TRUE";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);

                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    var storedPasswordHash = reader["password"].ToString();
                    // Hash the input password with the same algorithm before comparing
                    var inputPasswordHash = HashPassword(password);

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
                            MustChangePassword = Convert.ToBoolean(reader["must_change_password"]),
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
                System.Diagnostics.Debug.WriteLine($"使用者認證失敗: {ex}");
                return new LoginResult 
                { 
                    Success = false, 
                    ErrorMessage = $"登入過程發生錯誤: {ex.Message}" 
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
                INSERT INTO users (username, password, email, full_name, department, position, is_active, must_change_password)
                VALUES (@username, @password, @email, @fullName, @department, @position, @isActive, @mustChangePassword)";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@password", HashPassword(user.Password));
            command.Parameters.AddWithValue("@email", user.Email);
            command.Parameters.AddWithValue("@fullName", user.FullName);
            command.Parameters.AddWithValue("@department", user.Department);
            command.Parameters.AddWithValue("@position", user.Position);
            command.Parameters.AddWithValue("@isActive", user.IsActive);
            command.Parameters.AddWithValue("@mustChangePassword", user.MustChangePassword);

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
                if (!int.TryParse(record.UserId, out int userId))
                    throw new InvalidOperationException($"無效的 UserId 格式: {record.UserId}");
                command.Parameters.AddWithValue("@userId", userId);
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

        public async Task<List<GeofenceRegion>> GetAllGeofencesAsync()
        {
            var list = new List<GeofenceRegion>();
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString))
                    return list;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                // Ensure table exists before querying (handles fresh DB installations)
                var createTable = @"CREATE TABLE IF NOT EXISTS geofence_regions (
                    id VARCHAR(36) PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    latitude DOUBLE NOT NULL,
                    longitude DOUBLE NOT NULL,
                    radius_meters DOUBLE NOT NULL,
                    is_active BOOLEAN DEFAULT TRUE,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    transition_type TINYINT DEFAULT 3,
                    category VARCHAR(50) DEFAULT '',
                    description TEXT
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
                using var createCmd = new MySqlCommand(createTable, connection);
                await createCmd.ExecuteNonQueryAsync();

                var query = @"SELECT id, name, latitude, longitude, radius_meters, is_active,
                                     created_at, transition_type, category, description
                              FROM geofence_regions";
                using var command = new MySqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new GeofenceRegion
                    {
                        Id = reader["id"].ToString() ?? string.Empty,
                        Name = reader["name"] as string ?? string.Empty,
                        Latitude = Convert.ToDouble(reader["latitude"]),
                        Longitude = Convert.ToDouble(reader["longitude"]),
                        RadiusMeters = Convert.ToDouble(reader["radius_meters"]),
                        IsActive = Convert.ToBoolean(reader["is_active"]),
                        CreatedAt = reader["created_at"] is DateTime dt ? dt : DateTime.UtcNow,
                        TransitionType = (GeofenceTransitionType)Convert.ToInt32(reader["transition_type"]),
                        Category = reader["category"] as string ?? string.Empty,
                        Description = reader["description"] as string ?? string.Empty,
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取得地理圍欄列表失敗: {ex.Message}");
            }
            return list;
        }

        public async Task<bool> SaveGeofenceAsync(GeofenceRegion geofence)
        {
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString))
                    return false;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                // Ensure table exists (idempotent, first-time setup)
                var createTable = @"CREATE TABLE IF NOT EXISTS geofence_regions (
                    id VARCHAR(36) PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    latitude DOUBLE NOT NULL,
                    longitude DOUBLE NOT NULL,
                    radius_meters DOUBLE NOT NULL,
                    is_active BOOLEAN DEFAULT TRUE,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    transition_type TINYINT DEFAULT 3,
                    category VARCHAR(50) DEFAULT '',
                    description TEXT
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
                using var createCmd = new MySqlCommand(createTable, connection);
                await createCmd.ExecuteNonQueryAsync();

                var query = @"INSERT INTO geofence_regions
                                (id, name, latitude, longitude, radius_meters, is_active,
                                 created_at, transition_type, category, description)
                              VALUES
                                (@id, @name, @lat, @lng, @radius, @active,
                                 @createdAt, @transition, @category, @description)
                              ON DUPLICATE KEY UPDATE
                                name = VALUES(name), latitude = VALUES(latitude),
                                longitude = VALUES(longitude), radius_meters = VALUES(radius_meters),
                                is_active = VALUES(is_active), transition_type = VALUES(transition_type),
                                category = VALUES(category), description = VALUES(description)";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@id", geofence.Id);
                command.Parameters.AddWithValue("@name", geofence.Name);
                command.Parameters.AddWithValue("@lat", geofence.Latitude);
                command.Parameters.AddWithValue("@lng", geofence.Longitude);
                command.Parameters.AddWithValue("@radius", geofence.RadiusMeters);
                command.Parameters.AddWithValue("@active", geofence.IsActive);
                command.Parameters.AddWithValue("@createdAt", geofence.CreatedAt);
                command.Parameters.AddWithValue("@transition", (int)geofence.TransitionType);
                command.Parameters.AddWithValue("@category", geofence.Category ?? string.Empty);
                command.Parameters.AddWithValue("@description", geofence.Description ?? string.Empty);

                await command.ExecuteNonQueryAsync();
                // ON DUPLICATE KEY UPDATE returns 0 when row exists with identical values;
                // absence of exception means the operation succeeded.
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"儲存地理圍欄失敗: {ex.Message}");
                return false;
            }
        }

        public Task<bool> UpdateGeofenceAsync(GeofenceRegion geofence) => SaveGeofenceAsync(geofence);

        public async Task<bool> DeleteGeofenceAsync(string geofenceId)
        {
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString))
                    return false;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = "DELETE FROM geofence_regions WHERE id = @id";
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@id", geofenceId);

                var result = await command.ExecuteNonQueryAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刪除地理圍欄失敗: {ex.Message}");
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