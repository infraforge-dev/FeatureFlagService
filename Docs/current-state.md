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
**Phase 1 — CI/CD Foundation (PRs #33, #34): ✅ Complete**
**Phase 1 — CI/CD AI Reviewer (PR #35): ✅ Complete**
**Phase 1 — Error Handling (PR #36): ✅ Complete**
**Phase 1 — Input Validation Hardening (PR #37): ✅ Complete**
**Phase 1 — Testing & Developer Experience: 🔄 In Progress**

KI-008 and KI-NEW-001 are closed. `RouteParameterGuard` enforces the flag name
allowlist on all `{name}` route parameters. `DuplicateFlagNameException` is now
thrown from the service layer on duplicate `POST`. A TOCTOU race condition is
handled in the repository via `DbUpdateException` interception. `StrategyConfigRules`
is extracted — no more duplicated validator logic.

**Product direction locked:** Azure-native, .NET-first, AI-assisted feature flag
platform targeting .NET teams on Azure. Phase 1.5 introduces Key Vault, Application
Insights, and the AI analysis endpoint immediately after Phase 1 completes.

---

## ✅ What Is Completed

### Domain Layer

- `Flag` entity with controlled mutation (private setters, explicit update methods)
- `Flag.Update()` — atomic method that sets enabled state, strategy, and `UpdatedAt`
- `FeatureEvaluationContext` value object — `IEquatable<T>`, guard clauses, immutable roles
- `RolloutStrategy` enum (None, Percentage, RoleBased)
- `EnvironmentType` enum (None = 0 sentinel, Development, Staging, Production)
- `IRolloutStrategy` interface — includes `StrategyType` property for registry dispatch
- `IFeatureFlagRepository` interface — async signatures with `CancellationToken`
  - Includes `ExistsAsync(name, environment, ct)` — added PR #37
- Exception hierarchy in `FeatureFlag.Domain/Exceptions/`:
  - `FeatureFlagException` — abstract base, carries `StatusCode`
  - `FlagNotFoundException` — 404
  - `DuplicateFlagNameException` — 409, constructor accepts `(string, EnvironmentType)`
  - `FeatureFlagValidationException` — 400, for route parameter allowlist failures (PR #37)

### Application Layer

- `NoneStrategy`, `PercentageStrategy`, `RoleStrategy` — all strategies implemented
- `FeatureEvaluator` — registry dispatch pattern
- `FeatureFlagService` — async, orchestrates repository + evaluator
  - `CreateFlagAsync` — sanitizes name, calls `ExistsAsync`, throws `DuplicateFlagNameException` before insert (PR #37)
- `DependencyInjection.cs` — `AddApplication()` extension method
- DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`, `EvaluationResponse`, `FlagMappings`
- `IFeatureFlagService` — async signatures with `CancellationToken`, full CRUD + evaluation
- Validators:
  - `CreateFlagRequestValidator`, `UpdateFlagRequestValidator`, `EvaluationRequestValidator`
  - `InputSanitizer` — shared static helper, HTTP boundary sanitization
  - `StrategyConfigRules` — shared static class, `BeValidPercentageConfig` and `BeValidRoleConfig` (PR #37, closes KI-NEW-001)

### API Layer

- `FeatureFlagsController` — full CRUD
  - `RouteParameterGuard.ValidateName(name)` called first in `GetByNameAsync`, `UpdateAsync`, `ArchiveAsync` (PR #37, closes KI-008)
- `EvaluationController` — evaluation endpoint
- `GlobalExceptionMiddleware` — catches all exceptions, maps to `ProblemDetails`
  - Handles `400`, `404`, `409` domain exceptions
  - Returns `application/problem+json` on all error responses
- `FeatureFlag.Api/Helpers/RouteParameterGuard` — compiled regex allowlist on `{name}` route params (PR #37)
- OpenAPI enrichment: `EnumSchemaTransformer`, `ApiInfoTransformer`, Scalar UI
- Manual `ValidateAsync` in controllers (POST and PUT on flags; POST on evaluate)

### Infrastructure Layer

- `FeatureFlagRepository` — implements `IFeatureFlagRepository`
  - `ExistsAsync` implemented using `AnyAsync` — non-archived flags only (PR #37)
  - `SaveChangesAsync` intercepts `DbUpdateException` wrapping Postgres `23505` (unique constraint) and rethrows as `DuplicateFlagNameException` — handles TOCTOU race condition (PR #37)
- `FeatureFlagDbContext` + `FlagConfiguration` — Fluent API, `jsonb` for `StrategyConfig`
- Partial unique index on `(Name, Environment)` filtered to `IsArchived = false`

### CI/CD

- `.github/workflows/ci.yml` — parallel `lint-format` and `build-test` jobs
- AI reviewer job — activated by `ai-review` label
- CSharpier 1.x as final formatting authority
- `.editorconfig` with Allman brace style and Roslyn diagnostic severities

### Dev Environment

- DevContainer: `devcontainers/base:ubuntu-24.04` + .NET 10 SDK
- Docker-outside-of-Docker configured; `postStartCommand` automates network join
- `dotnet-ef` and `csharpier` in `.config/dotnet-tools.json`
- Connection string: `Host=postgres`
- `docker-compose.yml` at repo root

### Tests

- `FeatureEvaluationContextTests` — 8/8 passing, `[Trait("Category", "Unit")]`
- Build: ✅ 0 warnings, 0 errors
- CSharpier: ✅ 0 violations

---

## ❌ What Is Not Yet Built (Phase 1 Remaining)

### Testing

- Unit tests for `PercentageStrategy`, `RoleStrategy`, `NoneStrategy`
- Unit tests for `FeatureEvaluator` — dispatch, missing strategy fallback
- Unit tests for all three validators — every acceptance criterion covered
- Integration tests for all API endpoints including `/api/evaluate`

### Developer Experience

- `.http` smoke test request file committed to repo (`requests/smoke-test.http`)
- Seed data for development/staging flags
- Evaluation decision logging

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
**Longer-term fix:** Full docker-compose devcontainer setup. Deferred to Phase 8.

---

### Spec Writing — Lessons Learned

**Audit all service methods in AC-6-style tasks:** When a spec instructs updating
methods that throw or return null, explicitly list every affected method.

**ProblemDetails responses require `application/problem+json`:** RFC 9457 §8.1.
Do not use `MediaTypeNames.Application.Json`. Future specs must specify
`"application/problem+json"` explicitly.

**Address race conditions (TOCTOU) in uniqueness checks:** The spec for PR #37 did
not address the concurrent-request scenario. The implementer correctly placed the
`DbUpdateException` catch in the repository (Infrastructure), not the service
(Application), to avoid introducing an EF Core dependency into the Application layer.
Future specs involving uniqueness checks must explicitly address TOCTOU handling and
designate the correct layer for the catch.

**Dispose `JsonDocument` after parsing:** `JsonDocument.Parse()` allocates from pooled
memory. Always wrap with `using` — failure to dispose increases GC pressure. Future
specs providing `JsonDocument` code must use `using JsonDocument doc = ...`.

---

## 🎯 Current Focus

**Phase 1 — MVP Completion (Testing & Developer Experience)**

### Immediate Next Tasks

1. Unit tests for `PercentageStrategy`, `RoleStrategy`, `NoneStrategy`
2. Unit tests for `FeatureEvaluator` — dispatch, missing strategy fallback
3. Unit tests for all three validators — every acceptance criterion covered
4. Integration tests for all endpoints
5. Commit `.http` smoke test file (`requests/smoke-test.http`)
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
- Do not run `dotnet format` without following up with `dotnet csharpier format .` —
  CSharpier is the final formatting authority
- Do not add `try/catch` blocks to controllers — `GlobalExceptionMiddleware` handles
  all exceptions; controllers contain only the happy path
- Do not catch `DbUpdateException` in the Application layer — it is an Infrastructure
  concern; the Application project has no EF Core reference

---

## 📌 Definition of Done — Phase 1

- [x] `InputSanitizer` implemented and called in validators and service layer
- [x] `FluentValidation` v12 on all three request DTOs
- [x] Manual `ValidateAsync` in controllers (POST and PUT on flags; POST on evaluate)
- [x] CSharpier formatting enforced — CI blocks on violations
- [x] `.github/workflows/ci.yml` — `lint-format` and `build-test` parallel jobs live
- [x] AI reviewer job live (PR #35) — activated by `ai-review` label
- [x] `ANTHROPIC_API_KEY` secret added to GitHub repo
- [x] `ai-review` label created in GitHub repo
- [x] Global exception middleware in place
- [x] Standardized `ProblemDetails` error response shape
- [x] Name uniqueness check at the service layer (PR #37)
- [x] Route parameter guard for `{name}` on GET, PUT, DELETE — closes KI-008 (PR #37)
- [x] `StrategyConfigRules` extracted — closes KI-NEW-001 (PR #37)
- [ ] Unit tests for `PercentageStrategy`, `RoleStrategy`, `NoneStrategy`
- [ ] Unit tests for `FeatureEvaluator` — dispatch, missing strategy fallback
- [ ] Unit tests for all three validators
- [ ] Integration tests for all 6 endpoints
- [ ] `.http` smoke test file committed
- [ ] Seed data for local development
- [ ] Evaluation decision logging

---

## 🧩 Notes for AI Assistants

- The system is not production-ready
- Prioritize correctness over feature expansion
- Follow Clean Architecture — dependencies point inward toward Domain
- Work within established layer boundaries (Api → Application → Domain ← Infrastructure)
- `IFeatureFlagService` speaks entirely in DTOs — never return `Flag` from the service
- All evaluation logic must remain deterministic and isolated from persistence
- `GlobalExceptionMiddleware` is registered first in `Program.cs` — wraps entire pipeline
- Controllers contain only the happy path — zero `try/catch` blocks anywhere in `FeatureFlag.Api`
- All error responses return `ProblemDetails` with `Content-Type: application/problem+json`
- `ProblemDetails.Type` is set to `"about:blank"` — RFC 9457 recommendation
- `RouteParameterGuard.ValidateName(name)` is the first call in `GetByNameAsync`,
  `UpdateAsync`, and `ArchiveAsync` — do not remove or reorder
- `StrategyConfigRules` is the single source of truth for `BeValidPercentageConfig`
  and `BeValidRoleConfig` — do not re-add these methods to individual validators
- `DuplicateFlagNameException` constructor accepts `(string flagName, EnvironmentType environment)`
- `ExistsAsync` on the repository checks non-archived flags only
- `SaveChangesAsync` in `FeatureFlagRepository` catches `DbUpdateException` wrapping
  Postgres `23505` and rethrows as `DuplicateFlagNameException` — this is intentional
  TOCTOU handling and must not be removed
- `appsettings.Development.json` is intentionally committed — local Docker defaults only
- Connection string uses `Host=postgres` — do not change to `localhost`
- Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design`
  with `PrivateAssets=all`
- Do not use `FluentValidation.AspNetCore` or `.Transform()` — see FluentValidation v12 notes
- Any spec referencing ProblemDetails must specify `application/problem+json`
- Any spec with uniqueness checks must address TOCTOU and designate the correct layer
- Any spec providing `JsonDocument` code must use `using JsonDocument doc = ...`
