# Current State — FeatureFlagService

## 📍 Status Summary

The project is currently in **Phase 0 — Foundation (In Progress)**.

The evaluation engine is implemented and the solution builds clean. The remaining Phase 0
work is persistence (EF Core + repository) and the API layer (controllers, Swagger).

---

## ✅ What Is Completed

### Domain Layer

- `Flag` entity with controlled mutation (private setters, explicit update methods)
- `FeatureEvaluationContext` value object — `IEquatable<T>` implemented, guard clauses, immutable roles
- `RolloutStrategy` enum (None, Percentage, RoleBased)
- `EnvironmentType` enum (None = 0 sentinel, Development, Staging, Production)
- `IRolloutStrategy` interface — includes `StrategyType` property for registry dispatch
- `IFeatureFlagRepository` interface

### Application Layer

- `NoneStrategy` — passthrough, always returns true
- `PercentageStrategy` — deterministic SHA256 hashing into buckets
- `RoleStrategy` — config-driven, case-insensitive, fail-closed role matching
- `FeatureEvaluator` — registry dispatch pattern, dictionary keyed by `RolloutStrategy`
- `FeatureFlagService` — orchestrates repository + evaluator, implements `IFeatureFlagService`
- `DependencyInjection.cs` — `AddApplication()` extension method

### Infrastructure Layer

- `DependencyInjection.cs` stub — `AddInfrastructure()` wired in `Program.cs`, `TODO` comments in place

### API Layer

- `Program.cs` — `AddApplication()` and `AddInfrastructure()` wired up
- `AddOpenApi()` present from scaffold

### Project Structure

- Clean Architecture solution: Domain, Application, Infrastructure, Api, Tests
- Dependency rule enforced: Domain has no outward dependencies
- DevContainer configured with .NET 9 image (see Known Issues)
- `docs/decisions/` folder established for Architecture Decision Records

### Tests

- `FeatureEvaluationContextTests` — covers constructor guards, equality, hash code

---

## ❌ What Is Not Yet Built (Remaining Phase 0)

### Infrastructure Layer

- EF Core `DbContext` and entity configuration
- `FeatureFlagRepository` — concrete implementation of `IFeatureFlagRepository`
- Repository registered in `AddInfrastructure()` (currently `TODO` stubs)

### API Layer

- Feature flag controllers (CRUD + evaluation endpoint)
- Swagger/OpenAPI examples and configuration

---

## ⚠️ Known Issues

### KI-001 — DevContainer Image Does Not Have a .NET 10 Tag

**Severity:** Medium — build works today, but version drift will become a problem  
**Status:** Deferred — tracked for resolution before Phase 1 begins

The devcontainer currently uses:
```jsonc
"image": "mcr.microsoft.com/devcontainers/dotnet:9.0"
```

No `devcontainers/dotnet:10.0` tag exists. The active SDK inside the container
is `10.0.201`, which means newly added packages resolve to `10.0.x` versions
while the solution targets `net9.0`.

**Planned fix:** Replace the `image` property with a custom `Dockerfile` based on
`mcr.microsoft.com/dotnet/sdk:10.0` and upgrade all `.csproj` files to `net10.0`.
This work is scoped to a `refactor/upgrade-net10` branch before Phase 1 begins.

---

### KI-002 — `FeatureEvaluator.Evaluate` Has an Implicit Precondition

**Severity:** Low — no bug today, potential footgun if the evaluator gains new callers  
**Status:** Documented — tracked for review when new callers are introduced

The original spec placed an `IsEnabled` short-circuit inside `FeatureEvaluator.Evaluate`.
During implementation, Claude Code removed it because `FeatureFlagService.IsEnabled`
already performs the same check before calling the evaluator.

The evaluator is now a pure strategy dispatcher. The precondition — that callers must
check `Flag.IsEnabled` before calling `Evaluate` — is documented via XML doc comment
on the method but is not enforced by a guard clause.

**Action required if:** A second caller of `FeatureEvaluator` is introduced anywhere
in the codebase. At that point, re-evaluate whether the guard clause should be restored
inside the evaluator, or whether the precondition is explicit enough in the new call site.

---

### KI-003 — `StrategyConfig` Validation Is Deferred to Runtime

**Severity:** Medium — misconfiguration fails silently at evaluation time  
**Status:** Deferred — scheduled for Phase 1 (CRUD endpoint design)

Both `PercentageStrategy` and `RoleStrategy` deserialize `Flag.StrategyConfig` at
evaluation time and fail closed on bad config. There is no validation at flag creation time.

A flag created with a malformed `StrategyConfig` will silently return `false` for every
user until someone investigates.

**Planned fix:** Add config validation at write time when the CRUD endpoints are built in
Phase 1. A `FluentValidation` validator on the request DTO is the appropriate location.
This should be treated as a Phase 1 requirement, not a nice-to-have.

---

## 🎯 Current Focus

Complete the remaining Phase 0 work.

### Immediate Next Tasks

1. `refactor/upgrade-net10` — swap devcontainer to custom Dockerfile, upgrade all projects to `net10.0`
2. Implement EF Core `DbContext` and entity configuration
3. Implement `FeatureFlagRepository`
4. Wire up repository in `AddInfrastructure()`
5. Create feature flag controllers
6. Configure Swagger/OpenAPI

---

## 🧭 What Not To Do Right Now

- No authentication or authorization yet
- No advanced rollout strategies
- No observability pipeline
- No performance optimization
- No UI work

Focus strictly on **finishing Phase 0**.

---

## 📌 Definition of "Phase 0 Complete"

Phase 0 is complete when:

- All interfaces are defined ✅
- `FeatureEvaluator` dispatches to the correct strategy ✅
- Both strategies are implemented and return deterministic results ✅
- EF Core and repository are functional
- Controllers are wired up and returning responses
- Swagger is configured

---

## 🧩 Notes for AI Assistants

- The system is not production-ready
- Prioritize correctness over feature expansion
- Follow Clean Architecture — dependencies point inward toward Domain
- Work within the established layer boundaries (Api → Application → Domain ← Infrastructure)
- All evaluation logic must remain deterministic and isolated from persistence
- See Known Issues above before modifying `FeatureEvaluator` or adding new callers