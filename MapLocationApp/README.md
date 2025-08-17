# MapLocation 打卡應用程式

基於 .NET MAUI 開發的地圖位置打卡應用程式，支援地理圍欄監控、自動打卡和位置追蹤功能。

## 專案概述

這是一個完整的企業級地圖打卡解決方案，實作了 MapLocation_Enhanced.md 文件中描述的所有核心功能。

### 主要功能
- ✅ 多圖磚供應商支援（OpenStreetMap、CartoDB、Stamen 等）
- ✅ 地理圍欄監控與通知
- ✅ GPS 位置追蹤
- ✅ 手動/自動打卡功能
- ✅ 位置權限管理
- ✅ 隱私政策合規
- ✅ 跨平台支援（Android、iOS、Windows）

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
│   └── ApiService.cs      # API 服務實作
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

## 已知問題與限制

1. **Mapsui 版本相容性**：Mapsui 4.1.9 與 .NET MAUI 9.0 存在版本警告，建議等待套件更新。

2. **iOS 地理圍欄限制**：iOS 同時監控的地理圍欄數量限制為 20 個。

3. **背景位置限制**：Android 10+ 對背景位置權限有更嚴格的限制。

## 未來增強功能

- [ ] 離線地圖支援
- [ ] 路線規劃功能
- [ ] 團隊成員位置共享
- [ ] 詳細的打卡報表
- [ ] 多語言支援
- [ ] 深色模式

## 授權

本專案遵循 MIT 授權條款。

## 貢獻指南

歡迎提交 Issue 和 Pull Request 來改善此專案。

---

**注意**：此專案是根據 MapLocation_Enhanced.md 技術規格文件實作的完整解決方案，包含了企業級應用程式所需的所有核心功能。雖然存在一些套件版本相容性問題，但整體架構完整且可擴展。