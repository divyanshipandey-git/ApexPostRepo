using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApexPost.Courier.Console.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CA1416 // This call site is reachable on all platforms. ProtectedData is only supported on Windows.
// The console app is Windows-only and uses DPAPI for secure token storage.

namespace ApexPost.Courier.Console.Services;

/// <summary>
/// Secure token storage for console application using encrypted file storage.
/// </summary>
public interface ITokenStorage
{
    Task<TokenData?> GetTokenAsync();
    Task StoreTokenAsync(TokenData token);
    Task ClearTokenAsync();
    Task<bool> HasValidTokenAsync();
}

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
            
            var token = JsonSerializer.Deserialize<TokenData>(json, JsonOptions.Default);
            
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

            var json = JsonSerializer.Serialize(token, JsonOptions.Default);
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

    public async Task ClearTokenAsync()
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

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
}