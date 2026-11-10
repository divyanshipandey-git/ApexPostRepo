using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApexPost.Courier.Console.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace ApexPost.Courier.Console.Services;

/// <summary>
/// Secure token storage. This is the console-app equivalent of MAUI <c>SecureStorage</c>: the token is never
/// written in clear text; it is encrypted with Windows DPAPI (<see cref="DataProtectionScope.CurrentUser"/>),
/// so only the same Windows user on the same machine can read it. <see cref="ITokenStorage"/> is the seam: a MAUI
/// head would register a SecureStorage-backed implementation of the same interface without touching callers.
/// </summary>
public interface ITokenStorage
{
    Task<TokenData?> GetTokenAsync();
    Task StoreTokenAsync(TokenData token);
    Task ClearTokenAsync();
    Task<bool> HasValidTokenAsync();
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class FileTokenStorage : ITokenStorage
{
    private readonly string _filePath;
    private readonly byte[] _entropy;
    private readonly ILogger<FileTokenStorage> _logger;

    public FileTokenStorage(IOptions<TokenStorageOptions> options, ILogger<FileTokenStorage> logger)
    {
        _filePath = options.Value.FilePath ?? "courier_token.json";
        _entropy = Encoding.UTF8.GetBytes("ApexPost-Courier-Token-Storage-v1");
        _logger = logger;
    }

    public async Task<TokenData?> GetTokenAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            var encryptedData = await File.ReadAllBytesAsync(_filePath);
            var jsonBytes = ProtectedData.Unprotect(encryptedData, _entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(jsonBytes);
            
            var token = JsonSerializer.Deserialize(json, ApexJsonContext.Default.TokenData);
            
            if (token?.IsExpired == true)
            {
                _logger.LogInformation("Stored token has expired");
                await ClearTokenAsync();
                return null;
            }

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read token from storage");
            return null;
        }
    }

    public async Task StoreTokenAsync(TokenData token)
    {
        try
        {
            token.IssuedAt = DateTimeOffset.UtcNow;
            token.ExpiresAt = token.IssuedAt.AddSeconds(token.ExpiresIn);

            var json = JsonSerializer.Serialize(token, ApexJsonContext.Default.TokenData);
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var encryptedData = ProtectedData.Protect(jsonBytes, _entropy, DataProtectionScope.CurrentUser);
            
            await File.WriteAllBytesAsync(_filePath, encryptedData);
            _logger.LogInformation("Token stored securely for courier {CourierId}", token.CourierId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store token");
            throw;
        }
    }

    public Task ClearTokenAsync()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
                _logger.LogInformation("Token cleared from storage");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear token file");
        }

        return Task.CompletedTask;
    }

    public async Task<bool> HasValidTokenAsync()
    {
        var token = await GetTokenAsync();
        return token != null && !token.IsExpired;
    }
}

public sealed class TokenStorageOptions
{
    public string FilePath { get; set; } = "courier_token.json";
}
