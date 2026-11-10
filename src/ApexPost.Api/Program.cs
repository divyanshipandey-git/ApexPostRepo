using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using ApexPost.Api.Extensions;
using ApexPost.Api.OpenApi;
using ApexPost.Api.Routing;
using ApexPost.Api.Services;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// WebApplication.CreateBuilder already loads appsettings.json, appsettings.{Environment}.json,
// User Secrets (Development), environment variables and the command line, in that order.
// Secrets (Jwt:SigningKey, ConnectionStrings:ApexPost, PartnerApiKey) are NOT in committed files.

// ============================================================================
// Services - JWT Authentication & Authorization
// ============================================================================
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://apexpost.example.com";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "apexpost-api";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException(
        "Jwt:SigningKey must be configured (dotnet user-secrets set \"Jwt:SigningKey\" \"<at least 32 characters>\" or the Jwt__SigningKey environment variable)");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ClockSkew = TimeSpan.FromMinutes(2),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };

        // Tokens are accepted from the Authorization header only (no query-string tokens in any environment).
        // 401 and 403 are returned as RFC 9457 problem details like every other error.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();

                var http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                http.Response.Headers.WWWAuthenticate = context.AuthenticateFailure is null
                    ? "Bearer"
                    : "Bearer error=\"invalid_token\"";

                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Unauthorized",
                        Detail = context.AuthenticateFailure is null
                            ? "Authentication is required. Send a valid Bearer token."
                            : "The Bearer token is invalid or has expired.",
                        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2"
                    }
                });
            },
            OnForbidden = async context =>
            {
                var http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status403Forbidden;

                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Forbidden",
                        Detail = "Your token does not allow this operation.",
                        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
                    }
                });
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CourierOnly", policy => policy.RequireRole("Courier"))
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    .AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());

// Development-only token service (the /dev/token endpoint is only mapped in Development)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<ITokenService, DevelopmentTokenService>();
}

// ============================================================================
// Services - Controllers, routing constraints, validation
// ============================================================================
builder.Services.Configure<RouteOptions>(options =>
    options.ConstraintMap[TrackingNumberRouteConstraint.Name] = typeof(TrackingNumberRouteConstraint));

// [ApiController] validates every request model with the DataAnnotations on the DTOs (single source of truth)
// and answers invalid input with a 400 ValidationProblemDetails.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict);

// The built-in OpenAPI generator reads this option set: Strict keeps numbers as plain "integer"/"number" in the
// generated documents (otherwise they become "number or string", which breaks client generation).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict);

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "One or more validation errors occurred",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problemDetails)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

// ============================================================================
// Services - API Versioning (Asp.Versioning) and OpenAPI (built-in generator)
// ============================================================================
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = false;   // the version is always in the URL: /api/v1/..., /api/v2/...
    options.ReportApiVersions = true;                      // api-supported-versions / api-deprecated-versions headers
    options.ApiVersionReader = new UrlSegmentApiVersionReader();

    // v1 is deprecated (see [ApiVersion("1.0", Deprecated = true)]) and has a published sunset date.
    // Responses to v1 requests carry the Sunset header.
    options.Policies.Sunset(1.0).Effective(new DateTimeOffset(2027, 7, 1, 0, 0, 0, TimeSpan.Zero));
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";                    // "v1", "v2" = the OpenAPI document names
    options.SubstituteApiVersionInUrl = true;
});

// One document per version: /openapi/v1.json and /openapi/v2.json
builder.Services.AddApexPostOpenApi();

// ============================================================================
// Services - Infrastructure & Application
// ============================================================================
builder.Services.AddSqlInfrastructure(builder.Configuration);
builder.Services.AddScoped<IShipmentApiService, ShipmentApiService>();
builder.Services.AddScoped<IScanUploadService, ScanUploadService>();

// ============================================================================
// Services - Exception Handling & ProblemDetails
// ============================================================================
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        if (builder.Environment.IsDevelopment() && context.Exception is not null)
        {
            context.ProblemDetails.Extensions["stackTrace"] = context.Exception.ToString();
        }
    };
});

// ============================================================================
// Services - CORS (mobile app / portal)
// ============================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiPolicy", policy =>
    {
        policy.WithOrigins(
                "https://localhost:57559",
                "https://portal.apexpost.example.com",
                "https://partner.apexpost.example.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

// ============================================================================
// Services - Rate Limiting
// ============================================================================
static FixedWindowRateLimiterOptions FixedWindow(int permits, int queue) => new()
{
    PermitLimit = permits,
    Window = TimeSpan.FromMinutes(1),
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    QueueLimit = queue
};

static string PartitionKey(HttpContext context) =>
    context.User.Identity?.Name ?? context.Request.Headers.Host.ToString();

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => FixedWindow(100, 10)));

    options.AddPolicy("ApiPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => FixedWindow(100, 10)));

    options.AddPolicy("strict", context =>
        RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => FixedWindow(20, 5)));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ============================================================================
// Services - Health Checks
// ============================================================================
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApexPostDbContext>()
    .AddCheck("self", () => HealthCheckResult.Healthy());

// ============================================================================
// Build App
// ============================================================================
var app = builder.Build();

// ============================================================================
// Middleware pipeline (order matters)
//   exception handler -> HSTS -> HTTPS redirection -> security headers -> status-code pages
//   -> routing -> CORS -> rate limiter -> authentication -> authorization -> endpoints
// ============================================================================
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var isSwaggerUi = context.Request.Path.StartsWithSegments("/swagger");

    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // Version 1 is deprecated
    if (context.Request.Path.StartsWithSegments("/api/v1"))
    {
        context.Response.Headers["Deprecation"] = "true";
    }

    context.Response.Headers["Content-Security-Policy"] = isSwaggerUi
        // The Swagger UI viewer needs inline scripts and styles
        ? "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; frame-ancestors 'none'"
        : "default-src 'self'; frame-ancestors 'none'";

    await next();
});

// Gives every empty 4xx/5xx response (404 for an unknown route, 405, ...) a problem-details body
app.UseStatusCodePages();

app.UseRouting();
app.UseCors("ApiPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// OpenAPI documents + Swagger UI viewer (Development only)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();   // /openapi/v1.json, /openapi/v2.json

    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        foreach (var description in provider.ApiVersionDescriptions.OrderByDescending(d => d.ApiVersion))
        {
            var label = description.IsDeprecated
                ? $"v{description.ApiVersion.MajorVersion} (deprecated)"
                : $"v{description.ApiVersion.MajorVersion}";
            options.SwaggerEndpoint($"/openapi/{description.GroupName}.json", label);
        }

        options.RoutePrefix = "swagger";
        options.DisplayRequestDuration();
        options.EnableDeepLinking();
    });

    app.MapDevelopmentTokenEndpoint();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();

// Make Program accessible for testing
public partial class Program { }
