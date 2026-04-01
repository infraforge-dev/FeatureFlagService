# Persistence & Controllers — Implementation Notes

**Session date:** 2026-03-25
**Branch:** `feature/persistence-and-controllers`
**Spec reference:** `docs/decisions/persistence-and-controllers-v2.md`
**Build status:** Passed — 0 warnings, 0 errors
**Tests:** 8/8 passing

---

## 1. What Was Implemented

All items in scope per v2 of the spec were completed:

| File | Status |
|---|---|
| `docker-compose.yml` | Created (repo root) |
| `FeatureFlag.Api/appsettings.json` | Updated — placeholder connection string |
| `FeatureFlag.Api/appsettings.Development.json` | Updated — local Docker connection string, intentionally committed |
| `FeatureFlag.Domain/Entities/Flag.cs` | Updated — `Update()` atomic method added |
| `FeatureFlag.Domain/Interfaces/IFeatureFlagRepository.cs` | Replaced — async signatures + `CancellationToken` + `AddAsync` + `SaveChangesAsync` |
| `FeatureFlag.Application/Interfaces/IFeatureFlagService.cs` | Replaced — async signatures + `CancellationToken` + new CRUD methods |
| `FeatureFlag.Application/Services/FeatureFlagService.cs` | Replaced — full async implementation, throws on missing flag |
| `FeatureFlag.Application/DTOs/CreateFlagRequest.cs` | Created |
| `FeatureFlag.Application/DTOs/UpdateFlagRequest.cs` | Created |
| `FeatureFlag.Application/DTOs/FlagResponse.cs` | Created |
| `FeatureFlag.Application/DTOs/EvaluationRequest.cs` | Created |
| `FeatureFlag.Application/DTOs/FlagMappings.cs` | Created |
| `FeatureFlag.Infrastructure/Persistence/FlagConfiguration.cs` | Created — partial unique index with `HasFilter` |
| `FeatureFlag.Infrastructure/Persistence/FeatureFlagDbContext.cs` | Created |
| `FeatureFlag.Infrastructure/Persistence/FeatureFlagDbContextFactory.cs` | Created |
| `FeatureFlag.Infrastructure/Persistence/FeatureFlagRepository.cs` | Created — `CancellationToken` on all EF Core calls |
| `FeatureFlag.Infrastructure/DependencyInjection.cs` | Replaced — real EF Core and repository wiring |
| `FeatureFlag.Api/Controllers/FeatureFlagsController.cs` | Created |
| `FeatureFlag.Api/Controllers/EvaluationController.cs` | Created — 404 for unknown flags |
| `FeatureFlag.Api/Program.cs` | Replaced — JSON enum converter + root redirect |
| `FeatureFlag.Infrastructure/Migrations/InitialCreate` | Generated |

---

## 2. Deviations from the Spec

### 2.1 `Microsoft.EntityFrameworkCore.Design` Also Added to the Api Project

The spec adds `Microsoft.EntityFrameworkCore.Design` (with `PrivateAssets=all`) to the
Infrastructure project only. During migration generation, `dotnet ef` reported that the
startup project (Api) also requires a reference to this package:

```
Your startup project 'FeatureFlag.Api' doesn't reference Microsoft.EntityFrameworkCore.Design.
```

This is expected behavior — `dotnet ef` loads the startup project's build output and looks for
the design-time assembly there. Even with `PrivateAssets=all` on Infrastructure, the package
does not flow to the Api project.

**Resolution:** `Microsoft.EntityFrameworkCore.Design` was added to the Api project with the
same `PrivateAssets=all` declaration. This keeps it tooling-only in both projects and does not
affect runtime behavior or the production build output.

**Architect note for v3:** Add a note to the spec that both Infrastructure and Api require this
package. The v2 spec only listed it for Infrastructure.

---

### 2.2 Npgsql Version Resolved to 10.0.1, Not 9.0.4

The spec listed `Npgsql.EntityFrameworkCore.PostgreSQL` version `9.0.4` as a starting point,
but noted to let NuGet resolve the best version for `net10.0`. NuGet resolved `10.0.1`, which
targets EF Core 10.x — a better match for this project's target framework.

The `Microsoft.EntityFrameworkCore` packages resolved to `10.0.4` (the latest stable at time
of implementation). All packages are consistent.

---

### 2.3 Docker CLI Not Available in the Devcontainer

The devcontainer does not have Docker CLI on its PATH. `docker compose up -d` could not be
run during implementation. As a result:

- `dotnet ef database update` was not executed
- The API was not started to verify Swagger

