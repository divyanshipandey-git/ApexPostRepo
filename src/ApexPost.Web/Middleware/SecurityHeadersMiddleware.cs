using System.Security.Cryptography;

namespace ApexPost.Web.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string CspNonceItemKey = "ApexPost.CspNonce";

    public async Task InvokeAsync(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        context.Items[CspNonceItemKey] = nonce;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.ContentSecurityPolicy =
                $"default-src 'self'; script-src 'self' 'nonce-{nonce}'; style-src 'self'; img-src 'self' data:; " +
                "object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            return Task.CompletedTask;
        });

        await next(context);
    }
}
