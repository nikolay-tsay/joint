using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Joint.Api.Common;

/// <summary>OpenAPI document and the Scalar UI for trying endpoints by hand.</summary>
public static class ApiDocs
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddApiDocs(this IServiceCollection services) =>
        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            // Lets Scalar send a Supabase access token with each request.
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
            };
            document.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [],
                },
            ];
            return Task.CompletedTask;
        }));

    /// <summary>Serves the document at /openapi/v1.json and the UI at /scalar, outside production only.</summary>
    public static void MapApiDocs(this WebApplication app)
    {
        if (app.Environment.IsProduction())
        {
            return;
        }

        app.MapOpenApi();
        app.MapScalarApiReference(options => options.WithTitle("Joint API"));
    }
}
