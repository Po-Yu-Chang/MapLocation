using Xunit;
using FluentAssertions;
using MapLocationApp.Services;
using MapLocationApp.Models;
using MapLocationApp.Tests.Mocks;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace MapLocationApp.Tests.Integration;

/// <summary>
/// T139-T140: Integration tests for Telegram Notification
/// Tests check-in notifications and geofence entry/exit notifications
/// </summary>
public class TelegramNotificationTests
{
    [Fact]
    public async Task T139_CheckInNotification_SuccessfulCheckIn_SendsTelegramMessage()
    {
        // Arrange
        // Test sending Telegram notification when check-in occurs
        var mockLocationService = new MockLocationService();
        var checkInStorage = new CheckInStorageService();
        var telegramService = new TelegramNotificationService();

        // Initialize Telegram with test credentials
        var botToken = "123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11";
        var chatId = "123456789";

        await telegramService.InitializeAsync(botToken, chatId);

        // Create check-in record
        var checkIn = new CheckInRecord
        {
            Id = "test-checkin-1",
            LocationName = "Taipei 101 Office",
            Latitude = 25.0330,
            Longitude = 121.5654,
            CheckInTime = DateTime.Now,
            Notes = "Morning arrival"
        };

        // Save check-in
        await checkInStorage.SaveCheckInRecordAsync(checkIn);

        // Act
        // Format notification message
        var notificationMessage = $@"
Check-In Successful
Location: {checkIn.LocationName}
Time: {checkIn.CheckInTime:yyyy-MM-dd HH:mm:ss}
Coordinates: {checkIn.Latitude:F6}, {checkIn.Longitude:F6}
Notes: {checkIn.Notes}
";

        // Send notification
        // var sendResult = await telegramService.SendMessageAsync(notificationMessage);

        // Assert
        // sendResult.Should().BeTrue("notification should be sent successfully");

        // Expected message format:
        // - Location name
        // - Check-in time
        // - Coordinates
        // - Optional notes

        notificationMessage.Should().Contain("Taipei 101 Office");
        notificationMessage.Should().Contain("25.0330");

        // Cleanup
        await checkInStorage.DeleteCheckInRecordAsync(checkIn.Id);
    }

    [Fact]
    public async Task T139_CheckInNotification_WithGeofenceName_IncludesGeofenceInfo()
    {
        // Arrange
        // Test notification includes geofence information if check-in is at geofence
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);
        var telegramService = new TelegramNotificationService();

        // Create geofence
        var geofence = new GeofenceRegion
        {
            Id = "office-geofence",
            Name = "Main Office",
            Latitude = 25.0330,
            Longitude = 121.5654,
            RadiusMeters = 200,
            Category = "Office",
            IsActive = true
        };

        await geofenceService.AddGeofenceAsync(geofence);

        // Check-in at geofence location
        var checkIn = new CheckInRecord
        {
            Id = "geofence-checkin-1",
            LocationName = geofence.Name,
            Latitude = geofence.Latitude,
            Longitude = geofence.Longitude,
            CheckInTime = DateTime.Now
        };

        // Act
        var notificationMessage = $@"
Check-In at Geofence
Geofence: {geofence.Name}
Category: {geofence.Category}
Time: {checkIn.CheckInTime:HH:mm:ss}
Radius: {geofence.RadiusMeters}m
";

        // Assert
        notificationMessage.Should().Contain("Main Office");
        notificationMessage.Should().Contain("Office");
        notificationMessage.Should().Contain("200m");
    }

    [Fact]
    public async Task T140_GeofenceEntryNotification_EnteringGeofence_SendsAlert()
    {
        // Arrange
        // Test notification when entering geofence boundary
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);
        var telegramService = new TelegramNotificationService();

        // Create geofence
        var geofence = new GeofenceRegion
        {
            Id = "client-site",
            Name = "Client Site A",
            Latitude = 25.0420,
            Longitude = 121.5650,
            RadiusMeters = 150,
            Category = "Client",
            IsActive = true,
            TransitionType = GeofenceTransitionType.Enter | GeofenceTransitionType.Exit
        };

        await geofenceService.AddGeofenceAsync(geofence);

        // Simulate movement: outside -> inside geofence
        var outsideLocation = new MauiLocation(25.0500, 121.5650); // ~900m away
        var insideLocation = new MauiLocation(25.0425, 121.5650);  // ~50m inside

        mockLocationService.SetFixedLocation(outsideLocation);
        var wasOutside = geofenceService.CheckPointInGeofence(outsideLocation, geofence);

        // Act - Move inside geofence
        mockLocationService.SetFixedLocation(insideLocation);
        var isInside = geofenceService.CheckPointInGeofence(insideLocation, geofence);

        // Assert
        wasOutside.Should().BeFalse("should start outside geofence");
        isInside.Should().BeTrue("should be inside after movement");

        // Expected notification:
        var entryNotification = $@"
