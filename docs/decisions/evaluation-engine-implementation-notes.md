# Evaluation Engine — Implementation Notes
 
**Session date:** 2026-03-24  
**Branch:** `feature/evaluation-engine`  
**Spec reference:** `docs/decisions/evaluation-engine.md`  
**Build status:** Passed — 0 warnings, 0 errors
 
---
 
## 1. What Was Implemented
 
All items in scope per the spec were completed:
 
| File | Status |
|---|---|
| `Domain/Interfaces/IRolloutStrategy.cs` | Updated — `StrategyType` property added |
| `Domain/Interfaces/IFeatureFlagRepository.cs` | Created |
| `Application/Strategies/NoneStrategy.cs` | Created |
| `Application/Strategies/PercentageStrategy.cs` | Created |
| `Application/Strategies/RoleStrategy.cs` | Created |
| `Application/Evaluation/FeatureEvaluator.cs` | Created |
| `Application/Services/FeatureFlagService.cs` | Created |
| `Application/DependencyInjection.cs` | Created |
| `Infrastructure/DependencyInjection.cs` | Created (stub) |
| `Api/Program.cs` | Updated — wired up `AddApplication()` and `AddInfrastructure()` |
 
---
 
## 2. Deviations from the Spec
 
### 2.1 NuGet Packages Not Listed in the Spec
 
The spec did not mention that any NuGet packages would need to be added. Two were required
and added after user confirmation:
 
| Package | Version | Project | Reason |
|---|---|---|---|
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.5 | Application | Required for `IServiceCollection` in `DependencyInjection.cs` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.5 | Infrastructure | Required for `IServiceCollection` in `DependencyInjection.cs` |
| `Microsoft.Extensions.Configuration.Abstractions` | 10.0.5 | Infrastructure | Required for `IConfiguration` in `DependencyInjection.cs` |
 
**Architect note:** Using the `Abstractions`-only packages is intentional and correct.
The Application and Infrastructure layers should depend only on contracts — not on the full
DI implementation. The concrete implementation is provided transitively by the API project's
`Microsoft.AspNetCore` SDK. This keeps the dependency direction clean.
 
**Risk flag:** The solution targets `net9.0` but the SDK in the dev container is `10.0.201`,
which resolved these packages to version `10.0.5`. The packages are backwards compatible and
the build is clean, but this mismatch is tracked as **KI-001** in `docs/current-state.md`
and must be resolved before Phase 1 begins.
 
---
 
### 2.2 `using` Directives Not Specified for `Program.cs`
 
The spec instructed uncommenting the two wiring lines in `Program.cs` but did not include
the required `using` directives. The following were added:
 
```csharp
using FeatureFlag.Application;
using FeatureFlag.Infrastructure;
```
 
Without these, the extension methods `AddApplication()` and `AddInfrastructure()` are not
visible to the top-level statement file, even though the project references were already
in place. Minor spec gap — no architectural impact.
 
---
 
### 2.3 `IsEnabled` Check Consolidated to Service Layer
 
The spec placed an `IsEnabled` short-circuit in `FeatureEvaluator.Evaluate`, but
`FeatureFlagService.IsEnabled` already checks the same property before calling the evaluator.
The check was removed from the evaluator.
 
`FeatureEvaluator` is now a pure strategy dispatcher — given a flag, select the strategy
and return the result. Policy decisions (is this flag on or off?) live at the service
boundary, not inside the evaluation engine.
 
**Architect note:** This is a defensible design call for the current codebase, where
`FeatureEvaluator` has exactly one caller. However, if a second caller is introduced,
the missing guard becomes a silent footgun. This deviation is tracked as **KI-002**
in `docs/current-state.md`. A precondition comment has been added to
`FeatureEvaluator.Evaluate` to document the contract explicitly.
 
The comment reads:
 
```csharp
/// <summary>
/// Evaluates whether the given flag is enabled for the provided context.
/// </summary>
/// <remarks>
/// PRECONDITION: Callers are responsible for checking <see cref="Flag.IsEnabled"/>
/// before calling this method. The evaluator assumes the flag is active and will
/// dispatch to the appropriate strategy regardless of enabled state.
///
/// This contract is intentionally not enforced here via a guard clause — the
/// evaluator is a pure strategy dispatcher, not a policy enforcer. Policy lives
/// at the service boundary (<see cref="FeatureFlagService"/>).
///
/// If this method is called from any context other than FeatureFlagService,
/// revisit whether the IsEnabled check needs to be added back here.
/// </remarks>
public bool Evaluate(Flag flag, FeatureEvaluationContext context)
```
 
