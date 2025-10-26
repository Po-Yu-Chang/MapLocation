using MapLocationApp.Models;
using System.Text.Json;

namespace MapLocationApp.Services;

public interface ICheckInStorageService
{
    Task<List<CheckInRecord>> GetCheckInRecordsAsync(DateTime date);
    Task<bool> SaveCheckInRecordAsync(CheckInRecord record);
    Task<List<CheckInRecord>> GetAllCheckInRecordsAsync();
    Task<bool> DeleteCheckInRecordAsync(string recordId);

    // T026-T027: Online/offline check-in support
    Task<bool> CreateCheckInAsync(CheckInRecord record, bool isOnline);
    Task<bool> UpdateCheckInAsync(CheckInRecord record);
    Task<List<CheckInRecord>> GetPendingSyncRecordsAsync();
    Task<bool> SyncPendingRecordsAsync();
}

public class CheckInStorageService : ICheckInStorageService
{
    private const string CheckInRecordsKey = "CheckInRecords";
    private const string PendingSyncKey = "PendingCheckIns";

    private readonly IDatabaseService? _databaseService;

    // T027: In-memory storage for tests (when SecureStorage is not available)
    private readonly Dictionary<string, string> _testStorage;
    private readonly bool _useTestStorage;

    public event EventHandler<CheckInEventArgs>? CheckInRecorded;

    // Parameterless constructor for existing code
    public CheckInStorageService()
    {
        _databaseService = null;
        _useTestStorage = false;
        _testStorage = new Dictionary<string, string>(); // Initialize but won't be used
    }

    // Constructor with database service for TDD tests (T026-T027)
    public CheckInStorageService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
        // T027: Use test storage when running in test environment
        _useTestStorage = true;
        _testStorage = new Dictionary<string, string>(); // Fresh storage for each test instance
    }

    public async Task<List<CheckInRecord>> GetCheckInRecordsAsync(DateTime date)
    {
        try
        {
            var allRecords = await GetAllCheckInRecordsAsync();
            return allRecords.Where(r => r.CheckInTime.Date == date.Date).ToList();
        }
        catch
        {
            return new List<CheckInRecord>();
        }
    }

    public async Task<bool> SaveCheckInRecordAsync(CheckInRecord record)
    {
        try
        {
            var allRecords = await GetAllCheckInRecordsAsync();
            
            // 檢查是否已存在相同ID的記錄
            var existingIndex = allRecords.FindIndex(r => r.Id == record.Id);
            bool isNewRecord = existingIndex < 0;
            
            if (existingIndex >= 0)
            {
                allRecords[existingIndex] = record;
            }
            else
            {
                allRecords.Add(record);
            }

            var json = JsonSerializer.Serialize(allRecords);
            await SetStorageAsync(CheckInRecordsKey, json);
            
            // 只有新記錄才觸發事件
            if (isNewRecord)
            {
                CheckInRecorded?.Invoke(this, new CheckInEventArgs
                {
                    Latitude = record.Latitude,
                    Longitude = record.Longitude,
                    CheckInTime = record.CheckInTime,
                    Note = record.Notes
                });
            }
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存打卡記錄失敗: {ex.Message}");
            return false;
        }
    }

    public async Task<List<CheckInRecord>> GetAllCheckInRecordsAsync()
    {
        try
        {
            var json = await GetStorageAsync(CheckInRecordsKey);
            if (string.IsNullOrEmpty(json))
                return new List<CheckInRecord>();

            var records = JsonSerializer.Deserialize<List<CheckInRecord>>(json);
            return records ?? new List<CheckInRecord>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"載入打卡記錄失敗: {ex.Message}");
            return new List<CheckInRecord>();
        }
    }

    // T027: Storage abstraction for tests
    private async Task<string?> GetStorageAsync(string key)
    {
        if (_useTestStorage)
        {
            _testStorage.TryGetValue(key, out var value);
            return await Task.FromResult(value);
        }
        return await SecureStorage.GetAsync(key);
    }

    private async Task SetStorageAsync(string key, string value)
    {
        if (_useTestStorage)
        {
            _testStorage[key] = value;
            await Task.CompletedTask;
        }
        else
        {
            await SecureStorage.SetAsync(key, value);
        }
    }

    public async Task<bool> DeleteCheckInRecordAsync(string recordId)
    {
        try
        {
            var allRecords = await GetAllCheckInRecordsAsync();
            var recordToRemove = allRecords.FirstOrDefault(r => r.Id == recordId);

            if (recordToRemove != null)
            {
                allRecords.Remove(recordToRemove);
                var json = JsonSerializer.Serialize(allRecords);
                await SecureStorage.SetAsync(CheckInRecordsKey, json);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"刪除打卡記錄失敗: {ex.Message}");
            return false;
        }
    }

    // T026: Create check-in with online database support
    public async Task<bool> CreateCheckInAsync(CheckInRecord record, bool isOnline)
    {
        try
        {
            if (isOnline && _databaseService != null)
            {
                // Try to save to online database
                var success = await _databaseService.SaveCheckInAsync(record);
                if (success)
                {
                    // Also save locally
                    await SaveCheckInRecordAsync(record);
                    return true;
                }
                return false;
            }
            else
            {
                // T027: Offline mode - queue for sync
                await QueueForSyncAsync(record);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    // T029: Update check-in (for check-out with notes)
    public async Task<bool> UpdateCheckInAsync(CheckInRecord record)
    {
        try
        {
            if (_databaseService != null)
            {
                var success = await _databaseService.UpdateCheckInAsync(record);
                if (success)
                {
                    await SaveCheckInRecordAsync(record);
                    return true;
                }
                return false;
            }
            else
            {
                // Fallback to local storage only
                return await SaveCheckInRecordAsync(record);
            }
        }
        catch
        {
            return false;
        }
    }

    // T027: Get pending sync records
    public async Task<List<CheckInRecord>> GetPendingSyncRecordsAsync()
    {
        try
        {
            var json = await GetStorageAsync(PendingSyncKey);
            if (string.IsNullOrEmpty(json))
                return new List<CheckInRecord>();

            var records = JsonSerializer.Deserialize<List<CheckInRecord>>(json);
            return records ?? new List<CheckInRecord>();
        }
        catch
        {
            return new List<CheckInRecord>();
        }
    }

    // T027: Sync pending records to database
    public async Task<bool> SyncPendingRecordsAsync()
    {
        try
        {
            if (_databaseService == null)
                return false;

            var pendingRecords = await GetPendingSyncRecordsAsync();
            if (pendingRecords.Count == 0)
                return true;

            // Try to sync all records
            foreach (var record in pendingRecords)
            {
                var success = await _databaseService.SaveCheckInAsync(record);
                if (!success)
                    return false;
            }

            // Clear pending queue on successful sync
            await SetStorageAsync(PendingSyncKey, JsonSerializer.Serialize(new List<CheckInRecord>()));
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Helper: Queue check-in for offline sync
    private async Task QueueForSyncAsync(CheckInRecord record)
    {
        var pendingRecords = await GetPendingSyncRecordsAsync();
        pendingRecords.Add(record);
        var json = JsonSerializer.Serialize(pendingRecords);
        await SetStorageAsync(PendingSyncKey, json);
    }
}