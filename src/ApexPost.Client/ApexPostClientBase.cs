using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApexPost.Client;

/// <summary>
/// Base class for the generated ApexPost API client.
/// Provides authentication and common functionality.
/// </summary>
public abstract class ApexPostClientBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApexPostClientBase>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApexPostClientBase"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">Optional logger.</param>
    protected ApexPostClientBase(HttpClient httpClient, ILogger<ApexPostClientBase>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    /// <summary>
    /// Gets the underlying HTTP client.
    /// </summary>
    protected HttpClient HttpClient => _httpClient;

    /// <summary>
    /// Sets the authentication token for API requests.
    /// </summary>
    /// <param name="token">The JWT token.</param>
    public void SetAuthenticationToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <summary>
    /// Clears the authentication token.
    /// </summary>
    public void ClearAuthenticationToken()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Sends a request and handles common error scenarios.
    /// </summary>
    /// <typeparam name="T">The response type.</typeparam>
    /// <param name="requestFunc">The request function.</param>
    /// <returns>The response data.</returns>
    /// <exception cref="ApiException">Thrown when the API returns an error response.</exception>
    protected async Task<T> SendAsync<T>(Func<Task<HttpResponseMessage>> requestFunc)
    {
        var response = await requestFunc();
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<T>(content, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }

        var errorContent = await response.Content.ReadAsStringAsync();
        var errorMessage = $"API request failed with status {(int)response.StatusCode}: {response.ReasonPhrase}";
        
        if (!string.IsNullOrWhiteSpace(errorContent))
        {
            errorMessage += $". Details: {errorContent}";
        }

        throw new ApiException((int)response.StatusCode, errorMessage, errorContent);
    }
}

/// <summary>
/// Extension methods for registering the ApexPost client.
/// </summary>
public static class ApexPostClientExtensions
{
    /// <summary>
    /// Registers the ApexPost API client in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The base address of the API.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApexPostClient(this IServiceCollection services, string baseAddress)
    {
        services.AddHttpClient<ApexPostClientBase>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}

/// <summary>
/// Exception thrown when an API request fails.
/// </summary>
public class ApiException : Exception
{
    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the error response content.
    /// </summary>
    public string? ResponseContent { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="responseContent">The response content.</param>
    public ApiException(int statusCode, string message, string? responseContent = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }
}