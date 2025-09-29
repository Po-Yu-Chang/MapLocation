using SQLite;
using MapLocationApp.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MapLocationApp.Services;

public interface IFaceDatabase
{
    Task<bool> InitializeAsync();
    Task<FaceData?> GetFaceByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<FaceData>> GetAllFacesAsync(CancellationToken cancellationToken = default);
    Task<bool> SaveFaceAsync(FaceData faceData, CancellationToken cancellationToken = default);
    Task<bool> UpdateFaceAsync(FaceData faceData, CancellationToken cancellationToken = default);
    Task<bool> DeleteFaceAsync(string name, CancellationToken cancellationToken = default);
    Task<int> GetFaceCountAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetFaceNamesAsync(CancellationToken cancellationToken = default);
}

public class FaceDatabase : IFaceDatabase
{
    private SQLiteAsyncConnection? _database;
    private readonly string _dbPath;
    private readonly ILogger<FaceDatabase> _logger;

    public FaceDatabase(ILogger<FaceDatabase> logger)
    {
        _logger = logger;
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "faces.db");
        _logger.LogInformation("Face database path: {DbPath}", _dbPath);
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            if (_database == null)
            {
                _database = new SQLiteAsyncConnection(_dbPath);
                await _database.CreateTableAsync<FaceData>();
                _logger.LogInformation("Face database initialized successfully");
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize face database");
            return false;
        }
    }

    public async Task<FaceData?> GetFaceByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_database == null) return null;

        try
        {
            var face = await _database.Table<FaceData>()
                .Where(f => f.Name == name)
                .FirstOrDefaultAsync();

            if (face != null && !string.IsNullOrEmpty(face.FeatureVectorJson))
            {
                face.FeatureVector = JsonSerializer.Deserialize<float[]>(face.FeatureVectorJson);
            }

            return face;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting face by name: {Name}", name);
            return null;
        }
    }

    public async Task<List<FaceData>> GetAllFacesAsync(CancellationToken cancellationToken = default)
    {
        if (_database == null) return new List<FaceData>();

        try
        {
            var faces = await _database.Table<FaceData>().ToListAsync();
            
            // Deserialize feature vectors
            foreach (var face in faces)
            {
                if (!string.IsNullOrEmpty(face.FeatureVectorJson))
                {
                    face.FeatureVector = JsonSerializer.Deserialize<float[]>(face.FeatureVectorJson);
                }
            }

            return faces;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all faces");
            return new List<FaceData>();
        }
    }

    public async Task<bool> SaveFaceAsync(FaceData faceData, CancellationToken cancellationToken = default)
    {
        if (_database == null) return false;

        try
        {
            // Serialize feature vector
            if (faceData.FeatureVector != null)
            {
                faceData.FeatureVectorJson = JsonSerializer.Serialize(faceData.FeatureVector);
            }

            faceData.LastUpdated = DateTime.UtcNow;
            
            await _database.InsertOrReplaceAsync(faceData);
            _logger.LogInformation("Face saved successfully: {Name}", faceData.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving face: {Name}", faceData.Name);
            return false;
        }
    }

    public async Task<bool> UpdateFaceAsync(FaceData faceData, CancellationToken cancellationToken = default)
    {
        if (_database == null) return false;

        try
        {
            // Serialize feature vector
            if (faceData.FeatureVector != null)
            {
                faceData.FeatureVectorJson = JsonSerializer.Serialize(faceData.FeatureVector);
            }

            faceData.LastUpdated = DateTime.UtcNow;
            
            await _database.UpdateAsync(faceData);
            _logger.LogInformation("Face updated successfully: {Name}", faceData.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating face: {Name}", faceData.Name);
            return false;
        }
    }

    public async Task<bool> DeleteFaceAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_database == null) return false;

        try
        {
            await _database.Table<FaceData>()
                .Where(f => f.Name == name)
                .DeleteAsync();
            
            _logger.LogInformation("Face deleted successfully: {Name}", name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting face: {Name}", name);
            return false;
        }
    }

    public async Task<int> GetFaceCountAsync(CancellationToken cancellationToken = default)
    {
        if (_database == null) return 0;

        try
        {
            return await _database.Table<FaceData>().CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting face count");
            return 0;
        }
    }

    public async Task<List<string>> GetFaceNamesAsync(CancellationToken cancellationToken = default)
    {
        if (_database == null) return new List<string>();

        try
        {
            var faces = await _database.Table<FaceData>().ToListAsync();
            return faces.Select(f => f.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting face names");
            return new List<string>();
        }
    }
}