# 人臉模型管理功能指南

## 📋 概述

本文件描述了 MapLocationApp 中實作的完整人臉模型管理功能，包括覆蓋、刪除、修改和管理所有人臉模型的能力。

## 🎯 功能特色

### 1. 人臉模型覆蓋功能
- ✅ 可以使用相同名字的照片覆蓋現有模型
- ✅ 支援可選的覆蓋參數控制
- ✅ 自動處理重複名稱的衝突

### 2. 完整的 CRUD 操作
- ✅ **建立**: 儲存新的人臉模型
- ✅ **讀取**: 取得指定名稱或所有人臉模型
- ✅ **更新**: 修改人臉模型名稱
- ✅ **刪除**: 刪除指定或所有人臉模型

### 3. 管理功能
- ✅ 檢查人臉模型是否存在
- ✅ 取得人臉模型總數
- ✅ 列出所有人臉模型名稱
- ✅ 清空所有人臉模型

## 🔧 API 方法說明

### 儲存人臉模型

#### 基本儲存
```csharp
Task<bool> SaveFaceAsync(FaceData faceData, string name, CancellationToken cancellationToken = default)
```

#### 帶覆蓋選項的儲存
```csharp
Task<bool> SaveFaceAsync(FaceData faceData, string name, bool allowOverwrite, CancellationToken cancellationToken = default)
```

**參數說明:**
- `faceData`: 人臉資料物件
- `name`: 人臉模型名稱
- `allowOverwrite`: 是否允許覆蓋現有模型（預設為 false）
- `cancellationToken`: 取消權杖

**使用範例:**
```csharp
// 基本儲存（不覆蓋）
bool saved = await faceService.SaveFaceAsync(faceData, "張三");

// 允許覆蓋儲存
bool overwritten = await faceService.SaveFaceAsync(faceData, "張三", allowOverwrite: true);
```

### 取得人臉模型

#### 取得指定名稱的人臉模型
```csharp
Task<FaceData?> GetSavedFaceAsync(string name, CancellationToken cancellationToken = default)
```

#### 取得所有人臉模型
```csharp
Task<List<FaceData>> GetAllSavedFacesAsync(CancellationToken cancellationToken = default)
```

#### 取得所有人臉模型名稱
```csharp
Task<List<string>> GetAllFaceNamesAsync(CancellationToken cancellationToken = default)
```

**使用範例:**
```csharp
// 取得特定人臉模型
FaceData? face = await faceService.GetSavedFaceAsync("張三");

// 取得所有人臉模型
List<FaceData> allFaces = await faceService.GetAllSavedFacesAsync();

// 取得所有名稱
List<string> names = await faceService.GetAllFaceNamesAsync();
```

### 更新人臉模型

#### 修改人臉模型名稱
```csharp
Task<bool> UpdateFaceNameAsync(string oldName, string newName, CancellationToken cancellationToken = default)
```

**使用範例:**
```csharp
// 將 "張三" 改名為 "張三丰"
bool renamed = await faceService.UpdateFaceNameAsync("張三", "張三丰");
```

### 刪除人臉模型

#### 刪除指定人臉模型
```csharp
Task<bool> DeleteSavedFaceAsync(string name, CancellationToken cancellationToken = default)
```

#### 清空所有人臉模型
```csharp
Task<bool> ClearAllFacesAsync(CancellationToken cancellationToken = default)
```

**使用範例:**
```csharp
// 刪除特定人臉模型
bool deleted = await faceService.DeleteSavedFaceAsync("張三");

// 清空所有人臉模型
bool cleared = await faceService.ClearAllFacesAsync();
```

### 查詢功能

#### 檢查人臉模型是否存在
```csharp
Task<bool> FaceExistsAsync(string name, CancellationToken cancellationToken = default)
```

#### 取得人臉模型總數
```csharp
Task<int> GetFaceCountAsync(CancellationToken cancellationToken = default)
```

**使用範例:**
```csharp
// 檢查是否存在
bool exists = await faceService.FaceExistsAsync("張三");

// 取得總數
int count = await faceService.GetFaceCountAsync();
```

## 🔄 工作流程範例

### 1. 覆蓋現有人臉模型
```csharp
// 檢查是否存在
if (await faceService.FaceExistsAsync("張三"))
{
    // 使用新照片覆蓋
    bool success = await faceService.SaveFaceAsync(newFaceData, "張三", allowOverwrite: true);
    if (success)
    {
        Console.WriteLine("✅ 人臉模型已成功覆蓋");
    }
}
```

### 2. 批次管理人臉模型
```csharp
// 取得所有人臉模型名稱
var allNames = await faceService.GetAllFaceNamesAsync();
Console.WriteLine($"📊 目前有 {allNames.Count} 個人臉模型");

foreach (var name in allNames)
{
    Console.WriteLine($"👤 {name}");
}

// 選擇性刪除
if (allNames.Contains("舊模型"))
{
    await faceService.DeleteSavedFaceAsync("舊模型");
    Console.WriteLine("🗑️ 舊模型已刪除");
}
```

### 3. 人臉模型重新命名
```csharp
// 將臨時名稱改為正式名稱
bool renamed = await faceService.UpdateFaceNameAsync("temp_001", "李四");
if (renamed)
{
    Console.WriteLine("✏️ 人臉模型名稱已更新");
}
```

## 🛠️ 實作細節

### 日誌記錄
所有管理操作都包含詳細的日誌記錄，使用 emoji 標識符便於除錯：

- 🔄 **操作開始**
- ✅ **操作成功**
- ❌ **操作失敗**
- 🔍 **查詢操作**
- 📊 **統計資訊**

### 錯誤處理
- 自動處理 null 值和例外狀況
- 提供詳細的錯誤訊息
- 支援操作取消機制

### 效能考量
- 使用非同步操作避免阻塞 UI
- 支援取消權杖提升回應性
- 資料庫操作最佳化

## 📱 UI 整合建議

### 管理介面設計
1. **人臉模型列表頁面**
   - 顯示所有人臉模型縮圖
   - 支援選擇多個項目
   - 提供搜尋功能

2. **操作按鈕**
   - 🔄 覆蓋模型
   - ✏️ 重新命名
   - 🗑️ 刪除模型
   - 📋 匯出/匯入

3. **確認對話框**
   - 覆蓋操作需要使用者確認
   - 刪除操作顯示警告訊息
   - 批次操作顯示影響範圍

## 🔧 除錯功能

### 診斷日誌
- 人臉特徵向量比較詳情
- 相似度分數計算過程
- 資料庫操作狀態

### 測試模式
- 降低相似度閾值（0.1f）便於測試
- 詳細的匹配過程日誌
- 模擬資料產生功能

## 📋 已修復問題

1. ✅ **face.IsUnknown 永遠為 true**
   - 實作一致的特徵向量產生
   - 調整相似度閾值
   - 加入詳細診斷日誌

2. ✅ **bestMatch 永遠為 null**
   - 修正特徵向量比較邏輯
   - 改善相似度計算演算法
   - 加入除錯輸出

3. ✅ **缺乏覆蓋功能**
   - 實作 allowOverwrite 參數
   - 支援相同名稱模型替換
   - 保持資料完整性

## 🎯 使用建議

1. **初次使用**: 建議使用較低的相似度閾值進行測試
2. **生產環境**: 調整閾值以平衡準確性與誤判率
3. **資料備份**: 在執行批次刪除前先備份重要模型
4. **效能監控**: 定期檢查資料庫大小與查詢效能

---

**版本**: 1.0.0  
**最後更新**: 2024年12月  
**支援平台**: Windows (主要), iOS, macOS, Android