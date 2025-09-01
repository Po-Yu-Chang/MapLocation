using MapLocationApp.Models;

namespace MapLocationApp.Services;

public interface IFaceRecognitionService
{
    Task<List<FaceData>> DetectFacesAsync(byte[] imageData, CancellationToken cancellationToken = default);
    Task<List<FaceData>> DetectFacesAsync(string imagePath, CancellationToken cancellationToken = default);
    Task<RecognitionResult> RecognizeFaceAsync(byte[] imageData, CancellationToken cancellationToken = default);
    Task<RecognitionResult> RecognizeFaceAsync(string imagePath, CancellationToken cancellationToken = default);
    Task<bool> SaveFaceAsync(FaceData faceData, string name, CancellationToken cancellationToken = default);
    Task<bool> SaveFaceAsync(FaceData faceData, string name, bool allowOverwrite, CancellationToken cancellationToken = default);
    Task<bool> DeleteSavedFaceAsync(string name, CancellationToken cancellationToken = default);
    Task<List<string>> GetSavedFaceNamesAsync(CancellationToken cancellationToken = default);
    Task<List<FaceData>> GetAllSavedFacesAsync(CancellationToken cancellationToken = default);
    Task<float> GetFaceSimilarityAsync(FaceData face1, FaceData face2, CancellationToken cancellationToken = default);

    // Face Model Management Methods 
    Task<FaceData?> GetSavedFaceAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> UpdateFaceNameAsync(string oldName, string newName, CancellationToken cancellationToken = default);
    Task<bool> FaceExistsAsync(string name, CancellationToken cancellationToken = default);
    Task<int> GetFaceCountAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetAllFaceNamesAsync(CancellationToken cancellationToken = default);
    Task<bool> ClearAllFacesAsync(CancellationToken cancellationToken = default);

    bool IsInitialized { get; }
    bool IsSupported { get; }
    
    event EventHandler<FaceDetectedEventArgs>? FaceDetected;
    event EventHandler<FaceRecognizedEventArgs>? FaceRecognized;
}

public class FaceDetectedEventArgs : EventArgs
{
    public List<DetectedFace> Faces { get; }
    public TimeSpan ProcessingTime { get; }
    public DateTime Timestamp { get; }

    public FaceDetectedEventArgs(List<DetectedFace> faces, TimeSpan processingTime)
    {
        Faces = faces;
        ProcessingTime = processingTime;
        Timestamp = DateTime.UtcNow;
    }
}

public class FaceRecognizedEventArgs : EventArgs
{
    public DetectedFace RecognizedFace { get; }
    public bool IsNewFace { get; }
    public DateTime Timestamp { get; }

    public FaceRecognizedEventArgs(DetectedFace face, bool isNewFace)
    {
        RecognizedFace = face;
        IsNewFace = isNewFace;
        Timestamp = DateTime.UtcNow;
    }
}