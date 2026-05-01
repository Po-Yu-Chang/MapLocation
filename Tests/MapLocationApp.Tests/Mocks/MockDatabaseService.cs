using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp.Tests.Mocks;

/// <summary>
/// Mock implementation of IDatabaseService for testing without MySQL dependency
/// T021: Provides in-memory test data for unit/integration tests
/// </summary>
public class MockDatabaseService : IDatabaseService
{
    private readonly Dictionary<int, User> _users = new();
    private readonly Dictionary<string, CheckInRecord> _checkInRecords = new();
    private int _nextUserId = 1;
    private bool _isConnected = true;

    public MockDatabaseService()
    {
        // Seed with default admin user
        var adminUser = new User
        {
            Id = _nextUserId++,
            Username = "admin",
            Password = "admin123", // Will be hashed in real implementation (T012)
            Email = "admin@test.com",
            FullName = "Test Admin",
            Department = "IT",
            Position = "Administrator",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _users[adminUser.Id] = adminUser;
    }

    /// <summary>
    /// Simulates connection failure for testing error scenarios
    /// </summary>
    public void SimulateConnectionFailure()
    {
        _isConnected = false;
    }

    /// <summary>
    /// Restores connection after simulated failure
    /// </summary>
    public void RestoreConnection()
    {
        _isConnected = true;
    }

    public Task<bool> TestConnectionAsync()
    {
        return Task.FromResult(_isConnected);
    }

    public Task<bool> TestConnectionAsync(DatabaseConfig config, string password)
    {
        return Task.FromResult(_isConnected);
    }

    public void ResetConnection()
    {
        // No-op for mock; in-memory state always considered connected.
    }

    public Task<bool> InitializeDatabaseAsync()
    {
        // Already initialized with admin user in constructor
        return Task.FromResult(true);
    }

    public Task<LoginResult> AuthenticateUserAsync(string username, string password)
    {
        if (!_isConnected)
        {
            return Task.FromResult(new LoginResult
            {
                Success = false,
                ErrorMessage = "資料庫連線未設定"
            });
        }

        var user = _users.Values.FirstOrDefault(u => u.Username == username && u.IsActive);

        if (user == null)
        {
            return Task.FromResult(new LoginResult
            {
                Success = false,
                ErrorMessage = "帳號或密碼錯誤"
            });
        }

        // Simple password comparison for mock (real service uses BCrypt - T012)
        if (user.Password == password)
        {
            user.LastLoginAt = DateTime.UtcNow;

            return Task.FromResult(new LoginResult
            {
                Success = true,
                User = user
            });
        }

        return Task.FromResult(new LoginResult
        {
            Success = false,
            ErrorMessage = "帳號或密碼錯誤"
        });
    }

    public Task<bool> CreateUserAsync(User user)
    {
        if (!_isConnected)
            return Task.FromResult(false);

        // Check for duplicate username
        if (_users.Values.Any(u => u.Username == user.Username))
        {
            return Task.FromResult(false);
        }

        user.Id = _nextUserId++;
        user.CreatedAt = DateTime.UtcNow;
        _users[user.Id] = user;

        return Task.FromResult(true);
    }

    public Task<User?> GetUserByIdAsync(int userId)
    {
        if (!_isConnected)
            return Task.FromResult<User?>(null);

        _users.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetUserByUsernameAsync(string username)
    {
        if (!_isConnected)
            return Task.FromResult<User?>(null);

        var user = _users.Values.FirstOrDefault(u => u.Username == username);
        return Task.FromResult(user);
    }

    public Task<bool> UpdateUserAsync(User user)
    {
        if (!_isConnected)
            return Task.FromResult(false);

        if (!_users.ContainsKey(user.Id))
        {
            return Task.FromResult(false);
        }

        _users[user.Id] = user;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteUserAsync(int userId)
    {
        if (!_isConnected)
            return Task.FromResult(false);

        if (_users.TryGetValue(userId, out var user))
        {
            user.IsActive = false; // Soft delete
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    // T026-T027: Interface methods required by CheckInStorageService tests
    public Task<bool> SaveCheckInAsync(CheckInRecord record)
    {
        if (!_isConnected)
            return Task.FromResult(false);

        // T030: Check for duplicate ID to prevent double check-ins
        if (_checkInRecords.ContainsKey(record.Id))
        {
            return Task.FromResult(false);
        }

        // T013: Notes are XSS-sanitized in CheckInRecord model automatically
        _checkInRecords[record.Id] = record;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateCheckInAsync(CheckInRecord record)
    {
        if (!_isConnected)
            return Task.FromResult(false);

        // T029: Update requires existing record
        if (!_checkInRecords.ContainsKey(record.Id))
        {
            return Task.FromResult(false);
        }

        _checkInRecords[record.Id] = record;
        return Task.FromResult(true);
    }

    public Task<bool> SaveCheckInRecordAsync(CheckInRecord record)
    {
        if (!_isConnected)
            return Task.FromResult(false);

        // T013: Notes are XSS-sanitized in CheckInRecord model automatically
        _checkInRecords[record.Id] = record;
        return Task.FromResult(true);
    }

    public Task<List<CheckInRecord>> GetCheckInRecordsAsync(int userId, DateTime? date = null)
    {
        if (!_isConnected)
            return Task.FromResult(new List<CheckInRecord>());

        var records = _checkInRecords.Values
            .Where(r => r.UserId == userId.ToString())
            .AsEnumerable();

        if (date.HasValue)
        {
            records = records.Where(r => r.CheckInTime.Date == date.Value.Date);
        }

        return Task.FromResult(records.OrderByDescending(r => r.CheckInTime).ToList());
    }

    public Task<CheckInRecord?> GetLatestCheckInAsync(int userId)
    {
        if (!_isConnected)
            return Task.FromResult<CheckInRecord?>(null);

        var latestRecord = _checkInRecords.Values
            .Where(r => r.UserId == userId.ToString())
            .OrderByDescending(r => r.CheckInTime)
            .FirstOrDefault();

        return Task.FromResult(latestRecord);
    }

    public Task<bool> UpdateCheckInRecordAsync(CheckInRecord record)
    {
        if (!_isConnected)
            return Task.FromResult(false);

        if (!_checkInRecords.ContainsKey(record.Id))
        {
            return Task.FromResult(false);
        }

        _checkInRecords[record.Id] = record;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteCheckInRecordAsync(string recordId)
    {
        if (!_isConnected)
            return Task.FromResult(false);

        return Task.FromResult(_checkInRecords.Remove(recordId));
    }

    /// <summary>
    /// Test helper: Get all users (for verification in tests)
    /// </summary>
    public List<User> GetAllUsers()
    {
        return _users.Values.ToList();
    }

    /// <summary>
    /// Test helper: Clear all data (reset between tests)
    /// </summary>
    public void ClearAllData()
    {
        _users.Clear();
        _checkInRecords.Clear();
        _nextUserId = 1;

        // Re-add admin user
        var adminUser = new User
        {
            Id = _nextUserId++,
            Username = "admin",
            Password = "admin123",
            Email = "admin@test.com",
            FullName = "Test Admin",
            Department = "IT",
            Position = "Administrator",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _users[adminUser.Id] = adminUser;
    }

    // Geofence stubs (not used in current tests, but required by IDatabaseService)
    private readonly Dictionary<string, GeofenceRegion> _geofences = new();

    public Task<List<GeofenceRegion>> GetAllGeofencesAsync()
        => Task.FromResult(_geofences.Values.ToList());

    public Task<bool> SaveGeofenceAsync(GeofenceRegion geofence)
    {
        _geofences[geofence.Id] = geofence;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateGeofenceAsync(GeofenceRegion geofence)
    {
        _geofences[geofence.Id] = geofence;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteGeofenceAsync(string geofenceId)
        => Task.FromResult(_geofences.Remove(geofenceId));
}
