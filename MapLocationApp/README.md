# MapLocation 打卡 / 導航 / 人臉辨識應用程式

基於 .NET MAUI (.NET 9) 開發的跨平台智慧位置與基礎人臉辨識解決方案，整合地圖、地理圍欄、打卡、路線規劃、團隊位置共享、報表，以及 (Windows 先行) FaceAiSharp 人臉偵測 / 辨識功能。採用 Liquid Glass 視覺主題與模組化服務架構，並提供關閉釋放服務設計避免背景殘留行程。

## 專案概述

這是一個朝企業級延展性的定位與人臉基礎辨識整合專案。早期規格（MapLocation_Enhanced.md）所列功能已被重構並增加 Windows 端人臉辨識、Liquid Glass UI、資源釋放機制與資料庫設定腳本。

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
- ✅ **Liquid Glass UI 主題** - iPhone 風格毛玻璃 + Glow 裝飾
- ✅ **Windows 人臉偵測/辨識 (FaceAiSharp)** - 支援臉部偵測、Embedding 產生、向量比對、快取
- ✅ **關閉釋放服務** - AppShutdownService 停止計時器 / 釋放 FaceAiSharp / 定位背景工作

> 注意：相機即時串流 + 跨平台 (Android / iOS) 人臉辨識仍在 Roadmap；目前 Windows 支援檔案載入 / 單張辨識與演示模式 fallback。

## 技術架構

### 核心技術棧
- **.NET MAUI 9.0** - 跨平台 UI 框架
- **Mapsui 4.1.9** - 地圖渲染引擎
- **SkiaSharp** - 2D 圖形渲染
- **Microsoft.Maui.Essentials** - 平台功能整合
- **FaceAiSharp.Bundle** (Windows) - SCRFD 偵測 + ArcFace Embedding 產生
- **Microsoft.ML.OnnxRuntime / DirectML** - GPU/CPU 推理
- **SixLabors.ImageSharp** - 圖片載入 / 前處理

### 架構模式
- **MVVM** - 使用依賴注入的服務導向架構
- **Repository Pattern** - 資料存取抽象化
- **Factory Pattern** - 圖磚供應商管理
- **資源釋放與關閉管理** - IAppShutdownService 統一釋放 / 停止背景執行

### 人臉辨識子系統 (Windows 先行)
| 元件 | 說明 |
|------|------|
| FaceAiSharpService | 實作 IFaceRecognitionService：初始化、Detect、Recognize、Save/DB、事件觸發 |
| FaceDatabase (SQLite) | 儲存 Name / FeatureVectorJson / Metadata，啟動載入快取 |
| Demo Fallback | 若模型載入失敗 (缺 DLL / VC++ 等)，改為演示模式生成一致特徵向量 |
| Threshold | 同人閾值測試中 (預設 0.1 低門檻 → 需按實際資料調整) |
| AppShutdownService | 關閉時 Dispose 偵測器/嵌入產生器、停止定位分享 |

#### 執行流程 (單張辨識)
1. 讀檔 → ImageSharp 載入為 Rgb24
2. SCRFD 偵測人臉 + 標記 Landmarks
3. 對齊 (AlignFaceUsingLandmarks)
4. 產生 Embedding (float[] 512)
5. 與快取內已知 Embeddings 計算 Cosine Similarity → 達閾值即回傳姓名
6. 事件 FaceRecognized 觸發，UI 反映結果

#### 環境需求 (Windows)
- Windows 10 19041+ / x64
- .NET 9 SDK
- Visual C++ Redistributable (若 FaceAiSharp 初始化失敗要安裝)
- 可選：支援 DirectML 的 GPU 驅動 (提升推理效能)

