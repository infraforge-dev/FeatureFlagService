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
**Phase 1 — Unit Tests (PR #38): ✅ Complete**
**Phase 1 — Integration Tests (PR #39): ✅ Complete**
**Phase 1 — Evaluation Decision Logging (PR #48): ✅ Complete**
**Phase 1 — NuGet Locked Restore (rolled into PR #48): ✅ Complete**

110/110 tests passing (79 unit + 31 integration). Evaluation outcomes are now modeled
as a discriminated union (`EvaluationResult` → `FlagDisabled` | `StrategyEvaluated`)
with an explicit `EvaluationReason` dimension on every log entry. Raw `UserId` values
are never logged — a SHA256 surrogate (`HashedUserId`, 8 hex chars) is used throughout.
NuGet locked restore is now enforced in CI via `--locked-mode`; `packages.lock.json`
is committed for all projects.

**Two tasks remain before Phase 1 DoD is declared complete:**
1. `.http` smoke test file
2. Seed data for local development

Phase 1.5 begins immediately after both are shipped.

---

## ✅ What Is Completed

### Domain Layer

- `Flag` entity with controlled mutation (private setters, explicit mutation methods)
- `Flag.Update()` — atomic method that sets enabled state, strategy, and `UpdatedAt`
- `FeatureEvaluationContext` value object — `IEquatable<T>`, guard clauses, immutable roles
- `RolloutStrategy` enum (None, Percentage, RoleBased)
- `EnvironmentType` enum (None = 0 sentinel, Development, Staging, Production)
- `IRolloutStrategy` interface — includes `StrategyType` for registry dispatch
- `IFeatureFlagRepository` interface — async signatures with `CancellationToken`
- Domain exceptions: `FlagNotFoundException`, `DuplicateFlagNameException`,
  `FeatureFlagValidationException`

### Application Layer

- `NoneStrategy` — passthrough, always returns true
- `PercentageStrategy` — deterministic SHA256 hashing into 100 buckets
- `RoleStrategy` — config-driven, case-insensitive, fail-closed role matching
- `FeatureEvaluator` — registry dispatch, `Dictionary<RolloutStrategy, IRolloutStrategy>`
- `FeatureFlagService` — async, orchestrates repository + evaluator + logging
- `IFeatureFlagService` — async signatures with `CancellationToken`, full CRUD + evaluation
- `DependencyInjection.cs` — `AddApplication()` extension method
- DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`,
  `FlagMappings`
- `InputSanitizer` — single source of truth for HTTP boundary sanitization
- `EnvironmentRules` — single source of truth for environment validation
- `StrategyConfigRules` — single source of truth for strategy config validation
- Validators: `CreateFlagRequestValidator`, `UpdateFlagRequestValidator`,
  `EvaluationRequestValidator` (FluentValidation v12)

### Evaluation Logging (PR #48)

- `EvaluationResult.cs` — discriminated union in `FeatureFlag.Application/Evaluation/`:
  - `EvaluationReason` enum (`FlagDisabled`, `StrategyEvaluated`)
  - `EvaluationResult` abstract base record
  - `FlagDisabled` sealed record — carries `FlagName`, `Environment`, `HashedUserId`,
    `Reason`
  - `StrategyEvaluated` sealed record — additionally carries `IsEnabled`, `StrategyType`
- `FeatureFlagService` updated:
  - `ILogger<FeatureFlagService>` injected
  - `HashUserId(string)` — `internal static`, SHA256, 8 hex chars, lowercase
  - `LogResult(EvaluationResult)` — structured log, `Reason` as explicit dimension,
    `default: throw UnreachableException` for exhaustiveness
  - `LogWarning` before `FlagNotFoundException` (flag-not-found path)
  - `IsEnabled(LogLevel.Information)` guard in `LogResult` (CA1873)
  - Raw `UserId` never appears in any log entry
- `AssemblyInfo.cs` — `InternalsVisibleTo("FeatureFlag.Tests")`
- `Microsoft.Extensions.Diagnostics.Testing` added to `FeatureFlag.Tests` (`10.4.0`,
  pinned) — provides `FakeLogger<T>` for structured log assertions
- New unit tests: `EvaluationResultTests` (8 tests) + `UserIdHashTests` (4 tests) +
  `FeatureFlagServiceLoggingTests` (4 tests) = 16 new unit tests

### NuGet Locked Restore (rolled into PR #48)

- `RestorePackagesWithLockFile=true` added to `Directory.Build.props`
- `packages.lock.json` committed for all projects
- CI restore steps updated to `dotnet restore FeatureFlagService.sln --locked-mode`
- NuGet caching re-enabled in both `lint-format` and `build-test` CI jobs

### Infrastructure Layer

- `FeatureFlagRepository` — EF Core, async, filters archived flags on all reads
- `FlagDbContext` + `FlagConfiguration` — Fluent API, enums as strings, `StrategyConfig`
  as `jsonb`
- Partial unique index on `(Name, Environment)` filtered to `IsArchived = false`
- `SaveChangesAsync` catches Postgres `23505` → rethrows `DuplicateFlagNameException`

### API Layer

- `FeatureFlagsController` — 5 endpoints, zero `try/catch`, happy path only
- `EvaluationController` — `POST /api/evaluate`, zero `try/catch`
- `GlobalExceptionMiddleware` — RFC 9457 `ProblemDetails`, `application/problem+json`
- `RouteParameterGuard` — compiled regex, called first in GET/PUT/DELETE by name
- Swagger/OpenAPI at `/openapi/v1.json`

### CI/CD

- `lint-format` job — CSharpier check + zero-warning build
- `build-test` job — unit tests via `--filter "Category=Unit"`
- `integration-test` job — integration tests via `--filter "Category=Integration"`,
  Testcontainers Postgres
- `ai-review` job — Claude API reviewer, activated by `ai-review` label, fail-open
- `--locked-mode` on all restore steps
- NuGet caching active

### Tests

- `FeatureFlag.Tests` — 79/79 unit tests passing (`[Trait("Category", "Unit")]`)
- `FeatureFlag.Tests.Integration` — 31/31 integration tests passing
  (`[Trait("Category", "Integration")]`)
- **Total: 110/110 passing**
- Build: ✅ 0 warnings, 0 errors
- CSharpier: ✅ 0 violations

---

## ❌ What Is Not Yet Built (Phase 1 Remaining)

### Developer Experience

- `.http` smoke test request file (`requests/smoke-test.http`)
- Seed data for local development

---

## ⚠️ Known Issues

### KI-002 — `FeatureEvaluator.Evaluate` Has an Implicit Precondition

**Severity:** Low
**Status:** Documented

Callers must check `Flag.IsEnabled` before calling `Evaluate`. Documented via XML
doc comment. If a second caller of `FeatureEvaluator` is introduced, re-evaluate
whether a guard clause should be added.

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

## Spec Writing — Lessons Learned

**Audit all service methods in AC-6-style tasks:** When a spec instructs updating
methods that throw or return null, explicitly list every affected method.

**ProblemDetails responses require `application/problem+json`:** RFC 9457 §8.1.
Future specs must specify `"application/problem+json"` explicitly.

**Address race conditions (TOCTOU) in uniqueness checks:** Designate the correct
layer for the catch — `DbUpdateException` belongs in Infrastructure.

**Dispose `JsonDocument` after parsing:** Always wrap with `using JsonDocument doc = ...`.

**DTO nullability must match wire contract:** Explicitly state nullability on DTO
fields when the field is optional on the wire.

**Log PII defensively from day one:** Raw user identifiers must not appear in log
output. Use a SHA256 surrogate (`HashedUserId`) from the start — retrofitting
pseudonymization after logs are flowing into a telemetry sink is painful.

**`EvaluationReason` must be a first-class log dimension:** Inferring reason from
branch shape or message template presence/absence is fragile for App Insights
queries and unusable for the Phase 4 trace endpoint. Always log `Reason` explicitly.

**`IsEnabled` guard before logging:** Add `if (!_logger.IsEnabled(level)) return;`
in any void log helper method (CA1873) to avoid unnecessary work on hot paths.

**Pin test package versions explicitly:** Floating NuGet references in test projects
cause CI drift. Pin to an exact version and commit `packages.lock.json`.

---

## 🎯 Current Focus

**Phase 1 — Final Two Tasks**

1. `.http` smoke test file (`requests/smoke-test.http`)
2. Seed data for local development

Phase 1 DoD is complete when both are shipped. Phase 1.5 begins immediately after.

---

## 🧭 What Not To Do Right Now

- No authentication or authorization yet (Phase 3)
- No caching layer yet (Phase 6)
- No advanced rollout strategies yet (Phase 5)
- No observability pipeline yet (Phase 4) — App Insights comes in Phase 1.5
- No AI analysis endpoint yet (Phase 1.5)
- No UI work
- Do not change `Host=postgres` back to `localhost` in connection string
- Do not use `FluentValidation.AspNetCore` or `AddFluentValidationAutoValidation()`
- Do not use `.Transform()` — removed in FluentValidation v12
- Do not add `try/catch` blocks to controllers
- Do not catch `DbUpdateException` in the Application layer
- Do not log raw `UserId` — always use `HashUserId()` and log `HashedUserId`
- Do not add new `EvaluationResult` subtypes without adding a `LogResult` branch and
  updating the `UnreachableException` message

---

## 📌 Definition of Done — Phase 1

- [x] `InputSanitizer` implemented and called in validators and service layer
- [x] `FluentValidation` v12 on all three request DTOs
- [x] Manual `ValidateAsync` in controllers
- [x] CSharpier formatting enforced — CI blocks on violations
- [x] CI — `lint-format` and `build-test` parallel jobs live
- [x] AI reviewer job live (PR #35) — activated by `ai-review` label
- [x] `ANTHROPIC_API_KEY` secret added to GitHub repo
- [x] Global exception middleware in place
- [x] Standardized `ProblemDetails` error response shape
- [x] Name uniqueness check with TOCTOU backstop (PR #37)
- [x] Route parameter guard — closes KI-008 (PR #37)
- [x] `StrategyConfigRules` extracted — closes KI-NEW-001 (PR #37)
- [x] Unit tests — 79/79 passing (PR #38 + PR #48)
- [x] Integration tests — 31/31 passing (PR #39)
- [x] NuGet locked restore — `--locked-mode` in CI, lock files committed
- [x] Evaluation decision logging (PR #48)
- [ ] `.http` smoke test file committed
- [ ] Seed data for local development

---

## 🧩 Notes for AI Assistants

- Architecture follows Clean Architecture: Api → Application → Domain ← Infrastructure
- `IFeatureFlagService` speaks entirely in DTOs — no `Flag` entity crosses the boundary
- `GlobalExceptionMiddleware` is registered first in `Program.cs` — wraps entire pipeline
- Controllers contain only the happy path — zero `try/catch` blocks anywhere in Api
- All error responses return `ProblemDetails` with `Content-Type: application/problem+json`
- `RouteParameterGuard.ValidateName(name)` is the first call in GET/PUT/DELETE by name
- `StrategyConfigRules` is the single source of truth for strategy config validation
- `EnvironmentRules` is the single source of truth for environment validation
- `SaveChangesAsync` catches Postgres `23505` → `DuplicateFlagNameException` — do not remove
- `ExistsAsync` checks non-archived flags only
- `CreateFlagRequest.StrategyConfig` and `UpdateFlagRequest.StrategyConfig` are `string?`
- `GetByNameAsync` has a named route — used by `CreatedAtRoute` in POST; do not remove
- `public partial class Program { }` is required for `WebApplicationFactory<Program>`
- Connection string uses `Host=postgres` — do not change to `localhost`
- Both Infrastructure and Api require `Microsoft.EntityFrameworkCore.Design` with
  `PrivateAssets=all`
- Do not use `FluentValidation.AspNetCore`, `AddFluentValidationAutoValidation()`,
  or `.Transform()`
- Any spec with ProblemDetails must specify `application/problem+json`
- Any spec with uniqueness checks must address TOCTOU and designate the correct layer
- Any spec with `JsonDocument` code must use `using JsonDocument doc = ...`
- Any spec with optional DTO fields must state nullability explicitly
- `HashUserId` is `internal static` on `FeatureFlagService` — never log raw `UserId`
- `EvaluationReason` must be an explicit named log dimension — never infer from message shape
- Any new `EvaluationResult` subtype requires a `LogResult` branch; omission throws
  `UnreachableException` at runtime
- CI restore uses `--locked-mode` — do not remove; `packages.lock.json` must be
  committed when adding new packages (CI does not use `NuGet.config` to override)
