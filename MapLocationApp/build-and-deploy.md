# MapLocationApp 建置與部署指南

## 前置需求

### 開發環境
- Visual Studio 2022 (17.8 或更新版本)
- .NET 9.0 SDK
- .NET MAUI 工作負載

### Android 部署
- Android SDK (API 21+ / Android 5.0+)
- Android NDK
- Java Development Kit (JDK) 11 或更新版本

### iOS 部署
- macOS 開發機器
- Xcode 15 或更新版本
- Apple Developer 帳號

## 建置命令

### Windows (Android)
```bash
# 除錯建置
dotnet build -f net9.0-android

# 發布建置
dotnet publish -f net9.0-android -c Release

# 建立 APK
dotnet build -f net9.0-android -c Release -p:AndroidPackageFormat=apk
```

### macOS (iOS)
```bash
# 除錯建置
dotnet build -f net9.0-ios

# 發布建置
dotnet publish -f net9.0-ios -c Release
```

### Windows 平台
```bash
# 除錯建置
dotnet build -f net9.0-windows10.0.19041.0

# 發布建置
dotnet publish -f net9.0-windows10.0.19041.0 -c Release
```

## 部署設定

### 1. 設定應用程式版本
編輯 `MapLocationApp.csproj` 檔案:
```xml
<PropertyGroup>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
    <ApplicationVersion>1</ApplicationVersion>
    <ApplicationTitle>MapLocation 打卡App</ApplicationTitle>
    <ApplicationId>com.company.maplocationapp</ApplicationId>
</PropertyGroup>
```

### 2. Android 發布設定
```xml
<PropertyGroup Condition="$(TargetFramework.Contains('-android'))">
    <AndroidSigningKeyStore>maplocationapp.keystore</AndroidSigningKeyStore>
    <AndroidSigningKeyAlias>maplocationapp</AndroidSigningKeyAlias>
    <AndroidSigningStorePass>your-store-password</AndroidSigningStorePass>
    <AndroidSigningKeyPass>your-key-password</AndroidSigningKeyPass>
</PropertyGroup>
```

### 3. iOS 發布設定
```xml
<PropertyGroup Condition="$(TargetFramework.Contains('-ios'))">
    <CodesignKey>iPhone Distribution</CodesignKey>
    <CodesignProvision>MapLocationApp_Distribution</CodesignProvision>
</PropertyGroup>
```

## 測試

### 單元測試
```bash
# 執行測試（如果有設定測試專案）
dotnet test
```

### 手動測試檢查清單

#### 基本功能測試
- [ ] 應用程式啟動正常
- [ ] 地圖顯示正常
- [ ] 圖磚供應商切換功能
- [ ] 位置權限請求

#### 位置功能測試
- [ ] GPS 位置獲取
- [ ] 位置標記顯示
- [ ] 位置追蹤功能
- [ ] 位置精確度顯示

#### 地理圍欄測試
- [ ] 地理圍欄顯示
- [ ] 進入地理圍欄通知
- [ ] 離開地理圍欄通知
- [ ] 地理圍欄距離計算

#### 打卡功能測試
- [ ] 手動打卡
- [ ] 打卡記錄顯示
- [ ] 打卡下班功能
- [ ] 今日記錄查看

#### 隱私與權限測試
- [ ] 隱私政策顯示
- [ ] 權限請求流程
- [ ] 權限拒絕處理
- [ ] 資料本地存儲

## 發布檢查清單

### 發布前檢查
- [ ] 更新版本號
- [ ] 檢查 API 端點設定
- [ ] 驗證所有權限設定
- [ ] 測試發布建置
- [ ] 檢查應用程式圖示
- [ ] 驗證隱私政策內容

### Android 發布
1. 產生簽名的 APK 或 AAB
2. 上傳到 Google Play Console
3. 設定應用程式商店清單
4. 配置內部測試
5. 發布到正式環境

### iOS 發布
1. 建立 Archive
2. 上傳到 App Store Connect
3. 設定應用程式商店清單
4. 提交審查
5. 發布到 App Store

## 故障排除

### 常見問題

#### 建置錯誤
- 檢查 .NET 版本和工作負載
- 清理並重建專案: `dotnet clean && dotnet build`
- 檢查 NuGet 套件版本相容性

#### Android 問題
- 檢查 Android SDK 版本
- 驗證簽名設定
- 檢查權限設定

#### iOS 問題
- 檢查佈建描述檔
- 驗證程式碼簽名
- 檢查 Info.plist 設定

#### 位置功能問題
- 檢查權限設定
- 驗證 GPS 硬體
- 測試在實際設備上

## 效能最佳化

### 建議
1. 啟用 AOT 編譯 (iOS)
2. 最小化應用程式套件大小
3. 使用 ProGuard/R8 (Android)
4. 最佳化圖片資源
5. 減少不必要的依賴

### 監控
- 使用 Application Insights 或類似工具
- 監控當機報告
- 追蹤效能指標
- 監控電池使用情況