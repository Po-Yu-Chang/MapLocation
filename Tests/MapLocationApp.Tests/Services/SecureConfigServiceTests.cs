using MapLocationApp.Services;
using MapLocationApp.Services.Interfaces;

namespace MapLocationApp.Tests.Services;

/// <summary>
/// Unit tests for SecureConfigService
/// These tests validate the service interface and error handling.
/// NOTE: Full integration tests require MAUI platform runtime.
/// </summary>
public class SecureConfigServiceTests
{
    [Fact]
    public void SecureConfigService_ImplementsInterface()
    {
        // Arrange & Act
        var service = new SecureConfigService();

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<ISecureConfigService>();
    }

    [Fact]
    public async Task SetDatabasePasswordAsync_WithEmptyPassword_ThrowsArgumentException()
    {
        // Arrange
        var service = new SecureConfigService();

        // Act
        Func<Task> act = async () => await service.SetDatabasePasswordAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Password cannot be null or empty.*");
    }

    [Fact]
    public async Task SetDatabasePasswordAsync_WithNullPassword_ThrowsArgumentException()
    {
        // Arrange
        var service = new SecureConfigService();

        // Act
        Func<Task> act = async () => await service.SetDatabasePasswordAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Password cannot be null or empty.*");
    }

    [Fact]
    public async Task SetApiKeyAsync_WithEmptyKeyName_ThrowsArgumentException()
    {
        // Arrange
        var service = new SecureConfigService();

        // Act
        Func<Task> act = async () => await service.SetApiKeyAsync(string.Empty, "test-key");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Key name cannot be null or empty.*");
    }

    [Fact]
    public async Task SetApiKeyAsync_WithEmptyApiKey_ThrowsArgumentException()
    {
        // Arrange
        var service = new SecureConfigService();

        // Act
        Func<Task> act = async () => await service.SetApiKeyAsync("TestKey", string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("API key cannot be null or empty.*");
    }

    [Fact]
    public void Remove_WithEmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var service = new SecureConfigService();

        // Act
        Action act = () => service.Remove(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Key cannot be null or empty.*");
    }

    [Fact]
    public async Task ContainsKeyAsync_WithEmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var service = new SecureConfigService();

        // Act
        Func<Task> act = async () => await service.ContainsKeyAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Key cannot be null or empty.*");
    }

    // NOTE: Integration tests for actual SecureStorage functionality require MAUI runtime
    // These tests would be added in Integration/ folder once workload is configured:
    // - SetDatabasePasswordAsync_ValidPassword_StoresSuccessfully
    // - GetDatabasePasswordAsync_AfterSet_ReturnsCorrectPassword
    // - SetApiKeyAsync_ValidKey_StoresSuccessfully
    // - GetApiKeyAsync_AfterSet_ReturnsCorrectKey
    // - Remove_ExistingKey_ReturnsTrue
    // - ContainsKeyAsync_ExistingKey_ReturnsTrue
}
