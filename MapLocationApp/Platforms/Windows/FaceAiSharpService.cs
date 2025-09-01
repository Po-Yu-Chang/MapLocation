#if WINDOWS
using MapLocationApp.Models;
using MapLocationApp.Services;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Collections.Concurrent;
using FaceAiSharp;
using FaceAiSharp.Extensions;

namespace MapLocationApp.Platforms.Windows;

public class FaceAiSharpService : IFaceRecognitionService, IDisposable
{
    private readonly ILogger<FaceAiSharpService> _logger;
    private readonly IFaceDatabase _database;
    private readonly SemaphoreSlim _semaphore;
    
    // FaceAiSharp components
    private IFaceDetectorWithLandmarks? _faceDetector;
    private IFaceEmbeddingsGenerator? _embeddingsGenerator;
    private bool _disposed;
    private readonly ConcurrentDictionary<string, FaceData> _faceCache = new();
    
    // Recognition thresholds - 降低演示模式閾值以改善匹配
    private const float SamePersonThreshold = 0.1f;  // 臨時降低到 0.1 以便測試
    private const float MaybeSamePersonThreshold = 0.05f;

    public event EventHandler<FaceDetectedEventArgs>? FaceDetected;
    public event EventHandler<FaceRecognizedEventArgs>? FaceRecognized;

    public bool IsInitialized { get; private set; }
    public bool IsSupported => OperatingSystem.IsWindows();

