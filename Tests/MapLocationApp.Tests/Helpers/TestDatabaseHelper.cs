using SQLite;

namespace MapLocationApp.Tests.Helpers;

/// <summary>
/// Helper class for creating SQLite in-memory databases for testing
/// T023: Provides isolated test databases that are automatically cleaned up
/// </summary>
public class TestDatabaseHelper : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    /// <summary>
    /// Creates a new in-memory SQLite database
    /// </summary>
    public TestDatabaseHelper()
    {
        // In-memory database (":memory:") - destroyed when connection closes
        _connection = new SQLiteConnection(":memory:");
        InitializeSchema();
    }

    /// <summary>
    /// Creates a new SQLite database with custom path (for persistent test data)
    /// </summary>
    /// <param name="databasePath">Path to database file</param>
    public TestDatabaseHelper(string databasePath)
    {
        _connection = new SQLiteConnection(databasePath);
        InitializeSchema();
    }

    /// <summary>
    /// Gets the SQLite connection for direct database operations
    /// </summary>
    public SQLiteConnection Connection => _connection;

    /// <summary>
    /// Initializes database schema for testing
    /// Creates tables used by MapLocationApp (geofences, offline maps, face embeddings, etc.)
    /// </summary>
    private void InitializeSchema()
    {
        // Geofence regions table
        _connection.Execute(@"
            CREATE TABLE IF NOT EXISTS geofence_regions (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL,
                radius_meters REAL NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 1,
                transition_type INTEGER NOT NULL DEFAULT 3,
                category TEXT,
                description TEXT,
                created_at TEXT NOT NULL
            )
        ");

        // Offline map tiles table
        _connection.Execute(@"
            CREATE TABLE IF NOT EXISTS map_tiles (
                x INTEGER NOT NULL,
                y INTEGER NOT NULL,
                zoom INTEGER NOT NULL,
                tile_data BLOB NOT NULL,
                downloaded_at TEXT DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (x, y, zoom)
            )
        ");

        _connection.Execute(@"
            CREATE INDEX IF NOT EXISTS idx_tiles_zoom ON map_tiles(zoom)
        ");

        // Face embeddings table (for face recognition)
        _connection.Execute(@"
            CREATE TABLE IF NOT EXISTS face_embeddings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL UNIQUE,
                username TEXT NOT NULL,
                embedding BLOB NOT NULL,
                created_at TEXT NOT NULL
            )
        ");

        // Offline check-in queue (for sync when back online)
        _connection.Execute(@"
            CREATE TABLE IF NOT EXISTS offline_check_ins (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL,
                geofence_id TEXT,
                geofence_name TEXT,
                check_in_time TEXT NOT NULL,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL,
                notes TEXT,
                type TEXT NOT NULL,
                synced INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL
            )
        ");

        // App configuration table
        _connection.Execute(@"
            CREATE TABLE IF NOT EXISTS app_config (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at TEXT NOT NULL
            )
        ");
    }

    /// <summary>
    /// Inserts test data into geofence_regions table
    /// </summary>
    public void SeedGeofences(params (string id, string name, double lat, double lon, double radius)[] geofences)
    {
        foreach (var (id, name, lat, lon, radius) in geofences)
        {
            _connection.Execute(
                @"INSERT INTO geofence_regions (id, name, latitude, longitude, radius_meters, is_active, created_at)
                  VALUES (?, ?, ?, ?, ?, 1, ?)",
                id, name, lat, lon, radius, DateTime.UtcNow.ToString("o")
            );
        }
    }

    /// <summary>
    /// Inserts test tile data into map_tiles table
    /// </summary>
    public void SeedMapTiles(params (int x, int y, int zoom, byte[] data)[] tiles)
    {
        foreach (var (x, y, zoom, data) in tiles)
        {
            _connection.Execute(
                @"INSERT INTO map_tiles (x, y, zoom, tile_data, downloaded_at)
                  VALUES (?, ?, ?, ?, ?)",
                x, y, zoom, data, DateTime.UtcNow.ToString("o")
            );
        }
    }

    /// <summary>
    /// Inserts test face embedding data
    /// </summary>
    public void SeedFaceEmbeddings(params (int userId, string username, float[] embedding)[] embeddings)
    {
        foreach (var (userId, username, embedding) in embeddings)
        {
            var embeddingBytes = new byte[embedding.Length * sizeof(float)];
            Buffer.BlockCopy(embedding, 0, embeddingBytes, 0, embeddingBytes.Length);

            _connection.Execute(
                @"INSERT INTO face_embeddings (user_id, username, embedding, created_at)
                  VALUES (?, ?, ?, ?)",
                userId, username, embeddingBytes, DateTime.UtcNow.ToString("o")
            );
        }
    }

    /// <summary>
    /// Clears all data from all tables (reset between tests)
    /// </summary>
    public void ClearAllData()
    {
        _connection.Execute("DELETE FROM geofence_regions");
        _connection.Execute("DELETE FROM map_tiles");
        _connection.Execute("DELETE FROM face_embeddings");
        _connection.Execute("DELETE FROM offline_check_ins");
        _connection.Execute("DELETE FROM app_config");
    }

    /// <summary>
    /// Executes raw SQL query (for custom test scenarios)
    /// </summary>
    public void ExecuteRaw(string sql, params object[] args)
    {
        _connection.Execute(sql, args);
    }

    /// <summary>
    /// Queries data from database
    /// </summary>
    public List<T> Query<T>(string sql, params object[] args) where T : new()
    {
        return _connection.Query<T>(sql, args);
    }

    /// <summary>
    /// Gets count of rows in a table
    /// </summary>
    public int GetRowCount(string tableName)
    {
        return _connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM {tableName}");
    }

    /// <summary>
    /// Disposes database connection
    /// In-memory databases are automatically destroyed on disposal
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _connection?.Dispose();
        }

        _disposed = true;
    }
}

/// <summary>
/// Example usage in tests:
///
/// [Fact]
/// public void GeofenceService_AddGeofence_StoresInDatabase()
/// {
///     // Arrange
///     using var dbHelper = new TestDatabaseHelper();
///     var geofenceService = new GeofenceService(dbHelper.Connection);
///
///     var geofence = new GeofenceRegion
///     {
///         Id = "test-geofence-1",
///         Name = "Test Office",
///         Latitude = 25.0330,
///         Longitude = 121.5654,
///         RadiusMeters = 200
///     };
///
///     // Act
///     geofenceService.AddGeofenceAsync(geofence).Wait();
///
///     // Assert
///     var count = dbHelper.GetRowCount("geofence_regions");
///     count.Should().Be(1);
///
///     var stored = dbHelper.Query<GeofenceRegion>("SELECT * FROM geofence_regions").First();
///     stored.Name.Should().Be("Test Office");
/// }
///
/// // Database is automatically disposed and destroyed at end of using block
/// </summary>
