using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation.AspNetCore;
using ApexPost.Api.DTOs;
using ApexPost.Api.Validation;
using ApexPost.Api.Endpoints;
using ApexPost.Api.Services;
using ApexPost.Api.Extensions;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Infrastructure;
using ApexPost.Application;
using Swashbuckle.AspNetCore.SwaggerGen;
using AspVersioning = Asp.Versioning;
using AspVersioningApiExplorer = Asp.Versioning.ApiExplorer;
using AspVersioningBuilder = Asp.Versioning.Builder;
using AspVersioningApiVersionReader = Asp.Versioning.ApiVersionReader;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// Configuration
// ============================================================================
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

// ============================================================================
// Services - JWT Authentication & Authorization
// ============================================================================
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://apexpost.example.com";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "apexpost-api";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey must be configured in User Secrets or appsettings.json");

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
        
        // Development: allow token from query string for easier testing
        if (builder.Environment.IsDevelopment())
        {
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var token = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(token))
                    {
                        context.Token = token;
                    }
                    return Task.CompletedTask;
                }
            };
        }
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CourierOnly", policy => policy.RequireRole("Courier"))
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    .AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser())
    .AddPolicy("CourierIdMatch", policy => 
        policy.RequireAssertion(context =>
        {
            var courierIdClaim = context.User.FindFirst("courierId")?.Value;
            var routeCourierId = context.Resource as HttpContext;
            if (routeCourierId?.Request.RouteValues.TryGetValue("courierId", out var routeValue) == true)
            {
                return string.Equals(courierIdClaim, routeValue?.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            // For scan upload, check the request body
            return true; // Detailed check in the endpoint
        }));

// Development-only token endpoint for easy testing
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<ITokenService, DevelopmentTokenService>();
}

// ============================================================================
// Services - API Versioning (Asp.Versioning)
// ============================================================================
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new AspVersioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = AspVersioningApiVersionReader.Combine(
        new CustomUrlSegmentApiVersionReader(1), // segment index 1: /api/v{version}/...
        new AspVersioning.HeaderApiVersionReader("x-api-version"),
        new AspVersioning.QueryStringApiVersionReader("api-version"));
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
});

// ============================================================================
// Services - OpenAPI / Swagger
// ============================================================================
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "ApexPost Logistics API";
        document.Info.Description = "RESTful API for managing shipments and offline scan uploads for ApexPost Logistics courier operations.";
        document.Info.Version = "v1";
        document.Info.Contact = new OpenApiContact
        {
            Name = "ApexPost Logistics API Team",
            Email = "api-support@apexpost.example.com",
            Url = new Uri("https://apexpost.example.com")
        };
        document.Info.License = new OpenApiLicense
        {
            Name = "Proprietary",
            Url = new Uri("https://apexpost.example.com/license")
        };
        return Task.CompletedTask;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

builder.Services.AddSingleton<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerGenOptions>();

// ============================================================================
// Services - Infrastructure & Application
// ============================================================================
builder.Services.AddSqlInfrastructure(builder.Configuration);

// ============================================================================
// Services - Custom API Services
// ============================================================================
builder.Services.AddScoped<IShipmentApiService, ShipmentApiService>();
builder.Services.AddScoped<IScanUploadService, ScanUploadService>();

// ============================================================================
// Services - Validation
// ============================================================================
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddScoped<FluentValidation.IValidator<CreateShipmentRequest>, CreateShipmentRequestValidator>();
builder.Services.AddScoped<FluentValidation.IValidator<UpdateShipmentRequest>, UpdateShipmentRequestValidator>();
builder.Services.AddScoped<FluentValidation.IValidator<UpdateStatusRequest>, UpdateStatusRequestValidator>();
builder.Services.AddScoped<FluentValidation.IValidator<ScanUploadRequest>, ScanUploadRequestValidator>();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "One or more validation errors occurred",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };
        return new BadRequestObjectResult(problemDetails)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

// ============================================================================
// Services - Exception Handling
// ============================================================================
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        
        if (builder.Environment.IsDevelopment())
        {
            context.ProblemDetails.Extensions["stackTrace"] = context.Exception?.ToString();
        }
    };
});

// ============================================================================
// Services - CORS (configure for mobile app / portal)
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
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));
    
    options.AddPolicy("ApiPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));
    
    options.AddPolicy("strict", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));
    
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
// Middleware Pipeline
// ============================================================================

// Global exception handler (must be first)
app.UseExceptionHandler();

// Rate limiting
app.UseRateLimiter();