    public FaceAiSharpService(ILogger<FaceAiSharpService> logger, IFaceDatabase database)
    {
        _logger = logger;
        _database = database;
        _semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        
        // 在背景執行初始化，不阻塞建構函式
        _ = Task.Run(async () =>
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "背景初始化失敗");
            }
        });
    }

    private async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("正在初始化 FaceAiSharp 服務...");
            
            // Check Windows platform version
            if (!IsSupported)
            {
                _logger.LogWarning("當前平台不支援 FaceAiSharp");
                return;
            }
            
            // Initialize database first
            await _database.InitializeAsync();
            _logger.LogInformation("資料庫初始化完成");
            
            // Run environment diagnostics
            DiagnoseFaceAiSharpEnvironment();
            
            // Initialize FaceAiSharp components using Bundle Factory
            try
            {
                _logger.LogInformation("嘗試初始化 FaceAiSharp Bundle...");
                
                // Check runtime environment
                _logger.LogInformation("系統資訊：64位元處理序={Is64Bit}, Windows版本={OSVersion}", 
                    Environment.Is64BitProcess, Environment.OSVersion.Version);
                
                // Try to create detector first
                _logger.LogInformation("正在創建人臉偵測器...");
                _faceDetector = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
                _logger.LogInformation("人臉偵測器創建成功");
                
                // Try to create embeddings generator
                _logger.LogInformation("正在創建嵌入產生器...");
                _embeddingsGenerator = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();
                _logger.LogInformation("嵌入產生器創建成功");
                
                _logger.LogInformation("FaceAiSharp 元件初始化完成 - 使用完整功能模式");
            }
            catch (TypeInitializationException ex)
            {
                _logger.LogError(ex, "FaceAiSharp 類型初始化失敗 - 可能缺少原生函式庫");
                _logger.LogInformation("建議解決方案：");
                _logger.LogInformation("1. 安裝 Microsoft Visual C++ Redistributable");
                _logger.LogInformation("2. 確認系統支援 FaceAiSharp 需求");
                _faceDetector = null;
                _embeddingsGenerator = null;
            }
            catch (DllNotFoundException ex)
            {
                _logger.LogError(ex, "FaceAiSharp 找不到必要的 DLL 檔案");
                _logger.LogInformation("建議解決方案：");
                _logger.LogInformation("1. 重新安裝 FaceAiSharp.Bundle 套件");
                _logger.LogInformation("2. 確認 NuGet 套件正確下載");
                _faceDetector = null;
                _embeddingsGenerator = null;
            }
            catch (EntryPointNotFoundException ex)
            {
                _logger.LogError(ex, "FaceAiSharp 找不到函式入口點：{EntryPoint}", ex.Message);
                _logger.LogInformation("建議解決方案：");
                _logger.LogInformation("1. 確認 FaceAiSharp.Bundle 版本與平台相容");
                _logger.LogInformation("2. 檢查是否使用正確的目標框架");
                _faceDetector = null;
                _embeddingsGenerator = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FaceAiSharp 套件載入失敗：{ErrorType} - {Message}", 
                    ex.GetType().Name, ex.Message);
                _logger.LogInformation("將使用演示模式繼續運行");
                _faceDetector = null;
                _embeddingsGenerator = null;
            }
            
            // Load saved faces into cache
            await LoadSavedFacesAsync();
            
            IsInitialized = true;
            
            var mode = _faceDetector != null && _embeddingsGenerator != null ? "完整功能" : "演示";
            _logger.LogInformation("FaceAiSharp 服務初始化完成 ({Mode} 模式)", mode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FaceAiSharp 服務初始化失敗");
            IsInitialized = false;
            // 不重新拋出異常，讓服務在有限模式下運行
        }
    }

    private async Task LoadSavedFacesAsync()
    {
        try
        {
            var savedFaces = await _database.GetAllFacesAsync();
            _faceCache.Clear();
            
            foreach (var face in savedFaces)
            {
                _faceCache.TryAdd(face.Name, face);
            }
            
            _logger.LogInformation("已載入 {Count} 個已儲存的人臉", savedFaces.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "載入已儲存人臉失敗");
        }
    }

    public async Task<List<FaceData>> DetectFacesAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("服務尚未初始化");

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var faces = new List<FaceData>();

            using var image = SixLabors.ImageSharp.Image.Load<Rgb24>(imageData);
            
            if (_faceDetector != null)
            {
                // Use real FaceAiSharp detection
                _logger.LogInformation("使用 FaceAiSharp 進行人臉偵測");
                var detectionResults = _faceDetector.DetectFaces(image);
                
                foreach (var result in detectionResults)
                {
                    var faceData = new FaceData
                    {
                        Id = Guid.NewGuid().ToString(),
                        BoundingBox = ConvertRectangleF(result.Box),
                        Quality = result.Confidence ?? 0.8f,
                        CreatedAt = DateTime.UtcNow
                        // Store landmarks info in a different way if needed
                    };

                    faces.Add(faceData);
                }
                
                _logger.LogInformation("FaceAiSharp 偵測到 {Count} 個人臉", faces.Count);
            }
            else
            {
                // Fallback demo mode
                _logger.LogInformation("使用演示模式進行人臉偵測");
                await Task.Delay(100, cancellationToken); // Simulate processing time
                
                var simulatedFace = new FaceData
                {
                    Id = Guid.NewGuid().ToString(),
                    BoundingBox = new Models.Rectangle(
                        image.Width / 4, 
                        image.Height / 4, 
                        image.Width / 2, 
                        image.Height / 2
                    ),
                    Quality = 0.85f,
                    CreatedAt = DateTime.UtcNow,
                    FeatureVector = GenerateSimulatedFeatureVectorFromImage(imageData)
                };

                faces.Add(simulatedFace);
                _logger.LogInformation("演示模式：模擬偵測到 1 個人臉");
            }
            
            stopwatch.Stop();
            
            // Trigger event
            var detectedFaces = faces.Select(f => new DetectedFace
            {
                BoundingBox = f.BoundingBox,
                Confidence = f.Quality,
                Quality = f.Quality
            }).ToList();
            
            OnFaceDetected(detectedFaces, stopwatch.Elapsed);
            
            return faces;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<FaceData>> DetectFacesAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var imageData = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        return await DetectFacesAsync(imageData, cancellationToken);
    }

    public async Task<RecognitionResult> RecognizeFaceAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("服務尚未初始化");

        var stopwatch = Stopwatch.StartNew();
        var result = new RecognitionResult
        {
            ModelUsed = _faceDetector != null && _embeddingsGenerator != null ? 
                "FaceAiSharp-SCRFD + ArcFace" : "FaceAiSharp (Demo Mode)",
            Metrics = new Dictionary<string, object>()
        };

        try
        {
            using var image = SixLabors.ImageSharp.Image.Load<Rgb24>(imageData);
            
            if (_faceDetector != null && _embeddingsGenerator != null)
            {
                // Use real FaceAiSharp pipeline
                _logger.LogInformation("使用 FaceAiSharp 進行人臉辨識");
                var detectionResults = _faceDetector.DetectFaces(image);
                
                foreach (var detection in detectionResults)
                {
                    var detectedFace = new DetectedFace
                    {
                        BoundingBox = ConvertRectangleF(detection.Box),
                        Confidence = detection.Confidence ?? 0.8f,
                        Quality = detection.Confidence ?? 0.8f
                    };
                    
                    // Face recognition using embeddings
                    if (detection.Landmarks != null && detection.Landmarks.Count >= 5)
                    {
                        try
                        {
                            // Clone image for alignment (in-place operation)
                            using var alignedImage = image.Clone();
                            
                            // Align face using landmarks
                            _embeddingsGenerator.AlignFaceUsingLandmarks(alignedImage, detection.Landmarks);
                            
                            // Generate embedding
                            var embedding = _embeddingsGenerator.GenerateEmbedding(alignedImage);
                            
                            // Find best match
                            var bestMatch = await FindBestMatchAsync(embedding.ToArray(), cancellationToken);
                            
                            if (bestMatch != null)
                            {
                                detectedFace.Name = bestMatch.Name;
                                detectedFace.Confidence = bestMatch.Confidence;
                                _logger.LogInformation("FaceAiSharp 辨識到人臉: {Name} (信心度: {Confidence:F2})", 
                                    bestMatch.Name, bestMatch.Confidence);
                            }
                            else
                            {
                                _logger.LogInformation("FaceAiSharp 偵測到未知人臉");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "FaceAiSharp 人臉對齊或嵌入產生失敗");
                        }
                    }
                    
                    result.DetectedFaces.Add(detectedFace);
                    
                    // Trigger event
                    OnFaceRecognized(detectedFace, string.IsNullOrEmpty(detectedFace.Name));
                }
            }
            else
            {
                // Fallback to demo mode
                _logger.LogInformation("使用演示模式進行人臉辨識");
                var detectedFaces = await DetectFacesAsync(imageData, cancellationToken);
                
                foreach (var faceData in detectedFaces)
                {
                    // 演示模式：檢查是否有匹配的已儲存人臉
                    var bestMatch = await FindBestMatchAsync(faceData.FeatureVector!, cancellationToken);
                    
                    var detectedFace = new DetectedFace
                    {
                        BoundingBox = faceData.BoundingBox,
                        Confidence = bestMatch?.Confidence ?? faceData.Quality,
                        Name = bestMatch?.Name ?? string.Empty,
                        Quality = faceData.Quality
                    };
                    
                    if (bestMatch != null)
                    {
                        _logger.LogInformation("演示模式辨識到人臉: {Name} (模擬信心度: {Confidence:F2})", 
                            bestMatch.Name, bestMatch.Confidence);
                    }
                    else
                    {
                        _logger.LogInformation("演示模式偵測到未知人臉");
                    }
                    
                    result.DetectedFaces.Add(detectedFace);
                    
                    // Trigger event
                    OnFaceRecognized(detectedFace, bestMatch == null);
                }
            }
            
            result.ProcessingTime = stopwatch.Elapsed;
            result.Metrics["face_count"] = result.DetectedFaces.Count;
            result.Metrics["processing_time_ms"] = result.ProcessingTime.TotalMilliseconds;
            result.Metrics["use_real_faceaisharp"] = _faceDetector != null && _embeddingsGenerator != null;
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "人臉辨識處理失敗");
            throw;
        }
    }

    public async Task<RecognitionResult> RecognizeFaceAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var imageData = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        return await RecognizeFaceAsync(imageData, cancellationToken);
    }

    public async Task<bool> SaveFaceAsync(FaceData faceData, string name, CancellationToken cancellationToken = default)
    {
        return await SaveFaceAsync(faceData, name, false, cancellationToken);
    }

    public async Task<bool> SaveFaceAsync(FaceData faceData, string name, bool allowOverwrite, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("服務尚未初始化");

        try
        {
            // Check if name already exists
            var existingFace = await _database.GetFaceByNameAsync(name, cancellationToken);
            if (existingFace != null)
            {
                if (!allowOverwrite)
                {
                    _logger.LogWarning("嘗試儲存重複名稱的人臉: {Name}，設定 allowOverwrite=true 以覆蓋", name);
                    return false;
                }
                else
                {
                    _logger.LogInformation("覆蓋現有人臉: {Name}", name);
                    // 保留原有的 ID 以便更新
                    faceData.Id = existingFace.Id;
                }
            }
            
            faceData.Name = name;
            faceData.LastUpdated = DateTime.UtcNow;
            
            // 在演示模式下，如果特徵向量已經存在（從偵測階段產生），則保留它
            // 否則產生基於人名的一致特徵向量
            if (faceData.FeatureVector == null)
            {
                faceData.FeatureVector = GenerateSimulatedFeatureVectorForPerson(name);
                _logger.LogDebug("為 {Name} 產生基於人名的特徵向量", name);
            }
            else
            {
                _logger.LogDebug("保留 {Name} 的現有特徵向量（來自偵測階段）", name);
            }
            
            // Save to database
            var saved = await _database.SaveFaceAsync(faceData, cancellationToken);
            
            if (saved)
            {
                // Update cache (remove old and add new)
                _faceCache.TryRemove(name, out _);
                _faceCache.TryAdd(name, faceData);
                
                if (existingFace != null)
                {
                    _logger.LogInformation("已覆蓋人臉: {Name}", name);
                }
                else
                {
                    _logger.LogInformation("已儲存新人臉: {Name}", name);
                }
            }
            
            return saved;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "儲存人臉失敗: {Name}", name);
            return false;
        }
    }

    public async Task<bool> DeleteSavedFaceAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("服務尚未初始化");

        try
        {
            var deleted = await _database.DeleteFaceAsync(name, cancellationToken);
            
            if (deleted)
            {
                _faceCache.TryRemove(name, out _);
                _logger.LogInformation("已刪除人臉: {Name}", name);
            }
            
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除人臉失敗: {Name}", name);
            return false;
        }
    }

    public async Task<FaceData?> GetSavedFaceAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("服務尚未初始化");

        try
        {
            return await _database.GetFaceByNameAsync(name, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "載入人臉失敗: {Name}", name);
            return null;
        }
    }

    public async Task<bool> UpdateFaceNameAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("服務尚未初始化");

        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("舊名稱和新名稱都不能為空");

        try
        {
            var faceData = await _database.GetFaceByNameAsync(oldName, cancellationToken);
            if (faceData == null)
            {
                _logger.LogWarning("找不到名稱為 {OldName} 的人臉", oldName);
                return false;
            }

            // 檢查新名稱是否已存在
            var existingFace = await _database.GetFaceByNameAsync(newName, cancellationToken);
            if (existingFace != null && existingFace.Id != faceData.Id)
            {
                _logger.LogWarning("新名稱 {NewName} 已存在", newName);
                return false;
            }

            // 更新名稱
            faceData.Name = newName;
            faceData.LastUpdated = DateTime.UtcNow;

            var updated = await _database.SaveFaceAsync(faceData, cancellationToken);
            
            if (updated)
            {
                // 更新快取
                _faceCache.TryRemove(oldName, out _);
                _faceCache.TryAdd(newName, faceData);
                _logger.LogInformation("已將人臉名稱從 {OldName} 更新為 {NewName}", oldName, newName);
            }

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新人臉名稱失敗: {OldName} -> {NewName}", oldName, newName);
            return false;
        }
    }

    public async Task<bool> ClearAllFacesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("服務尚未初始化");

        try
        {
            // 取得所有人臉名稱
            var faceNames = await _database.GetFaceNamesAsync(cancellationToken);
            
            // 逐一刪除
            bool allDeleted = true;
            foreach (var name in faceNames)
            {
                var deleted = await _database.DeleteFaceAsync(name, cancellationToken);
                if (!deleted) allDeleted = false;
            }
            
            if (allDeleted)
            {
                _faceCache.Clear();
                _logger.LogInformation("已清空所有人臉資料，共刪除 {Count} 個人臉", faceNames.Count);
            }

            return allDeleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空人臉資料失敗");
            return false;
        }
    }

    public async Task<List<string>> GetAllFaceNamesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("服務尚未初始化");

        try
        {
            return await _database.GetFaceNamesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "載入人臉名稱清單失敗");
            return new List<string>();
        }
    }

    public async Task<bool> FaceExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("服務尚未初始化");

        try
        {
            var face = await _database.GetFaceByNameAsync(name, cancellationToken);
            return face != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查人臉是否存在失敗: {Name}", name);
            return false;
        }
    }

    public async Task<int> GetFaceCountAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("服務尚未初始化");

        try
        {
            return await _database.GetFaceCountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得人臉數量失敗");
            return 0;
        }
    }

    public async Task<List<string>> GetSavedFaceNamesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized) return new List<string>();

        try
        {
            return await _database.GetFaceNamesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取人臉名稱列表失敗");
            return new List<string>();
        }
    }

    public async Task<List<FaceData>> GetAllSavedFacesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized) return new List<FaceData>();

        try
        {
            return await _database.GetAllFacesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取所有人臉資料失敗");
            return new List<FaceData>();
        }
    }

    public async Task<bool> TryReinitializeFaceAiSharpAsync()
    {
        _logger.LogInformation("嘗試重新初始化 FaceAiSharp...");
        
        try
        {
            // Dispose existing instances
            if (_faceDetector is IDisposable disposableDetector)
                disposableDetector.Dispose();
            if (_embeddingsGenerator is IDisposable disposableGenerator)
                disposableGenerator.Dispose();
                
            _faceDetector = null;
            _embeddingsGenerator = null;
            
            // Run diagnostics again
            DiagnoseFaceAiSharpEnvironment();
            
            // Try to reinitialize
            _faceDetector = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
            _embeddingsGenerator = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();
            
            _logger.LogInformation("FaceAiSharp 重新初始化成功");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FaceAiSharp 重新初始化失敗");
            _faceDetector = null;
            _embeddingsGenerator = null;
            return false;
        }
    }

    public bool IsFaceAiSharpAvailable => _faceDetector != null && _embeddingsGenerator != null;

    public async Task<float> GetFaceSimilarityAsync(FaceData face1, FaceData face2, CancellationToken cancellationToken = default)
    {
        if (face1.FeatureVector == null || face2.FeatureVector == null)
            return 0f;

        await Task.CompletedTask; // Make it async
        return CalculateCosineSimilarity(face1.FeatureVector, face2.FeatureVector);
    }

    private async Task<FaceMatch?> FindBestMatchAsync(float[] embedding, CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== 開始診斷 FindBestMatchAsync ===");
        _logger.LogInformation("輸入特徵向量長度: {Length}", embedding?.Length ?? 0);
        _logger.LogInformation("快取中的人臉數量: {Count}", _faceCache.Count);
        
        if (embedding == null || embedding.Length == 0)
        {
            _logger.LogError("❌ 輸入的特徵向量為空或長度為 0");
            return null;
        }
        
        if (!_faceCache.Any())
        {
            _logger.LogWarning("❌ 人臉快取為空，沒有已儲存的人臉可供匹配");
            _logger.LogInformation("建議：先使用訓練模式儲存至少一個人臉");
            return null;
        }

        await Task.CompletedTask; // Make it async
        
        FaceMatch? bestMatch = null;
        var bestSimilarity = 0f;
        
        _logger.LogInformation("開始匹配，快取中有 {Count} 個人臉", _faceCache.Count);

        foreach (var kvp in _faceCache)
        {
            _logger.LogInformation("檢查人臉: {Name}", kvp.Key);
            
            if (kvp.Value.FeatureVector == null) 
            {
                _logger.LogWarning("⚠️ 跳過 {Name}：特徵向量為空", kvp.Key);
                continue;
            }
            
            if (kvp.Value.FeatureVector.Length != embedding.Length)
            {
                _logger.LogError("❌ {Name} 的特徵向量長度不匹配: {Expected} vs {Actual}", 
                    kvp.Key, kvp.Value.FeatureVector.Length, embedding.Length);
                continue;
            }
            
            var similarity = CalculateCosineSimilarity(embedding, kvp.Value.FeatureVector);
            
            _logger.LogInformation("🔍 與 {Name} 的相似度: {Similarity:F4} (閾值: {Threshold:F4})", 
                kvp.Key, similarity, SamePersonThreshold);
            
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                _logger.LogInformation("📈 更新最高相似度: {Similarity:F4} (來自 {Name})", similarity, kvp.Key);
                
                if (similarity >= SamePersonThreshold)
                {
                    bestMatch = new FaceMatch
                    {
                        Name = kvp.Key,
                        Confidence = similarity,
                        FaceData = kvp.Value
                    };
                    _logger.LogInformation("✅ 找到有效匹配: {Name} (相似度: {Similarity:F4})", 
                        kvp.Key, similarity);
                }
                else
                {
                    _logger.LogInformation("⚠️ 相似度未達閾值: {Similarity:F4} < {Threshold:F4}", 
                        similarity, SamePersonThreshold);
                }
            }
        }

        if (bestMatch != null)
        {
            _logger.LogInformation("🎉 最佳匹配: {Name} (信心度: {Confidence:F4})", 
                bestMatch.Name, bestMatch.Confidence);
        }
        else
        {
            _logger.LogWarning("❌ 未找到匹配，最高相似度: {Similarity:F4} (閾值: {Threshold:F4})", 
                bestSimilarity, SamePersonThreshold);
            
            if (bestSimilarity > 0)
            {
                _logger.LogInformation("💡 建議：如需更寬鬆匹配，可考慮降低閾值到 {SuggestedThreshold:F4}", 
                    bestSimilarity * 0.9f);
            }
        }

        _logger.LogInformation("=== FindBestMatchAsync 診斷完成 ===");
        return bestMatch;
    }

    private static float CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0f;

        var dotProduct = 0f;
        var normA = 0f;
        var normB = 0f;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0f || normB == 0f)
            return 0f;

        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    private float[] GenerateSimulatedFeatureVectorFromImage(byte[] imageBytes)
    {
        // 基於圖片內容產生一致的特徵向量（演示用）
        // 計算圖片的簡單雜湊值作為種子
        var hash = 0;
        for (int i = 0; i < Math.Min(imageBytes.Length, 1000); i += 10)
        {
            hash = hash * 31 + imageBytes[i];
        }
        
        var random = new Random(Math.Abs(hash));
        var vector = new float[512];
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(random.NextDouble() - 0.5) * 2;
        }
        
        _logger.LogDebug("為圖片產生特徵向量，雜湊種子: {Hash}", hash);
        return vector;
    }

    private float[] GenerateSimulatedFeatureVector()
    {
        // 為演示模式產生可重現的特徵向量
        // 使用固定種子確保相同「人臉」產生相似的向量
        var random = new Random(42); // 固定種子
        var vector = new float[512];
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(random.NextDouble() - 0.5) * 2; // Range: -1 to 1
        }
        return vector;
    }

    private float[] GenerateSimulatedFeatureVectorForPerson(string personName)
    {
        // 根據人名產生一致的特徵向量（演示用）
        var hash = personName.GetHashCode();
        var random = new Random(Math.Abs(hash));
        var vector = new float[512];
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(random.NextDouble() - 0.5) * 2;
        }
        return vector;
    }

    private static Models.Rectangle ConvertRectangleF(RectangleF rect)
    {
        return new Models.Rectangle(
            (int)rect.X,
            (int)rect.Y,
            (int)rect.Width,
            (int)rect.Height
        );
    }

    private void DiagnoseFaceAiSharpEnvironment()
    {
        try
        {
            _logger.LogInformation("=== FaceAiSharp 環境診斷 ===");
            _logger.LogInformation("作業系統：{OS}", Environment.OSVersion);
            _logger.LogInformation("處理器架構：{Architecture}", Environment.Is64BitProcess ? "x64" : "x86");
            _logger.LogInformation("運行時版本：{Runtime}", Environment.Version);
            _logger.LogInformation("工作目錄：{WorkingDirectory}", Environment.CurrentDirectory);
            
            // Check if FaceAiSharp assembly can be loaded
            try
            {
                var assembly = System.Reflection.Assembly.LoadFrom("FaceAiSharp.dll");
                _logger.LogInformation("FaceAiSharp.dll 載入成功，版本：{Version}", assembly.GetName().Version);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("無法載入 FaceAiSharp.dll：{Error}", ex.Message);
            }
            
            // Check bundle factory availability
            try
            {
                var bundleType = typeof(FaceAiSharpBundleFactory);
                _logger.LogInformation("FaceAiSharpBundleFactory 類型可用：{Assembly}", bundleType.Assembly.Location);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FaceAiSharpBundleFactory 不可用：{Error}", ex.Message);
            }
            
            _logger.LogInformation("=== 診斷完成 ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "環境診斷失敗");
        }
    }

    private void OnFaceDetected(List<DetectedFace> faces, TimeSpan processingTime)
    {
        FaceDetected?.Invoke(this, new FaceDetectedEventArgs(faces, processingTime));
    }

    private void OnFaceRecognized(DetectedFace face, bool isNewFace)
    {
        FaceRecognized?.Invoke(this, new FaceRecognizedEventArgs(face, isNewFace));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Dispose FaceAiSharp components if they implement IDisposable
        if (_faceDetector is IDisposable disposableDetector)
            disposableDetector.Dispose();
            
        if (_embeddingsGenerator is IDisposable disposableGenerator)
            disposableGenerator.Dispose();
            
        _semaphore?.Dispose();
        _disposed = true;
    }
}

public class FaceMatch
{
    public string Name { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public FaceData FaceData { get; set; } = new();
}
#endif
