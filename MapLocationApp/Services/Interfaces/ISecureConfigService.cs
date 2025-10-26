namespace MapLocationApp.Services.Interfaces;

/// <summary>
/// Service for secure credential storage using platform-specific secure storage mechanisms.
/// - Android: KeyStore (hardware-backed if available)
/// - iOS/macCatalyst: Keychain Services
/// - Windows: Windows Credential Manager
/// </summary>
public interface ISecureConfigService
{
    /// <summary>
    /// Retrieves the MySQL database password from secure storage.
    /// </summary>
    /// <returns>The database password, or null if not set.</returns>
    Task<string?> GetDatabasePasswordAsync();

    /// <summary>
    /// Stores the MySQL database password in secure storage.
    /// </summary>
    /// <param name="password">The password to store securely.</param>
    Task SetDatabasePasswordAsync(string password);

    /// <summary>
    /// Retrieves an API key from secure storage.
    /// </summary>
    /// <param name="keyName">The name of the API key (e.g., "OpenRouteService", "TelegramBot").</param>
    /// <returns>The API key, or null if not set.</returns>
    Task<string?> GetApiKeyAsync(string keyName);

    /// <summary>
    /// Stores an API key in secure storage.
    /// </summary>
    /// <param name="keyName">The name of the API key.</param>
    /// <param name="apiKey">The API key value.</param>
    Task SetApiKeyAsync(string keyName, string apiKey);

    /// <summary>
    /// Removes a stored credential from secure storage.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>True if the key was removed, false if it didn't exist.</returns>
    bool Remove(string key);

    /// <summary>
    /// Checks if a credential exists in secure storage.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key exists, false otherwise.</returns>
    Task<bool> ContainsKeyAsync(string key);
}
