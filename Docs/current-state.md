# Current State — FeatureFlagService

---

## Table of Contents

- [Status Summary](#-status-summary)
- [What Is Completed](#-what-is-completed)
- [What Is Not Yet Built](#-what-is-not-yet-built-phase-1-remaining)
- [Known Issues](#-known-issues)
- [Current Focus](#-current-focus)
- [What Not To Do Right Now](#-what-not-to-do-right-now)
- [Definition of Done — Phase 1](#-definition-of-done--phase-1)
- [Notes for AI Assistants](#-notes-for-ai-assistants)

---

## 📍 Status Summary

**Phase 0 — Foundation: ✅ Complete**  
**Phase 1 — Architectural Cleanup: ✅ Complete**  
**Phase 1 — Validation, Testing, Developer Experience: 🔄 In Progress**

The full stack is implemented, smoke-tested, and verified. The service interface
boundary has been cleaned up — domain entities no longer cross the service layer.
The API is running against a local Postgres instance via Docker.

**Product direction locked:** Azure-native, .NET-first, AI-assisted feature flag
platform targeting .NET teams on Azure. Phase 1.5 introduces Key Vault, Application
Insights, and the AI analysis endpoint immediately after Phase 1 completes.

---

## ✅ What Is Completed

### Domain Layer

- `Flag` entity with controlled mutation (private setters, explicit update methods)
- `Flag.Update()` — atomic method that sets enabled state, strategy, and `UpdatedAt`
- `FeatureEvaluationContext` value object — `IEquatable<T>`, guard clauses,
  immutable `IReadOnlyList<string>` roles, accepts `IEnumerable<string>` on construction
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
- DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`,
  `FlagMappings`
- `IFeatureFlagService` — async signatures with `CancellationToken`, full CRUD +
  evaluation

### Service Interface Boundary (refactor/service-interface-dtos) ✅

- `IFeatureFlagService` — no `Flag` entity in any method signature
- All signatures use DTOs: `FlagResponse`, `CreateFlagRequest`, `UpdateFlagRequest`
- `Flag` construction moved from controller into `FeatureFlagService.CreateFlagAsync`
- `UpdateFlagAsync` accepts `UpdateFlagRequest` DTO — not 5 primitive parameters
- `ToResponse()` mapping consolidated inside `FeatureFlagService` — called in exactly
  3 places
- `FeatureFlagsController` — zero `.ToResponse()` calls, zero domain entity references
- Smoke test verified: POST, GET, PUT, DELETE all return correct responses

### Infrastructure Layer

- `FeatureFlagDbContext` — EF Core DbContext with `DbSet<Flag>`
- `FlagConfiguration` — Fluent API: enums as strings, `jsonb` for StrategyConfig,
  partial unique index on `(Name, Environment)` filtered to non-archived flags
- `FeatureFlagDbContextFactory` — design-time factory for `dotnet ef` tooling
- `FeatureFlagRepository` — async, `CancellationToken` on all EF Core calls
- `DependencyInjection.cs` — `AddInfrastructure()` with Npgsql + DbContext + repository
- `InitialCreate` migration — generated and applied

### API Layer

- `FeatureFlagsController` — full CRUD: GET all, GET by name, POST, PUT, DELETE
  (soft archive)
- `EvaluationController` — POST `/api/evaluate`, returns 404 for unknown flags
- `Program.cs` — `JsonStringEnumConverter` wired, root redirect to OpenAPI docs
- Swagger/OpenAPI configured and accessible at `/openapi/v1.json`

### Documentation & Security

- `Docs/architecture.md` — updated with product vision, validation/sanitization layer,
  security model summary, Phase 1.5 future considerations
- `Docs/roadmap.md` — updated with full product vision, Phase 1.5 through Phase 9,
  .NET SDK as key product milestone
- `Docs/Security/adr-input-security-model.md` — threat model, mitigations in place,
  phase-gated deferred decisions
- `Docs/Decisions/fluent-validation/spec.md` — Claude Code-ready implementation spec

### Dev Environment

- DevContainer: `devcontainers/base:ubuntu-24.04` + .NET 10 SDK via `dotnet` feature
- Docker-outside-of-Docker configured: host socket mounted
- `dotnet-ef` added to `.config/dotnet-tools.json`
- `postStartCommand` joins `featureflagservice_default` Docker network on start
- Connection string: `Host=postgres` (Docker Compose service name — not `localhost`)
- `docker-compose.yml` at repo root — one-command local Postgres setup
- All five `.csproj` files targeting `net10.0`

### Tests

- `FeatureEvaluationContextTests` — covers constructor guards, equality, hash code
- Build: ✅ 0 warnings, 0 errors
- Tests: ✅ 8/8 passing

---

## ❌ What Is Not Yet Built (Phase 1 Remaining)

### Validation & Sanitization

- `InputSanitizer` — shared static helper in `FeatureFlag.Application/Validators/`;
  trims whitespace and strips control characters; called by validators and service layer
- `CreateFlagRequestValidator` — Name allowlist regex, env sentinel guard,
  StrategyConfig cross-field rules, 2000-char limit — closes KI-003
- `UpdateFlagRequestValidator` — StrategyConfig cross-field rules, 2000-char limit
- `EvaluationRequestValidator` — UserId max 256, UserRoles max 50×100,
  env sentinel guard
- `AddFluentValidationAutoValidation()` wired in `Program.cs` (FluentValidation v11 API)
- `InputSanitizer` called in `FeatureFlagService.IsEnabledAsync` and `CreateFlagAsync`
- Name uniqueness check at the service layer before hitting the DB

### Error Handling

- Global exception middleware — currently using per-controller try/catch
- Standardized `ValidationProblemDetails` error response shape
- Route parameter guard for `{name}` on GET and PUT — closes KI-008

### Testing

- Unit tests for `PercentageStrategy`, `RoleStrategy`, `NoneStrategy`
- Unit tests for `FeatureEvaluator` — dispatch, missing strategy fallback
- Unit tests for all three validators — every acceptance criterion covered
- Integration tests for all API endpoints including `/api/evaluate`

### Developer Experience

- `.http` smoke test request file committed to repo (`requests/smoke-test.http`)
- Seed data for development/staging flags
- Evaluation decision logging
- OpenAPI enum schema fix — enums currently render as `integer` in spec (cosmetic)

---

## ⚠️ Known Issues

### KI-002 — `FeatureEvaluator.Evaluate` Has an Implicit Precondition

**Severity:** Low  
**Status:** Documented — tracked for review when new callers are introduced

Callers must check `Flag.IsEnabled` before calling `Evaluate`. This contract is
documented via XML doc comment but not enforced by a guard clause.

**Action required if:** A second caller of `FeatureEvaluator` is introduced anywhere
in the codebase.

---

### KI-003 — `StrategyConfig` Validation Deferred to Runtime

**Severity:** Medium — misconfiguration fails silently at evaluation time  
**Status:** Fix in progress — `Docs/Decisions/fluent-validation/spec.md` is the
implementation spec. Closes when FluentValidation validators are implemented.

---

### KI-006 — `Microsoft.EntityFrameworkCore.Design` Required on Both Projects

**Severity:** Low — spec gap, not a runtime issue  
**Status:** Documented — handled during implementation

Any spec that includes EF Core migration steps must list this package on both
Infrastructure and Api projects with `PrivateAssets=all`.

---

### KI-007 — Devcontainer Networking Requires Postgres to Start First

**Severity:** Low — inconvenience, not a bug  
**Status:** Mitigated — `postStartCommand` automates the network join

**Workaround:** Run `docker compose up -d` before opening the devcontainer.
If devcontainer is already running:
```bash
docker network connect featureflagservice_default $(cat /etc/hostname)
```
**Longer-term fix:** Migrate to full docker-compose devcontainer setup. Deferred to
Phase 8.

---

### KI-008 — Route Parameters on GET and PUT Lack Allowlist Validation

**Severity:** Low  
**Status:** Open — Phase 1 fix

`GET /api/flags/{name}` and `PUT /api/flags/{name}/{environment}` accept a `name`
route parameter with no character allowlist validation. Flag creation enforces
`^[a-zA-Z0-9\-_]+$` on `Name`, but a caller can send arbitrary characters in the
URL on GET and PUT. EF Core parameterized queries prevent SQL injection. The risk
is unexpected characters reaching logs and repository method calls.

**Planned fix:** Static `RouteParameterGuard.ValidateName(string name)` helper
returning `400 Bad Request` for non-conforming values, called at the top of affected
controller actions. See `Docs/Security/adr-input-security-model.md` DEFERRED-005.

---

## 🎯 Current Focus

**Phase 1 — MVP Completion (Validation, Testing, Developer Experience)**

### Immediate Next Tasks

1. Implement `InputSanitizer` and all three FluentValidation validators — closes KI-003
2. Add global exception middleware and standardized error shape
3. Add route parameter guard for `{name}` on GET/PUT — closes KI-008
4. Unit tests for strategies, evaluator, and all three validators
5. Integration tests for all endpoints
6. Commit `.http` smoke test file

---

## 🧭 What Not To Do Right Now

- No authentication or authorization yet (Phase 3)
- No caching layer yet (Phase 6)
- No advanced rollout strategies yet (Phase 5)
- No observability pipeline yet (Phase 4) — App Insights comes in Phase 1.5
- No AI analysis endpoint yet (Phase 1.5)
- No UI work
- Do not change `Host=postgres` back to `localhost` in connection string
- Do not use `AddFluentValidation()` — that is the v9/v10 API; use
  `AddFluentValidationAutoValidation()` (FluentValidation.AspNetCore v11)

---

## 📌 Definition of Done — Phase 1

- [ ] `InputSanitizer` implemented and called in validators and service layer
- [ ] `FluentValidation` on all three request DTOs with all acceptance criteria passing
- [ ] `AddFluentValidationAutoValidation()` wired in `Program.cs`
- [ ] Global exception middleware in place
- [ ] Route parameter guard on GET/PUT — closes KI-008
- [ ] Unit tests for all three strategies
- [ ] Unit tests for `FeatureEvaluator`
- [ ] Unit tests for all three validators
- [ ] Integration tests for all 6 endpoints
- [ ] `.http` smoke test file committed
- [ ] Seed data for local development
- [ ] Evaluation logging in place
- [ ] Build: 0 warnings, 0 errors
- [ ] All tests passing

---

## 🧩 Notes for AI Assistants

### Architecture
- Follow Clean Architecture — dependencies point inward toward Domain
- `IFeatureFlagService` speaks entirely in DTOs — never return `Flag` from the service
- All evaluation logic must remain deterministic and isolated from persistence
- `FeatureEvaluationContext` constructor signature: `(string userId, IEnumerable<string> userRoles, EnvironmentType environment)`
- `appsettings.Development.json` is intentionally committed — local Docker defaults only
- Connection string uses `Host=postgres` — do not change to `localhost`
- Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design`
  with `PrivateAssets=all`

### Validation & Sanitization
- FluentValidation v11 API: use `AddFluentValidationAutoValidation()` — not
  `AddFluentValidation(config => ...)`
- `InputSanitizer` is `internal` to `FeatureFlag.Application` — accessible from
  `FeatureFlagService` in the same project, not from the Api project
- Do not inline sanitization logic — always call `InputSanitizer.Clean()` or
  `CleanCollection()`
- Do not sanitize `StrategyConfig` — it is JSON, stored verbatim; only length and
  structure are validated
- Any new `IRolloutStrategy` requires a corresponding validator rule before the API
  will accept its configuration

### Security
- See `Docs/Security/adr-input-security-model.md` before modifying the security boundary
- Raw SQL via `FromSqlRaw()` with string concatenation is prohibited

### Product Direction
- This is an Azure-native, .NET-first, AI-assisted feature flag platform
- Phase 1.5 immediately follows Phase 1 — do not skip or defer Azure/AI integration
- See `Docs/roadmap.md` for the full phase plan and product vision
