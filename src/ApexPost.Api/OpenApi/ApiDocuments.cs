using System.Text.RegularExpressions;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using ApexPost.Api.Filters;

namespace ApexPost.Api.OpenApi;

/// <summary>
/// One OpenAPI document per API version, produced by the built-in generator
/// (<c>Microsoft.AspNetCore.OpenApi</c>) and served at <c>/openapi/{documentName}.json</c>.
/// </summary>
/// <remarks>
/// The document names are the Asp.Versioning API explorer group names (<c>GroupNameFormat = 'v'VVV</c>),
/// so the built-in generator puts every operation of version 1 in "v1" and of version 2 in "v2".
/// </remarks>
public static class ApiDocuments
{
    public const string V1 = "v1";
    public const string V2 = "v2";

    public static readonly string[] All = [V1, V2];

    public const string BearerScheme = "Bearer";
    public const string ApiKeyScheme = "ApiKey";

    public static IServiceCollection AddApexPostOpenApi(this IServiceCollection services)
    {
        foreach (var documentName in All)
        {
            services.AddOpenApi(documentName, options =>
            {
                options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;   // widest generator support (NSwag)
                options.AddDocumentTransformer<ApiVersionDocumentTransformer>();
                options.AddOperationTransformer<VersionAndSecurityOperationTransformer>();
            });
        }

        return services;
    }
}

/// <summary>
/// Fills in the title, version, description, contact and security schemes of each version's document.
/// </summary>
internal sealed class ApiVersionDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var provider = context.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();
        var version = provider.ApiVersionDescriptions.FirstOrDefault(d => d.GroupName == context.DocumentName);

        var deprecated = version?.IsDeprecated == true;

        document.Info = new OpenApiInfo
        {
            Title = deprecated ? "ApexPost Logistics API (Deprecated)" : "ApexPost Logistics API",
            Version = version?.ApiVersion.ToString() ?? context.DocumentName,
            Description = deprecated
                ? "RESTful API for managing shipments and offline scan uploads. DEPRECATED: use version 2."
                : "RESTful API for managing shipments and offline scan uploads. Responses use a nested recipient and the latest scan.",
            Contact = new OpenApiContact
            {
                Name = "ApexPost Logistics API Team",
                Email = "api-support@apexpost.example.com",
                Url = new Uri("https://apexpost.example.com")
            },
            License = new OpenApiLicense
            {
                Name = "Proprietary",
                Url = new Uri("https://apexpost.example.com/license")
            }
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[ApiDocuments.BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT access token. In Development get one from POST /dev/token."
        };
        document.Components.SecuritySchemes[ApiDocuments.ApiKeyScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyAuthorizeAttribute.HeaderName,
            Description = "Partner API key for the read-only partner endpoints."
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// Marks operations of a deprecated API version as <c>deprecated: true</c> and attaches the security
/// requirement that the endpoint really enforces (Bearer for [Authorize], ApiKey for [ApiKeyAuthorize]).
/// </summary>
internal sealed partial class VersionAndSecurityOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var provider = context.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();
        var version = provider.ApiVersionDescriptions.FirstOrDefault(d => d.GroupName == context.Description.GroupName);

        if (version?.IsDeprecated == true)
        {
            operation.Deprecated = true;
        }

        // Stable operation ids for SDK generation: "GetShipmentsV2" -> "GetShipments" (each version has its own document).
        if (context.Description.ActionDescriptor.RouteValues.TryGetValue("action", out var action) && !string.IsNullOrEmpty(action))
        {
            operation.OperationId = VersionSuffix().Replace(action, string.Empty);
        }

        var metadata = context.Description.ActionDescriptor.EndpointMetadata ?? Array.Empty<object>();
        var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();

        if (!allowsAnonymous && metadata.OfType<IAuthorizeData>().Any())
        {
            (operation.Security ??= []).Add(Requirement(ApiDocuments.BearerScheme, context));
        }

        if (metadata.OfType<ApiKeyAuthorizeAttribute>().Any())
        {
            (operation.Security ??= []).Add(Requirement(ApiDocuments.ApiKeyScheme, context));
        }

        return Task.CompletedTask;
    }

    [GeneratedRegex("V[0-9]+$")]
    private static partial Regex VersionSuffix();

    private static OpenApiSecurityRequirement Requirement(string schemeId, OpenApiOperationTransformerContext context) => new()
    {
        [new OpenApiSecuritySchemeReference(schemeId, context.Document)] = []
    };
}
