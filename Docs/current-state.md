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
**Phase 1 — Validation & Sanitization: ✅ Complete**
**Phase 1 — Testing, Error Handling, Developer Experience: 🔄 In Progress**

FluentValidation v12 is implemented with manual controller validation. `InputSanitizer`
is in place. The service layer sanitizes evaluation inputs. KI-003 is closed.

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
- `IRolloutStrategy` interface — `StrategyType` property for registry dispatch
- `IFeatureFlagRepository` interface — async, `CancellationToken` throughout

### Application Layer

- `NoneStrategy`, `PercentageStrategy`, `RoleStrategy` — all strategies implemented
- `FeatureEvaluator` — registry dispatch pattern
- `FeatureFlagService` — async, orchestrates repository + evaluator; sanitizes inputs
  at two call sites (`IsEnabledAsync`, `CreateFlagAsync`)
- DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`,
  `FlagMappings`
- `IFeatureFlagService` — async, CancellationToken, full CRUD + evaluation

### Validation & Sanitization (PR #30) ✅

- `InputSanitizer` — `internal static` helper in `FeatureFlag.Application/Validators/`;
  trims whitespace and strips ASCII control characters; called by validators and
  service layer
- `CreateFlagRequestValidator` — Name allowlist regex (on cleaned value via `Must()`),
  env sentinel guard, StrategyConfig cross-field rules, 2000-char limit
- `UpdateFlagRequestValidator` — StrategyConfig cross-field rules, 2000-char limit
- `EvaluationRequestValidator` — FlagName/UserId length + empty checks, UserRoles
  null/count/per-role length, env sentinel guard
- Validators registered explicitly via `AddScoped<IValidator<T>, TValidator>()` in
  `DependencyInjection.cs`
- `FeatureFlagsController` — manual `ValidateAsync` on POST and PUT
- `EvaluationController` — manual `ValidateAsync` before `FeatureEvaluationContext`
  is constructed
- `FluentValidation.AspNetCore` not used — deprecated; manual validation is the v12
  approach
- Build: ✅ 0 warnings, 0 errors
- Tests: ✅ 8/8 passing

### Service Interface Boundary ✅

- `IFeatureFlagService` — no `Flag` entity in any method signature
- `ToResponse()` mapping consolidated inside `FeatureFlagService`
- `FeatureFlagsController` — zero domain entity references

### Infrastructure Layer

- `FeatureFlagDbContext`, `FlagConfiguration`, `FeatureFlagDbContextFactory`
- `FeatureFlagRepository` — async, CancellationToken on all EF Core calls
- `InitialCreate` migration — generated and applied

### API Layer

- `FeatureFlagsController` — full CRUD with manual validation on POST/PUT
- `EvaluationController` — POST `/api/evaluate` with manual validation
- `Program.cs` — `JsonStringEnumConverter` wired, root redirect to OpenAPI docs
- Swagger/OpenAPI at `/openapi/v1.json`

### Documentation & Security

- `Docs/architecture.md` — updated to reflect v12 manual validation approach
- `Docs/roadmap.md` — full product vision, Phase 1.5 through Phase 9
- `Docs/Security/adr-input-security-model.md` — threat model, phase-gated decisions
- `Docs/Decisions/fluent-validation - PR#30/spec.md` — implementation spec
- `Docs/Decisions/fluent-validation - PR#30/implementation-notes.md` — deviations
  documented (DEV-001, DEV-002, KI-NEW-001)

### Dev Environment

- DevContainer: `devcontainers/base:ubuntu-24.04` + .NET 10 SDK
- Docker-outside-of-Docker configured
- `dotnet-ef` in `.config/dotnet-tools.json`
- Connection string: `Host=postgres`
- `docker-compose.yml` at repo root
- All five `.csproj` files targeting `net10.0`

### Tests

- `FeatureEvaluationContextTests` — 8/8 passing
- Build: ✅ 0 warnings, 0 errors

---

## ❌ What Is Not Yet Built (Phase 1 Remaining)

### Validation (remaining)

- Name uniqueness check at the service layer before hitting the DB

### Error Handling

- Global exception middleware — currently using per-controller try/catch
- Standardized error response shape for unhandled exceptions
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

Callers must check `Flag.IsEnabled` before calling `Evaluate`. Documented via XML
doc comment, not enforced by a guard clause.

---

### KI-006 — `Microsoft.EntityFrameworkCore.Design` Required on Both Projects

**Severity:** Low — spec convention, not a runtime issue
**Status:** Documented

Any spec with EF Core migration steps must list this package on both Infrastructure
and Api projects with `PrivateAssets=all`.

---

### KI-007 — Devcontainer Networking Requires Postgres to Start First

**Severity:** Low — inconvenience, not a bug
**Status:** Mitigated — `postStartCommand` automates the network join

