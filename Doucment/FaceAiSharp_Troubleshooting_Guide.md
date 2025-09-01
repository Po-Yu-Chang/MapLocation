# FaceAiSharp 載入失敗疑難排解指南

## 🚨 常見錯誤分析

### 1. TypeInitializationException
**錯誤訊息**：`System.TypeInitializationException: The type initializer for 'Microsoft.ML.OnnxRuntime.NativeMethods' threw an exception.`

**原因**：
- 缺少 Visual C++ Redistributable
- ONNX Runtime 原生函式庫載入失敗
- 系統架構不相容（x86 vs x64）

**解決方案**：
```powershell
# 1. 安裝 Visual C++ Redistributable (x64)
# 下載並安裝：https://aka.ms/vs/17/release/vc_redist.x64.exe

# 2. 確認應用程式為 64-bit
# 檢查專案設定中的平台目標

# 3. 重新安裝 FaceAiSharp.Bundle
dotnet remove package FaceAiSharp.Bundle
dotnet add package FaceAiSharp.Bundle --version 0.5.23
```

### 2. EntryPointNotFoundException
**錯誤訊息**：`EntryPointNotFoundException: Unable to find an entry point named 'OnGetApiBase' in DLL 'onnxruntime'.`

**原因**：
- FaceAiSharp 版本與 ONNX Runtime 版本不相容
- DLL 檔案損壞或缺失
- 平台架構不匹配

**解決方案**：
```powershell
# 1. 清理並重建專案
dotnet clean
dotnet restore
dotnet build

# 2. 檢查 bin 目錄下的原生函式庫
# 應該包含：onnxruntime.dll, libonnxruntime.so 等

# 3. 嘗試不同版本的 FaceAiSharp.Bundle
dotnet add package FaceAiSharp.Bundle --version 0.5.22
```

### 3. DllNotFoundException
**錯誤訊息**：找不到 DLL 檔案

**原因**：
- 原生函式庫未正確複製到輸出目錄
- PATH 環境變數設定問題
- NuGet 套件下載不完整

**解決方案**：
```xml
<!-- 在 .csproj 中手動複製原生函式庫 -->
<ItemGroup Condition="'$(TargetFramework)' == 'net9.0-windows10.0.19041.0'">
  <None Include="$(NugetPackageRoot)faceaisharp.bundle\0.5.23\runtimes\win-x64\native\**\*.*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

## 🔧 診斷步驟

### 1. 環境檢查
```csharp
// 已在 FaceAiSharpService 中實作
public void DiagnoseFaceAiSharpEnvironment()
{
    // 檢查作業系統、架構、運行時版本
    // 檢查 FaceAiSharp.dll 是否可載入
    // 檢查 BundleFactory 是否可用
}
```

### 2. 手動測試
```csharp
// 測試基本載入
try
{
    var detector = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
    var generator = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();
    
    Console.WriteLine("FaceAiSharp 載入成功！");
}
catch (Exception ex)
{
    Console.WriteLine($"載入失敗：{ex.Message}");
}
```

### 3. 系統需求確認
- ✅ Windows 10/11 (版本 1809 或更新)
- ✅ .NET 9.0 Windows 目標框架
- ✅ x64 架構
- ✅ Visual C++ Redistributable 2015-2022

## 🛠️ 修復策略

### 策略 1：降級方案
如果最新版本有問題，嘗試使用較舊但穩定的版本：
```xml
<PackageReference Include="FaceAiSharp.Bundle" Version="0.5.20" />
```

### 策略 2：替代套件
考慮使用其他人臉辨識函式庫：
```xml
<!-- Windows ML -->
<PackageReference Include="Microsoft.AI.MachineLearning" Version="1.16.2" />

<!-- OpenCV.NET -->
<PackageReference Include="OpenCvSharp4" Version="4.8.0.20230708" />
<PackageReference Include="OpenCvSharp4.runtime.win" Version="4.8.0.20230708" />
```

### 策略 3：演示模式優化
改善演示模式使其更加真實：
```csharp
public class EnhancedDemoMode
{
    // 模擬真實的人臉偵測行為
    // 基於影像特徵產生一致的結果
    // 提供可預測的匹配邏輯
}
```

## 🚀 最佳實作建議

### 1. 條件式編譯
```csharp
#if WINDOWS && !DEBUG
    // 只在 Windows Release 模式使用 FaceAiSharp
    _useFaceAiSharp = true;
#else
    // 其他情況使用演示模式
    _useFaceAiSharp = false;
#endif
```

### 2. 延遲載入
```csharp
private Lazy<IFaceDetectorWithLandmarks> _detector = new(() =>
{
    try
    {
        return FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
    }
    catch
    {
        return null; // Fallback to demo mode
    }
});
```

### 3. 健康檢查
```csharp
public async Task<bool> PerformHealthCheckAsync()
{
    try
    {
        if (_faceDetector == null) return false;
        
        // 使用簡單的測試影像驗證功能
        using var testImage = CreateTestImage();
        var results = _faceDetector.DetectFaces(testImage);
        
        return true; // 能正常執行代表健康
    }
    catch
    {
        return false;
    }
}
```

## 📋 檢查清單

在部署前請確認：

- [ ] Visual C++ Redistributable 已安裝
- [ ] 目標平台為 x64
- [ ] FaceAiSharp.Bundle 版本相容
- [ ] 原生函式庫正確複製
- [ ] 演示模式作為備用方案
- [ ] 詳細的錯誤日誌記錄
- [ ] 使用者友善的錯誤訊息

## 🔍 進一步支援

如果問題持續存在：

1. **檢查 FaceAiSharp GitHub Issues**: https://github.com/georg-jung/FaceAiSharp/issues
2. **查看 ONNX Runtime 相容性**: https://onnxruntime.ai/docs/
3. **聯繫套件作者**: 提供詳細的錯誤日誌和系統資訊

記住：演示模式是一個有效的備用方案，可以讓應用程式在任何情況下都能正常運行！