// CORS
app.UseCors("ApiPolicy");

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Security headers
app.Use(async (context, next) =>
{
    var isSwagger = context.Request.Path.StartsWithSegments("/swagger");

    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // Allow Swagger UI resources
    if (isSwagger)
    {
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +  // Swagger UI needs inline scripts
            "style-src 'self' 'unsafe-inline'; " +                  // Swagger UI needs inline styles
            "img-src 'self' data:; " +                              // Swagger UI images
            "font-src 'self' data:; " +                             // Swagger UI fonts
            "frame-ancestors 'none'";
    }
    else
    {
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; frame-ancestors 'none'";
    }

    await next();
});
// HTTPS redirection
app.UseHttpsRedirection();

// HSTS (only in non-Development)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// OpenAPI / Swagger (only in Development)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<AspVersioningApiExplorer.IApiVersionDescriptionProvider>();
        foreach (var description in provider.ApiVersionDescriptions)
        {
            var label = description.ApiVersion.MajorVersion == 1 
                ? $"v{description.ApiVersion} (deprecated)" 
                : $"v{description.ApiVersion}";
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", label);
        }
        options.RoutePrefix = "swagger";
        options.DisplayRequestDuration();
        options.EnableDeepLinking();
        options.EnableTryItOutByDefault();
    });
}

// Health checks
app.MapHealthChecks("/health");

// ============================================================================
// API Endpoints with Versioning
// ============================================================================
var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new AspVersioning.ApiVersion(1, 0))
    .HasApiVersion(new AspVersioning.ApiVersion(2, 0))
    .ReportApiVersions()
    .Build();

var v1Group = app.MapGroup("/api/v1")
    .WithApiVersionSet(apiVersionSet)
    .MapToApiVersion(new AspVersioning.ApiVersion(1, 0))
    .RequireRateLimiting("ApiPolicy");

var v2Group = app.MapGroup("/api/v2")
    .WithApiVersionSet(apiVersionSet)
    .MapToApiVersion(new AspVersioning.ApiVersion(2, 0))
    .RequireRateLimiting("ApiPolicy");

// Map endpoints for both versions
ShipmentEndpoints.Map(v1Group, "1");
ScanEndpoints.Map(v1Group, "1");
ShipmentEndpoints.Map(v2Group, "2");
ScanEndpoints.Map(v2Group, "2");

// ============================================================================
// Development Token Endpoint (Development only)
// ============================================================================
if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/token", (ITokenService tokenService, TokenRequest request) =>
    {
        if (string.IsNullOrWhiteSpace(request.CourierId))
        {
            return Results.BadRequest(new { error = "CourierId is required" });
        }
        
        var role = string.IsNullOrWhiteSpace(request.Role) ? "Courier" : request.Role;
        if (role != "Courier" && role != "Admin")
        {
            return Results.BadRequest(new { error = "Role must be 'Courier' or 'Admin'" });
        }
        
        var token = tokenService.IssueToken(request.CourierId, role);
        return Results.Ok(new { access_token = token, token_type = "Bearer", expires_in = 28800 });
    })
    .WithName("GetDevToken")
    .WithSummary("Get a development JWT token")
    .WithDescription("Returns a JWT token for testing. Only available in Development environment.")
    .Produces<TokenResponse>(StatusCodes.Status200OK)
    .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
    .AllowAnonymous();
}

// ============================================================================
// Partner API Endpoint (v2 only, API Key authenticated)
// ============================================================================
var partnerGroup = app.MapGroup("/api/v2/partner")
    .WithApiVersionSet(apiVersionSet)
    .MapToApiVersion(new AspVersioning.ApiVersion(2, 0))
    .RequireRateLimiting("ApiPolicy")
    .AddEndpointFilter<ApiKeyEndpointFilter>();

partnerGroup.MapGet("/shipments/{trackingNumber}", async (
    string trackingNumber,
    IShipmentApiService service,
    CancellationToken cancellationToken) =>
{
    var shipment = await service.GetShipmentAsync(trackingNumber, cancellationToken);
    return shipment is not null ? Results.Ok(shipment) : Results.NotFound();
})
.WithName("GetPartnerShipmentV2")
.WithSummary("Get shipment by tracking number (Partner API)")
.WithDescription("Read-only endpoint for partners to retrieve shipment details. Requires X-Api-Key header.")
.Produces<ShipmentDetail>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
.Produces<ProblemDetails>(StatusCodes.Status500InternalServerError)
.AllowAnonymous(); // Auth is handled by the ApiKeyEndpointFilter

// ============================================================================
// Run
// ============================================================================
app.Run();

// Make Program accessible for testing
public partial class Program { }