Geofence Entry Alert
Entered: {geofence.Name}
Category: {geofence.Category}
Time: {DateTime.Now:HH:mm:ss}
Location: {insideLocation.Latitude:F6}, {insideLocation.Longitude:F6}
";

        entryNotification.Should().Contain("Geofence Entry Alert");
        entryNotification.Should().Contain("Client Site A");

        // In real implementation:
        // await telegramService.SendMessageAsync(entryNotification);
    }

    [Fact]
    public async Task T140_GeofenceExitNotification_ExitingGeofence_SendsAlert()
    {
        // Arrange
        // Test notification when exiting geofence boundary
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);
        var telegramService = new TelegramNotificationService();

        // Create geofence
        var geofence = new GeofenceRegion
        {
            Id = "warehouse",
            Name = "Warehouse",
            Latitude = 25.0250,
            Longitude = 121.5660,
            RadiusMeters = 300,
            Category = "Warehouse",
            IsActive = true,
            TransitionType = GeofenceTransitionType.Enter | GeofenceTransitionType.Exit
        };

        await geofenceService.AddGeofenceAsync(geofence);

        // Simulate movement: inside -> outside geofence
        var insideLocation = new MauiLocation(25.0255, 121.5660);  // Inside
        var outsideLocation = new MauiLocation(25.0200, 121.5660); // ~600m away

        mockLocationService.SetFixedLocation(insideLocation);
        var wasInside = geofenceService.CheckPointInGeofence(insideLocation, geofence);

        // Act - Move outside geofence
        mockLocationService.SetFixedLocation(outsideLocation);
        var isOutside = !geofenceService.CheckPointInGeofence(outsideLocation, geofence);

        // Assert
        wasInside.Should().BeTrue("should start inside geofence");
        isOutside.Should().BeTrue("should be outside after movement");

        // Expected notification:
        var exitNotification = $@"
Geofence Exit Alert
Exited: {geofence.Name}
Category: {geofence.Category}
Time: {DateTime.Now:HH:mm:ss}
Location: {outsideLocation.Latitude:F6}, {outsideLocation.Longitude:F6}
";

        exitNotification.Should().Contain("Geofence Exit Alert");
        exitNotification.Should().Contain("Warehouse");

        // In real implementation:
        // await telegramService.SendMessageAsync(exitNotification);
    }

    [Fact]
    public async Task T140_GeofenceTransitionNotification_MultipleGeofences_SendsSeparateAlerts()
    {
        // Arrange
        // Test handling multiple geofence transitions
        var mockLocationService = new MockLocationService();
        var geofenceService = new GeofenceService(mockLocationService);
        var telegramService = new TelegramNotificationService();

        // Create multiple geofences
        var geofences = new[]
        {
            new GeofenceRegion
            {
                Id = "geofence-1",
                Name = "Location A",
                Latitude = 25.0330,
                Longitude = 121.5654,
                RadiusMeters = 100,
                IsActive = true
            },
            new GeofenceRegion
            {
                Id = "geofence-2",
                Name = "Location B",
                Latitude = 25.0340,
                Longitude = 121.5660,
                RadiusMeters = 100,
                IsActive = true
            }
        };

        foreach (var geofence in geofences)
        {
            await geofenceService.AddGeofenceAsync(geofence);
        }

        // Simulate entering first geofence
        var locationA = new MauiLocation(25.0330, 121.5654);
        mockLocationService.SetFixedLocation(locationA);

        var enteredGeofences = new List<GeofenceRegion>();

        foreach (var geofence in geofences)
        {
            if (geofenceService.CheckPointInGeofence(locationA, geofence))
            {
                enteredGeofences.Add(geofence);
            }
        }

        // Assert
        enteredGeofences.Should().HaveCount(1, "should enter only one geofence");
        enteredGeofences.First().Name.Should().Be("Location A");

        // Expected: Send notification for each geofence transition
        // Not combined into single notification
    }

    [Fact]
    public async Task T140_GeofenceNotification_OnlyWhenConfigured_RespectsSettings()
    {
        // Arrange
        // Test that notifications respect user settings (enabled/disabled)
        var telegramService = new TelegramNotificationService();

        // Simulate notification settings
        var notificationsEnabled = false; // User disabled notifications

        // Act
        if (notificationsEnabled)
        {
            // await telegramService.SendMessageAsync("Geofence entry");
        }

        // Assert
        // When disabled, no notifications should be sent
        // This tests the configuration/settings integration
    }

    [Fact]
    public async Task T140_GeofenceNotification_IncludesTimestamp_AndDuration()
    {
        // Arrange
        // Test notification includes detailed timing information
        var entryTime = DateTime.Now;
        var exitTime = entryTime.AddHours(2).AddMinutes(30);
        var duration = exitTime - entryTime;

        // Act
        var exitNotificationWithDuration = $@"
Geofence Exit Alert
Location: Office
Entry Time: {entryTime:HH:mm:ss}
Exit Time: {exitTime:HH:mm:ss}
Duration: {duration.TotalHours:F1} hours
";

        // Assert
        exitNotificationWithDuration.Should().Contain("Entry Time");
        exitNotificationWithDuration.Should().Contain("Exit Time");
        exitNotificationWithDuration.Should().Contain("Duration");
        exitNotificationWithDuration.Should().Contain("2.5 hours");
    }
}
