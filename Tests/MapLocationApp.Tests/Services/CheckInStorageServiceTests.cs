using Xunit;
using FluentAssertions;
using MapLocationApp.Services;
using MapLocationApp.Models;
using MapLocationApp.Tests.Mocks;
using Microsoft.Maui.Devices.Sensors;

namespace MapLocationApp.Tests.Services;

/// <summary>
/// T026-T027: Unit tests for CheckInStorageService
/// Tests online check-in creation and offline queueing functionality
/// </summary>
public class CheckInStorageServiceTests
{
    [Fact]
    public async Task T026_CreateCheckInAsync_OnlineMode_SavesToDatabase()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        var checkInService = new CheckInStorageService(mockDb);

        var checkInRecord = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Main Office",
            CheckInTime = DateTime.UtcNow,
            Latitude = 25.0330,
            Longitude = 121.5654,
            Notes = "Arrived for work",
            Type = CheckInType.Automatic
        };

        // Act
        var result = await checkInService.CreateCheckInAsync(checkInRecord, isOnline: true);

        // Assert
        result.Should().BeTrue("check-in should be saved successfully");

        // Verify saved to database
        var savedRecords = await mockDb.GetCheckInRecordsAsync(1);
        savedRecords.Should().HaveCount(1);
        savedRecords[0].GeofenceName.Should().Be("Main Office");
    }

    [Fact]
    public async Task T026_CreateCheckInAsync_WithXSSNotes_SanitizesInput()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        var checkInService = new CheckInStorageService(mockDb);

        var checkInRecord = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Office",
            CheckInTime = DateTime.UtcNow,
            Latitude = 25.0330,
            Longitude = 121.5654,
            Notes = "<script>alert('XSS')</script>Normal text",  // XSS attempt
            Type = CheckInType.Manual
        };

        // Act
        await checkInService.CreateCheckInAsync(checkInRecord, isOnline: true);

        // Assert
        var savedRecords = await mockDb.GetCheckInRecordsAsync(1);
        savedRecords[0].Notes.Should().NotContain("<script>");
        savedRecords[0].Notes.Should().Be("Normal text", "XSS tags should be removed by T013 sanitization");
    }

    [Fact]
    public async Task T026_CreateCheckInAsync_DatabaseError_ReturnsFalse()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        mockDb.SimulateConnectionFailure();  // Simulate database error

        var checkInService = new CheckInStorageService(mockDb);
        var checkInRecord = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Office",
            CheckInTime = DateTime.UtcNow,
            Latitude = 25.0330,
            Longitude = 121.5654,
            Type = CheckInType.Automatic
        };

        // Act
        var result = await checkInService.CreateCheckInAsync(checkInRecord, isOnline: true);

        // Assert
        result.Should().BeFalse("database errors should be handled gracefully");
    }

    [Fact]
    public async Task T027_CreateCheckInAsync_OfflineMode_QueuesForSync()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        var checkInService = new CheckInStorageService(mockDb);

        var checkInRecord = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Office",
            CheckInTime = DateTime.UtcNow,
            Latitude = 25.0330,
            Longitude = 121.5654,
            Notes = "Offline check-in",
            Type = CheckInType.Automatic
        };

        // Act
        var result = await checkInService.CreateCheckInAsync(checkInRecord, isOnline: false);

        // Assert
        result.Should().BeTrue("offline check-ins should be queued");

        // Verify queued for sync
        var pendingSyncRecords = await checkInService.GetPendingSyncRecordsAsync();
        pendingSyncRecords.Should().HaveCount(1);
        pendingSyncRecords[0].Notes.Should().Be("Offline check-in");
    }

    [Fact]
    public async Task T027_GetPendingSyncRecordsAsync_MultipleOfflineCheckins_ReturnsAll()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        var checkInService = new CheckInStorageService(mockDb);

        var checkIn1 = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Office 1",
            CheckInTime = DateTime.UtcNow.AddHours(-2),
            Latitude = 25.0330,
            Longitude = 121.5654,
            Type = CheckInType.Automatic
        };

        var checkIn2 = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "1",
            GeofenceId = "office-2",
            GeofenceName = "Office 2",
            CheckInTime = DateTime.UtcNow.AddHours(-1),
            Latitude = 25.0340,
            Longitude = 121.5660,
            Type = CheckInType.Automatic
        };

        // Act
        await checkInService.CreateCheckInAsync(checkIn1, isOnline: false);
        await checkInService.CreateCheckInAsync(checkIn2, isOnline: false);

        var pendingRecords = await checkInService.GetPendingSyncRecordsAsync();

        // Assert
        pendingRecords.Should().HaveCount(2);
        pendingRecords.Should().Contain(r => r.GeofenceName == "Office 1");
        pendingRecords.Should().Contain(r => r.GeofenceName == "Office 2");
    }

    [Fact]
    public async Task T027_SyncPendingRecordsAsync_OnlineRestored_SyncsToDatabase()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        var checkInService = new CheckInStorageService(mockDb);

        // Create offline check-ins
        var checkIn1 = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Office",
            CheckInTime = DateTime.UtcNow,
            Latitude = 25.0330,
            Longitude = 121.5654,
            Type = CheckInType.Automatic
        };

        await checkInService.CreateCheckInAsync(checkIn1, isOnline: false);

        // Act - Restore connection and sync
        mockDb.RestoreConnection();
        var syncResult = await checkInService.SyncPendingRecordsAsync();

        // Assert
        syncResult.Should().BeTrue("sync should succeed when online");

        var pendingRecords = await checkInService.GetPendingSyncRecordsAsync();
        pendingRecords.Should().BeEmpty("all records should be synced");

        var syncedRecords = await mockDb.GetCheckInRecordsAsync(1);
        syncedRecords.Should().HaveCount(1, "record should be in database after sync");
    }

    [Fact]
    public async Task T027_SyncPendingRecordsAsync_StillOffline_RetainsPendingRecords()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        mockDb.SimulateConnectionFailure();  // Still offline

        var checkInService = new CheckInStorageService(mockDb);

        var checkIn = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Office",
            CheckInTime = DateTime.UtcNow,
            Latitude = 25.0330,
            Longitude = 121.5654,
            Type = CheckInType.Automatic
        };

        await checkInService.CreateCheckInAsync(checkIn, isOnline: false);

        // Act
        var syncResult = await checkInService.SyncPendingRecordsAsync();

        // Assert
        syncResult.Should().BeFalse("sync should fail when offline");

        var pendingRecords = await checkInService.GetPendingSyncRecordsAsync();
        pendingRecords.Should().HaveCount(1, "records should remain queued when sync fails");
    }
}
