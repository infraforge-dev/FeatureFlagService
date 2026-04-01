# Current State — FeatureFlagService

## 📍 Status Summary

**Phase 0 — Foundation: ✅ Complete**

The full stack is implemented and verified: domain, evaluation engine, persistence layer,
controllers, and Swagger. The API is running against a local Postgres instance via Docker.

**Next focus: Phase 1 — MVP Completion**

---

## ✅ What Is Completed

### Domain Layer

- `Flag` entity with controlled mutation (private setters, explicit update methods)
- `Flag.Update()` — atomic method that sets enabled state, strategy, and `UpdatedAt` in one operation
- `FeatureEvaluationContext` value object — `IEquatable<T>` implemented, guard clauses, immutable roles
- `RolloutStrategy` enum (None, Percentage, RoleBased)
- `EnvironmentType` enum (None = 0 sentinel, Development, Staging, Production)
- `IRolloutStrategy` interface — includes `StrategyType` property for registry dispatch
- `IFeatureFlagRepository` interface — async signatures with `CancellationToken`

### Application Layer

- `NoneStrategy` — passthrough, always returns true
- `PercentageStrategy` — deterministic SHA256 hashing into buckets
- `RoleStrategy` — config-driven, case-insensitive, fail-closed role matching
- `FeatureEvaluator` — registry dispatch pattern, dictionary keyed by `RolloutStrategy`
- `FeatureFlagService` — async, orchestrates repository + evaluator, throws `KeyNotFoundException` on missing flags
- `DependencyInjection.cs` — `AddApplication()` extension method
- DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`, `FlagMappings`
- `IFeatureFlagService` — async signatures with `CancellationToken`, full CRUD + evaluation

### Infrastructure Layer

- `FeatureFlagDbContext` — EF Core DbContext with `DbSet<Flag>`
- `FlagConfiguration` — Fluent API entity config: enums as strings, `jsonb` for StrategyConfig,
  partial unique index on `(Name, Environment)` filtered to non-archived flags
- `FeatureFlagDbContextFactory` — design-time factory for deterministic `dotnet ef` tooling
- `FeatureFlagRepository` — async, `CancellationToken` on all EF Core calls
- `DependencyInjection.cs` — `AddInfrastructure()` with real Npgsql + DbContext + repository wiring
- `InitialCreate` migration — generated and verified (schema confirmed correct)

### API Layer

- `FeatureFlagsController` — full CRUD: GET all, GET by name, POST, PUT, DELETE (soft archive)
- `EvaluationController` — POST `/api/evaluate`, returns 404 for unknown flags, 200 for known disabled flags
- `Program.cs` — `JsonStringEnumConverter` wired, root redirect to OpenAPI docs
- Swagger/OpenAPI configured and accessible at `http://localhost:5227/openapi/v1.json`

### Project Structure

- Clean Architecture solution: Domain, Application, Infrastructure, Api, Tests
- Dependency rule enforced: Domain has no outward dependencies
- DevContainer: `devcontainers/base:ubuntu-24.04` + .NET 10 SDK via `dotnet` feature
- Docker-outside-of-Docker configured: host socket mounted, `docker-outside-of-docker` feature added
- All five `.csproj` files targeting `net10.0`
- `docker-compose.yml` at repo root — one command local Postgres setup
- `docs/decisions/` folder established for Architecture Decision Records

### Tests

- `FeatureEvaluationContextTests` — covers constructor guards, equality, hash code
- Build: ✅ 0 warnings, 0 errors
- Tests: ✅ 8/8 passing

---

## ❌ What Is Not Yet Built (Phase 1+)

### Validation

- `FluentValidation` on request DTOs at write time — KI-003 (Phase 1)
- Name uniqueness check at the service layer before hitting the DB (Phase 1)

### Testing

- Integration tests for API endpoints (Phase 1)
- Unit tests for strategies and evaluator beyond the existing context tests (Phase 1)

### Developer Experience

- Seed data for development/staging flags (Phase 1)
- Logging for evaluation decisions (Phase 1)
- Swagger examples and descriptions on endpoints (Phase 1)

### Authentication & Authorization

- JWT or OAuth (Phase 3)
- Role-based access to flag management endpoints (Phase 3)

---

## ⚠️ Known Issues

### KI-001 — DevContainer Image Does Not Have a .NET 10 Tag

**Severity:** Medium
**Status:** ✅ Resolved — `refactor/upgrade-net10`

