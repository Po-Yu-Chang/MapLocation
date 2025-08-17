using MapLocationApp.Models;
using System.Text.Json;

namespace MapLocationApp.Services;

public interface ICheckInStorageService
{
    Task<List<CheckInRecord>> GetCheckInRecordsAsync(DateTime date);
    Task<bool> SaveCheckInRecordAsync(CheckInRecord record);
    Task<List<CheckInRecord>> GetAllCheckInRecordsAsync();
    Task<bool> DeleteCheckInRecordAsync(string recordId);
}

public class CheckInStorageService : ICheckInStorageService
{
    private const string CheckInRecordsKey = "CheckInRecords";
    
    public event EventHandler<CheckInEventArgs>? CheckInRecorded;

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
            await SecureStorage.SetAsync(CheckInRecordsKey, json);
            
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
            var json = await SecureStorage.GetAsync(CheckInRecordsKey);
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
}