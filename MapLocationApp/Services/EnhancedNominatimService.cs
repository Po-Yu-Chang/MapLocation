using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    /// <summary>
    /// 增強版 Nominatim 地理編碼服務 - 基於您提供的示例代碼
    /// 使用 Newtonsoft.Json 進行 JSON 解析，提供更穩定的地址搜尋功能
    /// </summary>
    public class EnhancedNominatimService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://nominatim.openstreetmap.org";

        public EnhancedNominatimService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MapLocationApp/1.0 (Educational Use)");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// 搜尋地址並返回坐標 - 使用您的示例方法
        /// </summary>
        /// <param name="address">要搜尋的地址</param>
        /// <returns>坐標和詳細資訊</returns>
        public async Task<SearchResult?> SearchAddressAsync(string address)
        {
            try
            {
                string url = $"{BaseUrl}/search?q={Uri.EscapeDataString(address)}&format=json&limit=1&accept-language=zh-TW,zh&countrycodes=tw";

                var response = await _httpClient.GetStringAsync(url);
                JArray json = JArray.Parse(response);

                if (json.Count > 0)
                {
                    var result = json[0];
                    var lat = result["lat"]?.ToString();
                    var lon = result["lon"]?.ToString();
                    var displayName = result["display_name"]?.ToString();
                    var type = result["type"]?.ToString();
                    var category = result["class"]?.ToString();

                    if (double.TryParse(lat, out double latitude) && 
                        double.TryParse(lon, out double longitude))
                    {
                        return new SearchResult
                        {
                            Address = address,
                            DisplayName = displayName ?? address,
                            Latitude = latitude,
                            Longitude = longitude,
                            Type = type ?? "",
                            Category = category ?? "",
                            Success = true
                        };
                    }
                }

                return new SearchResult
                {
                    Address = address,
                    Success = false,
                    ErrorMessage = "無法找到地址"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"地址搜尋錯誤: {ex.Message}");
                return new SearchResult
                {
                    Address = address,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 獲取多個搜尋建議 - 改進版
        /// </summary>
        /// <param name="query">搜尋查詢</param>
        /// <returns>搜尋建議列表</returns>
        public async Task<List<SearchSuggestion>> GetSearchSuggestionsAsync(string query)
        {
            var suggestions = new List<SearchSuggestion>();

            try
            {
                string url = $"{BaseUrl}/search?q={Uri.EscapeDataString(query)}&format=json&limit=8&accept-language=zh-TW,zh&addressdetails=1&countrycodes=tw";

                var response = await _httpClient.GetStringAsync(url);
                JArray json = JArray.Parse(response);

                foreach (var item in json)
                {
                    var lat = item["lat"]?.ToString();
                    var lon = item["lon"]?.ToString();
                    var displayName = item["display_name"]?.ToString();
                    var name = item["name"]?.ToString();
                    var type = item["type"]?.ToString();
                    var category = item["class"]?.ToString();

                    if (double.TryParse(lat, out double latitude) && 
                        double.TryParse(lon, out double longitude))
                    {
                        // 建立主要文字和次要文字
                        var mainText = !string.IsNullOrEmpty(name) ? name : 
                                      displayName?.Split(',').FirstOrDefault()?.Trim() ?? "未知地點";

                        var addressParts = displayName?.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList() ?? new List<string>();
                        var secondaryText = addressParts.Count > 1 ? string.Join(", ", addressParts.Skip(1).Take(3)) : "";

                        // 添加類型圖標
                        var typeIcon = GetLocationTypeIcon(category, type);
                        if (!string.IsNullOrEmpty(typeIcon))
                        {
                            secondaryText = $"{typeIcon} {secondaryText}";
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

                System.Diagnostics.Debug.WriteLine($"✅ 找到 {suggestions.Count} 個搜尋結果: {query}");
                return suggestions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 搜尋建議錯誤: {ex.Message}");
                return suggestions;
            }
        }

        /// <summary>
        /// 測試方法 - 類似您的示例代碼
        /// </summary>
        public async Task TestTaipei101Async()
        {
            try
            {
                string address = "台北101";
                var result = await SearchAddressAsync(address);
                
                if (result?.Success == true)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ 測試成功!");
                    System.Diagnostics.Debug.WriteLine($"地址: {result.DisplayName}");
                    System.Diagnostics.Debug.WriteLine($"坐標: Lat={result.Latitude:F6}, Lng={result.Longitude:F6}");
                    System.Diagnostics.Debug.WriteLine($"類型: {result.Category}/{result.Type}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 測試失敗: {result?.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 測試異常: {ex.Message}");
            }
        }

        private string GetLocationTypeIcon(string? category, string? type)
        {
            return (category?.ToLower(), type?.ToLower()) switch
            {
                ("tourism", "attraction") => "🏛️",
                ("tourism", _) => "🗺️",
                ("amenity", "restaurant") => "🍴",
                ("amenity", "cafe") => "☕",
                ("amenity", "bank") => "🏦",
                ("amenity", "hospital") => "🏥",
                ("amenity", "school") => "🏫",
                ("amenity", "fuel") => "⛽",
                ("shop", _) => "🛍️",
                ("highway", _) => "🛣️",
                ("railway", "station") => "🚉",
                ("building", _) => "🏢",
                ("place", _) => "📍",
                _ => ""
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// 搜尋結果模型
    /// </summary>
    public class SearchResult
    {
        public string Address { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Type { get; set; } = "";
        public string Category { get; set; } = "";
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
    }
}