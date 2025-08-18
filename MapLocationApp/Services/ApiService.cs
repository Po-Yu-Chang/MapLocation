using MapLocationApp.Models;
using System.Text;
using System.Text.Json;

namespace MapLocationApp.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiService()
    {
        _httpClient = new HttpClient();
        _baseUrl = "https://your-api-server.com/api"; // 替換為實際的 API 端點
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // 設定 HTTP 客戶端
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MapLocationApp/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<bool> SubmitCheckInAsync(CheckInRecord checkIn)
    {
        try
        {
            var response = await SendRequestAsync<object>("checkins", checkIn, HttpMethod.Post);
            return response.Success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"提交打卡記錄失敗: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SubmitGeofenceEventAsync(GeofenceEvent geofenceEvent)
    {
        try
        {
            var response = await SendRequestAsync<object>("geofence-events", geofenceEvent, HttpMethod.Post);
            return response.Success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"提交地理圍欄事件失敗: {ex.Message}");
            return false;
        }
    }

    public async Task<List<CheckInRecord>> GetCheckInHistoryAsync(string userId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var queryParams = new List<string> { $"userId={userId}" };
            
            if (fromDate.HasValue)
                queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
            
            if (toDate.HasValue)
                queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");

            var endpoint = $"checkins?{string.Join("&", queryParams)}";
            var response = await SendRequestAsync<List<CheckInRecord>>(endpoint, null, HttpMethod.Get);
            
            return response.Data ?? new List<CheckInRecord>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"獲取打卡歷史失敗: {ex.Message}");
            return new List<CheckInRecord>();
        }
    }

    public async Task<List<GeofenceRegion>> SyncGeofencesAsync(string userId)
    {
        try
        {
            var endpoint = $"geofences?userId={userId}";
            var response = await SendRequestAsync<List<GeofenceRegion>>(endpoint, null, HttpMethod.Get);
            
            return response.Data ?? new List<GeofenceRegion>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"同步地理圍欄失敗: {ex.Message}");
            return new List<GeofenceRegion>();
        }
    }

    public async Task<bool> UpdateLocationAsync(string userId, AppLocation location)
    {
        try
        {
            var locationUpdate = new LocationUpdateRequest
            {
                UserId = userId,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Accuracy = location.Accuracy ?? 0,
                Timestamp = location.Timestamp
            };

            var response = await SendRequestAsync<object>("locations", locationUpdate, HttpMethod.Post);
            return response.Success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"更新位置失敗: {ex.Message}");
            return false;
        }
    }

    public async Task<ApiResponse<T>> SendRequestAsync<T>(string endpoint, object? data, HttpMethod method)
    {
        try
        {
            var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
            var request = new HttpRequestMessage(method, url);

            if (data != null && (method == HttpMethod.Post || method == HttpMethod.Put))
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            // 如果有認證 token，在此處添加
            // request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                T? responseData = default(T);
                
                if (!string.IsNullOrEmpty(responseContent) && typeof(T) != typeof(object))
                {
                    responseData = JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
                }

                return new ApiResponse<T>
                {
                    Success = true,
                    Data = responseData,
                    StatusCode = (int)response.StatusCode
                };
            }
            else
            {
                return new ApiResponse<T>
                {
                    Success = false,
                    ErrorMessage = $"HTTP {response.StatusCode}: {responseContent}",
                    StatusCode = (int)response.StatusCode
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                ErrorMessage = $"網路錯誤: {ex.Message}",
                StatusCode = 0
            };
        }
        catch (TaskCanceledException ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                ErrorMessage = "請求超時",
                StatusCode = 0
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                ErrorMessage = $"未知錯誤: {ex.Message}",
                StatusCode = 0
            };
        }
    }

    // 模擬模式 - 當沒有實際後端時使用
    public static class MockApiService
    {
        private static readonly List<CheckInRecord> _mockCheckIns = new();
        private static readonly List<GeofenceEvent> _mockEvents = new();

        public static Task<bool> SubmitCheckInAsync(CheckInRecord checkIn)
        {
            _mockCheckIns.Add(checkIn);
            System.Diagnostics.Debug.WriteLine($"模擬提交打卡: {checkIn.GeofenceName} at {checkIn.CheckInTime}");
            return Task.FromResult(true);
        }

        public static Task<bool> SubmitGeofenceEventAsync(GeofenceEvent geofenceEvent)
        {
            _mockEvents.Add(geofenceEvent);
            System.Diagnostics.Debug.WriteLine($"模擬地理圍欄事件: {geofenceEvent.TransitionType} {geofenceEvent.GeofenceName}");
            return Task.FromResult(true);
        }

        public static Task<List<CheckInRecord>> GetCheckInHistoryAsync(string userId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var filtered = _mockCheckIns.Where(c => c.UserId == userId);
            
            if (fromDate.HasValue)
                filtered = filtered.Where(c => c.CheckInTime >= fromDate.Value);
                
            if (toDate.HasValue)
                filtered = filtered.Where(c => c.CheckInTime <= toDate.Value);

            return Task.FromResult(filtered.ToList());
        }
    }
}