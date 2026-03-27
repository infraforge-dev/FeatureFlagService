# Current State — FeatureFlagService

## 📍 Status Summary

**Phase 0 — Foundation: ✅ Complete**
**Phase 1 — Architectural Cleanup: ✅ Complete**

The full stack is implemented, smoke-tested, and verified. The service interface
boundary has been cleaned up — domain entities no longer cross the service layer.
The API is running against a local Postgres instance via Docker.

**Next focus: Phase 1 — Validation, Testing, and Developer Experience**

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
- `FeatureFlagService` — async, orchestrates repository + evaluator
- `DependencyInjection.cs` — `AddApplication()` extension method
- DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`, `FlagMappings`
- `IFeatureFlagService` — async signatures with `CancellationToken`, full CRUD + evaluation

### Service Interface Boundary (refactor/service-interface-dtos) ✅

- `IFeatureFlagService` — no `Flag` entity in any method signature
- All signatures use DTOs: `FlagResponse`, `CreateFlagRequest`, `UpdateFlagRequest`
- `Flag` construction moved from controller into `FeatureFlagService.CreateFlagAsync`
- `UpdateFlagAsync` accepts `UpdateFlagRequest` DTO — not 5 primitive parameters
- `ToResponse()` mapping consolidated inside `FeatureFlagService` — called in exactly 3 places
- `FeatureFlagsController` — zero `.ToResponse()` calls, zero `FeatureFlag.Domain.Entities` references
- Smoke test verified: POST, GET, PUT, DELETE all return correct responses

### Infrastructure Layer

- `FeatureFlagDbContext` — EF Core DbContext with `DbSet<Flag>`
- `FlagConfiguration` — Fluent API entity config: enums as strings, `jsonb` for StrategyConfig,
  partial unique index on `(Name, Environment)` filtered to non-archived flags
- `FeatureFlagDbContextFactory` — design-time factory for deterministic `dotnet ef` tooling
- `FeatureFlagRepository` — async, `CancellationToken` on all EF Core calls
- `DependencyInjection.cs` — `AddInfrastructure()` with Npgsql + DbContext + repository wiring
- `InitialCreate` migration — generated and applied

### API Layer

- `FeatureFlagsController` — full CRUD: GET all, GET by name, POST, PUT, DELETE (soft archive)
- `EvaluationController` — POST `/api/evaluate`, returns 404 for unknown flags
- `Program.cs` — `JsonStringEnumConverter` wired, root redirect to OpenAPI docs
- Swagger/OpenAPI configured and accessible at `/openapi/v1.json`

### Dev Environment

- DevContainer: `devcontainers/base:ubuntu-24.04` + .NET 10 SDK via `dotnet` feature
- Docker-outside-of-Docker configured: host socket mounted
- `dotnet-ef` added to `.config/dotnet-tools.json` — installed automatically via `dotnet tool restore`
- `postStartCommand` in `devcontainer.json` joins `featureflagservice_default` Docker network on start
- Connection string: `Host=postgres` (Docker Compose service name — not `localhost`)
- `docker-compose.yml` at repo root — one-command local Postgres setup
- `docs/decisions/` folder established for Architecture Decision Records
- All five `.csproj` files targeting `net10.0`

### Tests

- `FeatureEvaluationContextTests` — covers constructor guards, equality, hash code
- Build: ✅ 0 warnings, 0 errors
- Tests: ✅ 8/8 passing

---

## ❌ What Is Not Yet Built (Phase 1 Remaining)

### Validation

- `FluentValidation` on `CreateFlagRequest`, `UpdateFlagRequest`, `EvaluationRequest` — KI-003 (Phase 1)
- Name uniqueness check at the service layer before hitting the DB (Phase 1)

### Testing

- Unit tests for `PercentageStrategy`, `RoleStrategy`, `NoneStrategy` (Phase 1)
- Unit tests for `FeatureEvaluator` — dispatch, missing strategy fallback (Phase 1)
- Integration tests for all API endpoints including `/api/evaluate` (Phase 1)

### Error Handling

- Global exception middleware — currently using per-controller try/catch (Phase 1)
- Standardized API error response shape (Phase 1)

### Developer Experience

- `.http` smoke test request file committed to repo (`requests/smoke-test.http`) (Phase 1)
- Seed data for development/staging flags (Phase 1)
- Logging for evaluation decisions (Phase 1)
- OpenAPI enum schema fix — enums currently render as `integer` in spec (cosmetic) (Phase 1)

### Authentication & Authorization

- JWT or OAuth (Phase 3)
- Role-based access to flag management endpoints (Phase 3)

---

## ⚠️ Known Issues

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
**Status:** Deferred — Phase 1 requirement (next up)

Both `PercentageStrategy` and `RoleStrategy` deserialize `Flag.StrategyConfig` at
evaluation time and fail closed on bad config. There is no validation at flag creation time.

**Planned fix:** `FluentValidation` validators on the request DTOs. Next task in Phase 1.

---

### KI-006 — `Microsoft.EntityFrameworkCore.Design` Required on Both Infrastructure and Api

**Severity:** Low — spec gap, not a runtime issue
**Status:** Documented — handled during implementation

The spec listed `Microsoft.EntityFrameworkCore.Design` for Infrastructure only.
`dotnet ef` also requires it on the startup project (Api). Both projects carry the
package with `PrivateAssets=all`.

**Note for future specs:** Any spec that includes EF Core migration steps must list
this package on both the Infrastructure and Api projects.

---

### KI-007 — Devcontainer Networking Requires Postgres to Start First

**Severity:** Low — inconvenience, not a bug
**Status:** Mitigated — `postStartCommand` automates the network join on each container start

The `postStartCommand` in `devcontainer.json` runs `docker network connect featureflagservice_default`
on container start. If Postgres is not yet running at that moment (e.g., after a full
machine restart), the join silently fails and the API cannot reach the database.

**Workaround:** Run `docker compose up -d` before opening the devcontainer. If the
devcontainer is already running:
```bash
docker network connect featureflagservice_default $(cat /etc/hostname)
```

**Longer-term fix:** Migrate devcontainer to a full docker-compose devcontainer setup
so both services start together. Deferred — not a Phase 1 blocker. Tracked for Phase 8.

---

## 🎯 Current Focus

**Phase 1 — MVP Completion (Validation, Testing, Developer Experience)**

### Immediate Next Tasks

1. Add `FluentValidation` to request DTOs — closes KI-003
2. Add global exception middleware — closes per-controller try/catch pattern
3. Unit tests for strategies and evaluator
4. Integration tests for all endpoints
5. Commit `.http` smoke test file to repo

---

## 🧭 What Not To Do Right Now

- No authentication or authorization yet (Phase 3)
- No caching layer yet (Phase 6)
- No advanced rollout strategies yet (Phase 5)
- No observability pipeline yet (Phase 4)
- No UI work
- Do not change `Host=postgres` back to `localhost` in connection string

---

## 📌 Definition of Done — Phase 1

- [ ] `FluentValidation` on all request DTOs
- [ ] Global exception middleware in place
- [ ] Unit tests for all strategies and evaluator
- [ ] Integration tests for all 6 endpoints
- [ ] `.http` smoke test file committed
- [ ] Seed data for local development
- [ ] Evaluation logging in place

---

## 🧩 Notes for AI Assistants

- The system is not production-ready
- Prioritize correctness over feature expansion
- Follow Clean Architecture — dependencies point inward toward Domain
- Work within the established layer boundaries (Api → Application → Domain ← Infrastructure)
- `IFeatureFlagService` speaks entirely in DTOs — never return `Flag` from the service
- All evaluation logic must remain deterministic and isolated from persistence
- See Known Issues above before modifying `FeatureEvaluator` or adding new callers
- `appsettings.Development.json` is intentionally committed — local Docker defaults only
- Connection string uses `Host=postgres` — do not change to `localhost`
- Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design` with `PrivateAssets=all`