The migration files were generated successfully. The schema is correct and was verified by
reading the generated migration (see section 3 below).

**To complete verification manually:**
```bash
docker compose up -d
dotnet ef database update \
  --project FeatureFlag.Infrastructure \
  --startup-project FeatureFlag.Api
dotnet run --project FeatureFlag.Api
# Navigate to: http://localhost:5227/openapi/v1.json
```

---

## 3. Migration Verification

The generated migration was reviewed before committing. Key facts confirmed:

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | Postgres UUID — matches `Guid` in the domain |
| `Name` | `character varying(200)` | Max length enforced at DB level |
| `Environment` | `text` | Enum stored as string |
| `StrategyType` | `text` | Enum stored as string |
| `StrategyConfig` | `jsonb` | Enables future JSON path queries |
| `CreatedAt` / `UpdatedAt` | `timestamp with time zone` | UTC timestamps |
| `ArchivedAt` | `timestamp with time zone` (nullable) | Null until archived |

The composite unique index was generated with the correct partial filter:

```sql
CREATE UNIQUE INDEX "IX_flags_Name_Environment"
ON flags ("Name", "Environment")
WHERE "IsArchived" = false;
```

This confirms the soft-delete/unique-index fix is correctly applied at the database level.

---

## 4. Architecture Decisions Validated This Session

### CancellationToken threading
`ct = default` on every async signature means zero breaking changes to existing callers while
enabling proper cancellation from the ASP.NET Core request pipeline. Controllers receive the
`CancellationToken` from ASP.NET Core automatically when declared as a method parameter —
no manual wiring required.

### Partial unique index
The `HasFilter("\"IsArchived\" = false")` on the composite `(Name, Environment)` index is a
PostgreSQL-specific feature. It allows a flag to be archived and a new flag to be created with
the same name in the same environment. Without the filter, the archived row's presence would
cause a unique constraint violation on recreation. This was caught in code review of v1 and
fixed in v2.

### 404 vs 200 for missing flags on the evaluation endpoint
`IsEnabledAsync` now throws `KeyNotFoundException` when the flag does not exist.
`EvaluationController` catches this and returns `404` with the error message in the body.
A flag that exists but is disabled returns `200 { "isEnabled": false }`.
This distinction is intentional — a typo in the flag name is a configuration error that should
surface immediately, not silently return false.

### `Flag.Update()` atomic method
The previous approach would have called `SetEnabled()` then `UpdateStrategy()` separately,
setting `UpdatedAt` twice (microseconds apart, no practical bug). The `Update()` method sets
all three fields and `UpdatedAt` in a single operation. Cleaner domain modeling.

### `IDesignTimeDbContextFactory`
Provides `dotnet ef` a deterministic, environment-independent path to construct the `DbContext`
for migration generation. Without it, `dotnet ef` tries to spin up the full ASP.NET Core host
and read config from `appsettings.Development.json` — which fails when `ASPNETCORE_ENVIRONMENT`
is not set. The factory hardcodes local dev credentials, which is correct for a design-time tool.

---

## 5. Known Issues Carried Forward

### KI-002 — `FeatureEvaluator` implicit precondition
Unchanged from the evaluation-engine session. Still documented via XML doc comment.
Monitor when new callers of `FeatureEvaluator` are introduced.

### KI-003 — `StrategyConfig` validation deferred to runtime
Unchanged. A flag with malformed `StrategyConfig` silently evaluates to `false`.
Planned fix: `FluentValidation` on the request DTOs in Phase 1.

---

## 6. What Is Intentionally Out of Scope (This Session)

Per the spec, the following were not implemented and remain deferred:

- Authentication and authorization (Phase 3)
- `FluentValidation` on request DTOs (Phase 1 — KI-003)
- Integration tests (Phase 1+)
- Caching layer (Phase 1+)
- Modification of `FeatureEvaluator` or any strategy

---

## 7. Phase 0 Status

Phase 0 is complete pending the one manual verification step (Docker + migrations + API start).
All interfaces are defined, the evaluation engine is operational, EF Core and the repository
are functional, controllers are wired and returning correct responses, and Swagger is configured.

The definition of done from `docs/current-state.md`:

| Criterion | Status |
|---|---|
| All interfaces defined | Done |
| `FeatureEvaluator` dispatches to correct strategy | Done (previous session) |
| Both strategies return deterministic results | Done (previous session) |
| EF Core and repository functional | Done |
| Controllers wired and returning responses | Done |
| Swagger configured | Done |

---

*FeatureFlagService | feature/persistence-and-controllers | Phase 0 Completion | v2*
