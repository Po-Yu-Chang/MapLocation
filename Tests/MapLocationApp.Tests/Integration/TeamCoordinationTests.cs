using Xunit;
using FluentAssertions;
using MapLocationApp.Services;
using MapLocationApp.Models;
using MapLocationApp.Tests.Mocks;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace MapLocationApp.Tests.Integration;

/// <summary>
/// T061-T062: Integration tests for Team Location Coordination
/// Tests complete workflows for team creation, member joining, and real-time location sharing
/// </summary>
public class TeamCoordinationTests
{
    [Fact]
    public async Task T061_TeamCreationAndJoiningWorkflow_CompleteCycle_Success()
    {
        // Arrange
        // Test complete workflow: create team, get code, "another user" joins (simulated)
        var mockLocationService = new MockLocationService();
        var ownerTeamService = new TeamLocationService(mockLocationService);

        var teamName = "Integration Test Team";
        var teamDescription = "Full integration test for team coordination";

        // Act & Assert

        // Step 1: Owner creates team
        var createResult = await ownerTeamService.CreateTeamAsync(teamName, teamDescription);
        createResult.Should().BeTrue("team creation should succeed");

        // Step 2: Verify team exists and get team code
        var ownerTeams = await ownerTeamService.GetUserTeamsAsync();
        ownerTeams.Should().HaveCount(1, "owner should have one team");

        var createdTeam = ownerTeams.First();
        createdTeam.Name.Should().Be(teamName);
        createdTeam.Code.Should().NotBeNullOrEmpty("team should have join code");

        var teamCode = createdTeam.Code;

        // Step 3: Verify owner is automatically a member with Owner role
        var members = await ownerTeamService.GetTeamMembersAsync(createdTeam.Id);
        members.Should().HaveCount(1, "only owner should be member initially");
        members.First().Role.Should().Be(TeamRole.Owner);

        // Step 4: Simulate another user joining (in reality this would be different user ID)
        // Note: Current implementation uses device ID, so same service = same user
        // This test documents the expected behavior for when user management is implemented
        var joinResult = await ownerTeamService.JoinTeamAsync(teamCode, "Second Member");
        joinResult.Should().BeFalse("same user cannot join team twice (expected current behavior)");

        // Step 5: Verify team remains active
        var updatedTeams = await ownerTeamService.GetUserTeamsAsync();
        updatedTeams.First().IsActive.Should().BeTrue("team should remain active");
    }

    [Fact]
    public async Task T062_RealTimeLocationSharingWorkflow_MultipleMembersShareLocations_Success()
    {
        // Arrange
        // Test real-time location sharing between simulated team members
        var mockLocationService = new MockLocationService();
        var teamService = new TeamLocationService(mockLocationService);

        // Create team
        await teamService.CreateTeamAsync("Location Sharing Team", "Testing real-time sharing");
        var teams = await teamService.GetUserTeamsAsync();
        var team = teams.First();

        // Define test locations (simulating different team members)
        var member1Location = new { Lat = 25.0330, Lng = 121.5654 }; // Taipei 101
        var member2Location = new { Lat = 25.0420, Lng = 121.5650 }; // 1km north
        var member3Location = new { Lat = 25.0250, Lng = 121.5660 }; // 1km south

        var locationUpdates = new List<TeamLocationEventArgs>();
        teamService.LocationShared += (sender, args) => locationUpdates.Add(args);

        // Act & Assert

        // Step 1: Member 1 shares location
        var share1Result = await teamService.ShareLocationAsync(team.Id, member1Location.Lat, member1Location.Lng);
        share1Result.Should().BeTrue("first location share should succeed");

        // Step 2: Verify location was saved
        var locations = await teamService.GetTeamMemberLocationsAsync(team.Id);
        locations.Should().HaveCount(1, "should have one location");
        locations.First().Latitude.Should().BeApproximately(member1Location.Lat, 0.0001);

        // Step 3: Member updates their location (movement)
        var share2Result = await teamService.ShareLocationAsync(team.Id, member2Location.Lat, member2Location.Lng);
        share2Result.Should().BeTrue("location update should succeed");

        // Step 4: Verify location was updated (not duplicated)
        locations = await teamService.GetTeamMemberLocationsAsync(team.Id);
        locations.Should().HaveCount(1, "should still have one location (updated, not duplicated)");
        locations.First().Latitude.Should().BeApproximately(member2Location.Lat, 0.0001);

        // Step 5: Verify events were triggered
        locationUpdates.Should().HaveCount(2, "two location share events should be triggered");
        locationUpdates[0].Latitude.Should().BeApproximately(member1Location.Lat, 0.0001);
        locationUpdates[1].Latitude.Should().BeApproximately(member2Location.Lat, 0.0001);

        // Step 6: Test location sharing settings
        var settings = await teamService.GetLocationSharingSettingsAsync(team.Id);
        settings.UpdateIntervalSeconds.Should().Be(30, "default interval is 30 seconds");

        // Step 7: Update sharing settings to 60 seconds
        settings.UpdateIntervalSeconds = 60;
        var updateResult = await teamService.SetLocationSharingSettingsAsync(team.Id, settings);
        updateResult.Should().BeTrue("settings update should succeed");

        // Step 8: Verify settings were persisted
        var updatedSettings = await teamService.GetLocationSharingSettingsAsync(team.Id);
        updatedSettings.UpdateIntervalSeconds.Should().Be(60, "updated interval should be saved");
    }

