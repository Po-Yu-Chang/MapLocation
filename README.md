# MapLocation

.NET MAUI 跨平台地圖定位應用程式，提供地理圍欄、人臉辨識、導航、團隊協作與離線地圖等功能。

## 專案概述

MapLocation 是一個功能完整的 .NET 9.0 MAUI 跨平台應用程式，支援 Android、iOS、Windows 和 macOS 平台。採用現代化設計系統，提供直覺的使用者介面和豐富的地圖相關功能。

## 核心功能

### 地圖與定位
- **地圖顯示與互動** - 基於 Mapsui 4.1.9 的高效能地圖渲染
- **即時定位追蹤** - 支援跨平台的位置服務（包含 Windows 增強版）
- **地理圍欄** - 使用 NetTopologySuite 進行空間運算
- **地址編碼與反編碼** - 地址與座標轉換
- **離線地圖** - 本地地圖快取功能

### 導航與路線
- **轉向導航** - 即時語音導航指引
- **路線規劃** - 多點路徑計算
- **語音播報** - 整合 TTS 服務

### 進階功能
- **人臉辨識** - Windows 平台專屬（使用 FaceAiSharp + DirectML GPU 加速）
- **團隊位置分享** - 即時團隊成員位置追蹤
- **位置打卡** - 位置簽到功能
- **Telegram 通知** - Bot 整合通知服務

### UI/UX
- **ModernTheme 設計系統** - 高質感現代化介面
- **液態玻璃動畫** - 流暢的視覺效果（LiquidGlassAnimations）
- **多國語言支援** - 繁體中文、英文、日文、韓文

## 技術架構

### 框架與核心
- **.NET 9.0 MAUI** - 跨平台應用程式框架
- **C# 12** - 程式語言
- **Nullable Reference Types** - 型別安全

### 地圖與空間運算
- **Mapsui 4.1.9** - 地圖渲染引擎
- **NetTopologySuite** - 空間資料處理與地理圍欄

### AI 與機器學習
- **FaceAiSharp.Bundle** - 人臉辨識（Windows DirectML）

### 資料儲存
- **MySQL** - 主要資料庫（使用者資料、位置資訊）
- **SQLite** - 本地設定與人臉資料

### UI 組件
- **CommunityToolkit.Maui** - MAUI 擴充控制項與相機支援
- **SkiaSharp** - 2D 圖形渲染

### 其他
- **Newtonsoft.Json** - JSON 序列化

## 服務導向架構

### 核心服務
```
IMapService              - 地圖控制與渲染
ILocationService         - 平台定位服務
IGeofenceService         - 地理圍欄管理
IGeocodingService        - 地址編碼服務
INavigationService       - 導航與路線規劃
ITTSService              - 文字轉語音
```

### 資料服務
```
IDatabaseService         - MySQL 資料庫操作
IConfigService           - SQLite 設定管理
IUserSessionService      - 使用者認證與會話
CheckInStorageService    - 位置打卡
```

### 進階服務
```
IFaceRecognitionService  - 人臉辨識（Windows 限定）
ITeamLocationService     - 團隊位置分享
ITelegramNotificationService - Telegram Bot 通知
IOfflineMapService       - 離線地圖快取
LocalizationService      - 多語系支援
```

## 專案結構

```
MapLocation/
├── MapLocationApp/           # 主要應用程式
│   ├── Animations/           # 液態玻璃動畫系統
│   ├── Pages/                # 頁面視圖
│   ├── Services/             # 服務實作
│   ├── Models/               # 資料模型
│   ├── Resources/            # 資源與多語系
│   └── Platforms/            # 平台特定實作
├── Tests/                    # 測試專案（待實作）
├── CLAUDE.md                 # Claude Code 專案說明
├── create_database.sql       # 資料庫建立腳本
└── database_init.sql         # 資料庫初始化腳本
```

## 開發指令

### 建置專案
```bash
# 建置整個解決方案
dotnet build MapLocation.sln

# 建置特定平台
dotnet build MapLocationApp/MapLocationApp.csproj -f net9.0-android
dotnet build MapLocationApp/MapLocationApp.csproj -f net9.0-ios
dotnet build MapLocationApp/MapLocationApp.csproj -f net9.0-windows10.0.19041.0
dotnet build MapLocationApp/MapLocationApp.csproj -f net9.0-maccatalyst
```

### 執行應用程式
```bash
# Windows
dotnet run --project MapLocationApp/MapLocationApp.csproj -f net9.0-windows10.0.19041.0
```

### 清理與還原
```bash
# 清理建置產物
dotnet clean MapLocation.sln

# 還原 NuGet 套件
dotnet restore MapLocation.sln
```

### 套件管理
```bash
# 新增套件
dotnet add MapLocationApp/MapLocationApp.csproj package <PackageName>
```

## 環境需求

### 開發環境
- .NET 9.0 SDK
- Visual Studio 2022 (17.8+) 或 Visual Studio Code
- MAUI Workload

### 平台需求
- **Windows**: Windows 10 (19041+) 或更新版本
- **Android**: API Level 21+
- **iOS**: iOS 14+
- **macOS**: macOS 10.15+

### 資料庫
- MySQL Server（使用者資料）
- SQLite（本地設定）

## 設定說明

### 資料庫設定
1. 執行 `create_database.sql` 建立資料庫
2. 執行 `database_init.sql` 初始化資料表
3. 在應用程式設定中配置 MySQL 連線字串

### Telegram Bot（選用）
在應用程式設定中配置 Telegram Bot Token

## 平台特定功能

### Windows
- 完整人臉辨識支援（FaceAiSharp + DirectML）
- 增強的定位服務（WindowsLocationService）

### Android / iOS
- 標準定位服務
- 相機整合

### 所有平台
- 地圖顯示與操作
- 地理圍欄
- 導航與路線規劃
- 團隊位置分享
- 離線地圖

## 測試

測試專案結構已建立，但尚未實作測試案例。

新增測試專案：
```bash
dotnet new mstest -n MapLocationApp.Tests
dotnet sln add MapLocationApp.Tests/MapLocationApp.Tests.csproj
```

## 重要提醒

- **人臉辨識僅支援 Windows 平台**（受 FaceAiSharp 限制）
- 所有平台需要位置權限
- Telegram 整合需要設定 Bot Token
- MySQL 連線字串需在應用程式設定中配置

## 授權

本專案為私有專案。

## 貢獻

本專案由 Po-Yu-Chang 開發維護。

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
