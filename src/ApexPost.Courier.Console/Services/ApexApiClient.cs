using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApexPost.Api.DTOs;
using ApexPost.Courier.Console.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Courier.Console.Services;

/// <summary>
/// Typed HTTP client for calling the ApexPost v2 API with resilience policies.
/// </summary>
public interface IApexApiClient
{
    Task<ShipmentDetail?> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken cancellationToken = default);
    Task<ShipmentDetail?> GetShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default);
    Task<PagedResponse<ShipmentSummary>?> GetShipmentsAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<ShipmentDetail?> UpdateShipmentAsync(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateShipmentStatusAsync(string trackingNumber, UpdateStatusRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default);
    Task<ScanUploadResponse?> UploadScansAsync(string trackingNumber, ScanUploadRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScanSummary>?> GetScanHistoryAsync(string trackingNumber, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration options for the API client.
/// </summary>
public sealed class ApexApiClientOptions
{
    public string BaseUrl { get; set; } = "https://localhost:54402";
}

public sealed class ApexApiClient : IApexApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApexApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApexApiClient(HttpClient httpClient, IOptions<ApexApiClientOptions> options, ILogger<ApexApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var baseUrl = options.Value?.BaseUrl ?? "https://localhost:54402";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<ShipmentDetail?> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v2/shipments", request, _jsonOptions, cancellationToken);
        return await HandleResponseAsync<ShipmentDetail>(response, "CreateShipment");
    }

    public async Task<ShipmentDetail?> GetShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v2/shipments/{Uri.EscapeDataString(trackingNumber)}", cancellationToken);
        return await HandleResponseAsync<ShipmentDetail>(response, "GetShipment");
    }

    public async Task<PagedResponse<ShipmentSummary>?> GetShipmentsAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v2/shipments?page={page}&pageSize={pageSize}", cancellationToken);
        return await HandleResponseAsync<PagedResponse<ShipmentSummary>>(response, "GetShipments");
    }

    public async Task<ShipmentDetail?> UpdateShipmentAsync(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v2/shipments/{Uri.EscapeDataString(trackingNumber)}")
        {
            Content = JsonContent.Create(request, options: _jsonOptions)
        };
        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        return await HandleResponseAsync<ShipmentDetail>(response, "UpdateShipment");
    }

    public async Task<bool> UpdateShipmentStatusAsync(string trackingNumber, UpdateStatusRequest request, CancellationToken cancellationToken = default)
    {
        var requestMessage = new HttpRequestMessage(new HttpMethod("PATCH"), $"/api/v2/shipments/{Uri.EscapeDataString(trackingNumber)}/status")
        {
            Content = JsonContent.Create(request, options: _jsonOptions)
        };
        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        
        if (response.StatusCode == HttpStatusCode.NoContent)
            return true;

        await HandleErrorResponse(response, "UpdateShipmentStatus");
        return false;
    }

    public async Task<bool> DeleteShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v2/shipments/{Uri.EscapeDataString(trackingNumber)}", cancellationToken);
        
        if (response.StatusCode == HttpStatusCode.NoContent)
            return true;

        await HandleErrorResponse(response, "DeleteShipment");
        return false;
    }

    public async Task<ScanUploadResponse?> UploadScansAsync(string trackingNumber, ScanUploadRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/v2/shipments/{Uri.EscapeDataString(trackingNumber)}/scans", request, _jsonOptions, cancellationToken);
        return await HandleResponseAsync<ScanUploadResponse>(response, "UploadScans");
    }

    public async Task<IReadOnlyList<ScanSummary>?> GetScanHistoryAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v2/shipments/{Uri.EscapeDataString(trackingNumber)}/scans", cancellationToken);
        return await HandleResponseAsync<IReadOnlyList<ScanSummary>>(response, "GetScanHistory");
    }

    private async Task<T?> HandleResponseAsync<T>(HttpResponseMessage response, string operationName) where T : class
    {
        if (response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NoContent)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
                return null;

            try
            {
                return JsonSerializer.Deserialize<T>(content, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize {Operation} response: {Content}", operationName, content);
                throw;
            }
        }

        await HandleErrorResponse(response, operationName);
        return null;
    }

    private async Task HandleErrorResponse(HttpResponseMessage response, string operationName)
    {
        var content = await response.Content.ReadAsStringAsync();
        
        _logger.LogWarning("{Operation} failed with status {StatusCode}: {Content}", operationName, response.StatusCode, content);

        try
        {
            var problemDetails = JsonSerializer.Deserialize<Microsoft.AspNetCore.Mvc.ProblemDetails>(content, _jsonOptions);
            if (problemDetails != null)
            {
                throw new ApiException(problemDetails.Title ?? "API Error", 
                    (int)response.StatusCode, 
                    problemDetails.Detail ?? content,
                    problemDetails.Type);
            }
        }
        catch (JsonException)
        {
            // Not a ProblemDetails response
        }

        throw new ApiException($"API Error: {response.StatusCode}", 
            (int)response.StatusCode, 
            content);
    }
}

/// <summary>
/// Exception type for API errors with ProblemDetails information.
/// </summary>
public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public string? ProblemType { get; }

    public ApiException(string message, int statusCode, string? detail = null, string? problemType = null)
        : base(message)
    {
        StatusCode = statusCode;
        ProblemType = problemType;
        
        if (!string.IsNullOrEmpty(detail))
        {
            Data["Detail"] = detail;
        }
    }
}