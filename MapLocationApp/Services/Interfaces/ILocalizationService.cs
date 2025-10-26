using System.Globalization;

namespace MapLocationApp.Services.Interfaces;

/// <summary>
/// Service for managing application localization and language settings
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Sets the application language asynchronously
    /// </summary>
    /// <param name="languageCode">Language code (e.g., "en-US", "zh-TW")</param>
    /// <returns>True if language was set successfully</returns>
    Task<bool> SetLanguageAsync(string languageCode);

    /// <summary>
    /// Gets the list of supported languages
    /// </summary>
    /// <returns>List of supported culture codes</returns>
    Task<List<string>> GetSupportedLanguagesAsync();

    /// <summary>
    /// Gets the current application language
    /// </summary>
    /// <returns>Current language code</returns>
    Task<string> GetCurrentLanguageAsync();

    /// <summary>
    /// Gets a localized string for the given key
    /// </summary>
    /// <param name="key">Resource key</param>
    /// <returns>Localized string or key if not found</returns>
    string GetLocalizedString(string key);

    /// <summary>
    /// Event raised when the culture changes
    /// </summary>
    event Action<CultureInfo>? CultureChanged;
}