---
 
## 3. What Is Intentionally Out of Scope (This Session)
 
Per the spec, the following were not implemented and remain as stubs or placeholders:
 
- **EF Core / `DbContext`** — `Infrastructure/DependencyInjection.cs` contains `TODO` comments
  for `AddDbContext` and `AddScoped<IFeatureFlagRepository>`. The real repository does not
  exist yet.
- **`IFeatureFlagRepository` implementation** — The interface is defined in Domain; no concrete
  class exists. Any attempt to call `IFeatureFlagService` at runtime will fail with an unresolved
  dependency error until this is implemented.
- **Controllers** — No API endpoints exist. The service layer is fully wired but unreachable
  from HTTP.
- **Swagger/OpenAPI** — The `AddOpenApi()` call exists in `Program.cs` from the original
  scaffold but no endpoint documentation has been configured.
 
---
 
## 4. Items Requiring Attention Before Merging to Main
 
### 4.1 .NET Target Framework Version — KI-001
 
All five projects currently target `net9.0`. The active SDK is `10.0.201`. The newly added
packages resolved to version `10.0.5` (a .NET 10 release).
 
The `mcr.microsoft.com/devcontainers/dotnet:10.0` image tag does not yet exist. The fix
requires replacing the `image` property in `devcontainer.json` with a custom `Dockerfile`
based on `mcr.microsoft.com/dotnet/sdk:10.0`, then upgrading all `.csproj` files to
`net10.0`.
 
This work is scoped to a dedicated `refactor/upgrade-net10` branch and should be completed
before Phase 1 begins. Tracked as **KI-001** in `docs/current-state.md`.
 
---
 
### 4.2 `IsEnabled` Check Consolidated to Service Layer — KI-002
 
Covered in section 2.3 above. Tracked as **KI-002** in `docs/current-state.md`.
 
No immediate action required. Monitor when new callers of `FeatureEvaluator` are introduced.
 
---
 
### 4.3 `StrategyConfig` Validation Deferred to Runtime — KI-003
 
Both `PercentageStrategy` and `RoleStrategy` deserialize `Flag.StrategyConfig` at evaluation
time and fail closed on bad config. There is no validation at flag creation time. A flag with
a malformed `StrategyConfig` will silently evaluate to `false` for every user until someone
investigates.
 
**Planned fix:** Add config validation at write time (when a flag is created or updated) as
part of the Phase 1 CRUD endpoint work. A `FluentValidation` or custom validator on the
request DTO is the appropriate location. This is a Phase 1 requirement, not a nice-to-have.
 
Tracked as **KI-003** in `docs/current-state.md`.
 
---
 
## 5. Architecture Decisions Validated This Session
 
The following decisions from the spec were implemented as written and held up without issues:
 
- **Registry dispatch pattern** — `FeatureEvaluator` builds a `Dictionary<RolloutStrategy,
  IRolloutStrategy>` at construction time. No switch statements. Adding a new strategy
  requires only a new class and one DI registration line.
- **DI lifetime separation** — Strategies and `FeatureEvaluator` are Singleton;
  `FeatureFlagService` is Scoped. This matches the dependency chain and avoids the
  captive dependency bug with EF Core `DbContext`.
- **`IFeatureFlagRepository` in Domain** — Infrastructure implements it; Domain defines it;
  Application consumes it. Dependency Inversion Principle is intact.
- **Fail-closed behavior** — All strategies return `false` on null config, malformed JSON,
  or missing strategy registration. No exceptions are thrown from the evaluation path.
- **`Abstractions`-only NuGet packages** — Application and Infrastructure reference contracts
  only, not the full DI implementation. Dependency direction is clean.
 
---
 
## 6. Next Session Scope (Phase 0 Completion)
 
1. `refactor/upgrade-net10` — custom Dockerfile, upgrade all projects to `net10.0` **(do first)**
2. Implement EF Core `DbContext` and entity configuration
3. Implement `FeatureFlagRepository`
4. Register repository in `Infrastructure/DependencyInjection.cs`
5. Create feature flag controllers
6. Configure Swagger/OpenAPI with meaningful examples
 
---
 
*FeatureFlagService | feature/evaluation-engine | Phase 0*