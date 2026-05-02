using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace MapLocationApp.Services;

public interface IBiometricService
{
    /// <summary>True if device has Face ID / Touch ID / fingerprint / Windows Hello available.</summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Show the OS biometric prompt and return whether the user authenticated successfully.
    /// Falls back gracefully if biometric is unavailable.
    /// </summary>
    Task<bool> AuthenticateAsync(string reason, string? cancelTitle = null);

    // ====== Bind biometric to a specific app account ======
    // OS biometric only confirms "device owner". To answer "which app user is this",
    // we cache the user id in SecureStorage; biometric is the gate to unlock it.

    /// <summary>True if a user has previously enabled biometric quick-login on this device.</summary>
    Task<bool> HasBoundUserAsync();

    /// <summary>Cache the given user id behind a biometric gate. Call after a successful password login.</summary>
    Task BindUserAsync(int userId, string username);

    /// <summary>Forget the bound user (when logging out or user switches accounts).</summary>
    Task UnbindAsync();

    /// <summary>
    /// Display name of the currently-bound user — useful for showing "Sign in as 王小明" on the login page.
    /// </summary>
    Task<string?> GetBoundUsernameAsync();

    /// <summary>
    /// Trigger biometric prompt; if successful return the bound user id, otherwise null.
    /// </summary>
    Task<int?> AuthenticateAndGetBoundUserAsync(string reason);
}

public class BiometricService : IBiometricService
{
    private const string BoundUserIdKey = "biometric.bound_user_id";
    private const string BoundUsernameKey = "biometric.bound_username";

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var availability = await CrossFingerprint.Current.GetAvailabilityAsync(allowAlternativeAuthentication: true);
            // "Available" or "Weak" means the device CAN authenticate; anything else (no hardware,
            // permission denied, no enrolled biometrics) we treat as unavailable so callers fall back.
            return availability == FingerprintAvailability.Available;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AuthenticateAsync(string reason, string? cancelTitle = null)
    {
        try
        {
            var request = new AuthenticationRequestConfiguration("DAKA+", reason)
            {
                CancelTitle = cancelTitle ?? "取消",
                AllowAlternativeAuthentication = true,
                ConfirmationRequired = false,
            };
            var result = await CrossFingerprint.Current.AuthenticateAsync(request);
            return result.Authenticated;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Biometric auth failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> HasBoundUserAsync()
    {
        var id = await SecureStorage.Default.GetAsync(BoundUserIdKey);
        return !string.IsNullOrEmpty(id);
    }

    public async Task BindUserAsync(int userId, string username)
    {
        await SecureStorage.Default.SetAsync(BoundUserIdKey, userId.ToString());
        await SecureStorage.Default.SetAsync(BoundUsernameKey, username);
    }

    public Task UnbindAsync()
    {
        SecureStorage.Default.Remove(BoundUserIdKey);
        SecureStorage.Default.Remove(BoundUsernameKey);
        return Task.CompletedTask;
    }

    public async Task<string?> GetBoundUsernameAsync()
        => await SecureStorage.Default.GetAsync(BoundUsernameKey);

    public async Task<int?> AuthenticateAndGetBoundUserAsync(string reason)
    {
        var idStr = await SecureStorage.Default.GetAsync(BoundUserIdKey);
        if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out var id))
            return null;

        // Biometric prompt acts as the gate to release the cached user id.
        var ok = await AuthenticateAsync(reason);
        return ok ? id : null;
    }
}
