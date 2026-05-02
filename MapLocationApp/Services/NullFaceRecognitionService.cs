using MapLocationApp.Models;

namespace MapLocationApp.Services;

/// <summary>
/// Stub face-recognition service used after the FaceAiSharp Windows-only dependency was removed.
/// Always reports "not supported" so existing pages render their unavailable-mode UI.
/// New code should prefer <see cref="IBiometricService"/> for cross-platform device biometrics.
/// </summary>
public class NullFaceRecognitionService : IFaceRecognitionService
{
    public bool IsInitialized => false;
    public bool IsSupported => false;

    public event EventHandler<FaceDetectedEventArgs>? FaceDetected;
    public event EventHandler<FaceRecognizedEventArgs>? FaceRecognized;

    public Task<List<FaceData>> DetectFacesAsync(byte[] imageData, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<FaceData>());

    public Task<List<FaceData>> DetectFacesAsync(string imagePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<FaceData>());

    public Task<RecognitionResult> RecognizeFaceAsync(byte[] imageData, CancellationToken cancellationToken = default)
        => Task.FromResult(new RecognitionResult());

    public Task<RecognitionResult> RecognizeFaceAsync(string imagePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new RecognitionResult());

    public Task<bool> SaveFaceAsync(FaceData faceData, string name, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> SaveFaceAsync(FaceData faceData, string name, bool allowOverwrite, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> DeleteSavedFaceAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<List<string>> GetSavedFaceNamesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<string>());

    public Task<List<FaceData>> GetAllSavedFacesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<FaceData>());

    public Task<float> GetFaceSimilarityAsync(FaceData face1, FaceData face2, CancellationToken cancellationToken = default)
        => Task.FromResult(0f);

    public Task<FaceData?> GetSavedFaceAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult<FaceData?>(null);

    public Task<bool> UpdateFaceNameAsync(string oldName, string newName, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> FaceExistsAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<int> GetFaceCountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<List<string>> GetAllFaceNamesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<string>());

    public Task<bool> ClearAllFacesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
