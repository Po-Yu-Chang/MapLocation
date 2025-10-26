using Xunit;
using FluentAssertions;
using MapLocationApp.Services;
using MapLocationApp.Models;
using MapLocationApp.Tests.Mocks;
using Microsoft.Maui.Devices.Sensors;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace MapLocationApp.Tests.Integration;

/// <summary>
/// T028-T029: Integration tests for complete check-in workflows
/// Tests automatic geofence entry/exit and manual check-out with notes
/// </summary>
public class CheckInFlowTests
{
    [Fact]
    public async Task T028_AutomaticCheckIn_UserEntersGeofence_CreatesCheckInRecord()
    {
        // Arrange
        var mockLocation = new MockLocationService();
        var mockDb = new MockDatabaseService();
        var geofenceService = new GeofenceService(mockLocation);
        var checkInService = new CheckInStorageService(mockDb);

        // T028: Wire up integration - subscribe to GeofenceEntered event to create automatic check-ins
        geofenceService.GeofenceEntered += async (sender, geofenceEvent) =>
        {
            var checkInRecord = new CheckInRecord
            {
                Id = Guid.NewGuid().ToString(),
                UserId = "1",
                GeofenceId = geofenceEvent.GeofenceId,
                GeofenceName = geofenceEvent.GeofenceName,
                CheckInTime = geofenceEvent.Timestamp,
                Latitude = geofenceEvent.Latitude,
                Longitude = geofenceEvent.Longitude,
                Type = CheckInType.Automatic
            };
            await checkInService.CreateCheckInAsync(checkInRecord, isOnline: true);
        };

        // Setup geofence
        var officeGeofence = new GeofenceRegion
        {
            Id = "main-office",
            Name = "Main Office",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 200,
            IsActive = true,
            TransitionType = GeofenceTransitionType.Enter
        };

        await geofenceService.AddGeofenceAsync(officeGeofence);
        await geofenceService.StartMonitoringAsync();

        // Simulate user starting outside geofence
        var outsideLocation = new MauiLocation(25.0400, 121.5654);  // 800m away
        mockLocation.SetFixedLocation(outsideLocation);

        // Act - User moves inside geofence
        var insideLocation = new MauiLocation(25.0335, 121.5654);  // 50m from center
        mockLocation.SimulateLocationUpdate(insideLocation);

        // Simulate geofence transition event
        await geofenceService.HandleLocationUpdate(insideLocation);

        // Give async event handler time to complete
        await Task.Delay(100);

        // Check if automatic check-in was triggered
        var userId = 1;
        var latestCheckIn = await mockDb.GetLatestCheckInAsync(userId);

        // Assert
        latestCheckIn.Should().NotBeNull("automatic check-in should be created");
        latestCheckIn!.GeofenceId.Should().Be("main-office");
        latestCheckIn.Type.Should().Be(CheckInType.Automatic);
        latestCheckIn.Latitude.Should().BeApproximately(25.0335, 0.0001);
        latestCheckIn.Longitude.Should().BeApproximately(121.5654, 0.0001);
        latestCheckIn.CheckInTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task T028_AutomaticCheckIn_MultipleGeofences_TriggersCorrectOne()
    {
        // Arrange
        var mockLocation = new MockLocationService();
        var mockDb = new MockDatabaseService();
        var geofenceService = new GeofenceService(mockLocation);
        var checkInService = new CheckInStorageService(mockDb);

        // T028: Wire up integration - subscribe to GeofenceEntered event to create automatic check-ins
        geofenceService.GeofenceEntered += async (sender, geofenceEvent) =>
        {
            var checkInRecord = new CheckInRecord
            {
                Id = Guid.NewGuid().ToString(),
                UserId = "1",
                GeofenceId = geofenceEvent.GeofenceId,
                GeofenceName = geofenceEvent.GeofenceName,
                CheckInTime = geofenceEvent.Timestamp,
                Latitude = geofenceEvent.Latitude,
                Longitude = geofenceEvent.Longitude,
                Type = CheckInType.Automatic
            };
            await checkInService.CreateCheckInAsync(checkInRecord, isOnline: true);
        };

        // Setup multiple geofences
        var office1 = new GeofenceRegion
        {
            Id = "office-1",
            Name = "Office 1",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 100,
            IsActive = true,
            TransitionType = GeofenceTransitionType.Enter
        };

        var office2 = new GeofenceRegion
        {
            Id = "office-2",
            Name = "Office 2",
            Latitude = 25.0350,  // 200m away from office-1
            Longitude = 121.5654,
            RadiusMeters = 100,
            IsActive = true,
            TransitionType = GeofenceTransitionType.Enter
        };

        await geofenceService.AddGeofenceAsync(office1);
        await geofenceService.AddGeofenceAsync(office2);
        await geofenceService.StartMonitoringAsync();

        // Act - Enter office-2
        var locationNearOffice2 = new MauiLocation(25.0352, 121.5654);
        await geofenceService.HandleLocationUpdate(locationNearOffice2);

        // Give async event handler time to complete
        await Task.Delay(100);

        var userId = 1;
        var latestCheckIn = await mockDb.GetLatestCheckInAsync(userId);

        // Assert
        latestCheckIn.Should().NotBeNull();
        latestCheckIn!.GeofenceId.Should().Be("office-2", "should trigger the closest geofence");
        latestCheckIn.GeofenceName.Should().Be("Office 2");
    }

    [Fact]
    public async Task T029_ManualCheckOut_WithNotes_UpdatesCheckInRecord()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        var checkInService = new CheckInStorageService(mockDb);

        // Create initial check-in
        var checkInRecord = new CheckInRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Main Office",
            CheckInTime = DateTime.UtcNow.AddHours(-8),  // 8 hours ago
            Latitude = 25.0330,
            Longitude = 121.5654,
            Type = CheckInType.Automatic
        };

