# OpenAPI Enrichment — Spec
**Branch:** `feature/openapi-enrichment`
**Phase:** 1 — Developer Experience
**Status:** Ready for Implementation

---

## Table of Contents

- [User Story](#user-story)
- [Background & Goals](#background--goals)
- [Scope](#scope)
- [Acceptance Criteria](#acceptance-criteria)
  - [AC-1 EvaluationResponse DTO](#ac-1-evaluationresponse-dto)
  - [AC-2 XML Documentation Generation](#ac-2-xml-documentation-generation)
  - [AC-3 XML Doc Comments — Controllers](#ac-3-xml-doc-comments--controllers)
  - [AC-4 XML Doc Comments — DTOs](#ac-4-xml-doc-comments--dtos)
  - [AC-5 ProducesResponseType Attributes](#ac-5-producesresponsetype-attributes)
  - [AC-6 EnumSchemaTransformer](#ac-6-enumschematransformer)
  - [AC-7 ApiInfoTransformer](#ac-7-apiinfotransformer)
  - [AC-8 Scalar UI](#ac-8-scalar-ui)
  - [AC-9 Program.cs Wiring](#ac-9-programcs-wiring)
- [File Layout](#file-layout)
- [Implementation Notes](#implementation-notes)
- [Out of Scope](#out-of-scope)
- [Definition of Done](#definition-of-done)

---

## User Story

> As a developer integrating with FeatureFlagService, I want a self-documenting API
> with a clean interactive UI, accurate schema types, and complete status code
> documentation — so I can integrate without reading source code.

---

## Background & Goals

The current OpenAPI spec at `/openapi/v1.json` is generated but unenriched:

- Enums render as integers (`1`, `2`, `3`) instead of names (`"Percentage"`, `"RoleBased"`)
- No endpoint descriptions, parameter descriptions, or response body descriptions
- Status codes are undocumented — no `400`, `404`, or `500` entries
- The evaluation endpoint returns an anonymous type, which is invisible to the spec generator
- No API-level metadata (title, description, version, contact)
- Swagger UI is the default; Scalar is the preferred modern alternative

This spec closes all of the above as a single Phase 1 Developer Experience task.

---

## Scope

| # | What | File(s) affected |
|---|---|---|
| 1 | `EvaluationResponse` DTO | `Application/DTOs/EvaluationResponse.cs` |
| 2 | Enable XML doc generation | `FeatureFlag.Api.csproj`, `FeatureFlag.Application.csproj` |
| 3 | XML doc comments — controllers | `FeatureFlagsController.cs`, `EvaluationController.cs` |
| 4 | XML doc comments — DTOs | All 5 DTO files |
| 5 | `[ProducesResponseType]` attributes | Both controllers |
| 6 | `EnumSchemaTransformer` | `Api/OpenApi/EnumSchemaTransformer.cs` |
| 7 | `ApiInfoTransformer` | `Api/OpenApi/ApiInfoTransformer.cs` |
| 8 | Scalar UI | `FeatureFlag.Api.csproj`, `Program.cs` |
| 9 | Wire transformers in `Program.cs` | `Program.cs` |

---

## Acceptance Criteria

---

### AC-1: EvaluationResponse DTO

**File:** `FeatureFlag.Application/DTOs/EvaluationResponse.cs`

Create a named DTO to replace the anonymous type currently returned by `EvaluationController`.

```csharp
/// <summary>
/// The result of a feature flag evaluation for a given user context.
/// </summary>
/// <param name="IsEnabled">Whether the feature flag is enabled for the requesting user.</param>
public sealed record EvaluationResponse(
    bool IsEnabled
);
```

**Why:** Anonymous types produce an empty `object` schema in the spec — the most
important endpoint in the API becomes invisible to any consumer reading the spec.

**Update `EvaluationController`:** Replace `return Ok(new { isEnabled })` with:
```csharp
return Ok(new EvaluationResponse(isEnabled));
```

---

### AC-2: XML Documentation Generation

**File:** `FeatureFlag.Api/FeatureFlag.Api.csproj`

Add `<GenerateDocumentationFile>true</GenerateDocumentationFile>` to the `<PropertyGroup>`.

Also add `<NoWarn>$(NoWarn);1591</NoWarn>` to suppress CS1591 warnings for any
public members that intentionally lack XML comments (e.g. `Program`).

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

**File:** `FeatureFlag.Application/FeatureFlag.Application.csproj`

Add the same two properties to the `<PropertyGroup>`.

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

**Why this works in .NET 10:** `Microsoft.AspNetCore.OpenApi` uses a source generator
that automatically picks up XML files from referenced assemblies when those assemblies
also have `GenerateDocumentationFile` set. Because `FeatureFlag.Api` references
`FeatureFlag.Application` via `<ProjectReference>`, DTO comments flow into the spec
with no additional configuration.

---

### AC-3: XML Doc Comments — Controllers

Add `///` doc comments to every public action method on both controllers.
Comments must include `<summary>` and `<response>` tags for each documented status code.

#### `FeatureFlagsController`

```csharp
/// <summary>
/// Retrieves all feature flags for the specified environment.
/// </summary>
/// <param name="environment">The target deployment environment.</param>
/// <param name="ct">Cancellation token.</param>
/// <response code="200">Returns the list of feature flags.</response>
[HttpGet]

/// <summary>
/// Retrieves a single feature flag by name and environment.
/// </summary>
/// <param name="name">The unique name of the feature flag.</param>
/// <param name="environment">The target deployment environment.</param>
/// <param name="ct">Cancellation token.</param>
/// <response code="200">Returns the feature flag.</response>
/// <response code="404">No flag found with the given name in the specified environment.</response>
[HttpGet("{name}")]

/// <summary>
/// Creates a new feature flag.
/// </summary>
/// <param name="request">The flag creation payload.</param>
/// <param name="ct">Cancellation token.</param>
/// <response code="201">Flag created successfully. Returns the created flag.</response>
/// <response code="400">Validation failed. See the errors collection for details.</response>
[HttpPost]

/// <summary>
/// Updates an existing feature flag's enabled state and rollout strategy.
/// </summary>
/// <param name="name">The name of the flag to update.</param>
/// <param name="environment">The target deployment environment.</param>
/// <param name="request">The update payload.</param>
/// <param name="ct">Cancellation token.</param>
/// <response code="204">Flag updated successfully.</response>
/// <response code="400">Validation failed. See the errors collection for details.</response>
/// <response code="404">No flag found with the given name in the specified environment.</response>
[HttpPut("{name}")]

/// <summary>
/// Archives a feature flag (soft delete). The flag is retained for audit history
/// but will no longer appear in active flag queries.
/// </summary>
/// <param name="name">The name of the flag to archive.</param>
/// <param name="environment">The target deployment environment.</param>
/// <param name="ct">Cancellation token.</param>
/// <response code="204">Flag archived successfully.</response>
/// <response code="404">No flag found with the given name in the specified environment.</response>
[HttpDelete("{name}")]
```

#### `EvaluationController`

```csharp
/// <summary>
/// Evaluates whether a feature flag is enabled for a given user context.
/// Evaluation is deterministic — the same user will always receive the same result
/// for a given flag and strategy configuration.
/// </summary>
/// <param name="request">The evaluation context including user identity, roles, and environment.</param>
/// <param name="ct">Cancellation token.</param>
/// <response code="200">Returns the evaluation result.</response>
/// <response code="400">Validation failed. See the errors collection for details.</response>
/// <response code="404">No flag found with the given name in the specified environment.</response>
[HttpPost]
```

---

### AC-4: XML Doc Comments — DTOs

Add `///` doc comments to all 5 DTO record definitions and their properties.
Comments appear in the spec as field descriptions in the schema view.

#### `CreateFlagRequest`

```csharp
/// <summary>
/// Payload for creating a new feature flag.
/// </summary>
/// <param name="Name">The unique name of the feature flag. Alphanumeric, hyphens, and underscores only.</param>
/// <param name="Environment">The deployment environment this flag applies to. Cannot be None.</param>
/// <param name="IsEnabled">Whether the flag is active. Inactive flags always evaluate to false.</param>
/// <param name="StrategyType">The rollout strategy used to evaluate this flag.</param>
/// <param name="StrategyConfig">
/// JSON configuration for the selected strategy. Required when StrategyType is
/// Percentage or RoleBased. Must be a valid JSON object. Maximum 2000 characters.
/// </param>
public sealed record CreateFlagRequest(
    string Name,
    EnvironmentType Environment,
    bool IsEnabled,
    RolloutStrategy StrategyType,
    string StrategyConfig
);
```

#### `UpdateFlagRequest`

```csharp
/// <summary>
/// Payload for updating an existing feature flag's enabled state and rollout strategy.
/// </summary>
/// <param name="IsEnabled">Whether the flag should be active after this update.</param>
/// <param name="StrategyType">The rollout strategy to apply.</param>
/// <param name="StrategyConfig">
/// JSON configuration for the selected strategy. Required when StrategyType is
/// Percentage or RoleBased. Maximum 2000 characters.
/// </param>
public sealed record UpdateFlagRequest(
    bool IsEnabled,
    RolloutStrategy StrategyType,
    string StrategyConfig
);
```

#### `FlagResponse`

```csharp
/// <summary>
/// Represents a feature flag as returned by the API.
/// </summary>
/// <param name="Id">The unique identifier of the flag.</param>
/// <param name="Name">The unique name of the flag within its environment.</param>
/// <param name="Environment">The deployment environment this flag belongs to.</param>
/// <param name="IsEnabled">Whether the flag is currently active.</param>
/// <param name="IsArchived">Whether the flag has been archived (soft-deleted).</param>
/// <param name="StrategyType">The rollout strategy used to evaluate this flag.</param>
/// <param name="StrategyConfig">The raw JSON strategy configuration.</param>
/// <param name="CreatedAt">UTC timestamp when the flag was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
public sealed record FlagResponse(
    Guid Id,
    string Name,
    EnvironmentType Environment,
    bool IsEnabled,
    bool IsArchived,
    RolloutStrategy StrategyType,
    string StrategyConfig,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

#### `EvaluationRequest`

```csharp
/// <summary>
/// Payload for evaluating a feature flag against a user context.
/// </summary>
/// <param name="FlagName">The name of the feature flag to evaluate.</param>
/// <param name="UserId">The unique identifier of the requesting user.</param>
/// <param name="UserRoles">The roles assigned to the requesting user. Used by RoleBased strategy.</param>
/// <param name="Environment">The deployment environment to evaluate the flag in.</param>
public sealed record EvaluationRequest(
    string FlagName,
    string UserId,
    IEnumerable<string> UserRoles,
    EnvironmentType Environment
);
```

#### `EvaluationResponse`

```csharp
/// <summary>
/// The result of a feature flag evaluation for a given user context.
/// </summary>
/// <param name="IsEnabled">Whether the feature flag is enabled for the requesting user.</param>
public sealed record EvaluationResponse(
    bool IsEnabled
);
```

---

### AC-5: ProducesResponseType Attributes

Add `[ProducesResponseType]` attributes to every action method. Use the generic
`[ProducesResponseType<T>]` form where a body is returned. Use the .NET 10
`Description` parameter for inline response descriptions.

All actions that can return `400` should use `ValidationProblemDetails` as the type.

#### `FeatureFlagsController`

```csharp
// GetAll
[ProducesResponseType<IEnumerable<FlagResponse>>(StatusCodes.Status200OK,
    Description = "The list of feature flags for the specified environment.")]

// GetByName
[ProducesResponseType<FlagResponse>(StatusCodes.Status200OK,
    Description = "The requested feature flag.")]
[ProducesResponseType(StatusCodes.Status404NotFound,
    Description = "No flag with the given name exists in the specified environment.")]

// Create
[ProducesResponseType<FlagResponse>(StatusCodes.Status201Created,
    Description = "The newly created feature flag.")]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest,
    Description = "One or more validation errors. See the errors field for details.")]

// Update
[ProducesResponseType(StatusCodes.Status204NoContent,
    Description = "Flag updated successfully.")]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest,
    Description = "One or more validation errors. See the errors field for details.")]
[ProducesResponseType(StatusCodes.Status404NotFound,
    Description = "No flag with the given name exists in the specified environment.")]

// Archive
[ProducesResponseType(StatusCodes.Status204NoContent,
    Description = "Flag archived successfully.")]
[ProducesResponseType(StatusCodes.Status404NotFound,
    Description = "No flag with the given name exists in the specified environment.")]
```

#### `EvaluationController`

```csharp
[ProducesResponseType<EvaluationResponse>(StatusCodes.Status200OK,
    Description = "The evaluation result for the given user context.")]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest,
    Description = "One or more validation errors. See the errors field for details.")]
[ProducesResponseType(StatusCodes.Status404NotFound,
    Description = "No flag with the given name exists in the specified environment.")]
```

---

### AC-6: EnumSchemaTransformer

**File:** `FeatureFlag.Api/OpenApi/EnumSchemaTransformer.cs`

A schema transformer that rewrites any enum schema to use string values instead of
integer values. This fixes the known issue where `RolloutStrategy` and `EnvironmentType`
render as integers in the spec.

**Critical .NET 10 requirement:** `OpenApiAny` was removed in `Microsoft.OpenApi` 2.0.
Enum values must be written using `JsonNode` / `JsonValue`. Do not use `OpenApiString`
or `OpenApiInteger` — they do not exist in .NET 10.

```csharp
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
            .Select(name => (JsonNode)JsonValue.Create(name)!)
            .ToList();

        return Task.CompletedTask;
    }
}
```

---

### AC-7: ApiInfoTransformer

**File:** `FeatureFlag.Api/OpenApi/ApiInfoTransformer.cs`

A document transformer that sets the API title, version, and description at the
top of the OpenAPI document.

```csharp
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FeatureFlag.Api.OpenApi;

/// <summary>
/// Populates the top-level API metadata in the generated OpenAPI document.
/// </summary>
internal sealed class ApiInfoTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "FeatureFlagService API",
            Version = "v1",
            Description =
                "Azure-native, .NET-first feature flag evaluation service. " +
                "Supports percentage rollouts, role-based targeting, and " +
                "deterministic user bucketing. AI-assisted analysis coming in Phase 1.5.",
            Contact = new OpenApiContact
            {
                Name = "FeatureFlagService",
                Url = new Uri("https://github.com/amodelandme/FeatureFlagService")
            }
        };

        return Task.CompletedTask;
    }
}
```

---

### AC-8: Scalar UI

**File:** `FeatureFlag.Api/FeatureFlag.Api.csproj`

Add the Scalar package:

```xml
<PackageReference Include="Scalar.AspNetCore" Version="2.*" />
```

**File:** `Program.cs`

Add `app.MapScalarApiReference()` immediately after `app.MapOpenApi()` in the
development block. Add a redirect from `/scalar` for discoverability:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    // Redirect root to Scalar UI for development convenience
    app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
}
```

Add `using Scalar.AspNetCore;` to the top of `Program.cs`.

**Note on the redirect:** The previous root redirect pointed to `/openapi/v1.json`
(the raw JSON). Replace it with `/scalar/v1` (the Scalar UI). Developers who want
the raw spec can still navigate directly to `/openapi/v1.json`.

---

### AC-9: Program.cs Wiring

**File:** `Program.cs`

Register both transformers inside `AddOpenApi()`. Schema transformers run before
operation transformers — `EnumSchemaTransformer` must be listed before any operation
transformer that might reference enum types.

```csharp
builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer<EnumSchemaTransformer>();
    options.AddDocumentTransformer<ApiInfoTransformer>();
});
```

Add `using FeatureFlag.Api.OpenApi;` to the top of `Program.cs`.

The full updated `Program.cs` should read:

```csharp
using FeatureFlag.Api.OpenApi;
using FeatureFlag.Application;
using FeatureFlag.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer<EnumSchemaTransformer>();
    options.AddDocumentTransformer<ApiInfoTransformer>();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

**Note:** `AddEndpointsApiExplorer()` is removed — it's for Minimal APIs only and
is not needed (or used) in a controller-based API.

---

## File Layout

After this spec is implemented, the new and modified files are:

```
FeatureFlag.Api/
├── OpenApi/
│   ├── EnumSchemaTransformer.cs        ← NEW
│   └── ApiInfoTransformer.cs           ← NEW
├── Controllers/
│   ├── FeatureFlagsController.cs       ← MODIFIED (XML docs + ProducesResponseType)
│   └── EvaluationController.cs         ← MODIFIED (XML docs + ProducesResponseType + EvaluationResponse)
├── FeatureFlag.Api.csproj              ← MODIFIED (GenerateDocumentationFile, NoWarn, Scalar)
└── Program.cs                          ← MODIFIED (transformer wiring, Scalar, redirect)

FeatureFlag.Application/
├── DTOs/
│   ├── CreateFlagRequest.cs            ← MODIFIED (XML docs)
│   ├── UpdateFlagRequest.cs            ← MODIFIED (XML docs)
│   ├── FlagResponse.cs                 ← MODIFIED (XML docs)
│   ├── EvaluationRequest.cs            ← MODIFIED (XML docs)
│   └── EvaluationResponse.cs           ← NEW
└── FeatureFlag.Application.csproj      ← MODIFIED (GenerateDocumentationFile, NoWarn)
```

---

## Implementation Notes

### .NET 10 Breaking Changes — Microsoft.OpenApi 2.0

`Microsoft.AspNetCore.OpenApi` 10.x bundles `Microsoft.OpenApi` 2.0.0, which has three
breaking changes that affect transformer code. All three will silently compile in .NET 9
and fail in .NET 10.

**1. `OpenApiAny` removed — use `JsonNode` instead**

The `OpenApiAny` class and its subtypes (`OpenApiString`, `OpenApiInteger`, etc.) are gone.
```csharp
// ❌ .NET 9 / OpenApi 1.x — DO NOT USE
schema.Enum.Add(new OpenApiString("Percentage"));

// ✅ .NET 10 / OpenApi 2.0 — CORRECT
schema.Enum.Add(JsonValue.Create("Percentage")!);
```

**2. `schema.Type` is no longer a string**

`OpenApiSchema.Type` changed from `string?` to `JsonSchemaType` — a `[Flags]` enum
in the `Microsoft.OpenApi` namespace.
```csharp
// ❌ .NET 9 / OpenApi 1.x
schema.Type = "string";

// ✅ .NET 10 / OpenApi 2.0
schema.Type = JsonSchemaType.String;
```

The flags design allows combined types: `JsonSchemaType.String | JsonSchemaType.Null`
is valid and maps directly to nullable field semantics in OpenAPI 3.1.

**3. Model types moved to the root namespace**

All model types (`OpenApiDocument`, `OpenApiSchema`, `OpenApiInfo`, etc.) moved from
`Microsoft.OpenApi.Models` to the root `Microsoft.OpenApi` namespace.
```csharp
// ❌ Does not compile in .NET 10
using Microsoft.OpenApi.Models;

// ✅ Correct
using Microsoft.OpenApi;
```
---

## Out of Scope

- Scalar UI customization (theme, logo, custom CSS) — deferred
- OpenAPI 3.1 upgrade — default is 3.0, 3.1 opt-in deferred
- JWT security scheme in the OpenAPI doc — Phase 3 (auth not yet built)
- Example values on request bodies — Phase 1.5 or later
- Build-time OpenAPI document generation — Phase 8 (CI/CD)

---

## Definition of Done

- [ ] `EvaluationResponse` DTO created and wired into `EvaluationController`
- [ ] `GenerateDocumentationFile` and `NoWarn` set on both `Api` and `Application` projects
- [ ] All action methods on both controllers have `///` XML doc comments
- [ ] All 5 DTOs have `///` XML doc comments on the record and each property
- [ ] All action methods have `[ProducesResponseType]` attributes covering all response codes
- [ ] `EnumSchemaTransformer` created using `JsonNode` (not `OpenApiAny`)
- [ ] `ApiInfoTransformer` created with title, version, description, contact
- [ ] `Scalar.AspNetCore` package added and `MapScalarApiReference()` called
- [ ] Root redirect updated from `/openapi/v1.json` to `/scalar/v1`
- [ ] Transformers registered in `AddOpenApi()` in `Program.cs`
- [ ] `AddEndpointsApiExplorer()` removed from `Program.cs`
- [ ] Build: `dotnet build FeatureFlagService.sln` → 0 errors, 0 warnings
- [ ] All existing tests passing: `dotnet test`
- [ ] Spec visible at `/scalar/v1` with correct enum names, endpoint descriptions, and response codes
- [ ] Raw JSON spec still accessible at `/openapi/v1.json`