# MapLocation 路線規劃與導航功能擴展文件

## 📋 專案概述

本文件詳細記錄 MapLocation 應用程式中路線規劃頁面（`RoutePlanningPage.xaml.cs`）的功能需求分析、技術實作方案和開發 TODO List。目標是將導航功能提升至 Google Maps 等級的使用者體驗。

---

## 🔍 現有功能分析

### 已實作功能

#### 1. **位置搜尋與選擇**
- ✅ 起點和終點輸入功能
- ✅ 即時搜尋建議系統
- ✅ 使用目前位置作為起點
- ✅ 起終點交換功能
- ⚠️ 附近搜尋功能（僅佔位符）

#### 2. **路線計算與規劃**
- ✅ 多種交通方式支援（駕車/步行/單車/大眾運輸）
- ✅ 路線選項比較（最快/最短/環保路線）
- ✅ 路線資訊顯示（時間/距離/交通狀況）
- ✅ 路線儲存和管理功能

#### 3. **基礎導航功能**
- ✅ 開始/停止導航
- ✅ 導航會話管理（`NavigationSession`）
- ✅ 即時位置更新（5秒間隔）
- ✅ Telegram 通知整合

#### 4. **UI 互動與顯示**
- ✅ 路線選項選擇
- ✅ 交通方式切換
- ✅ 最近路線顯示
- ✅ 導航狀態更新

### 服務架構

```csharp
// 核心服務依賴
IRouteService              // 路線計算和管理
ILocationService           // GPS 定位服務
IGeocodingService          // 地理編碼服務
ITelegramNotificationService // 通知服務

// 資料模型
Route                      // 路線資料模型
RouteOption               // 路線選項模型
NavigationSession         // 導航會話模型
SearchSuggestion          // 搜尋建議模型
```

---

## 🎯 功能增強需求

### Google Maps 導航功能對標

參考 Google Maps 的導航體驗，需要增強以下核心功能：

1. **即時語音導航**
2. **精確位置追蹤**
3. **智慧路線重規劃**
4. **車道指引**
5. **到達目的地處理**

---

## 📝 開發 TODO List

### 🚀 **第一階段：核心導航功能**

#### **1. 語音導航系統**

**目標：** 實作類似 Google Maps 的語音導航指示

**技術實作：**

```csharp
// 1.1 TTS (Text-to-Speech) 服務實作
public interface ITTSService
{
    Task SpeakAsync(string text, string language = "zh-TW");
    Task SetSpeechRateAsync(float rate);
    Task SetVolumeAsync(float volume);
    bool IsSupported { get; }
}

// 平台特定實作
// Android: Android.Speech.Tts.TextToSpeech
// iOS: AVSpeechSynthesizer
// Windows: Windows.Media.SpeechSynthesis.SpeechSynthesizer
```

