using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FeatureFlag.Api.OpenApi;

/// <summary>
/// Rewrites enum schemas to use string member names instead of integer values.
/// Fixes the default behavior where enums render as integers in the OpenAPI spec.
/// </summary>
internal sealed class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        if (!type.IsEnum)
            return Task.CompletedTask;

        schema.Type = JsonSchemaType.String;
        schema.Format = null;
        schema.Enum = Enum.GetNames(type)
            .Select(name => (JsonNode)JsonValue.Create(name))
            .ToList();

        return Task.CompletedTask;
    }
}
