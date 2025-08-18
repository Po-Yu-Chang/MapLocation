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
        const int maxRetries = 2;
        int retryCount = 0;
        
        while (retryCount <= maxRetries)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 1)
                {
                    return Enumerable.Empty<SearchSuggestion>();
                }

                System.Diagnostics.Debug.WriteLine($"獲取搜尋建議 (嘗試 {retryCount + 1}): '{query}'");

                // 改善查詢參數以支援各種地址格式
                var suggestions = new List<SearchSuggestion>();
                
                // 1. 優先使用改善後的 Nominatim 查詢
                var nominatimSuggestions = await GetNominatimSuggestionsAsync(query);
                suggestions.AddRange(nominatimSuggestions);
                
                // 2. 如果結果不足，添加地標和商家搜尋
                if (suggestions.Count < 3)
                {
                    var landmarkSuggestions = await GetLandmarkSuggestionsAsync(query);
                    suggestions.AddRange(landmarkSuggestions.Where(s => !suggestions.Any(existing => 
                        Math.Abs(existing.Latitude - s.Latitude) < 0.001 && Math.Abs(existing.Longitude - s.Longitude) < 0.001)));
                }
                
                // 3. 如果還是不足，使用後備建議
                if (suggestions.Count < 2)
                {
                    var fallbackSuggestions = GetSmartFallbackSuggestions(query);
                    suggestions.AddRange(fallbackSuggestions.Where(s => !suggestions.Any(existing => 
                        Math.Abs(existing.Latitude - s.Latitude) < 0.001 && Math.Abs(existing.Longitude - s.Longitude) < 0.001)));
                }
                
                if (suggestions.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"成功獲得 {suggestions.Take(8).Count()} 個搜尋建議");
                    return suggestions.Take(8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"搜尋建議錯誤 (嘗試 {retryCount + 1}): {ex.Message}");
            }
            
            retryCount++;
            
            if (retryCount <= maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, retryCount - 1)));
            }
        }
        
        System.Diagnostics.Debug.WriteLine("所有重試都失敗，使用智能後備建議");
        return GetSmartFallbackSuggestions(query);
    }

    private async Task<List<SearchSuggestion>> GetNominatimSuggestionsAsync(string query)
    {
        var suggestions = new List<SearchSuggestion>();
        
        try
        {
            var queries = GenerateSearchQueries(query);
            
            foreach (var searchQuery in queries.Take(2)) // 限制搜尋次數
            {
                var encodedQuery = Uri.EscapeDataString(searchQuery);
                var url = $"https://nominatim.openstreetmap.org/search?format=json&q={encodedQuery}&limit=5&accept-language=zh-TW,zh&addressdetails=1&countrycodes=tw";
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var response = await _httpClient.GetStringAsync(url, cts.Token);
                
                if (!string.IsNullOrEmpty(response))
                {
                    var jsonArray = JsonDocument.Parse(response).RootElement;
                    
                    for (int i = 0; i < Math.Min(jsonArray.GetArrayLength(), 5); i++)
                    {
                        var result = jsonArray[i];
                        
                        if (result.TryGetProperty("lat", out var lat) && 
                            result.TryGetProperty("lon", out var lon))
                        {
                            if (double.TryParse(lat.GetString(), out var latitude) && 
                                double.TryParse(lon.GetString(), out var longitude))
                            {
                                var suggestion = CreateEnhancedSearchSuggestion(result, latitude, longitude, query);
                                if (suggestion != null && !suggestions.Any(s => 
                                    Math.Abs(s.Latitude - latitude) < 0.001 && Math.Abs(s.Longitude - longitude) < 0.001))
                                {
                                    suggestions.Add(suggestion);
                                }
                            }
                        }
                    }
                }
                
                if (suggestions.Count >= 5) break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Nominatim 搜尋錯誤: {ex.Message}");
        }
        
        return suggestions;
    }
    
    private List<string> GenerateSearchQueries(string query)
    {
        var queries = new List<string> { query };
        
        // 如果查詢包含數字，可能是地址
        if (System.Text.RegularExpressions.Regex.IsMatch(query, @"\d+"))
        {
            queries.Add($"{query} 台灣");
        }
        
        // 如果查詢很短，可能是路名或地標
        if (query.Length <= 4)
        {
            queries.Add($"{query}路");
            queries.Add($"{query}街");
            queries.Add($"{query}站");
        }
        
        // 如果包含"路"但沒有"段"，添加段數搜尋
        if (query.Contains("路") && !query.Contains("段"))
        {
            queries.Add($"{query}一段");
        }
        
        return queries;
    }
    
    private async Task<List<SearchSuggestion>> GetLandmarkSuggestionsAsync(string query)
    {
        var suggestions = new List<SearchSuggestion>();
        
        try
        {
            // 搜尋地標和商業場所
            var landmarkQuery = $"{query} landmark OR amenity OR shop OR tourism";
            var encodedQuery = Uri.EscapeDataString(landmarkQuery);
            var url = $"https://nominatim.openstreetmap.org/search?format=json&q={encodedQuery}&limit=3&accept-language=zh-TW,zh&addressdetails=1&countrycodes=tw";
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var response = await _httpClient.GetStringAsync(url, cts.Token);
            
            if (!string.IsNullOrEmpty(response))
            {
                var jsonArray = JsonDocument.Parse(response).RootElement;
                
                for (int i = 0; i < jsonArray.GetArrayLength(); i++)
                {
                    var result = jsonArray[i];
                    
                    if (result.TryGetProperty("lat", out var lat) && 
                        result.TryGetProperty("lon", out var lon))
                    {
                        if (double.TryParse(lat.GetString(), out var latitude) && 
                            double.TryParse(lon.GetString(), out var longitude))
                        {
                            var suggestion = CreateEnhancedSearchSuggestion(result, latitude, longitude, query);
                            if (suggestion != null)
                            {
                                suggestions.Add(suggestion);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"地標搜尋錯誤: {ex.Message}");
        }
        
        return suggestions;
    }

    private SearchSuggestion CreateEnhancedSearchSuggestion(JsonElement result, double latitude, double longitude, string originalQuery)
    {
        try
        {
            var fullAddress = result.GetProperty("display_name").GetString() ?? "";
            var addressParts = fullAddress.Split(',').Select(p => p.Trim()).ToList();
            
            var mainText = "";
            var secondaryText = "";
            var category = "";
            
            // 取得地點類型
            if (result.TryGetProperty("class", out var classElement))
            {
                category = classElement.GetString() ?? "";
            }
            
            // 如果有詳細地址資訊，進行智能格式化
            if (result.TryGetProperty("address", out var address))
            {
                mainText = ExtractMainAddress(address, category, originalQuery);
                secondaryText = ExtractSecondaryAddress(address, addressParts);
            }
            
            // 如果主要文字為空，使用第一個地址部分
            if (string.IsNullOrEmpty(mainText))
            {
                mainText = addressParts.FirstOrDefault() ?? "未知位置";
            }
            
            // 如果次要文字為空，使用後續地址部分
            if (string.IsNullOrEmpty(secondaryText) && addressParts.Count > 1)
            {
                secondaryText = string.Join(", ", addressParts.Skip(1).Take(3));
            }
            
            // 添加類型標示
            var typeIndicator = GetLocationTypeIndicator(category, address);
            if (!string.IsNullOrEmpty(typeIndicator))
            {
                secondaryText = $"{typeIndicator} • {secondaryText}";
            }
            
            return new SearchSuggestion
            {
                MainText = mainText,
                SecondaryText = secondaryText,
                Latitude = latitude,
                Longitude = longitude
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"創建搜尋建議錯誤: {ex.Message}");
            return null;
        }
    }

    private string ExtractMainAddress(JsonElement address, string category, string originalQuery)
    {
        // 優先級：商家名稱 > 建築物 > 道路 > 地標
        var candidates = new[]
        {
            GetAddressComponent(address, "name"),
            GetAddressComponent(address, "shop"),
            GetAddressComponent(address, "amenity"),
            GetAddressComponent(address, "building"),
            GetAddressComponent(address, "house_number") + " " + GetAddressComponent(address, "road"),
            GetAddressComponent(address, "road"),
            GetAddressComponent(address, "neighbourhood"),
            GetAddressComponent(address, "suburb")
        };
        
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate?.Trim()))
            {
                return candidate.Trim();
            }
        }
        
        return "";
    }
    
    private string ExtractSecondaryAddress(JsonElement address, List<string> addressParts)
    {
        var parts = new List<string>();
        
        // 添加區域資訊
        var district = GetAddressComponent(address, "city_district") ?? 
                      GetAddressComponent(address, "suburb") ??
                      GetAddressComponent(address, "neighbourhood");
        
        var city = GetAddressComponent(address, "city") ?? 
                   GetAddressComponent(address, "town") ??
                   GetAddressComponent(address, "village");
        
        var state = GetAddressComponent(address, "state");
        
        if (!string.IsNullOrEmpty(district)) parts.Add(district);
        if (!string.IsNullOrEmpty(city) && city != district) parts.Add(city);
        if (!string.IsNullOrEmpty(state) && state != city) parts.Add(state);
        
        if (parts.Any())
        {
            return string.Join(", ", parts);
        }
        
        // 後備：使用原始地址部分
        return addressParts.Count > 1 ? string.Join(", ", addressParts.Skip(1).Take(2)) : "";
    }
    
    private string GetLocationTypeIndicator(string category, JsonElement address)
    {
        // 根據地點類型返回圖標或標示
        return category switch
        {
            "amenity" => "🏢",
            "shop" => "🛍️",
            "tourism" => "🏛️",
            "highway" => "🛣️",
            "railway" => "🚉",
            "building" => "🏢",
            "place" => "📍",
            _ => GetAddressComponent(address, "amenity") switch
            {
                "restaurant" => "🍴",
                "cafe" => "☕",
                "bank" => "🏦",
                "hospital" => "🏥",
                "school" => "🏫",
                "fuel" => "⛽",
                _ => ""
            }
        };
    }
    
    private IEnumerable<SearchSuggestion> GetSmartFallbackSuggestions(string query)
    {
        var suggestions = new List<SearchSuggestion>();
        
        // 擴展的智能地點建議資料庫
        var smartPlaces = new[]
        {
            // 交通樞紐
            new { Name = "台北車站", Address = "台北市中正區", Type = "🚉 火車站", Lat = 25.0478, Lng = 121.5170, Keywords = new[] {"台北", "車站", "火車", "高鐵"} },
            new { Name = "桃園機場第一航廈", Address = "桃園市大園區", Type = "✈️ 機場", Lat = 25.0797, Lng = 121.2342, Keywords = new[] {"桃園", "機場", "航廈", "第一"} },
            new { Name = "桃園機場第二航廈", Address = "桃園市大園區", Type = "✈️ 機場", Lat = 25.0777, Lng = 121.2328, Keywords = new[] {"桃園", "機場", "航廈", "第二"} },
            new { Name = "高雄車站", Address = "高雄市三民區", Type = "🚉 火車站", Lat = 22.6391, Lng = 120.3022, Keywords = new[] {"高雄", "車站", "火車"} },
            new { Name = "台中車站", Address = "台中市中區", Type = "🚉 火車站", Lat = 24.1369, Lng = 120.6839, Keywords = new[] {"台中", "車站", "火車"} },
            
            // 知名地標
            new { Name = "台北101", Address = "台北市信義區信義路五段7號", Type = "🏢 地標", Lat = 25.0340, Lng = 121.5645, Keywords = new[] {"101", "台北", "信義", "摩天樓"} },
            new { Name = "中正紀念堂", Address = "台北市中正區中山南路21號", Type = "🏛️ 紀念堂", Lat = 25.0361, Lng = 121.5200, Keywords = new[] {"中正", "紀念堂"} },
            new { Name = "國父紀念館", Address = "台北市信義區仁愛路四段505號", Type = "🏛️ 紀念館", Lat = 25.0403, Lng = 121.5603, Keywords = new[] {"國父", "紀念館"} },
            
            // 商圈夜市
            new { Name = "西門町", Address = "台北市萬華區", Type = "🛍️ 商圈", Lat = 25.0420, Lng = 121.5085, Keywords = new[] {"西門", "西門町", "商圈"} },
            new { Name = "士林夜市", Address = "台北市士林區大東路、大南路", Type = "🍴 夜市", Lat = 25.0877, Lng = 121.5241, Keywords = new[] {"士林", "夜市"} },
            new { Name = "逢甲夜市", Address = "台中市西屯區文華路", Type = "🍴 夜市", Lat = 24.1797, Lng = 120.6478, Keywords = new[] {"逢甲", "夜市", "台中"} },
            new { Name = "饒河街夜市", Address = "台北市松山區饒河街", Type = "🍴 夜市", Lat = 25.0515, Lng = 121.5770, Keywords = new[] {"饒河", "夜市"} },
            
            // 醫院
            new { Name = "台大醫院", Address = "台北市中正區常德街1號", Type = "🏥 醫院", Lat = 25.0416, Lng = 121.5166, Keywords = new[] {"台大", "醫院"} },
            new { Name = "馬偕醫院", Address = "台北市中山區中山北路二段92號", Type = "🏥 醫院", Lat = 25.0588, Lng = 121.5234, Keywords = new[] {"馬偕", "醫院"} },
            
            // 學校
            new { Name = "台灣大學", Address = "台北市大安區羅斯福路四段1號", Type = "🏫 大學", Lat = 25.0173, Lng = 121.5397, Keywords = new[] {"台大", "大學", "台灣大學"} },
            new { Name = "政治大學", Address = "台北市文山區指南路二段64號", Type = "🏫 大學", Lat = 24.9876, Lng = 121.5732, Keywords = new[] {"政大", "政治大學"} },
            
            // 商業區
            new { Name = "信義商圈", Address = "台北市信義區", Type = "🏢 商圈", Lat = 25.0358, Lng = 121.5683, Keywords = new[] {"信義", "商圈", "購物"} },
            new { Name = "東區商圈", Address = "台北市大安區忠孝東路四段", Type = "🛍️ 商圈", Lat = 25.0418, Lng = 121.5514, Keywords = new[] {"東區", "忠孝", "商圈"} }
        };
        
        // 智能匹配
        foreach (var place in smartPlaces)
        {
            var score = CalculateMatchScore(query, place.Name, place.Address, place.Keywords);
            if (score > 0)
            {
                suggestions.Add(new SearchSuggestion
                {
                    MainText = place.Name,
                    SecondaryText = $"{place.Type} • {place.Address}",
                    Latitude = place.Lat,
                    Longitude = place.Lng
                });
            }
        }
        
        // 按匹配度排序
        suggestions = suggestions.OrderByDescending(s => 
            CalculateMatchScore(query, s.MainText, s.SecondaryText, new[] { s.MainText })).ToList();
        
        // 添加通用地址格式建議
        if (query.Length >= 2)
        {
            suggestions.AddRange(GenerateAddressFormatSuggestions(query));
        }

        System.Diagnostics.Debug.WriteLine($"提供 {suggestions.Count} 個智能後備建議");
        return suggestions.Take(5);
    }
    
    private double CalculateMatchScore(string query, string name, string address, string[] keywords)
    {
        var queryLower = query.ToLowerInvariant();
        double score = 0;
        
        // 完全匹配加分
        if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 10;
            
        if (address.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 5;
            
        // 關鍵字匹配
        foreach (var keyword in keywords)
        {
            if (keyword.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
                score += 3;
        }
        
        // 模糊匹配（部分字符匹配）
        if (queryLower.Length > 1)
        {
            foreach (char c in queryLower)
            {
                if (name.Contains(c, StringComparison.OrdinalIgnoreCase))
                    score += 0.5;
            }
        }
        
        return score;
    }
    
    private List<SearchSuggestion> GenerateAddressFormatSuggestions(string query)
    {
        var suggestions = new List<SearchSuggestion>();
        
        // 如果輸入看起來像路名
        if (query.Contains("路") || query.Contains("街") || query.Contains("大道"))
        {
            suggestions.Add(new SearchSuggestion
            {
                MainText = $"搜尋 \"{query}\" 沿線地點",
                SecondaryText = "🔍 在地圖上顯示相關道路",
                Latitude = 25.0330,  // 台北市中心
                Longitude = 121.5654
            });
        }
        
        return suggestions;
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