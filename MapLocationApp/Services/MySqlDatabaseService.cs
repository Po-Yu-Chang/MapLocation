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
        private bool _schemaEnsured;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);

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

            // First-time-per-process: ensure all tables exist and apply column migrations.
            // Wraps in semaphore so concurrent first calls don't double-run schema setup.
            await EnsureSchemaAsync();
        }

        private async Task EnsureSchemaAsync()
        {
            if (_schemaEnsured) return;
            await _schemaLock.WaitAsync();
            try
            {
                if (_schemaEnsured) return;
                if (string.IsNullOrEmpty(_connectionString)) return;

                await InitializeDatabaseAsync();
                _schemaEnsured = true;
            }
            catch (Exception ex)
            {
                // Don't crash the app if migration partially fails — individual queries will surface
                // the specific column error and we can recover next launch.
                System.Diagnostics.Debug.WriteLine($"EnsureSchemaAsync failed (continuing): {ex.Message}");
            }
            finally
            {
                _schemaLock.Release();
            }
        }

        public void ResetConnection()
        {
            _isInitialized = false;
            _connectionString = null;
            _schemaEnsured = false;
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

                // Note: column names use snake_case to match all SELECT/INSERT/UPDATE queries below.
                var createUsersTable = @"
                    CREATE TABLE IF NOT EXISTS users (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        username VARCHAR(50) UNIQUE NOT NULL,
                        password VARCHAR(255) NOT NULL,
                        email VARCHAR(100),
                        full_name VARCHAR(100),
                        department VARCHAR(50),
                        position VARCHAR(50),
                        is_active BOOLEAN DEFAULT TRUE,
                        must_change_password BOOLEAN DEFAULT FALSE,
                        avatar_path VARCHAR(255) NULL,
                        phone_number VARCHAR(50) NULL,
                        work_hours_start TIME NULL,
                        work_hours_end TIME NULL,
                        role TINYINT DEFAULT 0,
                        created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                        updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                        last_login_at DATETIME NULL
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

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
                        Type ENUM('Manual', 'Automatic', 'GPS') DEFAULT 'Manual',
                        Method TINYINT DEFAULT 0,
                        WorkType TINYINT DEFAULT 0,
                        EditedAt DATETIME NULL,
                        EditReason VARCHAR(255) NULL,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (UserId) REFERENCES users(id) ON DELETE CASCADE,
                        INDEX idx_user_date (UserId, CheckInTime),
                        INDEX idx_geofence (GeofenceId)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

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
                        description TEXT,
                        work_type TINYINT DEFAULT 0
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

                using var command3 = new MySqlCommand(createGeofenceRegionsTable, connection);
                await command3.ExecuteNonQueryAsync();

                var createWorkSchedulesTable = @"
                    CREATE TABLE IF NOT EXISTS work_schedules (
                        user_id INT NOT NULL,
                        week_day TINYINT NOT NULL,
                        is_rest_day BOOLEAN DEFAULT FALSE,
                        start_time TIME NULL,
                        end_time TIME NULL,
                        note VARCHAR(255) NULL,
                        PRIMARY KEY (user_id, week_day),
                        FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
                using var commandWS = new MySqlCommand(createWorkSchedulesTable, connection);
                await commandWS.ExecuteNonQueryAsync();

                var createLeaveRecordsTable = @"
                    CREATE TABLE IF NOT EXISTS leave_records (
                        id VARCHAR(36) PRIMARY KEY,
                        user_id INT NOT NULL,
                        leave_type TINYINT NOT NULL DEFAULT 1,
                        start_date DATE NOT NULL,
                        end_date DATE NOT NULL,
                        note VARCHAR(500) NULL,
                        created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
                        INDEX idx_user_date (user_id, start_date)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
                using var commandLR = new MySqlCommand(createLeaveRecordsTable, connection);
                await commandLR.ExecuteNonQueryAsync();

                // Run idempotent migrations for existing databases that may be missing newer columns.
                await MigrateSchemaAsync(connection);

                await CreateDefaultAdminUser(connection);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化資料庫失敗: {ex.Message}");
                throw new InvalidOperationException($"資料庫初始化失敗：{ex.Message}", ex);
            }
        }
        
        private static User MapUser(System.Data.Common.DbDataReader reader)
        {
            T? Get<T>(string col) where T : struct
                => HasColumn(reader, col) && reader[col] != DBNull.Value ? (T?)Convert.ChangeType(reader[col], typeof(T)) : null;
            string? GetStr(string col)
                => HasColumn(reader, col) && reader[col] != DBNull.Value ? reader[col].ToString() : null;

            return new User
            {
                Id = Convert.ToInt32(reader["id"]),
                Username = reader["username"].ToString() ?? string.Empty,
                Email = GetStr("email"),
                FullName = GetStr("full_name"),
                Department = GetStr("department"),
                Position = GetStr("position"),
                IsActive = HasColumn(reader, "is_active") && Convert.ToBoolean(reader["is_active"]),
                MustChangePassword = HasColumn(reader, "must_change_password") && Convert.ToBoolean(reader["must_change_password"]),
                AvatarPath = GetStr("avatar_path"),
                PhoneNumber = GetStr("phone_number"),
                WorkHoursStart = HasColumn(reader, "work_hours_start") && reader["work_hours_start"] != DBNull.Value
                    ? (TimeSpan?)reader["work_hours_start"] : null,
                WorkHoursEnd = HasColumn(reader, "work_hours_end") && reader["work_hours_end"] != DBNull.Value
                    ? (TimeSpan?)reader["work_hours_end"] : null,
                CreatedAt = HasColumn(reader, "created_at") && reader["created_at"] != DBNull.Value
                    ? Convert.ToDateTime(reader["created_at"]) : default,
                LastLoginAt = HasColumn(reader, "last_login_at") ? reader["last_login_at"] as DateTime? : null,
                Role = HasColumn(reader, "role") && reader["role"] != DBNull.Value
                    ? (UserRole)Convert.ToInt32(reader["role"]) : UserRole.Employee,
            };
        }

        private static bool HasColumn(System.Data.Common.DbDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static CheckInRecord MapCheckIn(System.Data.Common.DbDataReader reader)
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
                Type = Enum.TryParse<CheckInType>(reader["Type"].ToString(), out var t) ? t : CheckInType.Manual,
                Method = HasColumn(reader, "Method") && reader["Method"] != DBNull.Value
                    ? (CheckInMethod)Convert.ToInt32(reader["Method"]) : CheckInMethod.GPS,
                WorkType = HasColumn(reader, "WorkType") && reader["WorkType"] != DBNull.Value
                    ? (WorkType)Convert.ToInt32(reader["WorkType"]) : WorkType.Office,
                EditedAt = HasColumn(reader, "EditedAt") ? reader["EditedAt"] as DateTime? : null,
                EditReason = HasColumn(reader, "EditReason") ? reader["EditReason"] as string : null,
            };
        }

        /// <summary>
        /// Idempotently adds columns introduced in later versions to pre-existing tables.
        /// Wraps each ALTER in try/catch so duplicate-column errors (1060) are swallowed.
        /// </summary>
        private static async Task MigrateSchemaAsync(MySqlConnection connection)
        {
            var migrations = new[]
            {
                "ALTER TABLE users ADD COLUMN avatar_path VARCHAR(255) NULL",
                "ALTER TABLE users ADD COLUMN phone_number VARCHAR(50) NULL",
                "ALTER TABLE users ADD COLUMN work_hours_start TIME NULL",
                "ALTER TABLE users ADD COLUMN work_hours_end TIME NULL",
                "ALTER TABLE users ADD COLUMN must_change_password BOOLEAN DEFAULT FALSE",
                "ALTER TABLE users ADD COLUMN role TINYINT DEFAULT 0",
                // Promote existing default admin account to role=Admin if it's still Employee.
                "UPDATE users SET role = 1 WHERE username = 'admin' AND role = 0",
                "ALTER TABLE CheckInRecords ADD COLUMN Method TINYINT DEFAULT 0",
                "ALTER TABLE CheckInRecords ADD COLUMN WorkType TINYINT DEFAULT 0",
                "ALTER TABLE CheckInRecords ADD COLUMN EditedAt DATETIME NULL",
                "ALTER TABLE CheckInRecords ADD COLUMN EditReason VARCHAR(255) NULL",
                "ALTER TABLE geofence_regions ADD COLUMN work_type TINYINT DEFAULT 0",
                "ALTER TABLE geofence_regions ADD COLUMN ssid VARCHAR(64) NULL",
                "ALTER TABLE geofence_regions ADD COLUMN bssid VARCHAR(32) NULL",
            };

            foreach (var sql in migrations)
            {
                try
                {
                    using var cmd = new MySqlCommand(sql, connection);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (MySqlException ex) when (ex.Number == 1060)
                {
                    // Duplicate column name: column already exists, expected on subsequent runs.
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Schema migration skipped ({sql}): {ex.Message}");
                }
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
                        Role = UserRole.Admin,
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
                           avatar_path, phone_number, work_hours_start, work_hours_end,
                           COALESCE(role, 0) AS role,
                           created_at, last_login_at
                    FROM users
                    WHERE username = @username";

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
                        // Pending approval — credentials are right but admin hasn't activated yet.
                        var isActive = Convert.ToBoolean(reader["is_active"]);
                        if (!isActive)
                        {
                            return new LoginResult
                            {
                                Success = false,
                                ErrorMessage = "AccountPendingApproval"  // i18n key, resolved by LoginPage
                            };
                        }

                        var user = MapUser(reader);

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
                INSERT INTO users (username, password, email, full_name, department, position, is_active,
                                   must_change_password, avatar_path, phone_number, work_hours_start, work_hours_end, role)
                VALUES (@username, @password, @email, @fullName, @department, @position, @isActive,
                        @mustChangePassword, @avatarPath, @phoneNumber, @workHoursStart, @workHoursEnd, @role)";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@password", HashPassword(user.Password));
            command.Parameters.AddWithValue("@email", user.Email);
            command.Parameters.AddWithValue("@fullName", user.FullName);
            command.Parameters.AddWithValue("@department", user.Department);
            command.Parameters.AddWithValue("@position", user.Position);
            command.Parameters.AddWithValue("@isActive", user.IsActive);
            command.Parameters.AddWithValue("@mustChangePassword", user.MustChangePassword);
            command.Parameters.AddWithValue("@avatarPath", (object?)user.AvatarPath ?? DBNull.Value);
            command.Parameters.AddWithValue("@phoneNumber", (object?)user.PhoneNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("@workHoursStart", (object?)user.WorkHoursStart ?? DBNull.Value);
            command.Parameters.AddWithValue("@workHoursEnd", (object?)user.WorkHoursEnd ?? DBNull.Value);
            command.Parameters.AddWithValue("@role", (int)user.Role);

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
                    SELECT id, username, email, full_name, department, position, is_active,
                           must_change_password, avatar_path, phone_number, work_hours_start, work_hours_end,
                           COALESCE(role, 0) AS role,
                           created_at, last_login_at
                    FROM users
                    WHERE id = @userId";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@userId", userId);

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapUser(reader);
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
                    SELECT id, username, email, full_name, department, position, is_active,
                           must_change_password, avatar_path, phone_number, work_hours_start, work_hours_end,
                           COALESCE(role, 0) AS role,
                           created_at, last_login_at
                    FROM users
                    WHERE username = @username";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapUser(reader);
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
                        position = @position, is_active = @isActive,
                        avatar_path = @avatarPath, phone_number = @phoneNumber,
                        work_hours_start = @workHoursStart, work_hours_end = @workHoursEnd
                    WHERE id = @id";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@email", (object?)user.Email ?? DBNull.Value);
                command.Parameters.AddWithValue("@fullName", (object?)user.FullName ?? DBNull.Value);
                command.Parameters.AddWithValue("@department", (object?)user.Department ?? DBNull.Value);
                command.Parameters.AddWithValue("@position", (object?)user.Position ?? DBNull.Value);
                command.Parameters.AddWithValue("@isActive", user.IsActive);
                command.Parameters.AddWithValue("@avatarPath", (object?)user.AvatarPath ?? DBNull.Value);
                command.Parameters.AddWithValue("@phoneNumber", (object?)user.PhoneNumber ?? DBNull.Value);
                command.Parameters.AddWithValue("@workHoursStart", (object?)user.WorkHoursStart ?? DBNull.Value);
                command.Parameters.AddWithValue("@workHoursEnd", (object?)user.WorkHoursEnd ?? DBNull.Value);
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

        public async Task<List<User>> GetPendingUsersAsync()
        {
            var list = new List<User>();
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString)) return list;
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"SELECT id, username, email, full_name, department, position, is_active,
                                     COALESCE(role, 0) AS role,
                                     created_at, last_login_at
                              FROM users
                              WHERE is_active = FALSE
                              ORDER BY created_at DESC";
                using var cmd = new MySqlCommand(query, connection);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(MapUser(reader));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取得待審核使用者失敗: {ex.Message}");
            }
            return list;
        }

        public async Task<bool> ApproveUserAsync(int userId)
        {
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString)) return false;
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                using var cmd = new MySqlCommand(
                    "UPDATE users SET is_active = TRUE WHERE id = @id", connection);
                cmd.Parameters.AddWithValue("@id", userId);
                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"啟用使用者失敗: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RejectUserAsync(int userId)
        {
            // Hard delete the row — DeleteUserAsync just toggles is_active which would leave
            // the rejected username "claimed" forever.
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString)) return false;
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                using var cmd = new MySqlCommand(
                    "DELETE FROM users WHERE id = @id AND is_active = FALSE", connection);
                cmd.Parameters.AddWithValue("@id", userId);
                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"拒絕使用者失敗: {ex.Message}");
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
                    (Id, UserId, GeofenceId, GeofenceName, CheckInTime, CheckOutTime,
                     Latitude, Longitude, Notes, Type, Method, WorkType, EditedAt, EditReason)
                    VALUES
                    (@id, @userId, @geofenceId, @geofenceName, @checkInTime, @checkOutTime,
                     @latitude, @longitude, @notes, @type, @method, @workType, @editedAt, @editReason)";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@id", record.Id);
                if (!int.TryParse(record.UserId, out int userId))
                    throw new InvalidOperationException($"無效的 UserId 格式: {record.UserId}");
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@geofenceId", record.GeofenceId);
                command.Parameters.AddWithValue("@geofenceName", record.GeofenceName);
                command.Parameters.AddWithValue("@checkInTime", record.CheckInTime);
                command.Parameters.AddWithValue("@checkOutTime", (object?)record.CheckOutTime ?? DBNull.Value);
                command.Parameters.AddWithValue("@latitude", record.Latitude);
                command.Parameters.AddWithValue("@longitude", record.Longitude);
                command.Parameters.AddWithValue("@notes", record.Notes);
                command.Parameters.AddWithValue("@type", record.Type.ToString());
                command.Parameters.AddWithValue("@method", (int)record.Method);
                command.Parameters.AddWithValue("@workType", (int)record.WorkType);
                command.Parameters.AddWithValue("@editedAt", (object?)record.EditedAt ?? DBNull.Value);
                command.Parameters.AddWithValue("@editReason", (object?)record.EditReason ?? DBNull.Value);

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
                           Latitude, Longitude, Notes, Type, Method, WorkType, EditedAt, EditReason, CreatedAt
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
                    records.Add(MapCheckIn(reader));
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
                           Latitude, Longitude, Notes, Type, Method, WorkType, EditedAt, EditReason
                    FROM CheckInRecords
                    WHERE UserId = @userId
                    ORDER BY CheckInTime DESC
                    LIMIT 1";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@userId", userId);

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapCheckIn(reader);
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
                    SET CheckInTime = @checkInTime, CheckOutTime = @checkOutTime,
                        Notes = @notes, Method = @method, WorkType = @workType,
                        EditedAt = @editedAt, EditReason = @editReason
                    WHERE Id = @id";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@checkInTime", record.CheckInTime);
                command.Parameters.AddWithValue("@checkOutTime", (object?)record.CheckOutTime ?? DBNull.Value);
                command.Parameters.AddWithValue("@notes", record.Notes);
                command.Parameters.AddWithValue("@method", (int)record.Method);
                command.Parameters.AddWithValue("@workType", (int)record.WorkType);
                command.Parameters.AddWithValue("@editedAt", (object?)record.EditedAt ?? DBNull.Value);
                command.Parameters.AddWithValue("@editReason", (object?)record.EditReason ?? DBNull.Value);
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

        public async Task<List<WorkSchedule>> GetWorkScheduleAsync(int userId)
        {
            var list = new List<WorkSchedule>();
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString)) return list;
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                var query = @"SELECT week_day, is_rest_day, start_time, end_time, note
                              FROM work_schedules WHERE user_id = @uid ORDER BY week_day";
                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@uid", userId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new WorkSchedule
                    {
                        WeekDay = Convert.ToInt32(r["week_day"]),
                        IsRestDay = Convert.ToBoolean(r["is_rest_day"]),
                        Start = r["start_time"] is DBNull ? null : (TimeSpan?)r["start_time"],
                        End = r["end_time"] is DBNull ? null : (TimeSpan?)r["end_time"],
                        Note = r["note"] as string,
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"GetWorkSchedule failed: {ex.Message}"); }
            return list;
        }

        public async Task<bool> SaveWorkScheduleAsync(int userId, IEnumerable<WorkSchedule> schedules)
        {
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString)) return false;
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                using var tx = await connection.BeginTransactionAsync();
                var del = new MySqlCommand("DELETE FROM work_schedules WHERE user_id = @uid", connection, (MySqlTransaction)tx);
                del.Parameters.AddWithValue("@uid", userId);
                await del.ExecuteNonQueryAsync();

                foreach (var s in schedules)
                {
                    var ins = new MySqlCommand(@"INSERT INTO work_schedules
                        (user_id, week_day, is_rest_day, start_time, end_time, note)
                        VALUES (@uid, @wd, @rest, @start, @end, @note)", connection, (MySqlTransaction)tx);
                    ins.Parameters.AddWithValue("@uid", userId);
                    ins.Parameters.AddWithValue("@wd", s.WeekDay);
                    ins.Parameters.AddWithValue("@rest", s.IsRestDay);
                    ins.Parameters.AddWithValue("@start", (object?)s.Start ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@end", (object?)s.End ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@note", (object?)s.Note ?? DBNull.Value);
                    await ins.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
                return true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SaveWorkSchedule failed: {ex.Message}"); return false; }
        }

        public async Task<List<LeaveRecord>> GetLeaveRecordsAsync(int userId, DateTime? from = null, DateTime? to = null)
        {
            var list = new List<LeaveRecord>();
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString)) return list;
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                var sql = @"SELECT id, user_id, leave_type, start_date, end_date, note, created_at
                            FROM leave_records WHERE user_id = @uid";
                if (from.HasValue) sql += " AND end_date >= @from";
                if (to.HasValue) sql += " AND start_date <= @to";
                sql += " ORDER BY start_date DESC";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@uid", userId);
                if (from.HasValue) cmd.Parameters.AddWithValue("@from", from.Value.Date);
                if (to.HasValue) cmd.Parameters.AddWithValue("@to", to.Value.Date);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new LeaveRecord
                    {
                        Id = r["id"].ToString() ?? string.Empty,
                        UserId = r["user_id"].ToString() ?? string.Empty,
                        Type = (LeaveType)Convert.ToInt32(r["leave_type"]),
                        StartDate = Convert.ToDateTime(r["start_date"]),
                        EndDate = Convert.ToDateTime(r["end_date"]),
                        Note = r["note"] as string,
                        CreatedAt = r["created_at"] is DateTime dt ? dt : DateTime.UtcNow,
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"GetLeaveRecords failed: {ex.Message}"); }
            return list;
        }

        public async Task<bool> SaveLeaveRecordAsync(LeaveRecord record)
        {
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString)) return false;
                if (!int.TryParse(record.UserId, out var uid)) return false;
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                var sql = @"INSERT INTO leave_records (id, user_id, leave_type, start_date, end_date, note)
                            VALUES (@id, @uid, @type, @start, @end, @note)";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", record.Id);
                cmd.Parameters.AddWithValue("@uid", uid);
                cmd.Parameters.AddWithValue("@type", (int)record.Type);
                cmd.Parameters.AddWithValue("@start", record.StartDate.Date);
                cmd.Parameters.AddWithValue("@end", record.EndDate.Date);
                cmd.Parameters.AddWithValue("@note", (object?)record.Note ?? DBNull.Value);
                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SaveLeaveRecord failed: {ex.Message}"); return false; }
        }

        public async Task<bool> UpdateLeaveRecordAsync(LeaveRecord record)
        {
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString)) return false;
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                var sql = @"UPDATE leave_records SET leave_type = @type, start_date = @start,
                            end_date = @end, note = @note WHERE id = @id";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", record.Id);
                cmd.Parameters.AddWithValue("@type", (int)record.Type);
                cmd.Parameters.AddWithValue("@start", record.StartDate.Date);
                cmd.Parameters.AddWithValue("@end", record.EndDate.Date);
                cmd.Parameters.AddWithValue("@note", (object?)record.Note ?? DBNull.Value);
                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"UpdateLeaveRecord failed: {ex.Message}"); return false; }
        }

        public async Task<bool> DeleteLeaveRecordAsync(string recordId)
        {
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString)) return false;
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                using var cmd = new MySqlCommand("DELETE FROM leave_records WHERE id = @id", connection);
                cmd.Parameters.AddWithValue("@id", recordId);
                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"DeleteLeaveRecord failed: {ex.Message}"); return false; }
        }

        public async Task<bool> DeleteCheckInRecordAsync(string recordId)
        {
            try
            {
                await InitializeConnectionStringAsync();
                if (string.IsNullOrEmpty(_connectionString))
                    return false;

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = "DELETE FROM CheckInRecords WHERE Id = @id";
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@id", recordId);

                var result = await command.ExecuteNonQueryAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刪除打卡記錄失敗: {ex.Message}");
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
                    description TEXT,
                    work_type TINYINT DEFAULT 0,
                    ssid VARCHAR(64) NULL,
                    bssid VARCHAR(32) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
                using var createCmd = new MySqlCommand(createTable, connection);
                await createCmd.ExecuteNonQueryAsync();

                var query = @"SELECT id, name, latitude, longitude, radius_meters, is_active,
                                     created_at, transition_type, category, description, work_type, ssid, bssid
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
                        WorkType = HasColumn(reader, "work_type") && reader["work_type"] != DBNull.Value
                            ? (WorkType)Convert.ToInt32(reader["work_type"]) : WorkType.Office,
                        Ssid = HasColumn(reader, "ssid") ? reader["ssid"] as string : null,
                        Bssid = HasColumn(reader, "bssid") ? reader["bssid"] as string : null,
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
                    description TEXT,
                    work_type TINYINT DEFAULT 0,
                    ssid VARCHAR(64) NULL,
                    bssid VARCHAR(32) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
                using var createCmd = new MySqlCommand(createTable, connection);
                await createCmd.ExecuteNonQueryAsync();

                var query = @"INSERT INTO geofence_regions
                                (id, name, latitude, longitude, radius_meters, is_active,
                                 created_at, transition_type, category, description, work_type, ssid, bssid)
                              VALUES
                                (@id, @name, @lat, @lng, @radius, @active,
                                 @createdAt, @transition, @category, @description, @workType, @ssid, @bssid)
                              ON DUPLICATE KEY UPDATE
                                name = VALUES(name), latitude = VALUES(latitude),
                                longitude = VALUES(longitude), radius_meters = VALUES(radius_meters),
                                is_active = VALUES(is_active), transition_type = VALUES(transition_type),
                                category = VALUES(category), description = VALUES(description),
                                work_type = VALUES(work_type), ssid = VALUES(ssid), bssid = VALUES(bssid)";

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
                command.Parameters.AddWithValue("@workType", (int)geofence.WorkType);
                command.Parameters.AddWithValue("@ssid", (object?)geofence.Ssid ?? DBNull.Value);
                command.Parameters.AddWithValue("@bssid", (object?)geofence.Bssid ?? DBNull.Value);

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