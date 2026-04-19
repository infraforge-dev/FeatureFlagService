# Current State — Banderas

---

## Table of Contents

- [Status Summary](#-status-summary)
- [What Is Completed](#-what-is-completed)
- [What Is Not Yet Built](#-what-is-not-yet-built-phase-15-remaining)
- [Known Issues](#-known-issues)
- [Current Focus](#-current-focus)
- [What Not To Do Right Now](#-what-not-to-do-right-now)
- [Definition of Done — Phase 1](#-definition-of-done--phase-1)
- [Definition of Done — Phase 1.5](#-definition-of-done--phase-15)
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
**Phase 1 — Smoke Test File (`requests/smoke-test.http`): ✅ Complete**

**🎉 Phase 1 — MVP Completion: ✅ COMPLETE**

113/113 tests passing (81 unit + 32 integration).

---

**Phase 1.5 — Azure Key Vault Integration (PR #50): ✅ Complete**
**Phase 1.5 — Application Insights Integration (PR #51): ✅ Complete**
**Phase 1.5 — AI Flag Health Analysis Endpoint (PR #52): 🔲 Not started**

**Phase 1.5 — Azure Foundation + AI Integration: 🔄 In Progress**

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
- `IBanderasRepository` interface — async signatures with `CancellationToken`
- Domain exceptions: `FlagNotFoundException`, `DuplicateFlagNameException`,
  `BanderasValidationException`

### Application Layer

- `NoneStrategy` — passthrough, always returns true
- `PercentageStrategy` — deterministic SHA256 hashing into 100 buckets
- `RoleStrategy` — config-driven, case-insensitive, fail-closed role matching
- `FeatureEvaluator` — registry dispatch, `Dictionary<RolloutStrategy, IRolloutStrategy>`
- `BanderasService` — async, orchestrates repository + evaluator + logging + telemetry
- `IBanderasService` — async signatures with `CancellationToken`, full CRUD + evaluation
- `DependencyInjection.cs` — `AddApplication()` extension method
- DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`,
  `EvaluationResponse`, `FlagMappings`
- `InputSanitizer` — single source of sanitization at HTTP boundary
- `EvaluationResult` discriminated union — `FlagDisabled`, `StrategyEvaluated`, `EvaluationReason`
- `ITelemetryService` — telemetry abstraction; Application layer has no SDK reference

### Infrastructure Layer

- `BanderasRepository` — EF Core async repository
- `BanderasDbContext` — Postgres via EF Core
- `DatabaseSeeder` — six seed flags; `IsSeeded` stamped `true`; idempotent
- `DependencyInjection.cs` — `AddInfrastructure(IConfiguration, IHostEnvironment)` extension
- `ApplicationInsightsTelemetryService` — `TelemetryClient`-backed; emits `flag.evaluated`
  custom events with `FlagName`, `Result`, `Strategy`, `Environment` dimensions
- `NullTelemetryService` — no-op; registered when environment is `"Testing"`

### Api Layer

- `BanderasController` — full CRUD (create, read, update, archive)
- `EvaluationController` — `POST /api/evaluate`
- RFC 9457 `ProblemDetails` global exception middleware
- `RouteParameterGuard` — route `{name}` validation
- FluentValidation v12 — manual `ValidateAsync()` in controllers
- OpenAPI enrichment — Scalar UI, enum schema transformer, XML doc comments
- `requests/smoke-test.http` — all 6 endpoints covered

### Azure / Infrastructure

- Key Vault integration — `DefaultAzureCredential`, configuration provider pattern,
  `ConnectionStrings--DefaultConnection` sourced from `kv-banderas-dev`
- Application Insights — `AddApplicationInsightsTelemetry()` auto-captures requests,
  exceptions, and EF Core dependencies; `flag.evaluated` custom event per evaluation;
  connection string sourced from Key Vault (`ApplicationInsights--ConnectionString`)
- `appsettings.json` — `Azure:KeyVaultUri` and `ApplicationInsights:ConnectionString`
  placeholders present; real values from Key Vault at runtime

### CI/CD

- `lint-format` job — CSharpier check, blocks on violations
- `build-test` job — `dotnet build` with `-p:TreatWarningsAsErrors=true`,
  `dotnet test` for unit and integration suites
- `integration-test` job — Testcontainers Postgres, 32 integration tests
- `ai-review` job — activated by `ai-review` label; Claude API code review
  posted as PR comment; depends on all three prior jobs
- NuGet locked restore enforced via `--locked-mode`; `packages.lock.json` committed

### Tests

- 81 unit tests — strategies, evaluator, validators, logging behavior
- 32 integration tests — all 6 endpoints via Testcontainers Postgres
- 113/113 passing
- `AssemblyInfo.cs` — `InternalsVisibleTo("Banderas.Tests")`
- `BanderasServiceLoggingTests` — local `NullTelemetryService` stub satisfies updated constructor

### Developer Experience

- `requests/smoke-test.http` — all 6 endpoints covered ✅
- `DatabaseSeeder` — six seed flags available immediately on `docker compose up`

---

## 🚧 What Is Not Yet Built — Phase 1.5 Remaining

- [ ] AI flag health analysis endpoint (PR #52) — natural language summary of
  flag status via Azure OpenAI + Semantic Kernel; `IPromptSanitizer` introduced
  alongside `IAiFlagAnalyzer`
- [ ] Architecture Review Document — technical health audit before Phase 2

---

## 🐛 Known Issues

### KI-007 — devcontainer network requires `Host=postgres`

The connection string must use `Host=postgres` (the Docker Compose service name),
not `localhost`. This is correct for the devcontainer environment. Do not change it.

**Longer-term fix:** Full docker-compose devcontainer setup. Deferred to Phase 8.

---

## 🎯 Current Focus

**Phase 1.5 — Azure Foundation + AI Integration**

### Immediate Next Tasks

1. AI flag health analysis endpoint spec + implementation (PR #52)
2. Architecture Review Document — technical health audit before Phase 2

---

## 🧭 What Not To Do Right Now

- No authentication or authorization yet (Phase 3)
- No caching layer yet (Phase 6)
- No advanced rollout strategies yet (Phase 5)
- No UI work
- Do not change `Host=postgres` back to `localhost` in connection string
- Do not start Phase 2 until Phase 1.5 DoD is met and architecture review is complete

---

## 📌 Definition of Done — Phase 1

- [x] `FluentValidation` on all request DTOs
- [x] Global exception middleware — RFC 9457 ProblemDetails
- [x] Input sanitization + route parameter hardening
- [x] Name uniqueness with TOCTOU protection
- [x] Unit tests for all strategies and evaluator
- [x] CI pipeline — format gate + zero-warnings build
- [x] AI PR reviewer in CI
- [x] Integration tests for all 6 endpoints
- [x] `.http` smoke test file committed
- [x] Seed data for local development
- [x] Evaluation decision logging

**Phase 1 DoD: ✅ COMPLETE**

---

## 📌 Definition of Done — Phase 1.5

- [x] Azure Key Vault integration — connection string sourced from vault at startup
- [x] Application Insights integration — structured telemetry, evaluation custom events
- [ ] AI flag health analysis endpoint — natural language flag status via Azure OpenAI
- [ ] `IPromptSanitizer` introduced alongside `IAiFlagAnalyzer`
- [ ] Architecture Review Document committed to `Docs/`

---

## 📝 Spec Writing — Lessons Learned

- RFC 9457 ProblemDetails — response content type must be `application/problem+json`,
  not `application/json`
- FluentValidation v12 — `.Transform()` removed; use `Must()` lambda instead
- `FluentValidation.AspNetCore` deprecated — use manual `ValidateAsync()` in controllers
- CSharpier 1.x — subcommand syntax: `dotnet csharpier check .` not `--check`
- `System.Text.Json` in .NET 10 — `schema.Type` is `JsonSchemaType` flags enum;
  model types in root `Microsoft.OpenApi` namespace, not `Microsoft.OpenApi.Models`
- Integration test factory must use `UseEnvironment("Testing")` to prevent
  `appsettings.Development.json` from loading Azure config during tests
- `AddInfrastructure()` must accept `IHostEnvironment` to support conditional
  service registration (e.g. `NullTelemetryService` vs `ApplicationInsightsTelemetryService`)
- `TelemetryClient` must be registered as Singleton — it maintains an internal buffer
  and is designed for application-lifetime use
- Unit tests that construct `BanderasService` directly need a `NullTelemetryService`
  stub — add as a private class in the test file; no new project reference required

---

## 🧩 Notes for AI Assistants

- The system is not production-ready
- Prioritize correctness over feature expansion
- Follow Clean Architecture — dependencies point inward toward Domain
- Work within the established layer boundaries (Api → Application → Domain ← Infrastructure)
- `IBanderasService` speaks entirely in DTOs — never return `Flag` from the service
- All evaluation logic must remain deterministic and isolated from persistence
- `FeatureEvaluator` is a pure function — no side effects, no ILogger, no ITelemetryService
- `BanderasService` is the imperative shell — owns all side effects (logging, telemetry)
- `appsettings.Development.json` is intentionally committed — local Docker defaults only
- Connection string uses `Host=postgres` — do not change to `localhost`
- Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design`
  with `PrivateAssets=all`
- `ITelemetryService` lives in Application layer — no SDK reference there
- `NullTelemetryService` is registered when `IHostEnvironment.IsEnvironment("Testing")`
  is true — integration tests resolve it automatically via `UseEnvironment("Testing")`
