using MapLocationApp.Models;

namespace MapLocationApp.Services;

public interface IApiService
{
    Task<bool> SubmitCheckInAsync(CheckInRecord checkIn);
    Task<bool> SubmitGeofenceEventAsync(GeofenceEvent geofenceEvent);
    Task<List<CheckInRecord>> GetCheckInHistoryAsync(string userId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<List<GeofenceRegion>> SyncGeofencesAsync(string userId);
    Task<bool> UpdateLocationAsync(string userId, AppLocation location);
    Task<ApiResponse<T>> SendRequestAsync<T>(string endpoint, object data, HttpMethod method);
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }
}

public class LocationUpdateRequest
{
    public string UserId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
    public DateTime Timestamp { get; set; }
}