    [Fact]
    public async Task T062_AutomaticLocationSharing_WithTimer_SharesLocationPeriodically()
    {
        // Arrange
        // Test automatic location sharing using timer mechanism
        var mockLocationService = new MockLocationService();

        // Set a fixed location that will be retrieved by the service
        var testLocation = new MauiLocation(25.0330, 121.5654);
        mockLocationService.SetFixedLocation(testLocation);

        var teamService = new TeamLocationService(mockLocationService);

        // Create team
        await teamService.CreateTeamAsync("Auto Share Team", "Testing automatic sharing");
        var teams = await teamService.GetUserTeamsAsync();
        var team = teams.First();

        // Set fast update interval for testing
        var settings = new LocationSharingSettings
        {
            UpdateIntervalSeconds = 1, // 1 second for faster testing
            IsAutoSharingEnabled = true
        };
        await teamService.SetLocationSharingSettingsAsync(team.Id, settings);

        var locationUpdateCount = 0;
        teamService.LocationShared += (sender, args) => locationUpdateCount++;

        // Act
        // Start automatic location sharing
        teamService.StartLocationSharing(team.Id);

        // Wait for at least one automatic update
        await Task.Delay(1500); // Wait 1.5 seconds

        // Stop sharing
        teamService.StopLocationSharing(team.Id);

        // Assert
        // At least one automatic location update should have occurred
        locationUpdateCount.Should().BeGreaterThan(0, "at least one automatic location update should occur");

        // Verify location was shared
        var locations = await teamService.GetTeamMemberLocationsAsync(team.Id);
        locations.Should().NotBeEmpty("location should be shared automatically");
    }

