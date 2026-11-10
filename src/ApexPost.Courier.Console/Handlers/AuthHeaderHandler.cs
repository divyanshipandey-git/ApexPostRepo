using System.Net.Http.Headers;
using ApexPost.Courier.Console.Services;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Console.Handlers;

/// <summary>
/// DelegatingHandler that attaches the Bearer token from secure storage to outgoing requests.
/// </summary>
public sealed class AuthHeaderHandler : DelegatingHandler
{
    private readonly ITokenStorage _tokenStorage;
    private readonly ILogger<AuthHeaderHandler> _logger;

    public AuthHeaderHandler(ITokenStorage tokenStorage, ILogger<AuthHeaderHandler> logger)
    {
        _tokenStorage = tokenStorage ?? throw new ArgumentNullException(nameof(tokenStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenStorage.GetTokenAsync();
        
        if (token != null && !string.IsNullOrEmpty(token.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            _logger.LogDebug("Attached Bearer token to request: {Method} {Uri}", request.Method, request.RequestUri);
        }
        else
        {
            _logger.LogWarning("No valid token available for request: {Method} {Uri}", request.Method, request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}