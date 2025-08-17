using System.Text.Json;

namespace MapLocationApp.Services;

public interface IGeocodingService
{
    Task<string> GetAddressFromCoordinatesAsync(double latitude, double longitude);
    Task<(double Latitude, double Longitude)?> GetCoordinatesFromAddressAsync(string address);
}

public class GeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    
    public GeocodingService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MapLocationApp/1.0");
    }

    public async Task<string> GetAddressFromCoordinatesAsync(double latitude, double longitude)
    {
        try
        {
            // 使用 OpenStreetMap Nominatim API（免費）
            var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latitude:F6}&lon={longitude:F6}&zoom=18&addressdetails=1&accept-language=zh-TW,zh";
            
            var response = await _httpClient.GetStringAsync(url);
            var jsonDoc = JsonDocument.Parse(response);
            
            if (jsonDoc.RootElement.TryGetProperty("display_name", out var displayName))
            {
                var fullAddress = displayName.GetString() ?? "未知位置";
                
                // 嘗試提取更簡潔的地址
                if (jsonDoc.RootElement.TryGetProperty("address", out var address))
                {
                    var addressParts = new List<string>();
                    
                    // 按優先順序提取地址組件
                    var components = new[] { "road", "neighbourhood", "suburb", "city", "town", "village", "county", "state", "country" };
                    
                    foreach (var component in components)
                    {
                        if (address.TryGetProperty(component, out var value))
                        {
                            var componentValue = value.GetString();
                            if (!string.IsNullOrEmpty(componentValue) && addressParts.Count < 3)
                            {
                                addressParts.Add(componentValue);
                            }
                        }
                    }
                    
                    if (addressParts.Any())
                    {
                        return string.Join(", ", addressParts);
                    }
                }
                
                return fullAddress;
            }
            
            return "未知位置";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"地址解析失敗: {ex.Message}");
            return $"位置 ({latitude:F4}, {longitude:F4})";
        }
    }

    public async Task<(double Latitude, double Longitude)?> GetCoordinatesFromAddressAsync(string address)
    {
        try
        {
            var encodedAddress = Uri.EscapeDataString(address);
            var url = $"https://nominatim.openstreetmap.org/search?format=json&q={encodedAddress}&limit=1&accept-language=zh-TW,zh";
            
            var response = await _httpClient.GetStringAsync(url);
            var jsonArray = JsonDocument.Parse(response).RootElement;
            
            if (jsonArray.GetArrayLength() > 0)
            {
                var firstResult = jsonArray[0];
                
                if (firstResult.TryGetProperty("lat", out var lat) && 
                    firstResult.TryGetProperty("lon", out var lon))
                {
                    if (double.TryParse(lat.GetString(), out var latitude) && 
                        double.TryParse(lon.GetString(), out var longitude))
                    {
                        return (latitude, longitude);
                    }
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"座標查詢失敗: {ex.Message}");
            return null;
        }
    }
}