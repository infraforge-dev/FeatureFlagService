# Current State — Bandera

---

## Table of Contents

- [Status Summary](#-status-summary)
- [What Is Completed](#-what-is-completed)
- [What Is Not Yet Built](#-what-is-not-yet-built-phase-1-remaining)
- [Known Issues](#-known-issues)
- [Current Focus](#-current-focus)
- [What Not To Do Right Now](#-what-not-to-do-right-now)
- [Definition of Done — Phase 1](#-definition-of-done--phase-1)
- [Spec Writing — Lessons Learned](#-spec-writing--lessons-learned)
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
**Phase 1 — Seed Data for Local Development (PR #49): ✅ Complete**

113/113 tests passing (81 unit + 32 integration).

**One task remains before Phase 1 DoD is declared complete:**
1. `.http` smoke test file (`requests/smoke-test.http`)

Phase 1.5 begins immediately after the smoke test file is committed.

---

## ✅ What Is Completed

### Domain Layer

- `Flag` entity with controlled mutation (private setters, explicit mutation methods)
- `Flag.Update()` — atomic method that sets enabled state, strategy, and `UpdatedAt`
- `Flag.IsSeeded` — provenance marker (`bool`, default `false`); stamped `true` by
  `DatabaseSeeder` at insert time; never exposed on any DTO or API response
- `Flag` constructor overload accepting `isSeeded` — used by seeder only; existing
  constructor unchanged, defaults `isSeeded` to `false`
- `FeatureEvaluationContext` value object — `IEquatable<T>`, guard clauses, immutable roles
- `RolloutStrategy` enum (None, Percentage, RoleBased)
- `EnvironmentType` enum (None = 0 sentinel, Development, Staging, Production)
- `IRolloutStrategy` interface — includes `StrategyType` for registry dispatch
- `IBanderaRepository` interface — async signatures with `CancellationToken`
- Domain exceptions: `FlagNotFoundException`, `DuplicateFlagNameException`,
  `BanderaValidationException`

### Application Layer

- `NoneStrategy` — passthrough, always returns true
- `PercentageStrategy` — deterministic SHA256 hashing into 100 buckets
- `RoleStrategy` — config-driven, case-insensitive, fail-closed role matching
- `FeatureEvaluator` — registry dispatch, `Dictionary<RolloutStrategy, IRolloutStrategy>`
- `Bandera` — async, orchestrates repository + evaluator + logging
- `IBanderaService` — async signatures with `CancellationToken`, full CRUD + evaluation
- `DependencyInjection.cs` — `AddApplication()` extension method
- DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`,
  `EvaluationResponse`, `FlagMappings`
- `InputSanitizer` — single source of truth for HTTP boundary sanitization
- `EnvironmentRules` — single source of truth for environment validation
- `StrategyConfigRules` — single source of truth for strategy config validation
- Validators: `CreateFlagRequestValidator`, `UpdateFlagRequestValidator`,
  `EvaluationRequestValidator` (FluentValidation v12)
- `EvaluationResult` discriminated union — `FlagDisabled` | `StrategyEvaluated`
- `EvaluationReason` enum — explicit reason dimension on every evaluation log entry

### Infrastructure Layer

- `BanderaDbContext` — EF Core, Postgres, `ApplyConfigurationsFromAssembly`
- `BanderaDbContextFactory` — design-time factory for `dotnet ef` tooling
- `FlagConfiguration` — EF Core Fluent API mapping; partial unique index
  `HasFilter("\"IsArchived\" = false")` prevents archived rows from blocking name reuse;
  `IsSeeded` column mapped with `HasDefaultValue(false)`
- `AddIsSeededToFlag` migration — adds `IsSeeded bool NOT NULL DEFAULT false`;
  existing rows receive `false` automatically
- `BanderaRepository` — async CRUD + `ExistsAsync`; `SaveChangesAsync` catches
  Postgres `23505` and rethrows as `DuplicateFlagNameException`
- `DatabaseSeeder` (`public sealed`) — runs on startup in Development only;
  per-record backfill in normal mode; `SEED_RESET=true` wipes `IsSeeded = true`
  rows and re-inserts; skips slots occupied by non-seeded active flags with a
  `Warning` log; seeds six representative flags across all three strategies and
  two environments
- `DependencyInjection.cs` — `AddInfrastructure()` registers `BanderaRepository`
  and `DatabaseSeeder`

### Api Layer

- `BanderasController` — full CRUD (Create, GetAll, GetByName, Update, Archive)
- `EvaluationController` — POST `/api/evaluate`
- `GlobalExceptionMiddleware` — catches domain exceptions, maps to RFC 9457
  `ProblemDetails` with `Content-Type: application/problem+json`
- `RouteParameterGuard` — compiled regex allowlist; called first in GetByName,
  Update, Archive
- OpenAPI enrichment — Scalar UI, `EnumSchemaTransformer`, `ApiInfoTransformer`,
  XML doc comments, `[ProducesResponseType]` attributes
- `WebApplicationExtensions.MigrateAsync()` — runs `db.Database.MigrateAsync()`
  in a scoped service provider; called in Development startup block before seeder
- `Program.cs` Development startup block — runs migration then seeder on every
  startup; `SEED_RESET` env var controls reset mode

### CI/CD

- `lint-format` job — CSharpier check, blocks on violations
- `build-test` job — `dotnet build` with `-p:TreatWarningsAsErrors=true`,
  `dotnet test` for unit and integration suites
- `integration-test` job — Testcontainers Postgres, 32 integration tests
- `ai-review` job — activated by `ai-review` label; Claude API code review
  posted as PR comment; depends on all three prior jobs
- NuGet locked restore enforced via `--locked-mode`; `packages.lock.json` committed

### Tests

- 81 unit tests — strategies, evaluator, validators
- 32 integration tests — all 6 endpoints via Testcontainers Postgres
- 2 production bugs caught by unit tests (PR #38)
- 2 production bugs caught by integration tests (PR #39)

### Developer Experience

- `requests/smoke-test.http` — partial; covers all 6 endpoints (needs final review)
- `DatabaseSeeder` — six seed flags available immediately on `docker compose up`

---

## 🚧 What Is Not Yet Built — Phase 1 Remaining

- [ ] `.http` smoke test file reviewed and finalized (`requests/smoke-test.http`)

---

## 🐛 Known Issues

### KI-007 — devcontainer network requires `Host=postgres`

The connection string must use `Host=postgres` (the Docker Compose service name),
not `localhost`. This is correct for the devcontainer environment. Do not change it.

**Longer-term fix:** Full docker-compose devcontainer setup. Deferred to Phase 8.

---

## 🎯 Current Focus

**Phase 1 — Final Task**

1. `.http` smoke test file reviewed, finalized, and committed (`requests/smoke-test.http`)

Phase 1 DoD is complete when this is shipped. Phase 1.5 begins immediately after.

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
- Do not expose `IsSeeded` on any DTO or API response
- Do not set `IsSeeded = true` anywhere outside `DatabaseSeeder`

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
- [x] Standardized `ProblemDetails` error responses with `application/problem+json`
- [x] `RouteParameterGuard` — route parameter allowlist with compiled regex
- [x] `DuplicateFlagNameException` — TOCTOU-safe uniqueness via `DbUpdateException` catch
- [x] Unit tests — 81 passing; 2 production bugs caught and fixed
- [x] Integration tests — 32 passing; 2 production bugs caught and fixed
- [x] `integration-test` CI job live; `ai-review` depends on all three jobs
- [x] Evaluation decision logging — discriminated union result, `EvaluationReason`,
      SHA256 `HashedUserId`, structured log output
- [x] NuGet locked restore enforced in CI
- [x] Seed data for local development — `DatabaseSeeder`, six flags, `IsSeeded`
      provenance marker, reset mode via `SEED_RESET=true` (PR #49)
- [x] `MigrateAsync()` on startup in Development — schema guaranteed before seeding
- [ ] `.http` smoke test file committed and finalized

---

## 📚 Spec Writing — Lessons Learned

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
output. Use a SHA256 surrogate (`HashedUserId`) from the start.

**`EvaluationReason` must be a first-class log dimension:** Always log `Reason`
explicitly — inferring it from branch shape is fragile.

**`IsEnabled` guard before logging:** Add `if (!_logger.IsEnabled(level)) return;`
in any void log helper method (CA1873).

**Pin test package versions explicitly:** Floating NuGet references in test projects
cause CI drift. Pin to exact version; commit `packages.lock.json`.

**Provenance markers beat name-matching for seeder ownership:** A seeder that
identifies its rows by name cannot distinguish seeded from manually-created flags
that share the same identity. An `IsSeeded` column is unambiguous, cheap, and
self-documenting.

**`internal` does not cross assembly boundaries:** `internal` is assembly-scoped.
A type in `Infrastructure` marked `internal` is not accessible from `Api` without
`InternalsVisibleTo`. Prefer constructor overloads or narrowly scoped public members.

**Seed identities are reserved baseline slots, not enforced constraints:** The
seeder treats its manifest identities as the expected local dev baseline, but
does not block developers from occupying those slots. Reset skips occupied slots
and logs a clear override warning rather than failing or deleting manual data.

---

## 🧩 Notes for AI Assistants

- Architecture follows Clean Architecture: Api → Application → Domain ← Infrastructure
- `IBanderaService` speaks entirely in DTOs — no `Flag` entity crosses the boundary
- Domain logic is intentionally strict — no public setters, explicit mutation methods
- Strategy pattern is central to extensibility — new strategies require zero changes to evaluator
- Evaluation must remain deterministic and testable
- `GlobalExceptionMiddleware` wraps the entire pipeline — controllers contain only happy path
- All error responses return `ProblemDetails` with `Content-Type: application/problem+json`
- `RouteParameterGuard.ValidateName(name)` is the first call in `GetByNameAsync`,
  `UpdateAsync`, and `ArchiveAsync` — do not remove or reorder
- `StrategyConfigRules` is the single source of truth for strategy config validation
- `EnvironmentRules` is the single source of truth for environment validation
- `SaveChangesAsync` in `BanderaRepository` catches Postgres `23505` and rethrows
  as `DuplicateFlagNameException` — intentional TOCTOU handling, do not remove
- `ExistsAsync` checks non-archived flags only — archived flags do not block name reuse
- `CreateFlagRequest.StrategyConfig` and `UpdateFlagRequest.StrategyConfig` are `string?`
- `GetByNameAsync` has a named route — used by `CreatedAtRoute` in POST; do not remove
- `public partial class Program { }` is required for `WebApplicationFactory<Program>`
- Connection string uses `Host=postgres` — do not change to `localhost`
- Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design`
  with `PrivateAssets=all`
- Do not use `FluentValidation.AspNetCore`, `AddFluentValidationAutoValidation()`,
  or `.Transform()` — all deprecated or removed in FluentValidation v12
- Any spec referencing ProblemDetails must specify `application/problem+json`
- Any spec with uniqueness checks must address TOCTOU and designate the correct layer
- `IsSeeded` must never appear on `FlagResponse` or any DTO
- `DatabaseSeeder` is `public sealed` — required for resolution from `Bandera.Api`
  across the assembly boundary; `internal` would cause `CS0122`
- `MigrateAsync()` runs before `SeedAsync()` in the Development startup block —
  order is load-bearing; do not swap
