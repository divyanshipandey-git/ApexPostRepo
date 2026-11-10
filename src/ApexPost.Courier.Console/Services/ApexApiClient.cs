using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Courier.Console.Models;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Console.Services;

/// <summary>
/// Typed HTTP client for the ApexPost v2 API. Only the call the courier app actually makes is exposed;
/// resilience (retry, circuit breaker, timeouts) is applied by the handler pipeline in Program.cs.
/// </summary>
public interface IApexApiClient
{
    Task<ScanUploadResultDto?> UploadScansAsync(string trackingNumber, ScanUploadRequestDto request, CancellationToken cancellationToken = default);
}

public sealed class ApexApiClient : IApexApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApexApiClient> _logger;

    public ApexApiClient(HttpClient httpClient, ILogger<ApexApiClient> logger)
    {
        // BaseAddress, Accept header and timeout are configured where the client is registered (Program.cs),
        // using the platform specific URL from IApiEndpointResolver.
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ScanUploadResultDto?> UploadScansAsync(string trackingNumber, ScanUploadRequestDto request, CancellationToken cancellationToken = default)
    {
        var url = $"api/v2/shipments/{Uri.EscapeDataString(trackingNumber)}/scans";
        using var response = await _httpClient.PostAsJsonAsync(url, request, ApexJsonContext.Default.ScanUploadRequestDto, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
                return null;

            try
            {
                return JsonSerializer.Deserialize(content, ApexJsonContext.Default.ScanUploadResultDto);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize UploadScans response: {Content}", content);
                throw;
            }
        }

        throw await CreateExceptionAsync(response, "UploadScans", cancellationToken);
    }

    private async Task<ApiException> CreateExceptionAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("{Operation} failed with status {StatusCode}: {Content}", operation, (int)response.StatusCode, content);

        ProblemDetailsDto? problem = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(content))
                problem = JsonSerializer.Deserialize(content, ApexJsonContext.Default.ProblemDetailsDto);
        }
        catch (JsonException)
        {
            // Not a ProblemDetails body; fall back to the raw content below.
        }

        return new ApiException(
            problem?.Title ?? $"API Error: {response.StatusCode}",
            (int)response.StatusCode,
            problem?.Detail ?? content,
            problem?.Type);
    }
}

/// <summary>Raised when the API answers with a non-success status.</summary>
public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public string? ProblemType { get; }
    public string? Detail { get; }

    public ApiException(string message, int statusCode, string? detail = null, string? problemType = null)
        : base(message)
    {
        StatusCode = statusCode;
        ProblemType = problemType;
        Detail = detail;
    }
}
