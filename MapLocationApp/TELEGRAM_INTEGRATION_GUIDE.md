# Telegram 推播通知整合 - 實作指南

## 概述
已成功將 Telegram 推播通知功能整合到 MapLocationApp 應用程式中，採用直接 HTTP API 呼叫方式，確保跨平台相容性。

## 主要特點

### 🔧 技術實作
- **HTTP API 整合**: 使用直接 HTTP 請求代替第三方套件
- **跨平台相容**: 支援 iOS、Android、Windows、macOS
- **設定管理**: 支援 appsettings.json 和 Preferences 雙重設定
- **錯誤處理**: 完整的例外處理和記錄功能

### 📱 功能特色
- **簽到通知**: 自動發送位置和時間資訊
- **地理圍欄警示**: 進入/離開區域即時通知
- **團隊位置分享**: 團隊成員位置更新推播
- **路線規劃通知**: 路線建立和導航狀態推播
- **位置分享**: 支援地圖位置和說明文字

### ⚙️ 已實作功能

#### 1. TelegramNotificationService
- `InitializeAsync()`: Bot Token 和 Chat ID 驗證
- `TestConnectionAsync()`: 連線測試功能
- `SendMessageAsync()`: 發送 HTML 格式訊息
- `SendLocationAsync()`: 發送地理位置
- `SendCheckInNotificationAsync()`: 簽到通知
- `SendGeofenceNotificationAsync()`: 地理圍欄通知
- `SendTeamLocationUpdateAsync()`: 團隊位置更新
- `SendRouteNotificationAsync()`: 路線規劃通知

#### 2. 設定頁面整合
- Bot Token 輸入欄位
- Chat ID 輸入欄位
- 詳細設定指南
- 測試連線功能
- 儲存設定功能

#### 3. 事件驅動整合
- `NotificationIntegrationService`: 統一事件協調
- 自動響應簽到事件
- 自動響應地理圍欄事件
- 自動響應團隊位置分享事件

## 設定步驟

### 第一步：建立 Telegram Bot
1. 開啟 Telegram，搜尋 @BotFather
2. 發送 `/newbot` 指令
3. 設定 Bot 名稱和使用者名稱
4. 取得 Bot Token（格式：123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZ）

### 第二步：取得 Chat ID
1. 將 Bot 加入對話或群組
2. 發送訊息給 Bot
3. 開啟瀏覽器，前往：
   ```
   https://api.telegram.org/bot<YOUR_BOT_TOKEN>/getUpdates
   ```
4. 在回應中找到 "chat":{"id": 數字}

### 第三步：應用程式設定
1. 開啟 MapLocationApp
2. 前往設定頁面
3. 在 Telegram 設定區塊中：
   - 輸入 Bot Token
   - 輸入 Chat ID
   - 點擊「測試連線」驗證設定
   - 點擊「儲存設定」完成設定

## API 端點

### 發送訊息
```
POST https://api.telegram.org/bot<TOKEN>/sendMessage
Content-Type: application/json

{
  "chat_id": "<CHAT_ID>",
  "text": "<MESSAGE>",
  "parse_mode": "HTML"
}
```

### 發送位置
```
POST https://api.telegram.org/bot<TOKEN>/sendLocation
Content-Type: application/json

{
  "chat_id": "<CHAT_ID>",
  "latitude": <LAT>,
  "longitude": <LNG>
}
```

### 驗證 Bot
```
GET https://api.telegram.org/bot<TOKEN>/getMe
```

## 通知範例

### 簽到通知
```
📍 簽到通知

👤 使用者: John Doe
📅 時間: 2024-01-20 14:30:15
📍 座標: 25.047924, 121.517081
```

### 地理圍欄通知
```
🚪➡️ 地理圍欄通知

👤 使用者: John Doe
🏷️ 圍欄: 辦公室區域
🎯 動作: 進入
📅 時間: 2024-01-20 14:30:15
```

### 團隊位置更新
```
👥 團隊位置更新

👥 團隊: 開發團隊
👤 成員: John Doe
📅 時間: 2024-01-20 14:30:15
📍 座標: 25.047924, 121.517081
```

### 路線規劃通知
```
🛣️ 路線規劃通知

👤 使用者: John Doe
🏷️ 路線: 回家路線
🚩 起點: 25.047924, 121.517081
🏁 終點: 25.047000, 121.516000
📅 時間: 2024-01-20 14:30:15
```

## 程式碼架構

### 依賴注入註冊
```csharp
// MauiProgram.cs
builder.Services.AddSingleton<ITelegramNotificationService, TelegramNotificationService>();
builder.Services.AddSingleton<NotificationIntegrationService>();
```

### HTTP 客戶端實作
```csharp
private readonly HttpClient _httpClient;

// 發送訊息範例
var requestData = new {
    chat_id = _chatId,
    text = message,
    parse_mode = "HTML"
};
var jsonContent = JsonSerializer.Serialize(requestData);
var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
var response = await _httpClient.PostAsync($"https://api.telegram.org/bot{_botToken}/sendMessage", content);
```

## 錯誤處理

### 常見錯誤碼
- `400 Bad Request`: 無效的請求參數
- `401 Unauthorized`: Bot Token 錯誤
- `403 Forbidden`: Bot 被封鎖或沒有權限
- `404 Not Found`: Chat ID 不存在

### 日誌記錄
- 成功發送: `LogInformation`
- 連線錯誤: `LogError`
- 參數錯誤: `LogWarning`

## 安全考量

### 設定保護
- Bot Token 和 Chat ID 儲存在本地 Preferences
- 支援 appsettings.json 環境設定
- 不在記錄檔中顯示敏感資訊

### 網路安全
- 使用 HTTPS 加密通信
- 30 秒連線逾時設定
- 完整的錯誤處理機制

## 測試方法

### 手動測試
1. 在設定頁面輸入正確的 Bot Token 和 Chat ID
2. 點擊「測試連線」按鈕
3. 檢查 Telegram 是否收到測試訊息

### 功能測試
1. 執行簽到操作，檢查通知
2. 進入/離開地理圍欄，檢查警示
3. 分享位置給團隊，檢查推播
4. 規劃路線，檢查通知

## 故障排除

### 常見問題
1. **Bot Token 無效**: 重新從 @BotFather 取得
2. **Chat ID 錯誤**: 重新發送訊息並取得正確 ID
3. **網路連線問題**: 檢查網路設定和防火牆
4. **權限問題**: 確保 Bot 在群組中有發送訊息權限

### 除錯方法
1. 檢查應用程式記錄檔
2. 使用瀏覽器直接測試 API 端點
3. 驗證 JSON 格式是否正確
4. 確認 HTTP 標頭設定

## 效能考量

### 最佳化建議
- 批次處理多個通知
- 實作訊息佇列機制
- 設定適當的重試邏輯
- 監控 API 呼叫頻率限制

### 資源管理
- HttpClient 正確釋放
- 避免記憶體洩漏
- 合理的逾時設定

## 未來擴展

### 功能增強
- 支援群組管理功能
- 實作訊息範本系統
- 新增訊息優先級機制
- 支援媒體檔案發送

### 整合改進
- 實作離線訊息佇列
- 新增統計和分析功能
- 支援多語言通知
- 整合推播通知設定

---

## 總結
成功完成 Telegram 推播通知整合，採用 HTTP API 直接呼叫方式確保跨平台相容性和穩定性。系統已準備好投入使用，並提供完整的設定指南和故障排除方法。