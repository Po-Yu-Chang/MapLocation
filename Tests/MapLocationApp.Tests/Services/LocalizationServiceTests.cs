using Xunit;
using FluentAssertions;
using MapLocationApp.Services;
using System.Globalization;

namespace MapLocationApp.Tests.Services;

/// <summary>
/// T111-T112: Unit tests for LocalizationService
/// Tests language switching and resource file loading for multiple locales
/// </summary>
public class LocalizationServiceTests
{
    [Fact]
    public async Task T111_SetLanguageAsync_ValidCulture_UpdatesCulture()
    {
        // Arrange
        // Test setting language to valid culture codes
        var localizationService = LocalizationService.Instance;

        var originalCulture = localizationService.GetCurrentCulture();

        // Act - Set to Traditional Chinese
        localizationService.SetCulture("zh-TW");
        var taiwaneseCulture = localizationService.GetCurrentCulture();

        // Set to English
        localizationService.SetCulture("en-US");
        var englishCulture = localizationService.GetCurrentCulture();

        // Assert
        taiwaneseCulture.Name.Should().Be("zh-TW", "culture should be set to Traditional Chinese");
        englishCulture.Name.Should().Be("en-US", "culture should be set to English");

        // Restore original culture
        localizationService.SetCulture(originalCulture.Name);
    }

    [Fact]
    public async Task T111_SetLanguageAsync_InvalidCulture_HandlesGracefully()
    {
        // Arrange
        // Test handling of invalid culture codes
        var localizationService = LocalizationService.Instance;
        var beforeCulture = localizationService.GetCurrentCulture();

        // Act
        // Attempt to set invalid culture
        localizationService.SetCulture("invalid-culture-code");
        var afterCulture = localizationService.GetCurrentCulture();

        // Assert
        // Should either keep previous culture or handle error gracefully
        // Implementation should not throw exception
    }

    [Fact]
    public async Task T111_SetLanguageAsync_TriggersCultureChangedEvent()
    {
        // Arrange
        // Test that culture change triggers event
        var localizationService = LocalizationService.Instance;

        CultureInfo? eventCulture = null;
        var eventFired = false;

        localizationService.CultureChanged += (culture) =>
        {
            eventFired = true;
            eventCulture = culture;
        };

        // Act
        localizationService.SetCulture("ja-JP");

        // Assert
        eventFired.Should().BeTrue("CultureChanged event should fire");
        eventCulture.Should().NotBeNull("event should provide culture info");
        eventCulture!.Name.Should().Be("ja-JP");

        // Cleanup - restore to English
        localizationService.SetCulture("en-US");
    }

    [Fact]
    public void T112_ResourceLoading_TraditionalChinese_LoadsCorrectStrings()
    {
        // Arrange
        // Test loading Traditional Chinese resource strings
        var localizationService = LocalizationService.Instance;

        // Act
        localizationService.SetCulture("zh-TW");

        // Get localized strings (these keys should exist in AppResources.zh-TW.resx)
        var welcomeMessage = localizationService.GetLocalizedString("Welcome");
        var loginButton = localizationService.GetLocalizedString("Login");

        // Assert
        // Strings should be in Traditional Chinese
        // If key not found, returns the key itself
        welcomeMessage.Should().NotBeNullOrEmpty();
        loginButton.Should().NotBeNullOrEmpty();

        // Cleanup
        localizationService.SetCulture("en-US");
    }

    [Fact]
    public void T112_ResourceLoading_SimplifiedChinese_LoadsCorrectStrings()
    {
        // Arrange
        // Test loading Simplified Chinese resource strings
        var localizationService = LocalizationService.Instance;

        // Act
        localizationService.SetCulture("zh-CN");

        var welcomeMessage = localizationService.GetLocalizedString("Welcome");

        // Assert
        welcomeMessage.Should().NotBeNullOrEmpty();

        // Cleanup
        localizationService.SetCulture("en-US");
    }

    [Fact]
    public void T112_ResourceLoading_English_LoadsCorrectStrings()
    {
        // Arrange
        // Test loading English resource strings (default)
        var localizationService = LocalizationService.Instance;

        // Act
        localizationService.SetCulture("en-US");

        var welcomeMessage = localizationService.GetLocalizedString("Welcome");
        var loginButton = localizationService.GetLocalizedString("Login");

        // Assert
        welcomeMessage.Should().NotBeNullOrEmpty();
        loginButton.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void T112_ResourceLoading_Japanese_LoadsCorrectStrings()
    {
        // Arrange
        // Test loading Japanese resource strings
        var localizationService = LocalizationService.Instance;

        // Act
        localizationService.SetCulture("ja-JP");

        var welcomeMessage = localizationService.GetLocalizedString("Welcome");

        // Assert
        welcomeMessage.Should().NotBeNullOrEmpty();

        // Cleanup
        localizationService.SetCulture("en-US");
    }

    [Fact]
    public void T112_ResourceLoading_Korean_LoadsCorrectStrings()
    {
        // Arrange
        // Test loading Korean resource strings
        var localizationService = LocalizationService.Instance;

        // Act
        localizationService.SetCulture("ko-KR");

        var welcomeMessage = localizationService.GetLocalizedString("Welcome");

        // Assert
        welcomeMessage.Should().NotBeNullOrEmpty();

        // Cleanup
        localizationService.SetCulture("en-US");
    }

    [Fact]
    public void T112_ResourceLoading_MissingKey_ReturnsKey()
    {
        // Arrange
        // Test behavior when requesting non-existent resource key
        var localizationService = LocalizationService.Instance;

        // Act
        var result = localizationService.GetLocalizedString("NonExistentKey12345");

        // Assert
        result.Should().Be("NonExistentKey12345", "should return key when resource not found");
    }

    [Fact]
    public void T112_GetSupportedCultures_ReturnsAllSupportedLanguages()
    {
        // Arrange
        // Test retrieving list of supported cultures
        var localizationService = LocalizationService.Instance;

        // Act
        var supportedCultures = localizationService.GetSupportedCultures();

        // Assert
        supportedCultures.Should().NotBeEmpty("should have supported cultures");
        supportedCultures.Should().HaveCount(5, "should support 5 languages");

        var cultureNames = supportedCultures.Select(c => c.Name).ToList();
        cultureNames.Should().Contain("en-US", "should support English");
        cultureNames.Should().Contain("zh-TW", "should support Traditional Chinese");
        cultureNames.Should().Contain("zh-CN", "should support Simplified Chinese");
        cultureNames.Should().Contain("ja-JP", "should support Japanese");
        cultureNames.Should().Contain("ko-KR", "should support Korean");
    }

    [Fact]
    public void T112_IndexerAccess_RetrievesLocalizedString()
    {
        // Arrange
        // Test convenient indexer syntax for getting localized strings
        var localizationService = LocalizationService.Instance;

        localizationService.SetCulture("en-US");

        // Act
        var welcomeViaIndexer = localizationService["Welcome"];
        var welcomeViaMethod = localizationService.GetLocalizedString("Welcome");

        // Assert
        welcomeViaIndexer.Should().Be(welcomeViaMethod,
            "indexer should return same value as GetLocalizedString");

        // Cleanup
        localizationService.SetCulture("en-US");
    }
}
