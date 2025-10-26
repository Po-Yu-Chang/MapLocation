using Xunit;
using FluentAssertions;
using MapLocationApp.Services;
using MapLocationApp.Models;
using MapLocationApp.Tests.Mocks;
using Microsoft.Maui.Devices.Sensors;
using NetTopologySuite.Geometries;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace MapLocationApp.Tests.Services;

/// <summary>
/// T024-T025: Unit tests for GeofenceService
/// Tests geofence point-in-polygon detection and geofence management
/// </summary>
public class GeofenceServiceTests
{
    [Fact]
    public void T024_CheckPointInGeofence_PointInside_ReturnsTrue()
    {
        // Arrange
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);
        var geofence = new GeofenceRegion
        {
            Id = "test-office",
            Name = "Test Office",
            Latitude = 25.0330,  // Taipei 101 center
            Longitude = 121.5654,
            RadiusMeters = 200,   // 200m radius
            IsActive = true
        };

        // Point inside geofence (50m from center)
        var testLocation = new MauiLocation(25.0335, 121.5654);

        // Act
        var result = geofenceService.CheckPointInGeofence(testLocation, geofence);

        // Assert
        result.Should().BeTrue("point is 50m from center, well within 200m radius");
    }

    [Fact]
    public void T024_CheckPointInGeofence_PointOutside_ReturnsFalse()
    {
        // Arrange
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);
        var geofence = new GeofenceRegion
        {
            Id = "test-office",
            Name = "Test Office",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 200
        };

        // Point outside geofence (500m from center)
        var testLocation = new MauiLocation(25.0380, 121.5654);

        // Act
        var result = geofenceService.CheckPointInGeofence(testLocation, geofence);

        // Assert
        result.Should().BeFalse("point is 500m from center, outside 200m radius");
    }

    [Fact]
    public void T024_CheckPointInGeofence_PointOnBoundary_ReturnsTrue()
    {
        // Arrange
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);
        var geofence = new GeofenceRegion
        {
            Id = "test-office",
            Name = "Test Office",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 200
        };

        // Point exactly on boundary (200m from center)
        // Using Haversine formula: ~0.0018° latitude ≈ 200m
        var testLocation = new MauiLocation(25.0348, 121.5654);

        // Act
        var result = geofenceService.CheckPointInGeofence(testLocation, geofence);

        // Assert
        result.Should().BeTrue("point is on the boundary, should be included");
    }

    [Fact]
    public void T024_CheckPointInGeofence_InactiveGeofence_ReturnsFalse()
    {
        // Arrange
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);
        var geofence = new GeofenceRegion
        {
            Id = "test-office",
            Name = "Test Office",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 200,
            IsActive = false  // Inactive geofence
        };

        var testLocation = new MauiLocation(25.0335, 121.5654);

        // Act
        var result = geofenceService.CheckPointInGeofence(testLocation, geofence);

        // Assert
        result.Should().BeFalse("inactive geofences should always return false");
    }

    [Fact]
    public async Task T025_AddGeofenceAsync_ValidGeofence_ReturnsSuccess()
    {
        // Arrange
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);
        var geofence = new GeofenceRegion
        {
            Id = "test-geofence-1",
            Name = "Test Location",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 100,
            Category = "Office",
            Description = "Test geofence for unit testing",
            IsActive = true,
            TransitionType = GeofenceTransitionType.Enter | GeofenceTransitionType.Exit
        };

        // Act
        var result = await geofenceService.AddGeofenceAsync(geofence);

        // Assert
        result.Should().BeTrue("valid geofence should be added successfully");

        // Verify geofence was added
        var monitoredGeofences = geofenceService.GetMonitoredGeofences();
        monitoredGeofences.Should().Contain(g => g.Id == "test-geofence-1");
    }

    [Fact]
    public async Task T025_AddGeofenceAsync_InvalidRadius_ThrowsArgumentException()
    {
        // Arrange
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);
        var geofence = new GeofenceRegion
        {
            Id = "invalid-geofence",
            Name = "Invalid",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = -100,  // Invalid negative radius
            IsActive = true
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await geofenceService.AddGeofenceAsync(geofence);
        });
    }

    [Fact]
    public async Task T025_AddGeofenceAsync_DuplicateId_ReturnsFalse()
    {
        // Arrange
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);
        var geofence1 = new GeofenceRegion
        {
            Id = "duplicate-id",
            Name = "First",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 100,
            IsActive = true
        };

        var geofence2 = new GeofenceRegion
        {
            Id = "duplicate-id",  // Same ID
            Name = "Second",
            Latitude = 25.0340,
            Longitude = 121.5660,
            RadiusMeters = 150,
            IsActive = true
        };

        // Act
        var firstResult = await geofenceService.AddGeofenceAsync(geofence1);
        var secondResult = await geofenceService.AddGeofenceAsync(geofence2);

        // Assert
        firstResult.Should().BeTrue("first geofence should be added");
        secondResult.Should().BeFalse("duplicate ID should be rejected");
    }

    [Fact]
    public async Task T025_RemoveGeofenceAsync_ExistingGeofence_ReturnsSuccess()
    {
        // Arrange
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);
        var geofence = new GeofenceRegion
        {
            Id = "to-remove",
            Name = "Test",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 100,
            IsActive = true
        };

        await geofenceService.AddGeofenceAsync(geofence);

        // Act
        var result = await geofenceService.RemoveGeofenceAsync("to-remove");

        // Assert
        result.Should().BeTrue("existing geofence should be removed");
        geofenceService.GetMonitoredGeofences().Should().NotContain(g => g.Id == "to-remove");
    }

    [Fact]
    public async Task T099_AddGeofenceAsync_WithValidation_ValidatesCoordinates()
    {
        // Arrange
        // Test that AddGeofenceAsync validates coordinates are within valid ranges
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);

        var validGeofence = new GeofenceRegion
        {
            Id = "valid-coords",
            Name = "Valid Location",
            Latitude = 25.0330,  // Valid latitude (-90 to 90)
            Longitude = 121.5654, // Valid longitude (-180 to 180)
            RadiusMeters = 100,
            IsActive = true
        };

        var invalidLatGeofence = new GeofenceRegion
        {
            Id = "invalid-lat",
            Name = "Invalid Latitude",
            Latitude = 95.0,  // Invalid > 90
            Longitude = 121.5654,
            RadiusMeters = 100,
            IsActive = true
        };

        var invalidLngGeofence = new GeofenceRegion
        {
            Id = "invalid-lng",
            Name = "Invalid Longitude",
            Latitude = 25.0330,
            Longitude = 185.0,  // Invalid > 180
            RadiusMeters = 100,
            IsActive = true
        };

        // Act
        var validResult = await geofenceService.AddGeofenceAsync(validGeofence);

        // Assert
        validResult.Should().BeTrue("valid coordinates should be accepted");

        // Invalid coordinates should throw ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await geofenceService.AddGeofenceAsync(invalidLatGeofence));

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await geofenceService.AddGeofenceAsync(invalidLngGeofence));
    }

    [Fact]
    public async Task T099_AddGeofenceAsync_WithValidation_ValidatesRadiusRange()
    {
        // Arrange
        // Test that radius is validated (must be positive, reasonable max)
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);

        var zeroRadiusGeofence = new GeofenceRegion
        {
            Id = "zero-radius",
            Name = "Zero Radius",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 0,  // Invalid
            IsActive = true
        };

        var negativeRadiusGeofence = new GeofenceRegion
        {
            Id = "negative-radius",
            Name = "Negative Radius",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = -100,  // Invalid
            IsActive = true
        };

        var tooLargeRadiusGeofence = new GeofenceRegion
        {
            Id = "too-large",
            Name = "Too Large",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 100000,  // 100km might be too large
            IsActive = true
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await geofenceService.AddGeofenceAsync(zeroRadiusGeofence));

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await geofenceService.AddGeofenceAsync(negativeRadiusGeofence));

        // Large radius might be allowed but flagged
        // Implementation could allow or reject based on requirements
    }

    [Fact]
    public async Task T100_RemoveGeofenceAsync_NonexistentGeofence_ReturnsFalse()
    {
        // Arrange
        // Test removing a geofence that doesn't exist
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);

        // Act
        var result = await geofenceService.RemoveGeofenceAsync("non-existent-id");

        // Assert
        result.Should().BeFalse("removing non-existent geofence should return false");
    }

    [Fact]
    public async Task T100_RemoveGeofenceAsync_StopsMonitoring()
    {
        // Arrange
        // Test that removing geofence stops monitoring for that geofence
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);

        var geofence = new GeofenceRegion
        {
            Id = "monitor-test",
            Name = "Monitoring Test",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 100,
            IsActive = true
        };

        await geofenceService.AddGeofenceAsync(geofence);

        var monitoredBefore = geofenceService.GetMonitoredGeofences();
        monitoredBefore.Should().Contain(g => g.Id == "monitor-test");

        // Act
        await geofenceService.RemoveGeofenceAsync("monitor-test");

        // Assert
        var monitoredAfter = geofenceService.GetMonitoredGeofences();
        monitoredAfter.Should().NotContain(g => g.Id == "monitor-test",
            "removed geofence should no longer be monitored");
    }

    [Fact]
    public async Task T101_UpdateGeofenceAsync_BoundaryChanges_UpdatesRadius()
    {
        // Arrange
        // Test updating geofence boundary (radius change)
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);

        var geofence = new GeofenceRegion
        {
            Id = "update-test",
            Name = "Update Test",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 100,
            IsActive = true
        };

        await geofenceService.AddGeofenceAsync(geofence);

        // Act
        // Update radius to 200 meters
        geofence.RadiusMeters = 200;
        var updateResult = await geofenceService.UpdateGeofenceAsync(geofence.Id, geofence);

        // Assert
        updateResult.Should().BeTrue("geofence update should succeed");

        var monitoredGeofences = geofenceService.GetMonitoredGeofences();
        var updatedGeofence = monitoredGeofences.First(g => g.Id == "update-test");
        updatedGeofence.RadiusMeters.Should().Be(200, "radius should be updated");
    }

    [Fact]
    public async Task T101_UpdateGeofenceAsync_LocationChanges_UpdatesCenter()
    {
        // Arrange
        // Test updating geofence center location
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);

        var geofence = new GeofenceRegion
        {
            Id = "move-test",
            Name = "Move Test",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 100,
            IsActive = true
        };

        await geofenceService.AddGeofenceAsync(geofence);

        // Act
        // Move geofence 1km north
        geofence.Latitude = 25.0420;
        geofence.Longitude = 121.5650;
        await geofenceService.UpdateGeofenceAsync(geofence.Id, geofence);

        // Assert
        var monitoredGeofences = geofenceService.GetMonitoredGeofences();
        var updatedGeofence = monitoredGeofences.First(g => g.Id == "move-test");
        updatedGeofence.Latitude.Should().BeApproximately(25.0420, 0.0001);
        updatedGeofence.Longitude.Should().BeApproximately(121.5650, 0.0001);
    }

    [Fact]
    public async Task T101_UpdateGeofenceAsync_NameAndMetadata_UpdatesProperties()
    {
        // Arrange
        // Test updating geofence name, category, and description
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);

        var geofence = new GeofenceRegion
        {
            Id = "metadata-test",
            Name = "Original Name",
            Category = "Office",
            Description = "Original description",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 100,
            IsActive = true
        };

        await geofenceService.AddGeofenceAsync(geofence);

        // Act
        geofence.Name = "Updated Name";
        geofence.Category = "Client Site";
        geofence.Description = "Updated description with more details";
        await geofenceService.UpdateGeofenceAsync(geofence.Id, geofence);

        // Assert
        var updatedGeofence = geofenceService.GetMonitoredGeofences()
            .First(g => g.Id == "metadata-test");
        updatedGeofence.Name.Should().Be("Updated Name");
        updatedGeofence.Category.Should().Be("Client Site");
        updatedGeofence.Description.Should().Be("Updated description with more details");
    }

    [Fact]
    public async Task T102_ToggleGeofenceActiveAsync_EnablesAndDisables()
    {
        // Arrange
        // Test toggling geofence active/inactive state
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);

        var geofence = new GeofenceRegion
        {
            Id = "toggle-test",
            Name = "Toggle Test",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 100,
            IsActive = true
        };

        await geofenceService.AddGeofenceAsync(geofence);

        var testLocation = new MauiLocation(25.0335, 121.5654); // Inside geofence

        // Act & Assert - Initially active
        var isInsideActive = geofenceService.CheckPointInGeofence(testLocation, geofence);
        isInsideActive.Should().BeTrue("point is inside active geofence");

        // Disable geofence
        geofence.IsActive = false;
        await geofenceService.UpdateGeofenceAsync(geofence.Id, geofence);

        var isInsideInactive = geofenceService.CheckPointInGeofence(testLocation, geofence);
        isInsideInactive.Should().BeFalse("inactive geofence should return false");

        // Re-enable geofence
        geofence.IsActive = true;
        await geofenceService.UpdateGeofenceAsync(geofence.Id, geofence);

        var isInsideReactivated = geofenceService.CheckPointInGeofence(testLocation, geofence);
        isInsideReactivated.Should().BeTrue("reactivated geofence should work again");
    }

    [Fact]
    public async Task T102_ToggleGeofenceActive_AffectsMonitoring()
    {
        // Arrange
        // Test that inactive geofences are not monitored
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);

        var activeGeofence = new GeofenceRegion
        {
            Id = "active-1",
            Name = "Active",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 100,
            IsActive = true
        };

        var inactiveGeofence = new GeofenceRegion
        {
            Id = "inactive-1",
            Name = "Inactive",
            Latitude = 25.0420,
            Longitude = 121.5650,
            RadiusMeters = 100,
            IsActive = false
        };

        await geofenceService.AddGeofenceAsync(activeGeofence);
        await geofenceService.AddGeofenceAsync(inactiveGeofence);

        // Act
        var allGeofences = geofenceService.GetMonitoredGeofences();
        var activeOnly = allGeofences.Where(g => g.IsActive).ToList();

        // Assert
        allGeofences.Should().HaveCount(2, "both geofences should be stored");
        activeOnly.Should().HaveCount(1, "only active geofence should be monitored");
        activeOnly.First().Id.Should().Be("active-1");
    }
}
