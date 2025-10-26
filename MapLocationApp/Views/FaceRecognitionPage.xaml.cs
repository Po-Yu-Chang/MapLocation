using MapLocationApp.Models;
using MapLocationApp.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MapLocationApp.Views;

public partial class FaceRecognitionPage : ContentPage
{
    private readonly IFaceRecognitionService? _faceService;
    private readonly ILogger<FaceRecognitionPage>? _logger;
    private string? _currentImagePath;
    private byte[]? _currentImageData;
    private bool _isTrainingMode;
    
    public ObservableCollection<DetectedFaceViewModel> DetectedFaces { get; } = new();

    public FaceRecognitionPage()
    {
        InitializeComponent();
        
        // Get services if available
        _faceService = ServiceHelper.GetService<IFaceRecognitionService>();
        _logger = ServiceHelper.GetService<ILogger<FaceRecognitionPage>>();
        
        DetectedFacesCollection.ItemsSource = DetectedFaces;
        _ = InitializeAsync(); // Fire and forget - 在背景初始化
    }

    private async Task InitializeAsync()
    {
        try
        {
            if (_faceService == null)
            {
                SystemStatusLabel.Text = "人臉辨識服務: 不可用 (僅Windows平台支援)";
                DatabaseStatusLabel.Text = "資料庫: 未連接";
                
                // 顯示詳細錯誤資訊
                await DisplayAlert("服務不可用", 
                    "人臉辨識服務未正確初始化。\n" +
                    "請確認：\n" +
                    "1. 執行在 Windows 平台\n" +
                    "2. FaceAiSharp 套件已正確安裝\n" +
                    "3. 服務已正確註冊", 
                    "確定");
                return;
            }

            SystemStatusLabel.Text = "FaceAiSharp 系統: 檢查中...";

            // 檢查服務是否支援當前平台
            if (!_faceService.IsSupported)
            {
                SystemStatusLabel.Text = "FaceAiSharp 系統: 平台不支援";
                await DisplayAlert("平台不支援", 
                    "人臉辨識功能僅支援 Windows 平台。\n" +
                    "當前平台：" + DeviceInfo.Platform.ToString(), 
                    "確定");
                return;
            }

            SystemStatusLabel.Text = "FaceAiSharp 系統: 已就緒";

            // 檢查初始化狀態
            if (_faceService.IsInitialized)
            {
                var faceCount = (await _faceService.GetSavedFaceNamesAsync()).Count;
                DatabaseStatusLabel.Text = $"資料庫: 已載入 {faceCount} 個人臉";
            }
            else
            {
                DatabaseStatusLabel.Text = "資料庫: 初始化中...";
                
                // 等待初始化完成
                var timeout = TimeSpan.FromSeconds(10);
                var start = DateTime.Now;
                
                while (!_faceService.IsInitialized && DateTime.Now - start < timeout)
                {
                    await Task.Delay(500);
                }
                
                if (_faceService.IsInitialized)
                {
                    var faceCount = (await _faceService.GetSavedFaceNamesAsync()).Count;
                    DatabaseStatusLabel.Text = $"資料庫: 已載入 {faceCount} 個人臉";
                }
                else
                {
                    DatabaseStatusLabel.Text = "資料庫: 初始化失敗";
                    await DisplayAlert("初始化失敗", 
                        "人臉辨識服務初始化超時。\n" +
                        "請重新啟動應用程式或檢查系統需求。", 
                        "確定");
                }
            }

            StorageStatusLabel.Text = "儲存空間: 可用";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "初始化人臉辨識頁面失敗");
            SystemStatusLabel.Text = "系統初始化失敗";
            
            await DisplayAlert("初始化錯誤", 
                $"人臉辨識服務初始化失敗：\n{ex.Message}\n\n" +
                "請檢查：\n" +
                "1. FaceAiSharp 套件安裝\n" +
                "2. Windows 版本相容性\n" +
                "3. 系統權限設定", 
                "確定");
        }
    }

    private async void OnTrainingModeClicked(object sender, EventArgs e)
    {
        _isTrainingMode = true;
        ShowImageSelection("訓練模式 - 請選擇要訓練的人臉影像");
    }

    private async void OnTestModeClicked(object sender, EventArgs e)
    {
        _isTrainingMode = false;
        ShowImageSelection("測試模式 - 請選擇要辨識的影像");
    }

    private void ShowImageSelection(string title)
    {
        WorkAreaFrame.IsVisible = true;
        ImageFrame.IsVisible = true;
        ResultFrame.IsVisible = false;
        
        // Update title
        WorkAreaTitle.Text = title;
    }

    private async void OnTakePhotoClicked(object sender, EventArgs e)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert("錯誤", "此裝置不支援拍照功能", "確定");
                return;
            }

            var result = await MediaPicker.Default.CapturePhotoAsync();
            if (result != null)
            {
                await LoadImageAsync(result);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "拍照失敗");
            await DisplayAlert("錯誤", $"拍照失敗: {ex.Message}", "確定");
        }
    }

    private async void OnSelectFileClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "選擇影像檔案",
                FileTypes = FilePickerFileType.Images
            });

            if (result != null)
            {
                await LoadImageAsync(result);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "選擇檔案失敗");
            await DisplayAlert("錯誤", $"選擇檔案失敗: {ex.Message}", "確定");
        }
    }

    private async Task LoadImageAsync(FileResult fileResult)
    {
        try
        {
            _currentImagePath = fileResult.FullPath;
            _currentImageData = await File.ReadAllBytesAsync(fileResult.FullPath);
            
            PreviewImage.Source = ImageSource.FromFile(fileResult.FullPath);
            PreviewImage.IsVisible = true;
            ProcessingButtons.IsVisible = true;
            
            if (_isTrainingMode)
            {
                ProcessButton.Text = "🎓 偵測人臉 (訓練)";
            }
            else
            {
                ProcessButton.Text = "🔍 辨識人臉 (測試)";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "載入影像失敗");
            await DisplayAlert("錯誤", $"載入影像失敗: {ex.Message}", "確定");
        }
    }

    private async void OnProcessImageClicked(object sender, EventArgs e)
    {
        if (_faceService == null)
        {
            await DisplayAlert("錯誤", "人臉辨識服務不可用", "確定");
            return;
        }
        
        if (_currentImageData == null)
        {
            await DisplayAlert("錯誤", "請先選擇影像", "確定");
            return;
        }

        try
        {
            ProcessButton.IsEnabled = false;
            ProcessButton.Text = "🔄 處理中...";
            
            DetectedFaces.Clear();
            ResultFrame.IsVisible = true;

            // 新增詳細的偵錯資訊
            _logger?.LogInformation($"開始處理影像，模式：{(_isTrainingMode ? "訓練" : "測試")}，影像大小：{_currentImageData.Length} bytes");

            if (_isTrainingMode)
            {
                // 訓練模式 - 偵測人臉
                var faces = await _faceService.DetectFacesAsync(_currentImageData);
                
                _logger?.LogInformation($"偵測到 {faces.Count} 個人臉");
                
                if (faces.Any())
                {
                    ResultLabel.Text = $"偵測到 {faces.Count} 個人臉";
                    
                    foreach (var face in faces)
                    {
                        DetectedFaces.Add(new DetectedFaceViewModel
                        {
                            Name = "未命名",
                            Confidence = face.Quality,
                            StatusText = $"品質: {face.Quality:P1} - 準備訓練"
                        });
                    }
                    
                    DetectedFacesCollection.IsVisible = true;
                    NameInputArea.IsVisible = true;
                }
                else
                {
                    ResultLabel.Text = "未偵測到人臉，請嘗試其他影像";
                    await DisplayAlert("偵測結果", 
                        "未偵測到人臉。\n" +
                        "建議：\n" +
                        "1. 確保影像包含清晰的人臉\n" +
                        "2. 人臉大小適中且正面\n" +
                        "3. 光線充足，避免過度曝光或陰影", 
                        "確定");
                }
            }
            else
            {
                // 測試模式 - 辨識人臉
                var result = await _faceService.RecognizeFaceAsync(_currentImageData);
                
                _logger?.LogInformation($"辨識結果：HasFaces={result.HasFaces}, ProcessingTime={result.ProcessingTime.TotalMilliseconds}ms");
                
                if (result.HasFaces)
                {
                    ResultLabel.Text = $"辨識結果 (處理時間: {result.ProcessingTime.TotalMilliseconds:F0}ms)";
                    
                    foreach (var face in result.DetectedFaces)
                    {
                        DetectedFaces.Add(new DetectedFaceViewModel
                        {
                            Name = face.IsUnknown ? "未知人臉" : face.Name,
                            Confidence = face.Confidence,
                            StatusText = face.IsUnknown ? "系統中無此人臉資料" : GetConfidenceDescription(face.Confidence)
                        });
                    }
                    
                    DetectedFacesCollection.IsVisible = true;
                }
                else
                {
                    ResultLabel.Text = "未偵測到人臉";
                    await DisplayAlert("辨識結果", 
                        "未偵測到人臉。\n" +
                        "請確保影像品質良好且包含清晰的人臉。", 
                        "確定");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "處理影像失敗");
            
            string errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $"\n內部錯誤：{ex.InnerException.Message}";
            }
            
            await DisplayAlert("處理失敗", 
                $"影像處理失敗：\n{errorMessage}\n\n" +
                "可能原因：\n" +
                "1. 影像格式不支援\n" +
                "2. 影像檔案損壞\n" +
                "3. 記憶體不足\n" +
                "4. FaceAiSharp 引擎錯誤", 
                "確定");
        }
        finally
        {
            ProcessButton.IsEnabled = true;
            ProcessButton.Text = _isTrainingMode ? "🎓 偵測人臉 (訓練)" : "🔍 辨識人臉 (測試)";
        }
    }

    private async void OnSaveFaceClicked(object sender, EventArgs e)
    {
        if (_faceService == null || _currentImageData == null || string.IsNullOrWhiteSpace(NameEntry.Text))
        {
            await DisplayAlert("錯誤", "請輸入有效的人臉名稱", "確定");
            return;
        }

        try
        {
            var faces = await _faceService.DetectFacesAsync(_currentImageData);
            if (faces.Any())
            {
                var success = await _faceService.SaveFaceAsync(faces.First(), NameEntry.Text.Trim());
                
                if (success)
                {
                    await DisplayAlert("成功", $"人臉 '{NameEntry.Text}' 已成功儲存到系統", "確定");
                    
                    // 更新資料庫狀態
                    var faceCount = (await _faceService.GetSavedFaceNamesAsync()).Count;
                    DatabaseStatusLabel.Text = $"資料庫: 已載入 {faceCount} 個人臉";
                    
                    // 重置介面
                    OnResetClicked(sender, e);
                }
                else
                {
                    await DisplayAlert("失敗", "儲存人臉失敗，可能是名稱已存在", "確定");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "儲存人臉失敗");
            await DisplayAlert("錯誤", $"儲存人臉失敗: {ex.Message}", "確定");
        }
    }

    private void OnResetClicked(object sender, EventArgs e)
    {
        PreviewImage.IsVisible = false;
        PreviewImage.Source = null;
        ProcessingButtons.IsVisible = false;
        NameInputArea.IsVisible = false;
        ResultFrame.IsVisible = false;
        NameEntry.Text = string.Empty;
        DetectedFaces.Clear();
        _currentImagePath = null;
        _currentImageData = null;
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        WorkAreaFrame.IsVisible = false;
        ImageFrame.IsVisible = false;
        ResultFrame.IsVisible = false;
        OnResetClicked(sender, e);
    }

    private async void OnManagementModeClicked(object sender, EventArgs e)
    {
        if (_faceService == null)
        {
            await DisplayAlert("錯誤", "人臉辨識服務不可用", "確定");
            return;
        }

        try
        {
            var faceNames = await _faceService.GetSavedFaceNamesAsync();
            
            if (!faceNames.Any())
            {
                await DisplayAlert("資料管理", "目前系統中沒有已儲存的人臉資料", "確定");
                return;
            }

            var action = await DisplayActionSheet("資料管理", "取消", null, faceNames.ToArray());
            
            if (action != null && action != "取消")
            {
                var deleteConfirm = await DisplayAlert("確認刪除", $"確定要刪除人臉 '{action}' 嗎？", "刪除", "取消");
                
                if (deleteConfirm)
                {
                    var deleted = await _faceService.DeleteSavedFaceAsync(action);
                    
                    if (deleted)
                    {
                        await DisplayAlert("成功", $"已刪除人臉 '{action}'", "確定");
                        
                        // 更新資料庫狀態
                        var faceCount = (await _faceService.GetSavedFaceNamesAsync()).Count;
                        DatabaseStatusLabel.Text = $"資料庫: 已載入 {faceCount} 個人臉";
                    }
                    else
                    {
                        await DisplayAlert("失敗", "刪除人臉失敗", "確定");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "管理人臉資料失敗");
            await DisplayAlert("錯誤", $"管理人臉資料失敗: {ex.Message}", "確定");
        }
    }

    private async void OnSystemSettingsClicked(object sender, EventArgs e)
    {
        var diagnosticInfo = await GetDiagnosticInfoAsync();
        
        await DisplayAlert("系統診斷", diagnosticInfo, "確定");
    }

    private async Task<string> GetDiagnosticInfoAsync()
    {
        var info = new System.Text.StringBuilder();
        
        info.AppendLine("=== 系統診斷資訊 ===");
        info.AppendLine($"平台：{DeviceInfo.Platform}");
        info.AppendLine($"版本：{DeviceInfo.VersionString}");
        info.AppendLine($"架構：{DeviceInfo.Idiom}");
        info.AppendLine();
        
        info.AppendLine("=== 服務狀態 ===");
        info.AppendLine($"IFaceRecognitionService：{(_faceService != null ? "已註冊" : "未註冊")}");
        
        if (_faceService != null)
        {
            info.AppendLine($"IsSupported：{_faceService.IsSupported}");
            info.AppendLine($"IsInitialized：{_faceService.IsInitialized}");
            
            try
            {
                var faceNames = await _faceService.GetSavedFaceNamesAsync();
                info.AppendLine($"已儲存人臉數量：{faceNames.Count}");
            }
            catch (Exception ex)
            {
                info.AppendLine($"取得人臉資料失敗：{ex.Message}");
            }
        }
        
        info.AppendLine();
        info.AppendLine("=== 套件資訊 ===");
        info.AppendLine("FaceAiSharp.Bundle：v0.5.23");
        info.AppendLine("僅支援：Windows 10/11");
        info.AppendLine();
        info.AppendLine("=== 當前設定 ===");
        info.AppendLine("- 辨識閾值: 70%");
        info.AppendLine("- 模型版本: FaceAiSharp v0.5.23");
        info.AppendLine("- 資料加密: 啟用");
        
        return info.ToString();
    }

    private string GetConfidenceDescription(float confidence)
    {
        return confidence switch
        {
            >= 0.9f => "極高信心度",
            >= 0.8f => "高信心度",
            >= 0.7f => "中等信心度",
            >= 0.6f => "低信心度",
            _ => "極低信心度"
        };
    }

    // ===== 相機功能相關方法 =====

    private async void OnStartLiveClicked(object sender, EventArgs e)
    {
        try
        {
            StartLiveButton.IsEnabled = false;
            StopLiveButton.IsEnabled = true;
            CaptureFromLiveButton.IsEnabled = true;
            LiveModeStatusLabel.Text = "即時辨識已啟動";

            // 顯示即時模式區域
            LiveCameraContainer.IsVisible = true;
            LiveOverlayLabel.IsVisible = true;
            if (LiveOverlayLabel.Content is Label overlayLabel)
            {
                overlayLabel.Text = "即時模式已啟動";
            }

            await DisplayAlert("提示", "即時相機功能目前為示範模式。\n完整功能需要相機權限和進階設定。", "確定");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "啟動即時相機失敗");
            await DisplayAlert("錯誤", $"啟動相機失敗: {ex.Message}", "確定");

            StartLiveButton.IsEnabled = true;
            StopLiveButton.IsEnabled = false;
            CaptureFromLiveButton.IsEnabled = false;
        }
    }

    private async void OnStopLiveClicked(object sender, EventArgs e)
    {
        try
        {
            StartLiveButton.IsEnabled = true;
            StopLiveButton.IsEnabled = false;
            CaptureFromLiveButton.IsEnabled = false;
            LiveModeStatusLabel.Text = "已停止";

            // 隱藏即時模式區域
            LiveOverlayLabel.IsVisible = false;
            if (LiveOverlayLabel.Content is Label overlayLabel)
            {
                overlayLabel.Text = "即時偵測中...";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "停止即時相機失敗");
            await DisplayAlert("錯誤", $"停止相機失敗: {ex.Message}", "確定");
        }
    }

    private async void OnCaptureFromLiveClicked(object sender, EventArgs e)
    {
        try
        {
            // 示範從即時相機擷取功能
            await DisplayAlert("提示",
                "即時相機擷取功能正在開發中。\n" +
                "目前請使用 '拍照' 或 '選擇檔案' 功能。",
                "確定");

            // 實際實作時的程式碼架構：
            // 1. 捕獲當前相機幀
            // 2. 轉換為適當的圖像格式
            // 3. 進行人臉辨識處理
            // 4. 顯示結果
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "從即時相機擷取失敗");
            await DisplayAlert("錯誤", $"擷取失敗: {ex.Message}", "確定");
        }
    }
}

public class DetectedFaceViewModel
{
    public string Name { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public string StatusText { get; set; } = string.Empty;
}

// Helper class for service resolution
public static class ServiceHelper
{
    private static IServiceProvider? _serviceProvider;

    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public static T? GetService<T>()
    {
        return _serviceProvider != null ? _serviceProvider.GetService<T>() : default;
    }
}