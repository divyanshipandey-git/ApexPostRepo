using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Asp.Versioning.ApiExplorer;
using System.Reflection;
using System.IO;

namespace ApexPost.Api.Extensions;

/// <summary>
/// Configures SwaggerGen options with API version descriptions.
/// </summary>
public sealed class ConfigureSwaggerGenOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerGenOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            var isDeprecated = description.ApiVersion.MajorVersion == 1;
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = isDeprecated ? "ApexPost Logistics API (Deprecated)" : "ApexPost Logistics API",
                Version = description.ApiVersion.ToString(),
                Description = isDeprecated 
                    ? "RESTful API for managing shipments and offline scan uploads (DEPRECATED - Use v2)"
                    : "RESTful API for managing shipments and offline scan uploads with enhanced response format.",
                Contact = new OpenApiContact
                {
                    Name = "ApexPost Logistics API Team",
                    Email = "api-support@apexpost.example.com",
                    Url = new Uri("https://apexpost.example.com")
                }
            });
        }

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
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
    }
}