#### 常見問題
| 問題 | 原因 | 解法 |
|------|------|------|
| 初始化失敗 TypeInitializationException | 缺原生 DLL / 目標框架不符 | 確認 FaceAiSharp.Bundle 版本、安裝 VC++ Runtime |
| 只看到 Demo 模式 | 模型或 runtime 載入失敗 | 檢視 log，呼叫 TryReinitializeFaceAiSharpAsync |
| 匹配不到 / 信心度低 | 閾值過高或向量品質低 | 降低 SamePersonThreshold 或重新儲存清晰臉部圖片 |
| 退出後 .NET Host 殘留 | 背景計時器未釋放 (舊版) | 已加入 AppShutdownService；更新到最新提交 |

#### 調整辨識閾值
在 `FaceAiSharpService` 內：

```csharp
private const float SamePersonThreshold = 0.35f; // 依實測調整 (建議 0.3 ~ 0.45)
```
重新建構後測試多組人臉資料。

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
│   ├── CheckInStorageService.cs # 打卡資料儲存服務
│   ├── FaceDatabase.cs     # 人臉資料儲存 (SQLite)
│   ├── AppShutdownService.cs # 關閉釋放服務
│   └── (Windows)/FaceAiSharpService.cs # 人臉辨識核心 (平台條件)
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

### 10. FaceAiSharpService (Windows 人臉辨識)

- 背景初始化 + 錯誤回退 Demo
- Detect / Recognize / Save / List / Delete / Reinitialize
- 事件：FaceDetected, FaceRecognized
- 快取加速：啟動讀取全部儲存臉部向量

### 11. AppShutdownService (資源釋放)

- 關閉視窗觸發 (Windows Lifecycle OnClosed)
- 停止定位更新 / 團隊分享 Timer
- Dispose FaceAiSharp 相關 unmanaged 資源
- 目標：避免關閉後殘留 .NET Host 行程


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

### 快速開始 (Windows 開發環境 範例)

```powershell
# 還原套件
dotnet restore

# 建構 Windows 版 (含人臉辨識)
dotnet build MapLocationApp/MapLocationApp.csproj -f net9.0-windows10.0.19041.0

# 啟動 (WinUI)
dotnet build MapLocationApp/MapLocationApp.csproj -t:Run -f net9.0-windows10.0.19041.0
```

### 其他目標

```powershell
# Android 發布
dotnet publish MapLocationApp/MapLocationApp.csproj -f net9.0-android -c Release

# iOS (需 macOS 環境)
dotnet publish MapLocationApp/MapLocationApp.csproj -f net9.0-ios -c Release
```

### (可選) 清理重建

```powershell
dotnet clean
dotnet build
```

### 人臉辨識檢查步驟 (Windows)

1. 執行 App → 開啟 AI 識別頁面
2. 載入清晰單人臉圖片 → Detect
3. 填寫名稱 → Save (寫入 SQLite + 快取)
4. 重新載入同一 / 不同人圖片 → Recognize → 回傳名稱 / 未知
5. 失敗轉 Demo：檢查 Output log 是否顯示 DLL / Runtime 訊息 → 修復後點 Reinitialize (若有提供)

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

### 最小測試建議 (尚未完全實作)

| 類別 | 測試項目 | 指標 |
|------|----------|------|
| FaceRecognition | Detect 單張 | 偵測 ≥1 臉 |
| FaceRecognition | Save → Recognize | 同張 Confidence > 閾值 |
| FaceRecognition | 未訓練人臉 | 無匹配 (Confidence < 閾值) |
| Geofence | 距離計算 | 與手算誤差 < 1m |
| RouteService | 路線計算 | 回傳 Steps / Distance > 0 |

### 待補自動化

- xUnit 封裝 IFaceRecognitionService mock (Demo / Real) 對照
- SQLite In-Memory 測試 FaceDatabase CRUD
- Timer / Shutdown 行為測試 (確保 Dispose 無例外)

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

### v2.1.0 (工作中 / 最新主分支) - 2025年 Q1

#### 版本概要 (v2.0.0)

新增 (Windows) 人臉辨識、Liquid Glass 主題、關閉釋放管理

#### 新增 / 改進

