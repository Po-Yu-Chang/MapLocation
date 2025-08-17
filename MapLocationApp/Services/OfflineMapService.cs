using System.Text.Json;
using Microsoft.Maui.Storage;

namespace MapLocationApp.Services
{
    public interface IOfflineMapService
    {
        Task<bool> DownloadMapTilesAsync(double centerLat, double centerLng, int zoomLevel, int radius, IProgress<int> progress = null);
        Task<bool> IsMapAreaAvailableOfflineAsync(double lat, double lng, int zoomLevel);
        Task<byte[]?> GetOfflineTileAsync(int x, int y, int z);
        Task<long> GetCacheStorageSizeAsync();
        Task ClearOfflineCacheAsync();
        Task<List<OfflineMapRegion>> GetDownloadedRegionsAsync();
        Task<bool> DeleteOfflineRegionAsync(string regionId);
    }

    public class OfflineMapService : IOfflineMapService
    {
        private readonly string _cacheDirectory;
        private readonly string _regionsFile;
        private readonly HttpClient _httpClient;

        public OfflineMapService()
        {
            _cacheDirectory = Path.Combine(FileSystem.AppDataDirectory, "OfflineMaps");
            _regionsFile = Path.Combine(FileSystem.AppDataDirectory, "offline_regions.json");
            _httpClient = new HttpClient();
            
            Directory.CreateDirectory(_cacheDirectory);
        }

