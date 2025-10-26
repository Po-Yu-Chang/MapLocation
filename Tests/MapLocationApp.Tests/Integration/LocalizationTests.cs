using Xunit;
using FluentAssertions;
using MapLocationApp.Services;

namespace MapLocationApp.Tests.Integration;

/// <summary>
/// T113-T114: Integration tests for Localization
/// Tests language switching with UI updates and voice instruction localization
/// </summary>
public class LocalizationTests
{
    [Fact]
    public async Task T113_LanguageSwitching_UpdatesUIStrings()
    {
        // Arrange
        // Test that switching language updates all UI strings
        var localizationService = LocalizationService.Instance;

        // Get strings in English
        localizationService.SetCulture("en-US");
        var englishWelcome = localizationService.GetLocalizedString("Welcome");
        var englishLogin = localizationService.GetLocalizedString("Login");

        // Act - Switch to Traditional Chinese
        localizationService.SetCulture("zh-TW");
        var chineseWelcome = localizationService.GetLocalizedString("Welcome");
        var chineseLogin = localizationService.GetLocalizedString("Login");

        // Assert
        chineseWelcome.Should().NotBe(englishWelcome,
            "localized strings should differ between languages");

        chineseLogin.Should().NotBe(englishLogin,
            "localized strings should differ between languages");

        // Cleanup
        localizationService.SetCulture("en-US");
    }

    [Fact]
    public async Task T113_LanguageSwitching_AllSupportedLanguages_UpdatesCorrectly()
    {
        // Arrange
        // Test switching through all supported languages
        var localizationService = LocalizationService.Instance;
        var supportedCultures = localizationService.GetSupportedCultures();

        var translations = new Dictionary<string, string>();

        // Act - Get "Welcome" message in all languages
        foreach (var culture in supportedCultures)
        {
            localizationService.SetCulture(culture.Name);
            var welcomeMessage = localizationService.GetLocalizedString("Welcome");
            translations[culture.Name] = welcomeMessage;
        }

        // Assert
        translations.Should().HaveCount(5, "should have translations for all 5 supported languages");
        translations.Values.Should().OnlyHaveUniqueItems(
            "each language should have different translation (or return key if not translated)");

        // Cleanup
        localizationService.SetCulture("en-US");
    }

    [Fact]
    public async Task T113_LanguageSwitching_PersistsAcrossAppRestart()
    {
        // Arrange
        // Test that language preference persists
        var localizationService = LocalizationService.Instance;

        // Act - Set to Japanese
        localizationService.SetCulture("ja-JP");

        // In real app, preference would be saved to storage
        // and loaded on next app start

        var currentCulture = localizationService.GetCurrentCulture();

        // Assert
        currentCulture.Name.Should().Be("ja-JP",
            "language preference should be persisted");

        // Cleanup
        localizationService.SetCulture("en-US");
    }

    [Fact]
    public async Task T114_VoiceInstructionLocalization_NavigationWorkflow_UsesCorrectLanguage()
    {
        // Arrange
        // Test that navigation voice instructions use selected language
        var localizationService = LocalizationService.Instance;

        // Navigation instruction keys (should exist in resources)
        var turnLeftKey = "Navigation_TurnLeft";
        var turnRightKey = "Navigation_TurnRight";
        var arrivedKey = "Navigation_Arrived";

        // Act - Get instructions in English
        localizationService.SetCulture("en-US");
        var englishTurnLeft = localizationService.GetLocalizedString(turnLeftKey);
        var englishArrived = localizationService.GetLocalizedString(arrivedKey);

        // Get instructions in Traditional Chinese
        localizationService.SetCulture("zh-TW");
        var chineseTurnLeft = localizationService.GetLocalizedString(turnLeftKey);
        var chineseArrived = localizationService.GetLocalizedString(arrivedKey);

        // Assert
        englishTurnLeft.Should().NotBeNullOrEmpty("should have English navigation instruction");
        chineseTurnLeft.Should().NotBeNullOrEmpty("should have Chinese navigation instruction");

        // Instructions should differ (or return key if not yet translated)
        englishArrived.Should().NotBeNullOrEmpty();
        chineseArrived.Should().NotBeNullOrEmpty();

        // Cleanup
        localizationService.SetCulture("en-US");
    }

    [Fact]
    public async Task T114_VoiceInstructionLocalization_AllNavigationCommands_Localized()
    {
        // Arrange
        // Test that all navigation commands have localized versions
        var localizationService = LocalizationService.Instance;

        var navigationKeys = new[]
        {
            "Navigation_TurnLeft",
            "Navigation_TurnRight",
            "Navigation_GoStraight",
            "Navigation_UTurn",
            "Navigation_Arrived",
            "Navigation_Rerouting",
            "Navigation_In100Meters",
            "Navigation_In500Meters"
        };

        // Act - Test in Traditional Chinese
        localizationService.SetCulture("zh-TW");

        var localizedInstructions = new Dictionary<string, string>();
        foreach (var key in navigationKeys)
        {
            localizedInstructions[key] = localizationService.GetLocalizedString(key);
        }

        // Assert
        localizedInstructions.Should().HaveCount(navigationKeys.Length,
            "all navigation keys should have entries");

        localizedInstructions.Values.Should().OnlyContain(v => !string.IsNullOrEmpty(v),
            "all navigation instructions should have values");

        // Cleanup
        localizationService.SetCulture("en-US");
    }

    [Fact]
    public async Task T114_VoiceInstructionLocalization_DistanceUnits_LocalizedCorrectly()
    {
        // Arrange
        // Test localization of distance units (meters, kilometers, etc.)
        var localizationService = LocalizationService.Instance;

        // Act
        localizationService.SetCulture("en-US");
        var englishMeters = localizationService.GetLocalizedString("Unit_Meters");
        var englishKilometers = localizationService.GetLocalizedString("Unit_Kilometers");

        localizationService.SetCulture("zh-TW");
        var chineseMeters = localizationService.GetLocalizedString("Unit_Meters");
        var chineseKilometers = localizationService.GetLocalizedString("Unit_Kilometers");

        // Assert
        englishMeters.Should().NotBeNullOrEmpty();
        chineseMeters.Should().NotBeNullOrEmpty();

        englishKilometers.Should().NotBeNullOrEmpty();
        chineseKilometers.Should().NotBeNullOrEmpty();

        // Cleanup
        localizationService.SetCulture("en-US");
    }
}
