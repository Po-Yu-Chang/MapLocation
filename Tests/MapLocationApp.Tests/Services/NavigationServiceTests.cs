using Xunit;
using FluentAssertions;
using Moq;
using MapLocationApp.Services;
using MapLocationApp.Models;
using MapLocationApp.Tests.Mocks;
using Microsoft.Maui.Devices.Sensors;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace MapLocationApp.Tests.Services;

/// <summary>
/// T040-T045: Unit tests for NavigationService
/// Tests turn-by-turn navigation, TTS voice guidance, and route management
/// </summary>
public class NavigationServiceTests
{
    [Fact]
    public async Task T040_StartNavigationAsync_ValidRoute_StartsSuccessfully()
    {
        // Arrange
        var mockLocation = new MockLocationService();
        var mockRouteService = new Mock<IRouteService>();
        var mockTTS = new Mock<ITTSService>();
        var mockTelegram = new Mock<ITelegramNotificationService>();
        var navService = new NavigationService(mockLocation, mockRouteService.Object, mockTTS.Object, mockTelegram.Object);

        var route = new Route
        {
            StartLocation = new MauiLocation(25.0330, 121.5654),
            EndLocation = new MauiLocation(25.0430, 121.5754),
            Steps = new List<RouteStep>
            {
                new RouteStep
                {
                    Distance = 500,
                    Duration = TimeSpan.FromSeconds(300),
                    Instruction = "Head north on Main Street",
                    StartLocation = new MauiLocation(25.0330, 121.5654),
                    EndLocation = new MauiLocation(25.0380, 121.5654)
                },
                new RouteStep
                {
                    Distance = 300,
                    Duration = TimeSpan.FromSeconds(180),
                    Instruction = "Turn right onto Park Avenue",
                    StartLocation = new MauiLocation(25.0380, 121.5654),
                    EndLocation = new MauiLocation(25.0430, 121.5754)
                }
            },
            TotalDistance = 800,
            TotalDuration = 480
        };

        // Act
        await navService.StartNavigationAsync(route);

        // Assert
        navService.IsNavigating.Should().BeTrue("navigation should start with valid route");
        navService.CurrentRoute.Should().NotBeNull();
        navService.CurrentRoute!.TotalDistance.Should().Be(800);

        // Verify TTS spoke first instruction
        mockTTS.Verify(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task T041_RouteStepProgression_LocationUpdate_AdvancesToNextStep()
    {
        // Arrange
        var mockLocation = new MockLocationService();
        var mockRouteService = new Mock<IRouteService>();
        var mockTTS = new Mock<ITTSService>();
        var mockTelegram = new Mock<ITelegramNotificationService>();
        var navService = new NavigationService(mockLocation, mockRouteService.Object, mockTTS.Object, mockTelegram.Object);

        var route = new Route
        {
            Steps = new List<RouteStep>
            {
                new RouteStep
                {
                    Instruction = "Step 1: Go straight",
                    StartLocation = new MauiLocation(25.0330, 121.5654),
                    EndLocation = new MauiLocation(25.0350, 121.5654),
                    Distance = 200
                },
                new RouteStep
                {
                    Instruction = "Step 2: Turn left",
                    StartLocation = new MauiLocation(25.0350, 121.5654),
                    EndLocation = new MauiLocation(25.0370, 121.5680),
                    Distance = 300
                }
            }
        };

        await navService.StartNavigationAsync(route);

        // Act - Simulate reaching end of step 1
        mockLocation.SimulateLocationUpdate(new MauiLocation(25.0350, 121.5654));

        // T041: Wait for navigation service to process location update (responds to LocationChanged event)
        await Task.Delay(200);

        // Assert
        navService.CurrentStepIndex.Should().Be(1, "should advance to step 2");

        // Verify TTS spoke next instruction
        mockTTS.Verify(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task T042_TurnByTurnInstructions_ProximityThreshold_SpeaksInstructions()
    {
        // Arrange
        var mockLocation = new MockLocationService();
        var mockRouteService = new Mock<IRouteService>();
        var mockTTS = new Mock<ITTSService>();
        var mockTelegram = new Mock<ITelegramNotificationService>();
        var navService = new NavigationService(mockLocation, mockRouteService.Object, mockTTS.Object, mockTelegram.Object);

        var route = new Route
        {
            Steps = new List<RouteStep>
            {
                new RouteStep
                {
                    Instruction = "In 500 meters, turn left",
                    StartLocation = new MauiLocation(25.0330, 121.5654),
                    EndLocation = new MauiLocation(25.0380, 121.5654),
                    Distance = 500
                }
            }
        };

        await navService.StartNavigationAsync(route);

        // Act - Simulate approaching turn (100m away)
        mockLocation.SimulateLocationUpdate(new MauiLocation(25.0370, 121.5654));

        // Assert - Should speak warning instruction
        mockTTS.Verify(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task T043_TTSVoiceGuidance_MultilanguageSupport_SpeaksInUserLanguage()
    {
        // Arrange
        var mockLocation = new MockLocationService();
        var mockRouteService = new Mock<IRouteService>();
        var mockTTS = new Mock<ITTSService>();
        var mockTelegram = new Mock<ITelegramNotificationService>();
        mockTTS.Setup(x => x.SetLanguageAsync("zh-TW")).ReturnsAsync(true);

        var navService = new NavigationService(mockLocation, mockRouteService.Object, mockTTS.Object, mockTelegram.Object);

        // Act - Set Traditional Chinese
        await navService.SetVoiceLanguageAsync("zh-TW");

        var route = new Route
        {
            Steps = new List<RouteStep>
            {
                new RouteStep
                {
                    Instruction = "前方500公尺後左轉",  // Traditional Chinese
                    StartLocation = new MauiLocation(25.0330, 121.5654),
                    EndLocation = new MauiLocation(25.0380, 121.5654),
                    Distance = 500
                }
            }
        };

        await navService.StartNavigationAsync(route);

        // Assert
        mockTTS.Verify(x => x.SetLanguageAsync("zh-TW"), Times.Once);
        mockTTS.Verify(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task T044_OffRouteDetection_DeviatesFromPath_TriggersRecalculation()
    {
        // Arrange
        var mockLocation = new MockLocationService();
        var mockRouteService = new Mock<IRouteService>();
        var mockTTS = new Mock<ITTSService>();
        var mockTelegram = new Mock<ITelegramNotificationService>();
        var navService = new NavigationService(mockLocation, mockRouteService.Object, mockTTS.Object, mockTelegram.Object);

        var route = new Route
        {
            Steps = new List<RouteStep>
            {
                new RouteStep
                {
                    Instruction = "Head north",
                    StartLocation = new MauiLocation(25.0330, 121.5654),
                    EndLocation = new MauiLocation(25.0430, 121.5654),
                    Distance = 1000
                }
            }
        };

        await navService.StartNavigationAsync(route);

        var routeRecalculatedEventFired = false;
        navService.RouteRecalculating += (sender, args) => { routeRecalculatedEventFired = true; };

        // Act - Simulate going off-route (200m deviation) - needs 3 consecutive deviations
        mockLocation.SimulateLocationUpdate(new MauiLocation(25.0350, 121.5754));  // 200m east deviation
        await Task.Delay(300);  // First check
        mockLocation.SimulateLocationUpdate(new MauiLocation(25.0350, 121.5754));  // Still off-route
        await Task.Delay(300);  // Second check
        mockLocation.SimulateLocationUpdate(new MauiLocation(25.0350, 121.5754));  // Still off-route (3rd time)
        await Task.Delay(300);  // Third check - should trigger recalculation

        // Assert
        routeRecalculatedEventFired.Should().BeTrue("route recalculation should be triggered");
        mockTTS.Verify(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task T045_NavigationCancellation_UserStopsNavigation_CleansUpResources()
    {
        // Arrange
        var mockLocation = new MockLocationService();
        var mockRouteService = new Mock<IRouteService>();
        var mockTTS = new Mock<ITTSService>();
        var mockTelegram = new Mock<ITelegramNotificationService>();
        var navService = new NavigationService(mockLocation, mockRouteService.Object, mockTTS.Object, mockTelegram.Object);

        var route = new Route
        {
            Steps = new List<RouteStep>
            {
                new RouteStep
                {
                    Instruction = "Head north",
                    StartLocation = new MauiLocation(25.0330, 121.5654),
                    EndLocation = new MauiLocation(25.0430, 121.5654),
                    Distance = 1000
                }
            }
        };

        await navService.StartNavigationAsync(route);

        // Act
        await navService.StopNavigationAsync();

        // Assert
        navService.IsNavigating.Should().BeFalse("navigation should stop");
        navService.CurrentRoute.Should().BeNull("route should be cleared");
        navService.CurrentStepIndex.Should().Be(0, "step index should reset");

        mockTTS.Verify(x => x.StopSpeakingAsync(), Times.AtLeastOnce, "TTS should stop speaking");
    }

    [Fact]
    public async Task T045_DestinationReached_ArrivesAtEndLocation_CompletesNavigation()
    {
        // Arrange
        var mockLocation = new MockLocationService();
        var mockRouteService = new Mock<IRouteService>();
        var mockTTS = new Mock<ITTSService>();
        var mockTelegram = new Mock<ITelegramNotificationService>();
        var navService = new NavigationService(mockLocation, mockRouteService.Object, mockTTS.Object, mockTelegram.Object);

        var destination = new MauiLocation(25.0430, 121.5654);
        var route = new Route
        {
            EndLocation = destination,
            Steps = new List<RouteStep>
            {
                new RouteStep
                {
                    Instruction = "Head north",
                    StartLocation = new MauiLocation(25.0330, 121.5654),
                    EndLocation = destination,
                    Distance = 1000
                }
            }
        };

        await navService.StartNavigationAsync(route);

        var navigationCompletedEventFired = false;
        navService.NavigationCompleted += (sender, args) => { navigationCompletedEventFired = true; };

        // Act - Arrive at destination (within 20m threshold)
        mockLocation.SimulateLocationUpdate(new MauiLocation(25.0431, 121.5654));  // 10m from destination

        // T045: Wait for navigation service to process arrival (responds to LocationChanged event)
        await Task.Delay(200);

        // Assert
        navigationCompletedEventFired.Should().BeTrue("navigation completed event should fire");
        navService.IsNavigating.Should().BeFalse("navigation should auto-stop on arrival");

        mockTTS.Verify(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task T045_NavigationPause_UserPausesNavigation_RetainsState()
    {
        // Arrange
        var mockLocation = new MockLocationService();
        var mockRouteService = new Mock<IRouteService>();
        var mockTTS = new Mock<ITTSService>();
        var mockTelegram = new Mock<ITelegramNotificationService>();
        var navService = new NavigationService(mockLocation, mockRouteService.Object, mockTTS.Object, mockTelegram.Object);

        var route = new Route
        {
            Steps = new List<RouteStep>
            {
                new RouteStep
                {
                    Instruction = "Step 1",
                    StartLocation = new MauiLocation(25.0330, 121.5654),
                    EndLocation = new MauiLocation(25.0350, 121.5654),
                    Distance = 200
                },
                new RouteStep
                {
                    Instruction = "Step 2",
                    StartLocation = new MauiLocation(25.0350, 121.5654),
                    EndLocation = new MauiLocation(25.0370, 121.5654),
                    Distance = 200
                }
            }
        };

        await navService.StartNavigationAsync(route);
        mockLocation.SimulateLocationUpdate(new MauiLocation(25.0340, 121.5654));  // Partway through step 1

        // Act
        await navService.PauseNavigationAsync();

        // Assert
        navService.IsNavigating.Should().BeFalse("navigation should pause");
        navService.CurrentRoute.Should().NotBeNull("route should be retained");
        navService.CurrentStepIndex.Should().Be(0, "step index should be preserved");

        // Resume
        await navService.ResumeNavigationAsync();

        navService.IsNavigating.Should().BeTrue("navigation should resume");
        navService.CurrentStepIndex.Should().Be(0, "should continue from same step");
    }
}
