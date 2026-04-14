using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bandera.Api.OpenApi;

/// <summary>
/// Populates the top-level API metadata in the generated OpenAPI document.
/// </summary>
internal sealed class ApiInfoTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        document.Info = new OpenApiInfo
        {
            Title = "Bandera API",
            Version = "v1",
            Description =
                "Azure-native, .NET-first feature flag evaluation service. "
                + "Supports percentage rollouts, role-based targeting, and "
                + "deterministic user bucketing. AI-assisted analysis coming in Phase 1.5.",
            Contact = new OpenApiContact
            {
                Name = "Bandera",
                Url = new Uri("https://github.com/amodelandme/Bandera"),
            },
        };

        return Task.CompletedTask;
    }
}
