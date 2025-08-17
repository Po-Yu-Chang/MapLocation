using System.Text.Json;
using MapLocationApp.Models;

namespace MapLocationApp.Services;

public interface IGeocodingService
{
    Task<string> GetAddressFromCoordinatesAsync(double latitude, double longitude);
    Task<(double Latitude, double Longitude)?> GetCoordinatesFromAddressAsync(string address);
    Task<IEnumerable<SearchSuggestion>> GetLocationSuggestionsAsync(string query);
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

    public async Task<IEnumerable<SearchSuggestion>> GetLocationSuggestionsAsync(string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Enumerable.Empty<SearchSuggestion>();
            }

            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"https://nominatim.openstreetmap.org/search?format=json&q={encodedQuery}&limit=5&accept-language=zh-TW,zh&addressdetails=1";
            
            var response = await _httpClient.GetStringAsync(url);
            var jsonArray = JsonDocument.Parse(response).RootElement;
            
            var suggestions = new List<SearchSuggestion>();
            
            for (int i = 0; i < jsonArray.GetArrayLength(); i++)
            {
                var result = jsonArray[i];
                
                if (result.TryGetProperty("lat", out var lat) && 
                    result.TryGetProperty("lon", out var lon) &&
                    result.TryGetProperty("display_name", out var displayName))
                {
                    if (double.TryParse(lat.GetString(), out var latitude) && 
                        double.TryParse(lon.GetString(), out var longitude))
                    {
                        var fullAddress = displayName.GetString() ?? "";
                        var addressParts = fullAddress.Split(',').Select(p => p.Trim()).ToList();
                        
                        // 提取主要地址和次要地址
                        var mainText = addressParts.FirstOrDefault() ?? "";
                        var secondaryText = addressParts.Count > 1 ? 
                            string.Join(", ", addressParts.Skip(1).Take(2)) : "";
                        
                        // 如果有詳細地址資訊，嘗試更好的格式化
                        if (result.TryGetProperty("address", out var address))
                        {
                            var name = GetAddressComponent(address, "name") ?? 
                                      GetAddressComponent(address, "road") ??
                                      GetAddressComponent(address, "building");
                            
                            if (!string.IsNullOrEmpty(name))
                            {
                                mainText = name;
                            }
                            
                            var locality = GetAddressComponent(address, "city") ?? 
                                          GetAddressComponent(address, "town") ??
                                          GetAddressComponent(address, "village") ??
                                          GetAddressComponent(address, "county");
                            
                            var country = GetAddressComponent(address, "country");
                            
                            if (!string.IsNullOrEmpty(locality) || !string.IsNullOrEmpty(country))
                            {
                                var parts = new List<string>();
                                if (!string.IsNullOrEmpty(locality)) parts.Add(locality);
                                if (!string.IsNullOrEmpty(country)) parts.Add(country);
                                secondaryText = string.Join(", ", parts);
                            }
                        }
                        
                        suggestions.Add(new SearchSuggestion
                        {
                            MainText = mainText,
                            SecondaryText = secondaryText,
                            Latitude = latitude,
                            Longitude = longitude
                        });
                    }
                }
            }
            
            return suggestions;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"搜尋建議失敗: {ex.Message}");
            return Enumerable.Empty<SearchSuggestion>();
        }
    }
    
    private string GetAddressComponent(JsonElement address, string componentName)
    {
        if (address.TryGetProperty(componentName, out var component))
        {
            return component.GetString();
        }
        return null;
    }
}