**Workaround:** Run `docker compose up -d` before opening the devcontainer.
If devcontainer is already running:
```bash
docker network connect featureflagservice_default $(cat /etc/hostname)
```
**Longer-term fix:** Full docker-compose devcontainer setup. Deferred to Phase 8.

---

### KI-008 — Route Parameters on GET and PUT Lack Allowlist Validation

**Severity:** Low
**Status:** Open — Phase 1 fix

`GET /api/flags/{name}` and `PUT /api/flags/{name}` accept a `name` route parameter
with no character allowlist validation. EF Core parameterized queries prevent SQL
injection. Risk is unexpected characters reaching logs and repository calls.

**Planned fix:** Static `RouteParameterGuard.ValidateName(string name)` helper
returning `400` for non-conforming values, called at top of affected controller
actions.

---

### KI-NEW-001 — `BeValidPercentageConfig` / `BeValidRoleConfig` Duplicated

**Severity:** Low — code quality, no runtime impact
**Status:** Deferred — not a Phase 1 blocker

`BeValidPercentageConfig` and `BeValidRoleConfig` are private static methods
duplicated identically in both `CreateFlagRequestValidator` and
`UpdateFlagRequestValidator`.

**Candidate fix:** Extract to `StrategyConfigRules` internal static class in
`FeatureFlag.Application/Validators/`. Both validators call the shared methods.

---

## 🎯 Current Focus

**Phase 1 — MVP Completion (Testing, Error Handling, Developer Experience)**

### Immediate Next Tasks

1. Global exception middleware and standardized error shape
2. Route parameter guard for `{name}` on GET/PUT — closes KI-008
3. Unit tests for strategies, evaluator, and all three validators
4. Integration tests for all endpoints
5. Commit `.http` smoke test file
6. Seed data for local development
7. Evaluation decision logging

---

## 🧭 What Not To Do Right Now

- No authentication or authorization yet (Phase 3)
- No caching layer yet (Phase 6)
- No advanced rollout strategies yet (Phase 5)
- No observability pipeline yet (Phase 4) — App Insights comes in Phase 1.5
- No AI analysis endpoint yet (Phase 1.5)
- No UI work
- Do not change `Host=postgres` back to `localhost` in connection string
- Do not use `FluentValidation.AspNetCore` or `AddFluentValidationAutoValidation()` —
  both are deprecated; use manual `ValidateAsync` in controllers
- Do not use `.Transform()` — removed in FluentValidation v12

---

## 📌 Definition of Done — Phase 1

- [x] `InputSanitizer` implemented and called in validators and service layer
- [x] `FluentValidation` v12 on all three request DTOs
- [x] Manual `ValidateAsync` in controllers (POST and PUT on flags; POST on evaluate)
- [ ] Name uniqueness check at service layer
- [ ] Global exception middleware in place
- [ ] Route parameter guard on GET/PUT — closes KI-008
- [ ] Unit tests for all three strategies
- [ ] Unit tests for `FeatureEvaluator`
- [ ] Unit tests for all three validators
- [ ] Integration tests for all 6 endpoints
- [ ] `.http` smoke test file committed
- [ ] Seed data for local development
- [ ] Evaluation logging in place
- [x] Build: 0 warnings, 0 errors
- [x] All existing tests passing

---

## 🧩 Notes for AI Assistants

### Architecture
- Follow Clean Architecture — dependencies point inward toward Domain
- `IFeatureFlagService` speaks entirely in DTOs — never return `Flag` from the service
- `FeatureEvaluationContext` constructor: `(string userId, IEnumerable<string> userRoles, EnvironmentType environment)`
- Connection string uses `Host=postgres` — do not change to `localhost`
- Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design`
  with `PrivateAssets=all`

### Validation & Sanitization
- FluentValidation v12: manual `ValidateAsync` in controllers — no auto-validation
  middleware, no `FluentValidation.AspNetCore`
- Do not use `.Transform()` — removed in v12; use `Must()` with `InputSanitizer.Clean()`
- `InputSanitizer` is `internal` to `FeatureFlag.Application` — accessible from
  `FeatureFlagService` in the same project, not from the Api project
- Do not inline sanitization logic — always call `InputSanitizer.Clean()` or
  `CleanCollection()`
- Do not sanitize `StrategyConfig` — JSON, stored verbatim; only length and structure
  validated
- Any new `IRolloutStrategy` requires a corresponding validator rule

### Security
- See `Docs/Security/adr-input-security-model.md` before modifying the security boundary
- Raw SQL via `FromSqlRaw()` with string concatenation is prohibited

### Known Issue Tracking
- KI-003: **CLOSED** — StrategyConfig validated at write time
- KI-NEW-001: duplicated private methods in validators — deferred cleanup

### Product Direction
- Azure-native, .NET-first, AI-assisted feature flag platform
- Phase 1.5 immediately follows Phase 1 — do not skip Azure/AI integration
- See `Docs/roadmap.md` for full phase plan