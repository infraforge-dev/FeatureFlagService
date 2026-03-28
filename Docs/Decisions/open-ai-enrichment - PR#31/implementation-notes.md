# OpenAPI Enrichment — Implementation Notes

**Session date:** 2026-03-28
**Branch:** `feature/openapi-enrichment`
**Spec reference:** `Docs/Decisions/open-ai-enrichment - PR#31/spec.md`
**Build status:** Passed — 0 warnings, 0 errors
**Tests:** 8/8 passing
**PR:** #31

---

## Deviations from Spec

### DEV-001 — `OpenApiAny` Removal Extends to `schema.Type`

**Spec says:** Use `JsonNode`/`JsonValue` instead of `OpenApiString`/`OpenApiInteger`.

**What actually changed:** In `Microsoft.OpenApi` 2.0.0 (used by `Microsoft.AspNetCore.OpenApi` 10.x), the `OpenApiSchema.Type` property also changed — from `string?` to `JsonSchemaType?` (a flags enum). The spec's example `schema.Type = "string"` compiles in .NET 9 but fails in .NET 10.

**Fix applied:** `schema.Type = JsonSchemaType.String;` using the `Microsoft.OpenApi` namespace.

**Why it matters:** The spec's `.NET 10 Breaking Change` section correctly identified `OpenApiAny` removal but did not capture this second breaking change in `schema.Type`. Both are part of the same OpenApi 2.0 upgrade.

---

### DEV-002 — `Microsoft.OpenApi.Models` Namespace Removed

**Spec says:** `using Microsoft.OpenApi.Models;` (implied by `OpenApiDocument`, `OpenApiSchema`, etc.)

**What actually changed:** In `Microsoft.OpenApi` 2.0.0, all model types moved from `Microsoft.OpenApi.Models` into the root `Microsoft.OpenApi` namespace. `using Microsoft.OpenApi.Models;` does not compile.

**Fix applied:** Both transformer files use `using Microsoft.OpenApi;` instead.

---

### DEV-003 — Inline XML Doc Comments on Positional Record Parameters

**Spec shows:** Inline `///` comments before each parameter inside the primary constructor parentheses:
```csharp
public sealed record CreateFlagRequest(
    /// <summary>The unique name...</summary>
    string Name,
```

**What actually happens:** This generates CS1587 ("XML comment is not placed on a valid language element") because the C# compiler does not treat inline `///` before positional record constructor parameters as valid XML doc targets.

**Fix applied:** All DTO XML docs use `<param name="...">` tags on the record's summary block — the correct C# pattern for documenting positional record properties:
```csharp
/// <summary>Payload for creating a new feature flag.</summary>
/// <param name="Name">The unique name...</param>
public sealed record CreateFlagRequest(string Name, ...);
```

This is semantically equivalent and correctly flows into the XML documentation file consumed by the OpenAPI source generator.

---

### DEV-004 — Pre-existing CS1574 in `FeatureEvaluator.cs`

**Root cause:** Enabling `GenerateDocumentationFile` on `FeatureFlag.Application` surfaced a pre-existing CS1574 warning: a `<see cref="FeatureFlagService"/>` reference that could not be resolved without a qualified name.

**Fix applied:** Updated to `<see cref="Services.FeatureFlagService"/>`, which resolves correctly within the `FeatureFlag.Application` assembly.

---

## Key Decisions

### `JsonSchemaType.String` vs `"string"`

In OpenApi 2.0, `schema.Type` is a `[Flags]` enum (`JsonSchemaType`) supporting bitwise combinations. Using `JsonSchemaType.String` is both the correct approach and more type-safe than string assignment. This is the idiomatic .NET 10 pattern going forward.

### `<param>` tags vs inline comments for positional records

The `<param>` approach is the correct C# pattern and produces identical output in the generated XML documentation file. The OpenAPI source generator reads property-level docs from the XML file regardless of which approach is used. This is not a functional deviation — only a syntactic one.

---

## Build Verification

- `dotnet build FeatureFlagService.sln` → **0 errors, 0 warnings**
- `dotnet test` → **8/8 passing**
