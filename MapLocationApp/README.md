# MapLocation 打卡應用程式

基於 .NET MAUI 開發的地圖位置打卡應用程式，支援地理圍欄監控、自動打卡和位置追蹤功能。

## 專案概述

這是一個完整的企業級地圖打卡解決方案，實作了 MapLocation_Enhanced.md 文件中描述的所有核心功能，並於 v2.0.0 版本新增了六大重要擴展功能：離線地圖支援、路線規劃、團隊位置共享、詳細報表、多語言支援及深色模式。

本專案提供跨平台的位置服務解決方案，適用於企業員工打卡、團隊協作、位置追蹤等應用場景。

### 主要功能
- ✅ 多圖磚供應商支援（OpenStreetMap、CartoDB、Stamen 等）
- ✅ 地理圍欄監控與通知
- ✅ GPS 位置追蹤
- ✅ 手動/自動打卡功能
- ✅ 位置權限管理
- ✅ 隱私政策合規
- ✅ 跨平台支援（Android、iOS、Windows）
- ✅ **離線地圖支援** - 本地圖磚快取與區域下載
- ✅ **路線規劃功能** - 路線計算與導航支援
- ✅ **團隊成員位置共享** - 即時團隊協作功能
- ✅ **詳細的打卡報表** - 統計分析與匯出功能
- ✅ **多語言支援** - 支援繁體中文、簡體中文、日文、韓文
- ✅ **深色模式** - 自適應主題切換

## 技術架構

### 核心技術棧
- **.NET MAUI 9.0** - 跨平台 UI 框架
- **Mapsui 4.1.9** - 地圖渲染引擎
- **SkiaSharp** - 2D 圖形渲染
- **Microsoft.Maui.Essentials** - 平台功能整合

### 架構模式
- **MVVM** - 使用依賴注入的服務導向架構
- **Repository Pattern** - 資料存取抽象化
- **Factory Pattern** - 圖磚供應商管理

## 專案結構

```
MapLocationApp/
├── Models/                 # 資料模型
│   ├── TileProvider.cs    # 圖磚供應商模型
│   └── GeofenceRegion.cs  # 地理圍欄模型
├── Services/              # 業務邏輯服務
│   ├── IMapService.cs     # 地圖服務介面
│   ├── MapService.cs      # 地圖服務實作
│   ├── ILocationService.cs # 位置服務介面
│   ├── LocationService.cs  # 位置服務實作
│   ├── IGeofenceService.cs # 地理圍欄服務介面
│   ├── GeofenceService.cs  # 地理圍欄服務實作
│   ├── IApiService.cs     # API 服務介面
│   ├── ApiService.cs      # API 服務實作
│   ├── OfflineMapService.cs # 離線地圖服務
│   ├── RouteService.cs    # 路線規劃服務
│   ├── TeamLocationService.cs # 團隊位置共享服務
│   ├── ReportService.cs   # 報表統計服務
│   ├── LocalizationService.cs # 本地化服務
│   ├── GeocodingService.cs # 地理編碼服務
│   └── CheckInStorageService.cs # 打卡資料儲存服務
├── Views/                 # 頁面視圖
│   ├── MapPage.xaml       # 地圖頁面
│   ├── CheckInPage.xaml   # 打卡頁面
│   └── PrivacyPolicyPage.xaml # 隱私政策頁面
├── Platforms/             # 平台特定設定
│   ├── Android/           # Android 權限與設定
│   └── iOS/              # iOS 權限與設定
└── Tests/                # 測試檔案
```

## 核心服務說明

### 1. MapService (地圖服務)
- 支援多個圖磚供應商（OpenStreetMap、CartoDB、Stamen Terrain）
- 動態切換地圖圖磚
- 地理圍欄視覺化
- 位置標記管理
- Attribution 合規顯示

### 2. LocationService (位置服務)
- GPS 位置獲取
- 背景位置追蹤
- 位置權限管理
- 位置精確度檢查

