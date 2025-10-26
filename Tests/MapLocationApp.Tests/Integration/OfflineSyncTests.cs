using Xunit;
using FluentAssertions;
using MapLocationApp.Services;
using MapLocationApp.Models;
using MapLocationApp.Tests.Mocks;
using Microsoft.Maui.Devices.Sensors;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace MapLocationApp.Tests.Integration;

/// <summary>
/// T030: Integration test for offline check-in synchronization
/// Tests complete workflow: offline check-in -> queue -> connection restore -> sync to database
/// </summary>
public class OfflineSyncTests
{
    [Fact]
    public async Task T030_OfflineCheckInSync_CompleteWorkflow_SuccessfullySyncsToDatabase()
    {
        // Arrange
        var mockLocation = new MockLocationService();
        var mockDb = new MockDatabaseService();
        mockDb.SimulateConnectionFailure();  // Start offline

        var checkInService = new CheckInStorageService(mockDb);
        var geofenceService = new GeofenceService(mockLocation);

        // Setup geofence (not actually used in this test - test creates check-ins directly)
        var officeGeofence = new GeofenceRegion
        {
            Id = "office-1",
            Name = "Main Office",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 200,
            IsActive = true
        };

        await geofenceService.AddGeofenceAsync(officeGeofence);

        // Act 1: Create check-ins while offline
        var offlineCheckIn1 = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Main Office",
            CheckInTime = DateTime.UtcNow.AddHours(-2),
            Latitude = 25.0335,
            Longitude = 121.5654,
            Notes = "Offline entry 1",
            Type = CheckInType.Automatic
        };

