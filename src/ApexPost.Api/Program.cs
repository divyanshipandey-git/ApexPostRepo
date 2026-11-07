using System.Reflection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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
// Services - API Versioning (Asp.Versioning)
// ============================================================================
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new AspVersioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = AspVersioningApiVersionReader.Combine(
        new AspVersioning.UrlSegmentApiVersionReader(),
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
// Run
// ============================================================================
app.Run();

// Make Program accessible for testing
public partial class Program { }