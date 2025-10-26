using Xunit;
using FluentAssertions;
using MapLocationApp.Services;
using MapLocationApp.Models;
using MapLocationApp.Tests.Mocks;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace MapLocationApp.Tests.Integration;

/// <summary>
/// T076-T077: Integration tests for Offline Map functionality
/// Tests complete workflow of downloading maps and using them offline
/// </summary>
public class OfflineMapTests
{
    [Fact]
    public async Task T076_OfflineMapDownload_WithProgressTracking_CompletesSuccessfully()
    {
        // Arrange
        // Test complete offline map download workflow with progress reporting
        var offlineMapService = new OfflineMapService();

        // Clear any existing cache
        await offlineMapService.ClearOfflineCacheAsync();

        // Define download area: Taipei 101 and surroundings
        var centerLat = 25.0330;
        var centerLng = 121.5654;
        var zoomLevel = 15;
        var radius = 2; // Download 5x5 tile grid

        // Track progress
        var progressUpdates = new List<int>();
        var progress = new Progress<int>(p =>
        {
            progressUpdates.Add(p);
            System.Diagnostics.Debug.WriteLine($"Download progress: {p}%");
        });

        // Act
        var startTime = DateTime.Now;

        var downloadResult = await offlineMapService.DownloadMapTilesAsync(
            centerLat,
            centerLng,
            zoomLevel,
            radius,
            progress);

        var downloadDuration = DateTime.Now - startTime;

        // Assert
        downloadResult.Should().BeTrue("download should complete successfully");

        // Verify progress tracking
        progressUpdates.Should().NotBeEmpty("progress should be reported");
        progressUpdates.Last().Should().Be(100, "should reach 100% completion");
        progressUpdates.Should().BeInAscendingOrder("progress should increase monotonically");

        // Verify download created a region
        var regions = await offlineMapService.GetDownloadedRegionsAsync();
        regions.Should().HaveCount(1, "should have one downloaded region");

        var region = regions.First();
        region.CenterLatitude.Should().BeApproximately(centerLat, 0.0001);
        region.CenterLongitude.Should().BeApproximately(centerLng, 0.0001);
        region.ZoomLevel.Should().Be(zoomLevel);
        region.Radius.Should().Be(radius);
        region.TileCount.Should().BeGreaterThan(0, "should have downloaded tiles");

        // For 5x5 grid, expect 25 tiles (if all downloads succeed)
        region.TileCount.Should().BeLessOrEqualTo(25, "should not exceed expected tile count");

        // Verify download time is reasonable (should complete in under 30 seconds for small area)
        downloadDuration.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "small area download should complete quickly");

        // Verify storage size
        var cacheSize = await offlineMapService.GetCacheStorageSizeAsync();
        cacheSize.Should().BeGreaterThan(0, "cache should have non-zero size");