        await checkInService.CreateCheckInAsync(checkInRecord, isOnline: true);

        // Act - Manual check-out with notes
        checkInRecord.CheckOutTime = DateTime.UtcNow;
        checkInRecord.Notes = "Completed daily tasks. Meeting with client at 3 PM.";

        var updateResult = await checkInService.UpdateCheckInAsync(checkInRecord);

        // Assert
        updateResult.Should().BeTrue("check-out should be recorded");

        var updatedRecord = await mockDb.GetLatestCheckInAsync(1);
        updatedRecord.Should().NotBeNull();
        updatedRecord!.CheckOutTime.Should().NotBeNull();
        updatedRecord.Notes.Should().Contain("Completed daily tasks");
        updatedRecord.Notes.Should().Contain("Meeting with client");

        // Verify work duration
        var workDuration = updatedRecord.CheckOutTime!.Value - updatedRecord.CheckInTime;
        workDuration.TotalHours.Should().BeApproximately(8, 0.1);
    }

    [Fact]
    public async Task T029_ManualCheckOut_WithXSSInNotes_SanitizesInput()
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
            CheckInTime = DateTime.UtcNow.AddHours(-1),
            Latitude = 25.0330,
            Longitude = 121.5654,
            Type = CheckInType.Manual
        };

        await checkInService.CreateCheckInAsync(checkInRecord, isOnline: true);

        // Act - Try to inject XSS in notes
        checkInRecord.CheckOutTime = DateTime.UtcNow;
        checkInRecord.Notes = "<script>alert('XSS')</script><img src=x onerror=alert('XSS')>Normal text here";

        await checkInService.UpdateCheckInAsync(checkInRecord);

        // Assert
        var updatedRecord = await mockDb.GetLatestCheckInAsync(1);
        updatedRecord!.Notes.Should().NotContain("<script>");
        updatedRecord.Notes.Should().NotContain("<img");
        updatedRecord.Notes.Should().NotContain("onerror");
        updatedRecord.Notes.Should().Be("Normal text here", "T013 XSS sanitization should remove all dangerous content");
    }

    [Fact]
    public async Task T029_ManualCheckOut_WithoutCheckIn_ThrowsException()
    {
        // Arrange
        var mockDb = new MockDatabaseService();
        var checkInService = new CheckInStorageService(mockDb);

        var checkOutRecord = new CheckInRecord
        {
            Id = "nonexistent-id",
            UserId = "1",
            GeofenceId = "office-1",
            GeofenceName = "Office",
            CheckInTime = DateTime.UtcNow.AddHours(-1),
            CheckOutTime = DateTime.UtcNow,
            Latitude = 25.0330,
            Longitude = 121.5654,
            Type = CheckInType.Manual
        };

        // Act & Assert
        var result = await checkInService.UpdateCheckInAsync(checkOutRecord);
        result.Should().BeFalse("cannot check-out without existing check-in");
    }

    [Fact]
    public async Task T029_ManualCheckOut_ExceedingMaxNotesLength_TruncatesTo500Chars()
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
            CheckInTime = DateTime.UtcNow.AddHours(-1),
            Latitude = 25.0330,
            Longitude = 121.5654,
            Type = CheckInType.Manual
        };

        await checkInService.CreateCheckInAsync(checkInRecord, isOnline: true);

        // Act - Provide notes exceeding 500 characters
        var longNotes = new string('A', 600);  // 600 characters
        checkInRecord.CheckOutTime = DateTime.UtcNow;
        checkInRecord.Notes = longNotes;

        await checkInService.UpdateCheckInAsync(checkInRecord);

        // Assert
        var updatedRecord = await mockDb.GetLatestCheckInAsync(1);
        updatedRecord!.Notes.Length.Should().BeLessOrEqualTo(500, "T013 sanitization limits to 500 chars");
    }
}