**參考資料：**
- [Microsoft.Maui TTS 官方文件](https://docs.microsoft.com/en-us/dotnet/maui/platform-integration/device-media/text-to-speech)
- [CommunityToolkit.Maui MediaElement](https://docs.microsoft.com/en-us/dotnet/communitytoolkit/maui/views/mediaelement)
- [Xamarin.Essentials TextToSpeech](https://docs.microsoft.com/en-us/xamarin/essentials/text-to-speech)

**實作步驟：**
- [ ] 建立 ITTSService 介面
- [ ] 實作平台特定的 TTS 服務
- [ ] 建立導航指令模板
- [ ] 整合語音播報時機
- [ ] 添加音量和語速控制

**導航指令範例：**
```csharp
public static class NavigationInstructions
{
    public const string TURN_RIGHT = "前方 {distance} 公尺右轉";
    public const string TURN_LEFT = "前方 {distance} 公尺左轉";
    public const string CONTINUE_STRAIGHT = "直行 {distance} 公尺";
    public const string ARRIVE_DESTINATION = "您已到達目的地";
    public const string RECALCULATING = "正在重新計算路線";
}
```

#### **1.2 多語言語音支援**

**實作方案：**
```csharp
public class LocalizedTTSService : ITTSService
{
    private readonly Dictionary<string, string> _languageCodes = new()
    {
        { "zh-TW", "zh-TW" },  // 繁體中文
        { "zh-CN", "zh-CN" },  // 簡體中文
        { "en-US", "en-US" },  // 英文
        { "ja-JP", "ja-JP" },  // 日文
        { "ko-KR", "ko-KR" }   // 韓文
    };
}
```

#### **2. 進階導航 UI**

**目標：** 建立類似 Google Maps 的導航介面

**UI 元件設計：**

```xml
<!-- 導航主視圖 -->
<Grid x:Name="NavigationView" IsVisible="{Binding IsNavigating}">
    <!-- 大型方向指示 -->
    <Frame x:Name="DirectionIndicator" 
           BackgroundColor="White" 
           CornerRadius="10"
           Padding="20">
        <StackLayout Orientation="Horizontal">
            <Image x:Name="DirectionArrow" 
                   Source="{Binding CurrentInstruction.ArrowIcon}"
                   WidthRequest="60" 
                   HeightRequest="60"/>
            <StackLayout>
                <Label x:Name="NextInstructionText"
                       Text="{Binding CurrentInstruction.Text}"
                       FontSize="18"
                       FontAttributes="Bold"/>
                <Label x:Name="DistanceToNext"
                       Text="{Binding CurrentInstruction.Distance}"
                       FontSize="14"
                       TextColor="Gray"/>
            </StackLayout>
        </StackLayout>
    </Frame>
    
    <!-- 路線進度 -->
    <StackLayout Orientation="Horizontal" 
                 BackgroundColor="LightBlue"
                 Padding="10">
        <Label Text="{Binding EstimatedArrivalTime}" FontSize="16"/>
        <Label Text="{Binding RemainingDistance}" FontSize="16"/>
        <Label Text="{Binding RemainingTime}" FontSize="16"/>
    </StackLayout>
</Grid>
```

**ViewModel 實作：**
```csharp
public class NavigationViewModel : INotifyPropertyChanged
{
    public NavigationInstruction CurrentInstruction { get; set; }
    public TimeSpan EstimatedArrivalTime { get; set; }
    public string RemainingDistance { get; set; }
    public string RemainingTime { get; set; }
    public bool IsNavigating { get; set; }
}

public class NavigationInstruction
{
    public string Text { get; set; }
    public string Distance { get; set; }
    public string ArrowIcon { get; set; }
    public NavigationType Type { get; set; }
}

public enum NavigationType
{
    TurnLeft, TurnRight, TurnSlightLeft, TurnSlightRight,
    Continue, UTurn, Merge, Exit, Arrive
}
```

### 🔄 **第二階段：智慧定位與追蹤**

#### **3. 高精度定位系統**

**目標：** 提升定位精度和穩定性

**技術實作：**

```csharp
public class AdvancedLocationService : ILocationService
{
    private readonly LocationManager _locationManager;
    private Location _lastKnownLocation;
    private readonly Queue<Location> _locationHistory = new(10);
    
    // 高精度定位請求
    private GeolocationRequest GetHighAccuracyRequest()
    {
        return new GeolocationRequest
        {
            DesiredAccuracy = GeolocationAccuracy.Best,
            Timeout = TimeSpan.FromSeconds(10),
            RequestFullAccuracy = true
        };
    }
    
    // 位置平滑算法
    public Location SmoothLocation(Location newLocation)
    {
        if (_locationHistory.Count < 3)
        {
            _locationHistory.Enqueue(newLocation);
            return newLocation;
        }
        
        // 卡爾曼濾波器實作
        return ApplyKalmanFilter(newLocation, _locationHistory.ToArray());
    }
    
    // 訊號強度監控
    public LocationSignalQuality GetSignalQuality(Location location)
    {
        if (location.Accuracy <= 5) return LocationSignalQuality.Excellent;
        if (location.Accuracy <= 10) return LocationSignalQuality.Good;
        if (location.Accuracy <= 20) return LocationSignalQuality.Fair;
        return LocationSignalQuality.Poor;
    }
}
```

**參考資料：**
- [Microsoft.Maui.Essentials Geolocation](https://docs.microsoft.com/en-us/dotnet/maui/platform-integration/device/geolocation)
- [Kalman Filter for GPS Tracking](https://github.com/villoren/KalmanFilter.NET)
- [高精度定位最佳實踐](https://developer.android.com/guide/topics/location/strategies)

#### **4. 路線偏離檢測**

**實作方案：**

```csharp
public class RouteTrackingService
{
    private const double ROUTE_DEVIATION_THRESHOLD = 50; // 公尺
    private const int CONSECUTIVE_DEVIATIONS_REQUIRED = 3;
    
    public async Task<RouteDeviationResult> CheckRouteDeviation(
        Location currentLocation, 
        Route currentRoute)
    {
        // 計算到最近路線點的距離
        var nearestPoint = FindNearestPointOnRoute(currentLocation, currentRoute);
        var distanceToRoute = CalculateDistance(currentLocation, nearestPoint);
        
        if (distanceToRoute > ROUTE_DEVIATION_THRESHOLD)
        {
            _consecutiveDeviations++;
            
            if (_consecutiveDeviations >= CONSECUTIVE_DEVIATIONS_REQUIRED)
            {
                return new RouteDeviationResult
                {
                    IsDeviated = true,
                    DeviationDistance = distanceToRoute,
                    SuggestedAction = RouteAction.Recalculate
                };
            }
        }
        else
        {
            _consecutiveDeviations = 0;
        }
        
        return new RouteDeviationResult { IsDeviated = false };
    }
}
```

### 🌐 **第三階段：即時交通資訊整合**

#### **5. 交通資料 API 整合**

**支援的 API 服務：**

1. **Google Maps Directions API**
```csharp
public class GoogleTrafficService : ITrafficService
{
    private const string API_BASE_URL = "https://maps.googleapis.com/maps/api/directions/json";
    
    public async Task<TrafficInfo> GetTrafficInfoAsync(double lat, double lng)
    {
        var requestUri = $"{API_BASE_URL}?origin={lat},{lng}&destination={destLat},{destLng}" +
                        $"&departure_time=now&traffic_model=best_guess&key={ApiKey}";
        
        var response = await _httpClient.GetAsync(requestUri);
        var content = await response.Content.ReadAsStringAsync();
        
        return ParseTrafficResponse(content);
    }
}
```

2. **HERE Traffic API**
```csharp
public class HereTrafficService : ITrafficService
{
    private const string API_BASE_URL = "https://traffic.ls.hereapi.com/traffic/6.3/flow.json";
    
    // 實作 HERE API 呼叫邏輯
}
```

**參考資料：**
- [Google Maps Platform Directions API](https://developers.google.com/maps/documentation/directions/overview)
- [HERE Traffic API](https://developer.here.com/documentation/traffic-api/dev_guide/index.html)
- [OpenStreetMap Routing](https://project-osrm.org/)

#### **6. 動態路線調整**

```csharp
public class DynamicRouteService : IRouteService
{
    public async Task<Route> RecalculateRouteAsync(
        Location currentLocation, 
        Location destination,
        TrafficCondition trafficCondition)
    {
        // 基於即時交通狀況重新計算路線
        var routeOptions = new RouteCalculationOptions
        {
            AvoidTraffic = true,
            TrafficModel = TrafficModel.BestGuess,
            DepartureTime = DateTime.Now,
            OptimizeFor = RouteOptimization.Time
        };
        
        return await CalculateOptimalRoute(currentLocation, destination, routeOptions);
    }
}
```

### 🎨 **第四階段：進階使用者體驗**

#### **7. 車道指引系統**

**實作目標：** 提供精確的車道變換指示

```csharp
public class LaneGuidanceService
{
    public class LaneInfo
    {
        public int LaneNumber { get; set; }
        public LaneDirection Direction { get; set; }
        public bool IsRecommended { get; set; }
        public string Instructions { get; set; }
    }
    
    public enum LaneDirection
    {
        Straight, Left, Right, SlightLeft, SlightRight, UTurn, Exit
    }
    
    public LaneInfo[] GetLaneGuidance(Location currentLocation, Route route)
    {
        // 分析當前位置的車道資訊
        // 返回建議車道和指示
    }
}
```

**UI 實作：**
```xml
<!-- 車道指引視圖 -->
<CollectionView x:Name="LaneGuidanceView" 
                ItemsSource="{Binding LaneInfo}"
                ItemsLayout="HorizontalList">
    <CollectionView.ItemTemplate>
        <DataTemplate>
            <Grid BackgroundColor="{Binding IsRecommended, Converter={StaticResource BoolToColorConverter}}">
                <Image Source="{Binding Direction, Converter={StaticResource DirectionToIconConverter}}"/>
            </Grid>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

#### **8. 到達目的地處理**

```csharp
public class DestinationArrivalService
{
    private const double ARRIVAL_THRESHOLD = 20; // 公尺
    
    public async Task<bool> CheckArrival(Location currentLocation, Location destination)
    {
        var distance = CalculateDistance(currentLocation, destination);
        
        if (distance <= ARRIVAL_THRESHOLD)
        {
            await HandleArrival();
            return true;
        }
        
        return false;
    }
    
    private async Task HandleArrival()
    {
        // 播放到達音效
        await _audioService.PlaySoundAsync("arrival_sound.mp3");
        
        // 震動提醒
        await Vibration.VibrateAsync(TimeSpan.FromMilliseconds(500));
        
        // 顯示到達通知
        await ShowArrivalNotification();
        
        // 建議停車場
        await SuggestParkingOptions();
        
        // 儲存導航記錄
        await SaveNavigationHistory();
    }
}
```

### 📱 **第五階段：個人化與優化**

#### **9. 使用者偏好設定**

```csharp
public class NavigationPreferences
{
    public bool AvoidTolls { get; set; } = false;
    public bool AvoidHighways { get; set; } = false;
    public bool AvoidFerries { get; set; } = false;
    public VoiceGuidanceLevel VoiceLevel { get; set; } = VoiceGuidanceLevel.Normal;
    public string PreferredLanguage { get; set; } = "zh-TW";
    public RouteOptimization DefaultOptimization { get; set; } = RouteOptimization.Time;
}

public enum VoiceGuidanceLevel
{
    Off, Essential, Normal, Detailed
}
```

#### **10. 離線地圖支援**

```csharp
public class OfflineMapService
{
    public async Task<bool> DownloadMapRegionAsync(GeoBounds region)
    {
        // 下載指定區域的離線地圖資料
        var mapTiles = await _mapTileService.GetTilesForRegionAsync(region);
        await _localStorage.SaveMapTilesAsync(mapTiles);
        return true;
    }
    
    public async Task<Route> CalculateOfflineRouteAsync(Location start, Location end)
    {
        // 使用離線資料計算路線
        var localMapData = await _localStorage.GetMapDataAsync(start, end);
        return await _routingEngine.CalculateRouteAsync(localMapData, start, end);
    }
}
```

**參考資料：**
- [Mapsui 離線地圖](https://github.com/Mapsui/Mapsui)
- [SQLite-net for local storage](https://github.com/praeclarum/sqlite-net)

---

## 🔧 技術架構建議

### 服務層架構

```csharp
// 核心導航服務介面
public interface INavigationService
{
    Task StartNavigationAsync(Route route);
    Task StopNavigationAsync();
    Task<NavigationStatus> GetCurrentStatusAsync();
    event EventHandler<NavigationInstruction> InstructionUpdated;
    event EventHandler<Location> LocationUpdated;
}

// 導航狀態管理
public class NavigationState
{
    public Route CurrentRoute { get; set; }
    public Location CurrentLocation { get; set; }
    public NavigationInstruction NextInstruction { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
    public double DistanceRemaining { get; set; }
    public bool IsActive { get; set; }
}
```

### 依賴注入設定

```csharp
// MauiProgram.cs
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // 註冊導航相關服務
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<ITTSService, TTSService>();
        builder.Services.AddSingleton<ITrafficService, GoogleTrafficService>();
        builder.Services.AddSingleton<IOfflineMapService, OfflineMapService>();
        builder.Services.AddSingleton<ILaneGuidanceService, LaneGuidanceService>();

        return builder.Build();
    }
}
```

---

## 📊 測試策略

### 單元測試

```csharp
[Test]
public async Task RouteDeviation_ShouldTriggerRecalculation_WhenOffRoute()
{
    // Arrange
    var routeTrackingService = new RouteTrackingService();
    var currentLocation = new Location(25.0330, 121.5654);
    var route = CreateTestRoute();

    // Act
    var result = await routeTrackingService.CheckRouteDeviation(currentLocation, route);

    // Assert
    Assert.IsTrue(result.IsDeviated);
    Assert.AreEqual(RouteAction.Recalculate, result.SuggestedAction);
}
```

### 整合測試

```csharp
[Test]
public async Task NavigationFlow_ShouldCompleteSuccessfully()
{
    // 測試完整的導航流程
    var navigationService = ServiceProvider.GetService<INavigationService>();
    var route = await CreateTestRoute();
    
    await navigationService.StartNavigationAsync(route);
    
    // 模擬位置變更
    await SimulateLocationUpdates();
    
    // 驗證導航指令
    Assert.IsNotNull(navigationService.CurrentInstruction);
}
```

---

## 🚀 部署與發布

### 效能監控

```csharp
public class NavigationPerformanceMonitor
{
    public void TrackLocationUpdateLatency(TimeSpan latency)
    {
        // 追蹤位置更新延遲
        ApplicationInsights.TrackMetric("LocationUpdateLatency", latency.TotalMilliseconds);
    }
    
    public void TrackRouteCalculationTime(TimeSpan calculationTime)
    {
        // 追蹤路線計算時間
        ApplicationInsights.TrackMetric("RouteCalculationTime", calculationTime.TotalSeconds);
    }
}
```

### 錯誤處理與記錄

```csharp
public class NavigationErrorHandler
{
    public async Task HandleGPSLostAsync()
    {
        // GPS 訊號丟失處理
        await ShowUserNotification("GPS 訊號丟失，請檢查位置設定");
        await SwitchToOfflineMode();
    }
    
    public async Task HandleNetworkErrorAsync(Exception ex)
    {
        // 網路錯誤處理
        Logger.LogError(ex, "Navigation network error");
        await EnableOfflineNavigation();
    }
}
```

---

## 📚 參考資料與學習資源

### 官方文件
- [.NET MAUI 官方文件](https://docs.microsoft.com/en-us/dotnet/maui/)
- [Xamarin.Essentials API](https://docs.microsoft.com/en-us/xamarin/essentials/)
- [Microsoft.Maui.Maps](https://docs.microsoft.com/en-us/dotnet/maui/user-interface/controls/map)

### 第三方函式庫
- [Mapsui - .NET Map Component](https://github.com/Mapsui/Mapsui)
- [Plugin.Geolocator](https://github.com/jamesmontemagno/GeolocatorPlugin)
- [CommunityToolkit.Maui](https://github.com/CommunityToolkit/Maui)

### 地圖服務 API
- [Google Maps Platform](https://developers.google.com/maps)
- [HERE Developer Portal](https://developer.here.com/)
- [OpenStreetMap](https://www.openstreetmap.org/)
- [Mapbox](https://www.mapbox.com/)

### 導航演算法
- [A* Path Finding Algorithm](https://en.wikipedia.org/wiki/A*_search_algorithm)
- [Dijkstra's Algorithm](https://en.wikipedia.org/wiki/Dijkstra%27s_algorithm)
- [OSRM - Open Source Routing Machine](http://project-osrm.org/)

### 效能優化
- [.NET MAUI Performance Tips](https://docs.microsoft.com/en-us/dotnet/maui/deployment/performance)
- [Mobile App Performance Best Practices](https://docs.microsoft.com/en-us/xamarin/cross-platform/deploy-test/memory-perf-best-practices)

---

## 🎯 階段性里程碑

### 第一季度目標
- [ ] 完成語音導航基礎架構
- [ ] 實作高精度定位系統
- [ ] 建立導航 UI 框架

### 第二季度目標
- [ ] 整合即時交通資訊
- [ ] 實作路線偏離檢測
- [ ] 完成車道指引功能

### 第三季度目標
- [ ] 完成離線地圖支援
- [ ] 實作個人化設定
- [ ] 進行效能優化

### 第四季度目標
- [ ] 完成整合測試
- [ ] 效能調優
- [ ] 準備正式發布

---

這份文件將持續更新，隨著開發進度和新需求的出現進行修正和補充。建議每個開發階段結束後，回顧並更新相關章節的內容。
