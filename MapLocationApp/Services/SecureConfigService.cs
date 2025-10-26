using MapLocationApp.Services.Interfaces;

namespace MapLocationApp.Services;

/// <summary>
/// Implementation of secure credential storage using MAUI SecureStorage API.
/// Platform-specific implementations:
/// - Android: KeyStore (AES-256 encryption, hardware-backed on supported devices)
/// - iOS/macCatalyst: Keychain Services (kSecAttrAccessibleWhenUnlocked)
/// - Windows: Windows Credential Manager (Data Protection API)
/// </summary>
public class SecureConfigService : ISecureConfigService
{
    private const string DatabasePasswordKey = "db_password";
    private const string ApiKeyPrefix = "api_key_";

    /// <inheritdoc/>
    public async Task<string?> GetDatabasePasswordAsync()
    {
        try
        {
            return await SecureStorage.GetAsync(DatabasePasswordKey);
        }
        catch (Exception ex)
        {
            // Log error but don't expose sensitive info
            System.Diagnostics.Debug.WriteLine($"[SecureConfigService] Failed to retrieve database password: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetDatabasePasswordAsync(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        }

        try
        {
            await SecureStorage.SetAsync(DatabasePasswordKey, password);
            System.Diagnostics.Debug.WriteLine("[SecureConfigService] Database password stored securely.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecureConfigService] Failed to store database password: {ex.Message}");
            throw new InvalidOperationException("Failed to store password in secure storage.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetApiKeyAsync(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
        {
            throw new ArgumentException("Key name cannot be null or empty.", nameof(keyName));
        }

        try
        {
            var fullKey = $"{ApiKeyPrefix}{keyName}";
            return await SecureStorage.GetAsync(fullKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecureConfigService] Failed to retrieve API key '{keyName}': {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetApiKeyAsync(string keyName, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(keyName))
        {
            throw new ArgumentException("Key name cannot be null or empty.", nameof(keyName));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key cannot be null or empty.", nameof(apiKey));
        }

        try
        {
            var fullKey = $"{ApiKeyPrefix}{keyName}";
            await SecureStorage.SetAsync(fullKey, apiKey);
            System.Diagnostics.Debug.WriteLine($"[SecureConfigService] API key '{keyName}' stored securely.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecureConfigService] Failed to store API key '{keyName}': {ex.Message}");
            throw new InvalidOperationException($"Failed to store API key '{keyName}' in secure storage.", ex);
        }
    }

    /// <inheritdoc/>
    public bool Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        }

        try
        {
            return SecureStorage.Remove(key);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecureConfigService] Failed to remove key '{key}': {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ContainsKeyAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        }

        try
        {
            var value = await SecureStorage.GetAsync(key);
            return value != null;
        }
        catch
        {
            return false;
        }
    }
}