### 3. GeofenceService (地理圍欄服務)
- 地理圍欄建立與管理
- 進入/離開事件監控
- 距離計算（Haversine 公式）
- 自動打卡觸發

### 4. ApiService (API 服務)
- RESTful API 通訊
- 打卡記錄同步
- 地理圍欄事件上傳
- 錯誤處理與重試機制

### 5. OfflineMapService (離線地圖服務)
- 地圖圖磚本地快取
- 區域地圖預下載
- 離線模式地圖顯示
- 快取管理與清理

### 6. RouteService (路線規劃服務)
- 兩點間路線計算
- 導航模式支援
- 路線匯出與儲存
- 距離與時間估算

### 7. TeamLocationService (團隊位置共享服務)
- 團隊建立與管理
- 即時位置共享
- 團隊成員狀態監控
- 位置歷史記錄

### 8. ReportService (報表統計服務)
- 打卡統計分析
- 圖表資料產生
- 報表匯出（JSON/CSV/HTML）
- 趨勢分析功能

### 9. LocalizationService (本地化服務)
- 多語言資源管理
- 動態語言切換
- 文化特定格式化
- XAML 本地化支援

## 權限設定

### Android 權限
```xml
<!-- 位置權限 -->
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
<uses-permission android:name="android.permission.ACCESS_BACKGROUND_LOCATION" />

<!-- 前景服務權限 -->
<uses-permission android:name="android.permission.FOREGROUND_SERVICE" />
<uses-permission android:name="android.permission.FOREGROUND_SERVICE_LOCATION" />
```

### iOS 權限
```xml
<key>NSLocationWhenInUseUsageDescription</key>
<string>我們需要您的位置以便提供地圖顯示和打卡功能。</string>

<key>NSLocationAlwaysAndWhenInUseUsageDescription</key>
<string>我們需要在背景中監控您的位置以便自動打卡和地理圍欄通知。</string>
```

## 建置指引

### 前置需求
- Visual Studio 2022 (17.8+)
- .NET 9.0 SDK
- .NET MAUI 工作負載

### 建置命令
```bash
# 還原 NuGet 套件
dotnet restore

# 建置專案
dotnet build

# Android 發布
dotnet publish -f net9.0-android -c Release

# iOS 發布 (需要 macOS)
dotnet publish -f net9.0-ios -c Release
```

## 隱私與合規

### OpenStreetMap 合規
- 正確顯示 Attribution：© OpenStreetMap contributors
- 支援多個圖磚供應商以減少對單一服務的依賴
- 遵循 OSM Tile Usage Policy

### 隱私保護
- 實作完整隱私政策頁面
- 位置資料本地化處理
- 明確的權限請求說明
- 使用者可控制的資料刪除

## 測試策略

### 單元測試
- 地理圍欄距離計算測試
- 位置服務功能測試
- API 服務模擬測試

### 整合測試
- 端到端打卡流程測試
- 地理圍欄進入/離開測試
- 權限請求流程測試

## 部署考量

### 效能最佳化
- 啟用 AOT 編譯（iOS）
- ProGuard/R8 最佳化（Android）
- 圖像資源壓縮
- 最小化應用程式套件大小

### 監控與分析
- 建議整合 Application Insights
- 當機報告收集
- 效能指標追蹤
- 電池使用情況監控

## 版本資訊

### v2.0.0 (最新版) - 2024年12月
**重大功能擴展版本**

#### 新增功能
- ✅ **離線地圖支援** - 地圖圖磚本地快取與區域預下載
- ✅ **路線規劃功能** - 兩點間路線計算、導航模式、路線儲存
- ✅ **團隊位置共享** - 團隊建立、即時位置共享、成員狀態監控
- ✅ **詳細打卡報表** - 統計分析、趨勢圖表、多格式匯出
- ✅ **多語言支援** - 支援繁體中文、簡體中文、日文、韓文
- ✅ **深色模式** - 自適應主題系統，跟隨系統設定

