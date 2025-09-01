# FaceAiSharp 修復指引

## 🎯 問題總結

根據您的錯誤訊息，FaceAiSharp 載入失敗的主要原因是：

1. **TypeInitializationException** - 類型初始化失敗
2. **EntryPointNotFoundException** - 找不到 `OnGetApiBase` 入口點

這表示 FaceAiSharp 的原生函式庫載入有問題。

## 🛠️ 立即修復步驟

### 步驟 1：檢查系統需求
```powershell
# 確認系統是 64-bit
[Environment]::Is64BitProcess
[Environment]::Is64BitOperatingSystem

# 確認 Windows 版本
Get-ComputerInfo | Select WindowsProductName, WindowsVersion
```

### 步驟 2：安裝 Visual C++ Redistributable
下載並安裝最新版本：
- [Microsoft Visual C++ Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe)

### 步驟 3：清理並重建專案
```powershell
# 在專案目錄執行
dotnet clean
Remove-Item -Recurse -Force .\bin, .\obj -ErrorAction SilentlyContinue
dotnet restore
dotnet build --framework net9.0-windows10.0.19041.0
```

### 步驟 4：檢查原生函式庫
檢查輸出目錄是否包含這些檔案：
```
bin\Debug\net9.0-windows10.0.19041.0\win10-x64\
├── onnxruntime.dll
├── onnxruntime_providers_shared.dll
├── scrfd_2.5g_kps.onnx
└── arcfaceresnet100-11-int8.onnx
```

## 🔧 增強版解決方案

我已經改善了您的 `FaceAiSharpService.cs`，加入：

1. **詳細的錯誤診斷** - 識別不同類型的載入錯誤
2. **環境檢查** - 驗證系統配置和檔案存在
3. **重新初始化功能** - 允許手動重試載入
4. **改善的演示模式** - 提供更好的備用方案

### 新增的診斷功能：
```csharp
// 檢查 FaceAiSharp 是否可用
bool isAvailable = faceService.IsFaceAiSharpAvailable;

// 嘗試重新初始化
bool success = await faceService.TryReinitializeFaceAiSharpAsync();
```

## 🎮 測試步驟

### 1. 執行應用程式
啟動應用程式並檢查日誌輸出，現在應該會看到：
```
=== FaceAiSharp 環境診斷 ===
作業系統：Microsoft Windows NT 10.0.xxxxx
處理器架構：x64
運行時版本：9.0.x
...
```

### 2. 檢查載入狀態
- 如果看到「FaceAiSharp 元件初始化完成 - 使用完整功能模式」→ ✅ 成功
- 如果看到錯誤訊息 → 📋 查看具體的修復建議

### 3. 測試人臉辨識
- 開啟人臉辨識頁面
- 載入一張人臉照片
- 檢查是否能正確偵測和辨識

## 🚀 備用方案

如果 FaceAiSharp 仍然無法載入，不用擔心！應用程式會自動切換到**增強演示模式**，提供：

- ✅ 模擬人臉偵測
- ✅ 一致的特徵向量產生
- ✅ 可預測的人臉匹配
- ✅ 完整的訓練和辨識功能

演示模式足以進行開發和測試，直到 FaceAiSharp 問題解決。

## 📞 需要幫助？

如果問題持續存在，請提供：

1. **完整的錯誤日誌** - 包含新的診斷資訊
2. **系統資訊** - Windows 版本、.NET 版本
3. **bin 目錄內容** - 檢查原生函式庫是否存在

記住：即使 FaceAiSharp 無法載入，您的應用程式仍然可以正常運行！🎉