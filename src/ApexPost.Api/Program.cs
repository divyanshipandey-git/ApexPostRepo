using System.Reflection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
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
// Services - API Versioning
// ============================================================================
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("x-api-version"),
        new QueryStringApiVersionReader("api-version"));
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ============================================================================
// Services - OpenAPI / Swagger
// ============================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ApexPost Shipments API",
        Version = "v1",
        Description = "RESTful API for managing shipments and offline scan uploads",
        Contact = new OpenApiContact
        {
            Name = "ApexPost Logistics",
            Email = "api-support@apexpost.example.com"
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter JWT token"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ============================================================================
// Services - Infrastructure & Application
// ============================================================================
builder.Services.AddInfrastructure(builder.Configuration);

// Register tracked shipment repository
builder.Services.AddScoped<ITrackedShipmentRepository, SqlTrackedShipmentRepository>();

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

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'";
    await next();
});

// HTTPS redirection
app.UseHttpsRedirection();

// Swagger / OpenAPI (only in Development/Staging)
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ApexPost Shipments API v1");
        options.RoutePrefix = "swagger";
        options.DisplayRequestDuration();
        options.EnableDeepLinking();
    });
}

// Health checks
app.MapHealthChecks("/health");

// ============================================================================
// API Endpoints
// ============================================================================
var apiGroup = app.MapGroup("/api/v1")
    .RequireRateLimiting("ApiPolicy");

ShipmentEndpoints.Map(apiGroup);
ScanEndpoints.Map(apiGroup);

// ============================================================================
// Run
// ============================================================================
app.Run();

// Make Program accessible for testing
public partial class Program { }