- FaceAiSharpService (SCRFD + ArcFace) 初始整合
- FaceDatabase (SQLite) + 啟動快取
- Demo Fallback 機制與重新初始化診斷
- AppShutdownService 統一釋放 (避免殘留 .NET Host)
- LiquidGlassTheme + 玻璃卡片、Glow 裝飾
- 主頁功能卡 UI 重構

#### 限制 / 尚未完成

- 即時 CameraView 串流 (計畫 WinUI MediaCapture → 抽象跨平台)
- 非 Windows 平台人臉辨識仍未啟用
- 特徵向量加密 / 索引 (未啟用) – 後續加入 AES + ANN 結構
- 自動化測試尚缺 (僅設計框架)

### v2.0.0 - 2024年12月

#### 版本概要 (v1.0.0)

重大功能擴展版本 (地圖與團隊功能強化)

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

#### 版本概要

基礎版本

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
4. **人臉辨識僅 Windows**：尚無跨平台推理 (等待適配 ML Kit / 平台相機串流)。
5. **Camera 即時預覽缺失**：尚未接入 CommunityToolkit.Maui.Camera 預覽 (版本差異)。
6. **Threshold 仍待校正**：預設 0.1 只為測試需要，正式使用需提高。

## 已完成的增強功能

- [x] 離線地圖支援 - 完整實作離線圖磚快取與區域下載
- [x] 路線規劃功能 - 支援路線計算、導航與路線儲存
- [x] 團隊成員位置共享 - 即時位置共享與團隊協作
- [x] 詳細的打卡報表 - 統計分析與多格式匯出
- [x] 多語言支援 - 支援繁體中文、簡體中文、日文、韓文
- [x] 深色模式 - 自適應主題系統
- [x] Windows 人臉辨識初版 (Detect / Recognize / Save / Demo Fallback)
- [x] 關閉釋放服務 (避免殘留行程)
- [x] Liquid Glass 主題 + 首頁重構

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

| 類別 | 項目 | 狀態 |
|------|------|------|
| FaceAI | 即時 CameraView 串流 (WinUI / Android) | 計畫中 |
| FaceAI | 向量加密 + ANN 索引 (FAISS / HNSW 替代方案調研) | 設計中 |
| FaceAI | 跨平台 (Android / iOS) 模型適配 | 評估中 |
| UI | Blur Effect (平台原生背景模糊) | 設計中 |
| 測試 | xUnit + Mock + CI Gate | 未開始 |
| 通知 | 推播 / Telegram / Webhook 整合 | 部分存在 (Telegram) |
| 資料 | 雲端同步 (增量 + 衝突解決) | 未開始 |
| 地圖 | 更多圖磚 / 動態主題 | 未開始 |
| 導航 | 語音導航 / 自動重新規劃 | 未開始 |
| 安全 | 權限細粒度控管 / 行為稽核 | 未開始 |

## 授權

本專案遵循 MIT 授權條款。

## 貢獻指南

歡迎提交 Issue 和 Pull Request 來改善此專案。

---

**注意**：此專案是根據 MapLocation_Enhanced.md 技術規格文件實作的完整解決方案，包含了企業級應用程式所需的所有核心功能。雖然存在一些套件版本相容性問題，但整體架構完整且可擴展。

---

### 快速 Q&A

**Q: 關閉後偶爾看到 .NET Host 沒退出?**  
A: 更新到含 AppShutdownService 版本，並確認未強制結束視窗；確保沒有外部工具附加行程。

**Q: FaceAiSharp 一直 Demo 模式?**  
A: 檢查 log 是否有 DLL 缺失；安裝 VC++ Redistributable 後按重新初始化。

**Q: 如何調亮或更霧面 UI?**  
A: 調整 `LiquidGlassTheme.xaml` 中 `UltraClearGlassBrush` 的 GradientStop 透明度；或新增噪點覆蓋層。

**Q: Embedding 數據庫安全?**  
A: 目前純 JSON 儲存；Roadmap 將新增 AES 加密 + Key 派生與 ANN 索引。