        System.Diagnostics.Debug.WriteLine($"Downloaded {region.TileCount} tiles, cache size: {cacheSize / 1024.0:F2} KB");
    }

    [Fact]
    public async Task T076_OfflineMapDownload_MultipleZoomLevels_DownloadsAllLevels()
    {
        // Arrange
        // Test downloading the same area at multiple zoom levels for better offline experience
        var offlineMapService = new OfflineMapService();
        await offlineMapService.ClearOfflineCacheAsync();

        var centerLat = 25.0330;
        var centerLng = 121.5654;
        var radius = 1;

        // Act
        // Download zoom levels 13, 14, and 15 (overview to detail)
        var progress = new Progress<int>();

        await offlineMapService.DownloadMapTilesAsync(centerLat, centerLng, 13, radius, progress);
        await offlineMapService.DownloadMapTilesAsync(centerLat, centerLng, 14, radius, progress);
        await offlineMapService.DownloadMapTilesAsync(centerLat, centerLng, 15, radius, progress);

        // Assert
        var regions = await offlineMapService.GetDownloadedRegionsAsync();
        regions.Should().HaveCount(3, "should have three regions for different zoom levels");

        var zoomLevels = regions.Select(r => r.ZoomLevel).OrderBy(z => z).ToList();
        zoomLevels.Should().Equal(new[] { 13, 14, 15 }, "all zoom levels should be downloaded");

        // Verify each region has tiles
        regions.Should().OnlyContain(r => r.TileCount > 0, "all regions should have tiles");

        // Verify total cache size
        var totalSize = await offlineMapService.GetCacheStorageSizeAsync();
        totalSize.Should().BeGreaterThan(0, "total cache should have size");
    }

    [Fact]
    public async Task T076_OfflineMapDownload_LargeArea_HandlesProgressCorrectly()
    {
        // Arrange
        // Test downloading a larger area with more tiles
        var offlineMapService = new OfflineMapService();
        await offlineMapService.ClearOfflineCacheAsync();

        var centerLat = 25.0330;
        var centerLng = 121.5654;
        var zoomLevel = 14;
        var radius = 3; // 7x7 = 49 tiles

        var progressReports = new List<int>();
        var progress = new Progress<int>(p => progressReports.Add(p));

        // Act
        var result = await offlineMapService.DownloadMapTilesAsync(
            centerLat,
            centerLng,
            zoomLevel,
            radius,
            progress);

        // Assert
        result.Should().BeTrue("download should succeed");

        // Verify progress was reported multiple times
        progressReports.Should().HaveCountGreaterThan(10, "should report progress many times for large download");
        progressReports.Distinct().Count().Should().BeGreaterThan(5, "progress values should vary");
        progressReports.Last().Should().Be(100, "should complete at 100%");

        var regions = await offlineMapService.GetDownloadedRegionsAsync();
        regions.First().TileCount.Should().BeGreaterThan(30, "should download many tiles for large area");
    }

    [Fact]
    public async Task T077_OfflineMapUsage_DisplayAndCheckIn_WorksWithoutInternet()
    {
        // Arrange
        // Test using offline maps for display and geofence check-in without internet connection
        // This integration test simulates offline usage workflow

        var offlineMapService = new OfflineMapService();
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);

        await offlineMapService.ClearOfflineCacheAsync();

        // Step 1: Download offline map data for an area (with "internet")
        var targetLat = 25.0330;  // Taipei 101
        var targetLng = 121.5654;
        var zoomLevel = 15;

        var downloadResult = await offlineMapService.DownloadMapTilesAsync(
            targetLat,
            targetLng,
            zoomLevel,
            2,
            null);

        downloadResult.Should().BeTrue("initial download should succeed");

        // Step 2: Verify offline availability
        var isAvailable = await offlineMapService.IsMapAreaAvailableOfflineAsync(targetLat, targetLng, zoomLevel);
        isAvailable.Should().BeTrue("downloaded area should be available offline");

        // Step 3: Simulate offline mode - retrieve cached tile
        var n = Math.Pow(2, zoomLevel);
        var tileX = (int)((targetLng + 180.0) / 360.0 * n);
        var tileY = (int)((1.0 - Math.Asinh(Math.Tan(targetLat * Math.PI / 180.0)) / Math.PI) / 2.0 * n);

        var tileData = await offlineMapService.GetOfflineTileAsync(tileX, tileY, zoomLevel);
        tileData.Should().NotBeNull("should retrieve cached tile in offline mode");
        tileData!.Should().NotBeEmpty("cached tile should have data");

        // Step 4: Create geofence at the cached location
        var geofence = new GeofenceRegion
        {
            Id = "offline-test-geofence",
            Name = "Offline Test Location",
            Latitude = targetLat,
            Longitude = targetLng,
            RadiusMeters = 200,
            IsActive = true
        };

        await geofenceService.AddGeofenceAsync(geofence);

        // Step 5: Simulate GPS location and check-in (offline mode)
        var userLocation = new MauiLocation(targetLat + 0.0005, targetLng + 0.0005); // ~50m offset
        mockLocationService.SetFixedLocation(userLocation);

        var isInsideGeofence = geofenceService.CheckPointInGeofence(userLocation, geofence);

        // Assert
        isInsideGeofence.Should().BeTrue("should detect geofence entry in offline mode");

        System.Diagnostics.Debug.WriteLine("✓ Offline map display and geofence check-in successful without internet");
    }

    [Fact]
    public async Task T077_OfflineMapUsage_NavigationWithCachedTiles_WorksOffline()
    {
        // Arrange
        // Test offline navigation using cached map tiles
        var offlineMapService = new OfflineMapService();
        var mockLocationService = new MockLocationService();

        await offlineMapService.ClearOfflineCacheAsync();

        // Download map tiles along a route
        var routePoints = new[]
        {
            new { Lat = 25.0330, Lng = 121.5654 },  // Start
            new { Lat = 25.0350, Lng = 121.5670 },  // Waypoint 1
            new { Lat = 25.0370, Lng = 121.5680 }   // End
        };

        var zoomLevel = 16;

        // Download tiles for each point along route
        foreach (var point in routePoints)
        {
            await offlineMapService.DownloadMapTilesAsync(point.Lat, point.Lng, zoomLevel, 1, null);
        }

        // Act
        // Simulate navigation: check tile availability at each point
        var allTilesAvailable = true;

        foreach (var point in routePoints)
        {
            var isAvailable = await offlineMapService.IsMapAreaAvailableOfflineAsync(
                point.Lat,
                point.Lng,
                zoomLevel);

            if (!isAvailable)
            {
                allTilesAvailable = false;
                break;
            }
        }

        // Assert
        allTilesAvailable.Should().BeTrue("all route points should have cached tiles for offline navigation");

        var regions = await offlineMapService.GetDownloadedRegionsAsync();
        regions.Count.Should().Be(routePoints.Length, "should have regions for all route points");

        System.Diagnostics.Debug.WriteLine($"✓ Offline navigation ready with {regions.Sum(r => r.TileCount)} cached tiles");
    }

    [Fact]
    public async Task T077_OfflineMapUsage_GeofenceCheckIn_MultipleLocationsOffline()
    {
        // Arrange
        // Test checking into multiple geofences using offline maps
        var offlineMapService = new OfflineMapService();
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);

        await offlineMapService.ClearOfflineCacheAsync();

        // Define multiple check-in locations
        var checkInLocations = new[]
        {
            new { Name = "Office", Lat = 25.0330, Lng = 121.5654 },
            new { Name = "Client Site A", Lat = 25.0420, Lng = 121.5650 },
            new { Name = "Client Site B", Lat = 25.0250, Lng = 121.5660 }
        };

        var zoomLevel = 15;

        // Download offline maps for all check-in locations
        foreach (var location in checkInLocations)
        {
            await offlineMapService.DownloadMapTilesAsync(
                location.Lat,
                location.Lng,
                zoomLevel,
                2,
                null);
        }

        // Create geofences for each location
        var geofences = new List<GeofenceRegion>();
        foreach (var location in checkInLocations)
        {
            var geofence = new GeofenceRegion
            {
                Id = $"offline-{location.Name.Replace(" ", "-").ToLower()}",
                Name = location.Name,
                Latitude = location.Lat,
                Longitude = location.Lng,
                RadiusMeters = 150,
                IsActive = true
            };

            await geofenceService.AddGeofenceAsync(geofence);
            geofences.Add(geofence);
        }

        // Act
        // Simulate checking in at each location (offline mode)
        var checkInResults = new List<bool>();

        foreach (var location in checkInLocations)
        {
            // Set GPS location to check-in point
            var gpsLocation = new MauiLocation(location.Lat, location.Lng);
            mockLocationService.SetFixedLocation(gpsLocation);

            // Verify offline map is available
            var mapAvailable = await offlineMapService.IsMapAreaAvailableOfflineAsync(
                location.Lat,
                location.Lng,
                zoomLevel);

            // Find matching geofence
            var geofence = geofences.First(g => g.Name == location.Name);

            // Check if inside geofence
            var isInside = geofenceService.CheckPointInGeofence(gpsLocation, geofence);

            checkInResults.Add(mapAvailable && isInside);
        }

        // Assert
        checkInResults.Should().OnlyContain(result => result == true,
            "all check-ins should succeed with offline maps and geofence detection");

        var regions = await offlineMapService.GetDownloadedRegionsAsync();
        regions.Count.Should().Be(checkInLocations.Length,
            "should have offline maps for all check-in locations");

        System.Diagnostics.Debug.WriteLine($"✓ Successfully checked in at {checkInResults.Count} locations using offline maps");
    }

    [Fact]
    public async Task T077_OfflineMapUsage_CacheManagement_DeleteUnusedRegions()
    {
        // Arrange
        // Test cache management: download multiple regions, then delete unused ones
        var offlineMapService = new OfflineMapService();
        await offlineMapService.ClearOfflineCacheAsync();

        // Download 3 regions
        await offlineMapService.DownloadMapTilesAsync(25.0330, 121.5654, 15, 1, null); // Region 1
        await offlineMapService.DownloadMapTilesAsync(25.0420, 121.5650, 15, 1, null); // Region 2
        await offlineMapService.DownloadMapTilesAsync(25.0250, 121.5660, 15, 1, null); // Region 3

        var initialRegions = await offlineMapService.GetDownloadedRegionsAsync();
        initialRegions.Should().HaveCount(3, "should have 3 regions initially");

        var initialSize = await offlineMapService.GetCacheStorageSizeAsync();

        // Act
        // Delete region 2 (keep regions 1 and 3)
        var regionToDelete = initialRegions[1];
        var deleteResult = await offlineMapService.DeleteOfflineRegionAsync(regionToDelete.Id);

        // Assert
        deleteResult.Should().BeTrue("deletion should succeed");

        var remainingRegions = await offlineMapService.GetDownloadedRegionsAsync();
        remainingRegions.Should().HaveCount(2, "should have 2 regions after deletion");

        var finalSize = await offlineMapService.GetCacheStorageSizeAsync();
        finalSize.Should().BeLessThan(initialSize, "cache size should decrease after deletion");

        // Verify the correct region was deleted
        remainingRegions.Should().NotContain(r => r.Id == regionToDelete.Id);
    }
}
