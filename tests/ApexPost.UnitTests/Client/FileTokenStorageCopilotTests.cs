// COPILOT-ASSISTED: drafted with GitHub Copilot (prompt 7 in COPILOT-LOG.md), then reviewed and extended by the student.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using ApexPost.Courier.Console.Models;
using ApexPost.Courier.Console.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApexPost.UnitTests.Client;

[SupportedOSPlatform("windows")]
public class FileTokenStorageCopilotTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string CreateTempFilePath()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        _tempFiles.Add(filePath);
        return filePath;
    }

    [Fact]
    public async Task GetTokenAsync_WhenNoFileExists_ReturnsNull()
    {
        // Arrange
        var filePath = CreateTempFilePath();
        var storage = new FileTokenStorage(Options.Create(new TokenStorageOptions { FilePath = filePath }), NullLogger<FileTokenStorage>.Instance);

        // Act
        var result = await storage.GetTokenAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task StoreTokenAsync_WhenTokenIsStored_ThenGetTokenAsyncReturnsSameValues()
    {
        // Arrange
        var filePath = CreateTempFilePath();
        var storage = new FileTokenStorage(Options.Create(new TokenStorageOptions { FilePath = filePath }), NullLogger<FileTokenStorage>.Instance);
        var token = new TokenData
        {
            AccessToken = "fake-token-for-tests",
            TokenType = "Bearer",
            ExpiresIn = 3600,
            CourierId = "courier-123",
            Role = "Courier"
        };

        // Act
        await storage.StoreTokenAsync(token);
        var result = await storage.GetTokenAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fake-token-for-tests", result.AccessToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(3600, result.ExpiresIn);
        Assert.Equal("courier-123", result.CourierId);
        Assert.Equal("Courier", result.Role);
    }

    [Fact]
    public async Task ClearTokenAsync_WhenCalled_RemovesFileAndLaterGetTokenAsyncReturnsNull()
    {
        // Arrange
        var filePath = CreateTempFilePath();
        var storage = new FileTokenStorage(Options.Create(new TokenStorageOptions { FilePath = filePath }), NullLogger<FileTokenStorage>.Instance);
        var token = new TokenData
        {
            AccessToken = "fake-token-for-tests",
            ExpiresIn = 3600,
            CourierId = "courier-456",
            Role = "Courier"
        };
        await storage.StoreTokenAsync(token);

        // Act
        await storage.ClearTokenAsync();
        var result = await storage.GetTokenAsync();

        // Assert
        Assert.False(File.Exists(filePath));
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTokenAsync_WhenFileIsCorruptOrTruncated_ReturnsNull()
    {
        // Arrange
        var filePath = CreateTempFilePath();
        var storage = new FileTokenStorage(Options.Create(new TokenStorageOptions { FilePath = filePath }), NullLogger<FileTokenStorage>.Instance);
        await File.WriteAllBytesAsync(filePath, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 });

        // Act
        var result = await storage.GetTokenAsync();

        // Assert
        Assert.Null(result);
    }

    // ---- Added in review. Copilot skipped the "not stored in clear text" case that the prompt asked for (the most important
    // security property of this class), the expired-token path, a truly truncated file and clearing when nothing is stored. ----

    [Fact]
    public async Task StoreTokenAsync_WhenTokenIsStored_FileOnDiskDoesNotContainTheTokenInClear()
    {
        // Arrange
        var filePath = CreateTempFilePath();
        var storage = new FileTokenStorage(Options.Create(new TokenStorageOptions { FilePath = filePath }), NullLogger<FileTokenStorage>.Instance);
        var token = new TokenData { AccessToken = "fake-token-for-tests", ExpiresIn = 3600, CourierId = "courier-123", Role = "Courier" };

        // Act
        await storage.StoreTokenAsync(token);

        // Assert
        var onDisk = await File.ReadAllBytesAsync(filePath);
        var asText = System.Text.Encoding.UTF8.GetString(onDisk);
        Assert.NotEmpty(onDisk);
        Assert.DoesNotContain("fake-token-for-tests", asText);
        Assert.DoesNotContain("courier-123", asText);
    }

    [Fact]
    public async Task GetTokenAsync_WhenTokenHasExpired_ReturnsNullAndRemovesTheFile()
    {
        // Arrange: a lifetime of minus one hour is expired the moment it is stored, whatever the clock says.
        var filePath = CreateTempFilePath();
        var storage = new FileTokenStorage(Options.Create(new TokenStorageOptions { FilePath = filePath }), NullLogger<FileTokenStorage>.Instance);
        await storage.StoreTokenAsync(new TokenData { AccessToken = "fake-token-for-tests", ExpiresIn = -3600, CourierId = "courier-123", Role = "Courier" });

        // Act
        var result = await storage.GetTokenAsync();

        // Assert
        Assert.Null(result);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task GetTokenAsync_WhenValidFileIsTruncated_ReturnsNullInsteadOfThrowing()
    {
        // Arrange
        var filePath = CreateTempFilePath();
        var storage = new FileTokenStorage(Options.Create(new TokenStorageOptions { FilePath = filePath }), NullLogger<FileTokenStorage>.Instance);
        await storage.StoreTokenAsync(new TokenData { AccessToken = "fake-token-for-tests", ExpiresIn = 3600, CourierId = "courier-123", Role = "Courier" });
        var whole = await File.ReadAllBytesAsync(filePath);
        await File.WriteAllBytesAsync(filePath, whole.Take(whole.Length / 2).ToArray());

        // Act
        var result = await storage.GetTokenAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task StoreTokenAsync_WhenCalled_SetsTheExpiryFromTheLifetime()
    {
        // Arrange
        var filePath = CreateTempFilePath();
        var storage = new FileTokenStorage(Options.Create(new TokenStorageOptions { FilePath = filePath }), NullLogger<FileTokenStorage>.Instance);
        await storage.StoreTokenAsync(new TokenData { AccessToken = "fake-token-for-tests", ExpiresIn = 3600, CourierId = "courier-123", Role = "Courier" });

        // Act
        var result = await storage.GetTokenAsync();

        // Assert: independent of the current time, only the difference is checked.
        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromSeconds(3600), result.ExpiresAt - result.IssuedAt);
    }

    [Fact]
    public async Task ClearTokenAsync_WhenNothingIsStored_DoesNotThrow()
    {
        // Arrange
        var filePath = CreateTempFilePath();
        var storage = new FileTokenStorage(Options.Create(new TokenStorageOptions { FilePath = filePath }), NullLogger<FileTokenStorage>.Instance);

        // Act
        var exception = await Record.ExceptionAsync(() => storage.ClearTokenAsync());

        // Assert
        Assert.Null(exception);
    }

    public void Dispose()
    {
        foreach (var filePath in _tempFiles.Distinct())
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