#### 技術改進
- 更新至 .NET MAUI 9.0
- 新增 9 個核心服務類別
- 完整的資源本地化系統
- 主題適應色彩系統
- 改善程式碼架構與可維護性

#### 檔案異動
- 新增 5 個多語言資源檔案
- 新增 5 個主要服務類別
- 新增設定頁面與相關 UI
- 更新色彩系統與主題支援

### v1.0.0 - 2024年12月初
**基礎版本**

#### 核心功能
- 地圖顯示與多圖磚供應商支援
- GPS 位置追蹤與地理圍欄
- 手動/自動打卡功能
- 位置權限管理
- 隱私政策合規
- 跨平台支援

#### 已知問題與限制

1. **Mapsui 版本相容性**：Mapsui 4.1.9 與 .NET MAUI 9.0 存在版本警告，建議等待套件更新。

2. **iOS 地理圍欄限制**：iOS 同時監控的地理圍欄數量限制為 20 個。

3. **背景位置限制**：Android 10+ 對背景位置權限有更嚴格的限制。

## 已完成的增強功能

- [x] 離線地圖支援 - 完整實作離線圖磚快取與區域下載
- [x] 路線規劃功能 - 支援路線計算、導航與路線儲存
- [x] 團隊成員位置共享 - 即時位置共享與團隊協作
- [x] 詳細的打卡報表 - 統計分析與多格式匯出
- [x] 多語言支援 - 支援繁體中文、簡體中文、日文、韓文
- [x] 深色模式 - 自適應主題系統

## 新增檔案清單

### 資源檔案
- `Resources/AppResources.resx` - 英文本地化資源
- `Resources/AppResources.zh-TW.resx` - 繁體中文資源
- `Resources/AppResources.zh-CN.resx` - 簡體中文資源  
- `Resources/AppResources.ja-JP.resx` - 日文資源
- `Resources/AppResources.ko-KR.resx` - 韓文資源

### 服務檔案
- `Services/OfflineMapService.cs` - 離線地圖服務
- `Services/RouteService.cs` - 路線規劃服務
- `Services/TeamLocationService.cs` - 團隊位置服務
- `Services/ReportService.cs` - 報表服務
- `Services/LocalizationService.cs` - 本地化服務

### 頁面檔案
- `Views/SettingsPage.xaml` - 設定頁面 UI
- `Views/SettingsPage.xaml.cs` - 設定頁面邏輯

### 樣式檔案
- `Resources/Styles/Colors.xaml` - 更新支援深色模式的色彩系統

## 使用指南

### 語言切換
1. 開啟應用程式設定頁面
2. 選擇所需語言（繁體中文、簡體中文、日文、韓文、英文）
3. 應用程式會立即切換語言顯示

### 深色模式
- 深色模式會自動跟隨系統設定
- 也可在設定頁面手動切換主題

### 離線地圖
1. 在地圖頁面選擇要下載的區域
2. 系統會自動下載該區域的地圖圖磚
3. 離線時仍可查看已下載的地圖區域

### 路線規劃
1. 在地圖上選擇起點和終點
2. 系統會計算最佳路線
3. 支援導航模式和路線匯出

### 團隊位置共享
1. 建立或加入團隊
2. 開啟位置共享功能
3. 即時查看團隊成員位置

### 打卡報表
1. 在報表頁面查看統計資料
2. 支援日報、週報、月報
3. 可匯出為 JSON、CSV 或 HTML 格式

## 未來改進計畫

- [ ] 推播通知整合
- [ ] 雲端同步功能
- [ ] 更多地圖供應商支援
- [ ] 進階地理圍欄功能
- [ ] 語音導航支援

## 授權

本專案遵循 MIT 授權條款。

## 貢獻指南

歡迎提交 Issue 和 Pull Request 來改善此專案。

---

**注意**：此專案是根據 MapLocation_Enhanced.md 技術規格文件實作的完整解決方案，包含了企業級應用程式所需的所有核心功能。雖然存在一些套件版本相容性問題，但整體架構完整且可擴展。