Base image swapped to `mcr.microsoft.com/devcontainers/base:ubuntu-24.04`. The `dotnet`
feature installs .NET 10 SDK. All five `.csproj` files updated to `net10.0`. Build and
tests pass clean.

---

### KI-002 — `FeatureEvaluator.Evaluate` Has an Implicit Precondition

**Severity:** Low — no bug today, potential footgun if the evaluator gains new callers
**Status:** Documented — tracked for review when new callers are introduced

The evaluator is a pure strategy dispatcher. The precondition — that callers must check
`Flag.IsEnabled` before calling `Evaluate` — is documented via XML doc comment on the
method but is not enforced by a guard clause.

**Action required if:** A second caller of `FeatureEvaluator` is introduced anywhere
in the codebase. At that point, re-evaluate whether the guard clause should be restored.

---

### KI-003 — `StrategyConfig` Validation Is Deferred to Runtime

**Severity:** Medium — misconfiguration fails silently at evaluation time
**Status:** Deferred — Phase 1 requirement

Both `PercentageStrategy` and `RoleStrategy` deserialize `Flag.StrategyConfig` at
evaluation time and fail closed on bad config. There is no validation at flag creation time.

**Planned fix:** `FluentValidation` validator on the request DTOs when CRUD endpoints
are hardened in Phase 1. Treat this as a requirement, not a nice-to-have.

---

### KI-004 — `Microsoft.AspNetCore.OpenApi` Package Not Aligned to .NET 10

**Severity:** Low
**Status:** ✅ Resolved — `feature/persistence-and-controllers`

Package upgraded from `9.0.3` to `10.0.5` as part of the Phase 0 API layer work.
All packages now consistently target .NET 10.

---

### KI-005 — Docker CLI Not Available in Devcontainer

**Severity:** Medium
**Status:** ✅ Resolved — `feature/persistence-and-controllers`

Docker was installed inside the container (Docker-in-Docker) which does not bind to the
host socket. Fixed by switching to Docker-outside-of-Docker:

- Added `ghcr.io/devcontainers/features/docker-outside-of-docker:1` feature
- Mounted host Docker socket: `source=/var/run/docker.sock,target=/var/run/docker.sock,type=bind`
- Added `vscode` user to `docker` group via `postCreateCommand`
- Added port `5432` to `forwardPorts` for direct Postgres access from host if needed

---

### KI-006 — `Microsoft.EntityFrameworkCore.Design` Required on Both Infrastructure and Api

**Severity:** Low — spec gap, not a runtime issue
**Status:** Documented — handled during implementation

The spec (v2) listed `Microsoft.EntityFrameworkCore.Design` for Infrastructure only.
`dotnet ef` also requires it on the startup project (Api) because it loads the startup
project's build output. Both projects now carry the package with `PrivateAssets=all`.

**Note for future specs:** Any spec that includes EF Core migration steps must list this
package on both the Infrastructure and Api projects.

---

## 🎯 Current Focus

**Phase 1 — MVP Completion**

### Immediate Next Tasks

1. Add `FluentValidation` to request DTOs — closes KI-003
2. Add integration tests for all six endpoints
3. Add seed data for local development
4. Add logging for evaluation decisions
5. Improve Swagger endpoint descriptions and examples

---

## 🧭 What Not To Do Right Now

- No authentication or authorization yet (Phase 3)
- No caching layer yet (Phase 6)
- No advanced rollout strategies yet (Phase 5)
- No observability pipeline yet (Phase 4)
- No UI work

---

## 📌 Definition of "Phase 0 Complete"

- All interfaces are defined ✅
- `FeatureEvaluator` dispatches to the correct strategy ✅
- Both strategies are implemented and return deterministic results ✅
- DevContainer and target frameworks on .NET 10 ✅
- EF Core and repository are functional ✅
- Controllers are wired up and returning responses ✅
- Swagger is configured ✅

**Phase 0 is complete.**

---

## 🧩 Notes for AI Assistants

- The system is not production-ready
- Prioritize correctness over feature expansion
- Follow Clean Architecture — dependencies point inward toward Domain
- Work within the established layer boundaries (Api → Application → Domain ← Infrastructure)
- All evaluation logic must remain deterministic and isolated from persistence
- See Known Issues above before modifying `FeatureEvaluator` or adding new callers
- `appsettings.Development.json` is intentionally committed — local Docker defaults only
- Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design` with `PrivateAssets=all`