        public async Task<bool> DownloadMapTilesAsync(double centerLat, double centerLng, int zoomLevel, int radius, IProgress<int> progress = null)
        {
            try
            {
                var regionId = Guid.NewGuid().ToString();
                var region = new OfflineMapRegion
                {
                    Id = regionId,
                    Name = $"Region {DateTime.Now:yyyy-MM-dd HH:mm}",
                    CenterLatitude = centerLat,
                    CenterLongitude = centerLng,
                    ZoomLevel = zoomLevel,
                    Radius = radius,
                    DownloadDate = DateTime.Now,
                    TileCount = 0
                };

                var tiles = CalculateTileCoordinates(centerLat, centerLng, zoomLevel, radius);
                var totalTiles = tiles.Count;
                var downloadedTiles = 0;

                foreach (var tile in tiles)
                {
                    var tileData = await DownloadTileAsync(tile.X, tile.Y, tile.Z);
                    if (tileData != null)
                    {
                        await SaveTileAsync(tile.X, tile.Y, tile.Z, tileData);
                        downloadedTiles++;
                        progress?.Report((downloadedTiles * 100) / totalTiles);
                    }
                }

                region.TileCount = downloadedTiles;
                await SaveOfflineRegionAsync(region);

                return downloadedTiles > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Download error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsMapAreaAvailableOfflineAsync(double lat, double lng, int zoomLevel)
        {
            var tileCoords = LatLngToTileCoords(lat, lng, zoomLevel);
            var tilePath = GetTilePath(tileCoords.X, tileCoords.Y, zoomLevel);
            return File.Exists(tilePath);
        }

        public async Task<byte[]?> GetOfflineTileAsync(int x, int y, int z)
        {
            try
            {
                var tilePath = GetTilePath(x, y, z);
                if (File.Exists(tilePath))
                {
                    return await File.ReadAllBytesAsync(tilePath);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<long> GetCacheStorageSizeAsync()
        {
            try
            {
                var directoryInfo = new DirectoryInfo(_cacheDirectory);
                if (!directoryInfo.Exists)
                    return 0;

                return directoryInfo.GetFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length);
            }
            catch
            {
                return 0;
            }
        }

        public async Task ClearOfflineCacheAsync()
        {
            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    Directory.Delete(_cacheDirectory, true);
                    Directory.CreateDirectory(_cacheDirectory);
                }

                if (File.Exists(_regionsFile))
                {
                    File.Delete(_regionsFile);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clear cache error: {ex.Message}");
            }
        }

        public async Task<List<OfflineMapRegion>> GetDownloadedRegionsAsync()
        {
            try
            {
                if (!File.Exists(_regionsFile))
                    return new List<OfflineMapRegion>();

                var json = await File.ReadAllTextAsync(_regionsFile);
                return JsonSerializer.Deserialize<List<OfflineMapRegion>>(json) ?? new List<OfflineMapRegion>();
            }
            catch
            {
                return new List<OfflineMapRegion>();
            }
        }

        public async Task<bool> DeleteOfflineRegionAsync(string regionId)
        {
            try
            {
                var regions = await GetDownloadedRegionsAsync();
                var region = regions.FirstOrDefault(r => r.Id == regionId);
                if (region == null)
                    return false;

                // 刪除該區域的磁貼
                var tiles = CalculateTileCoordinates(region.CenterLatitude, region.CenterLongitude, region.ZoomLevel, region.Radius);
                foreach (var tile in tiles)
                {
                    var tilePath = GetTilePath(tile.X, tile.Y, tile.Z);
                    if (File.Exists(tilePath))
                        File.Delete(tilePath);
                }

                // 從區域列表中移除
                regions.Remove(region);
                await SaveRegionsAsync(regions);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<byte[]?> DownloadTileAsync(int x, int y, int z)
        {
            try
            {
                // 使用 OpenStreetMap 磁貼伺服器
                var url = $"https://tile.openstreetmap.org/{z}/{x}/{y}.png";
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task SaveTileAsync(int x, int y, int z, byte[] tileData)
        {
            try
            {
                var tilePath = GetTilePath(x, y, z);
                var directory = Path.GetDirectoryName(tilePath);
                Directory.CreateDirectory(directory);
                await File.WriteAllBytesAsync(tilePath, tileData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save tile error: {ex.Message}");
            }
        }

        private string GetTilePath(int x, int y, int z)
        {
            return Path.Combine(_cacheDirectory, z.ToString(), x.ToString(), $"{y}.png");
        }

        private List<TileCoordinate> CalculateTileCoordinates(double centerLat, double centerLng, int zoomLevel, int radius)
        {
            var tiles = new List<TileCoordinate>();
            var centerTile = LatLngToTileCoords(centerLat, centerLng, zoomLevel);

            for (int x = centerTile.X - radius; x <= centerTile.X + radius; x++)
            {
                for (int y = centerTile.Y - radius; y <= centerTile.Y + radius; y++)
                {
                    if (x >= 0 && y >= 0 && x < Math.Pow(2, zoomLevel) && y < Math.Pow(2, zoomLevel))
                    {
                        tiles.Add(new TileCoordinate { X = x, Y = y, Z = zoomLevel });
                    }
                }
            }

            return tiles;
        }

        private TileCoordinate LatLngToTileCoords(double lat, double lng, int zoom)
        {
            var n = Math.Pow(2, zoom);
            var x = (int)((lng + 180.0) / 360.0 * n);
            var y = (int)((1.0 - Math.Asinh(Math.Tan(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * n);
            return new TileCoordinate { X = x, Y = y, Z = zoom };
        }

        private async Task SaveOfflineRegionAsync(OfflineMapRegion region)
        {
            try
            {
                var regions = await GetDownloadedRegionsAsync();
                regions.Add(region);
                await SaveRegionsAsync(regions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save region error: {ex.Message}");
            }
        }

        private async Task SaveRegionsAsync(List<OfflineMapRegion> regions)
        {
            try
            {
                var json = JsonSerializer.Serialize(regions, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_regionsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save regions error: {ex.Message}");
            }
        }
    }

    public class OfflineMapRegion
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double CenterLatitude { get; set; }
        public double CenterLongitude { get; set; }
        public int ZoomLevel { get; set; }
        public int Radius { get; set; }
        public DateTime DownloadDate { get; set; }
        public int TileCount { get; set; }
    }

    public class TileCoordinate
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }
}