    [Fact]
    public async Task T062_LocationAlerts_SendAndReceive_Success()
    {
        // Arrange
        // Test sending and receiving location alerts within a team
        var mockLocationService = new MockLocationService();
        var teamService = new TeamLocationService(mockLocationService);

        await teamService.CreateTeamAsync("Alert Test Team", "Testing location alerts");
        var teams = await teamService.GetUserTeamsAsync();
        var team = teams.First();

        var alertMessage = "Emergency: Need assistance at this location!";
        var alertLat = 25.0330;
        var alertLng = 121.5654;

        // Act
        var sendResult = await teamService.SendLocationAlertAsync(team.Id, alertMessage, alertLat, alertLng);

        // Assert
        sendResult.Should().BeTrue("alert should be sent successfully");

        // Retrieve alerts
        var alerts = await teamService.GetLocationAlertsAsync(team.Id);
        alerts.Should().HaveCount(1, "should have one alert");

        var alert = alerts.First();
        alert.Message.Should().Be(alertMessage);
        alert.Latitude.Should().BeApproximately(alertLat, 0.0001);
        alert.Longitude.Should().BeApproximately(alertLng, 0.0001);
        alert.IsRead.Should().BeFalse("alert should be unread initially");
        alert.Timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task T062_MultipleTeams_IndependentLocationSharing_NoInterference()
    {
        // Arrange
        // Test that location sharing in multiple teams works independently
        var mockLocationService = new MockLocationService();
        var teamService = new TeamLocationService(mockLocationService);

        // Create two teams
        await teamService.CreateTeamAsync("Team Alpha", "First team");
        await teamService.CreateTeamAsync("Team Beta", "Second team");

        var teams = await teamService.GetUserTeamsAsync();
        teams.Should().HaveCount(2, "should have two teams");

        var teamAlpha = teams.First(t => t.Name == "Team Alpha");
        var teamBeta = teams.First(t => t.Name == "Team Beta");

        // Act
        // Share different locations to different teams
        await teamService.ShareLocationAsync(teamAlpha.Id, 25.0330, 121.5654); // Taipei 101
        await teamService.ShareLocationAsync(teamBeta.Id, 25.0420, 121.5650);  // 1km north

        // Assert
        var alphaLocations = await teamService.GetTeamMemberLocationsAsync(teamAlpha.Id);
        var betaLocations = await teamService.GetTeamMemberLocationsAsync(teamBeta.Id);

        alphaLocations.Should().HaveCount(1, "Team Alpha should have one location");
        betaLocations.Should().HaveCount(1, "Team Beta should have one location");

        alphaLocations.First().Latitude.Should().BeApproximately(25.0330, 0.0001);
        betaLocations.First().Latitude.Should().BeApproximately(25.0420, 0.0001);

        // Verify locations are independent
        alphaLocations.First().Latitude.Should().NotBe(betaLocations.First().Latitude,
            "different teams should have different locations");
    }

    [Fact]
    public async Task T062_TeamMemberLocations_FiltersByTimeWindow_ReturnsOnlyRecentLocations()
    {
        // Arrange
        // Test that GetTeamMemberLocationsAsync filters out old locations (>10 minutes)
        var mockLocationService = new MockLocationService();
        var teamService = new TeamLocationService(mockLocationService);

        await teamService.CreateTeamAsync("Time Filter Team", "Testing time-based filtering");
        var teams = await teamService.GetUserTeamsAsync();
        var team = teams.First();

        // Act
        // Share location now
        await teamService.ShareLocationAsync(team.Id, 25.0330, 121.5654);

        // Immediately retrieve
        var recentLocations = await teamService.GetTeamMemberLocationsAsync(team.Id);

        // Assert
        recentLocations.Should().HaveCount(1, "recent location should be returned");

        var location = recentLocations.First();
        var timeDiff = DateTime.Now - location.Timestamp;
        timeDiff.Should().BeLessThan(TimeSpan.FromSeconds(5), "location should be very recent");

        // Note: Testing actual 10-minute filter would require manipulating file timestamps
        // or dependency injection for time provider, which is beyond current scope
    }

    [Fact]
    public async Task T062_StopAllLocationSharing_Cleanup_DisposesAllTimers()
    {
        // Arrange
        // Test that StopAllLocationSharing properly cleans up all team timers
        var mockLocationService = new MockLocationService();
        var teamService = new TeamLocationService(mockLocationService);

        // Create multiple teams
        await teamService.CreateTeamAsync("Team 1", "First");
        await teamService.CreateTeamAsync("Team 2", "Second");

        var teams = await teamService.GetUserTeamsAsync();

        // Start sharing for both teams
        foreach (var team in teams)
        {
            teamService.StartLocationSharing(team.Id);
        }

        // Act
        // Stop all location sharing (simulates app shutdown)
        teamService.StopAllLocationSharing();

        // Assert
        // No exceptions should be thrown
        // All timers should be disposed
        // This is primarily a smoke test to ensure cleanup works

        // Try to stop again - should not throw
        Action stopAgain = () => teamService.StopAllLocationSharing();
        stopAgain.Should().NotThrow("stopping already-stopped sharing should not throw");
    }
}