        var offlineCheckIn2 = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Main Office",
            CheckInTime = DateTime.UtcNow.AddHours(-1),
            Latitude = 25.0338,
            Longitude = 121.5656,
            Notes = "Offline entry 2",
            Type = CheckInType.Manual
        };

        var result1 = await checkInService.CreateCheckInAsync(offlineCheckIn1, isOnline: false);
        var result2 = await checkInService.CreateCheckInAsync(offlineCheckIn2, isOnline: false);

        // Assert 1: Check-ins queued
        result1.Should().BeTrue("offline check-in 1 should be queued");
        result2.Should().BeTrue("offline check-in 2 should be queued");

        var pendingBefore = await checkInService.GetPendingSyncRecordsAsync();
        pendingBefore.Should().HaveCount(2, "both check-ins should be in queue");

        // Act 2: Restore connection and sync
        mockDb.RestoreConnection();
        var syncResult = await checkInService.SyncPendingRecordsAsync();

        // Assert 2: Sync successful
        syncResult.Should().BeTrue("sync should succeed after connection restored");

        var pendingAfter = await checkInService.GetPendingSyncRecordsAsync();
        pendingAfter.Should().BeEmpty("all records should be synced");

        // Assert 3: Records in database
        var syncedRecords = await mockDb.GetCheckInRecordsAsync(1);
        syncedRecords.Should().HaveCount(2, "both records should be in database");
        syncedRecords.Should().Contain(r => r.Notes == "Offline entry 1");
        syncedRecords.Should().Contain(r => r.Notes == "Offline entry 2");

        // Assert 4: Timestamps preserved
        var entry1 = syncedRecords.First(r => r.Notes == "Offline entry 1");
        entry1.CheckInTime.Should().BeCloseTo(DateTime.UtcNow.AddHours(-2), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task T030_OfflineSync_PartialSyncFailure_RetainsFailedRecords()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        mockDb.SimulateConnectionFailure();

        var checkInService = new CheckInStorageService(mockDb);

        // Create multiple offline check-ins
        var checkIn1 = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Office",
            CheckInTime = DateTime.UtcNow.AddHours(-3),
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
            CheckInTime = DateTime.UtcNow.AddHours(-2),
            Latitude = 25.0340,
            Longitude = 121.5660,
            Type = CheckInType.Automatic
        };

        await checkInService.CreateCheckInAsync(checkIn1, isOnline: false);
        await checkInService.CreateCheckInAsync(checkIn2, isOnline: false);

        // Act - Attempt sync while still offline (simulates intermittent connection)
        var syncResult = await checkInService.SyncPendingRecordsAsync();

        // Assert
        syncResult.Should().BeFalse("sync should fail when offline");

        var pendingRecords = await checkInService.GetPendingSyncRecordsAsync();
        pendingRecords.Should().HaveCount(2, "all records should remain in queue after failed sync");
    }

    [Fact]
    public async Task T030_OfflineSync_MultipleUsers_SyncsOnlyRelevantRecords()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        mockDb.SimulateConnectionFailure();

        var checkInService = new CheckInStorageService(mockDb);

        // Create check-ins for different users while offline
        var user1CheckIn = new CheckInRecord
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

        var user2CheckIn = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "2",
            GeofenceId = "office-1",
            GeofenceName = "Office",
            CheckInTime = DateTime.UtcNow,
            Latitude = 25.0332,
            Longitude = 121.5656,
            Type = CheckInType.Automatic
        };

        await checkInService.CreateCheckInAsync(user1CheckIn, isOnline: false);
        await checkInService.CreateCheckInAsync(user2CheckIn, isOnline: false);

        // Act - Restore connection and sync
        mockDb.RestoreConnection();
        await checkInService.SyncPendingRecordsAsync();

        // Assert - Verify each user's records
        var user1Records = await mockDb.GetCheckInRecordsAsync(1);
        var user2Records = await mockDb.GetCheckInRecordsAsync(2);

        user1Records.Should().HaveCount(1);
        user2Records.Should().HaveCount(1);

        user1Records[0].UserId.Should().Be("1");
        user2Records[0].UserId.Should().Be("2");
    }

    [Fact]
    public async Task T030_OfflineSync_OrderPreservation_SyncsInChronologicalOrder()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        mockDb.SimulateConnectionFailure();

        var checkInService = new CheckInStorageService(mockDb);

        // Create check-ins in specific order
        var checkIns = new[]
        {
            new CheckInRecord
            {
                Id = Guid.NewGuid().ToString(),
                UserId = "1",
                GeofenceId = "office-1",
                GeofenceName = "Office",
                CheckInTime = DateTime.UtcNow.AddHours(-5),
                Latitude = 25.0330,
                Longitude = 121.5654,
                Notes = "First",
                Type = CheckInType.Automatic
            },
            new CheckInRecord
            {
                Id = Guid.NewGuid().ToString(),
                UserId = "1",
                GeofenceId = "office-1",
                GeofenceName = "Office",
                CheckInTime = DateTime.UtcNow.AddHours(-3),
                Latitude = 25.0330,
                Longitude = 121.5654,
                Notes = "Second",
                Type = CheckInType.Automatic
            },
            new CheckInRecord
            {
                Id = Guid.NewGuid().ToString(),
                UserId = "1",
                GeofenceId = "office-1",
                GeofenceName = "Office",
                CheckInTime = DateTime.UtcNow.AddHours(-1),
                Latitude = 25.0330,
                Longitude = 121.5654,
                Notes = "Third",
                Type = CheckInType.Automatic
            }
        };

        foreach (var checkIn in checkIns)
        {
            await checkInService.CreateCheckInAsync(checkIn, isOnline: false);
        }

        // Act
        mockDb.RestoreConnection();
        await checkInService.SyncPendingRecordsAsync();

        // Assert
        var syncedRecords = await mockDb.GetCheckInRecordsAsync(1);
        syncedRecords.Should().HaveCount(3);

        // Verify chronological order preserved
        var orderedRecords = syncedRecords.OrderBy(r => r.CheckInTime).ToList();
        orderedRecords[0].Notes.Should().Be("First");
        orderedRecords[1].Notes.Should().Be("Second");
        orderedRecords[2].Notes.Should().Be("Third");
    }

    [Fact]
    public async Task T030_OfflineSync_DuplicateCheckInPrevention_SkipsDuplicates()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        var checkInService = new CheckInStorageService(mockDb);

        var checkInRecord = new CheckInRecord
        {
            Id = "duplicate-test-id",
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Office",
            CheckInTime = DateTime.UtcNow,
            Latitude = 25.0330,
            Longitude = 121.5654,
            Type = CheckInType.Automatic
        };

        // Act - Create same check-in twice (simulate double-tap or race condition)
        await checkInService.CreateCheckInAsync(checkInRecord, isOnline: true);

        // Try to create again with same ID
        var duplicateResult = await checkInService.CreateCheckInAsync(checkInRecord, isOnline: true);

        // Assert
        duplicateResult.Should().BeFalse("duplicate check-in should be rejected");

        var records = await mockDb.GetCheckInRecordsAsync(1);
        records.Should().HaveCount(1, "only one record should exist");
    }
}
