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
**Phase 1 — Smoke Test File (`Requests/smoke-test.http`): ✅ Complete**

**🎉 Phase 1 — MVP Completion: ✅ COMPLETE**

**Phase 1.5 — Azure Key Vault Integration (PR #50): ✅ Complete**
**Phase 1.5 — Application Insights Integration (PR #51): ✅ Complete**
**Phase 1.5 — AI Flag Health Analysis Endpoint (PR #52): ✅ Complete**

**Phase 1.5 — Azure Foundation + AI Integration: ✅ Architecture Review Complete**

**Gate Decision:** GO WITH CONDITIONS

Audit report: `Docs/architecture-review-phase1-report.md`

146/146 tests passing (107 unit + 39 integration).

---

## ✅ What Is Completed

### Domain Layer

- `Flag` entity with controlled mutation (private setters, explicit mutation methods)
- `Flag.Update()` — atomic method that sets enabled state, strategy, and `UpdatedAt`
- `Flag.IsSeeded` — provenance marker (`bool`, default `false`); stamped `true` by
  `DatabaseSeeder` at insert time; never exposed on any DTO or API response
- `Flag` constructor overload accepting `isSeeded` — used by seeder only
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
  + prompt sanitization + AI analysis
- `IBanderasService` — async signatures with `CancellationToken`, full CRUD + evaluation
  + `AnalyzeFlagsAsync`; current evaluation path still accepts
  `FeatureEvaluationContext` directly as an intentional immutable value-object
  boundary input
- DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`,
  `FlagMappings`, `FlagHealthRequest`, `FlagAssessment`, `FlagHealthAnalysisResponse`
- `FlagResponse.StrategyConfig` — `string?` (nullable); flags with `RolloutStrategy.None`
  have no strategy config
- `IPromptSanitizer` / `PromptSanitizer` — newline normalization, instruction override
  phrase redaction, role confusion marker stripping, 500-char length cap;
  `GeneratedRegex` for compile-time regex
- `IAiFlagAnalyzer` — Application interface; contract decoupled from Semantic Kernel
- `FlagHealthConstants` — `internal` named constants for default (30), min (1),
  max (365) staleness threshold
- `AiAnalysisUnavailableException` — signals AI service failure; caught by middleware → 503
- `DependencyInjection.cs` — `AddApplication()` extension method

### Infrastructure Layer

- EF Core + Npgsql repository (`BanderasRepository`)
- `IBanderasRepository.GetAllAsync(EnvironmentType? environment = null, ...)` —
  nullable environment param; `null` = no filter, returns all non-archived flags
  across all environments; passing an explicit value preserves scoped behavior
- `AiFlagAnalyzer` — Semantic Kernel + Azure OpenAI implementation; all failures
  wrapped as `AiAnalysisUnavailableException`
- `UnavailableAiFlagAnalyzer` — endpoint-scoped unavailable implementation used when
  `AzureOpenAI:Endpoint` is missing or blank
- Semantic Kernel and `DefaultAzureCredential` fully excluded from `Testing`
  environment — never instantiated during CI
- Missing `AzureOpenAI:Endpoint` no longer blocks app startup; non-AI endpoints stay
  available and AI analysis fails through the documented 503 path

### API Layer

- `BanderasController` — full CRUD + evaluation + `POST /api/flags/health`
- `EvaluationController` — evaluation endpoint
- `GlobalExceptionMiddleware` — RFC 9457 ProblemDetails; `WriteProblemDetailsAsync`
  extended with optional `type` param; dedicated `catch (AiAnalysisUnavailableException)`
  block → 503 with RFC URI
- `RouteParameterGuard` — route parameter hardening
- OpenAPI enrichment with Scalar UI
- `FluentValidation` v12 on all request DTOs including `FlagHealthRequestValidator`

### Azure Infrastructure (provisioned in `rg-banderas-dev`)

- `kv-banderas-dev` — Azure Key Vault; `ConnectionStrings--DefaultConnection` and
  `ApplicationInsights--ConnectionString` secrets enabled
- `appi-banderas-dev` — Azure Application Insights, West US
- `aoai-banderas-dev` — Azure OpenAI resource, East US, Standard S0;
  `gpt-5-mini` model deployment active
- `appsettings.json` — `Azure:KeyVaultUri`, `ApplicationInsights:ConnectionString`,
  `AzureOpenAI:Endpoint`, and `AzureOpenAI:DeploymentName` placeholders present;
  real values from Key Vault at runtime

### CI/CD

- `lint-format` job — CSharpier check, blocks on violations
- `build-test` job — `dotnet build` with `-p:TreatWarningsAsErrors=true`,
  `dotnet test` for unit and integration suites
- `integration-test` job — Testcontainers Postgres, 39 integration tests
- `ai-review` job — activated by `ai-review` label; Claude API code review
  posted as PR comment; depends on all three prior jobs
- NuGet locked restore enforced via `--locked-mode`; `packages.lock.json` committed

### Tests

- 107 unit tests — strategies, evaluator, validators, logging behavior,
  prompt sanitization (21), service analysis (5)
- 39 integration tests — all endpoints including `POST /api/flags/health` and
  missing-Azure-OpenAI startup resilience
- 146/146 passing
- `AssemblyInfo.cs` — `InternalsVisibleTo("Banderas.Tests")`
- `BanderasServiceLoggingTests` — `NullPromptSanitizer` + `NullAiFlagAnalyzer`
  hand-written stubs (consistent with existing `NullTelemetryService` pattern)
- `BanderasApiFactory` — `StubAiFlagAnalyzer` registered for deterministic
  integration test responses; no Azure calls in CI

### Developer Experience

- `Requests/smoke-test.http` — all endpoints covered including `POST /api/flags/health`
  (default threshold + `stalenessThresholdDays: 7` variants)
- `DatabaseSeeder` — six seed flags available immediately on `docker compose up`

---

## 🚧 What Is Not Yet Built — Follow-Up From The Audit

- [x] Remove the Azure OpenAI startup dependency from the global app boot path
- [x] Explicitly ratify the `FeatureEvaluationContext` service-boundary exception
- [x] Add end-to-end coverage for AI-unavailable `503` behavior
- [ ] Tighten AI response validation after model output is deserialized

---

## 🐛 Known Issues

### KI-007 — devcontainer network requires `Host=postgres`

The connection string must use `Host=postgres` (the Docker Compose service name),
not `localhost`. This is correct for the devcontainer environment. Do not change it.

**Longer-term fix:** Full docker-compose devcontainer setup. Deferred to Phase 8.

### KI-008 — AI response semantics are not validated after deserialization

`AiFlagAnalyzer` deserializes model output into `FlagHealthAnalysisResponse` but does
not verify that every flag is represented or that status values stay within the
documented set.

**Audit status:** Identified in `Docs/architecture-review-phase1-report.md`.
Fix in early Phase 2.

---

## 🎯 Current Focus

**Phase 2 Prep — Remaining Gate Conditions**

### Immediate Next Tasks

1. Add semantic validation for AI model responses before returning 200
2. Strengthen `Flag` invariants and direct domain tests before adding new input surfaces
3. Decide whether GET query environment validation should move to the HTTP boundary
   or remain documented as service-level validation

---

## 🧭 What Not To Do Right Now

- No authentication or authorization yet (Phase 3)
- No caching layer yet (Phase 6)
- No advanced rollout strategies yet (Phase 5)
- No UI work
- Do not change `Host=postgres` back to `localhost` in connection string
- Do not start broad Phase 2 work until the remaining gate-condition fixes are either
  completed or consciously deferred in writing

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
- [x] AI flag health analysis endpoint — `POST /api/flags/health`; natural language
  flag status via Azure OpenAI + Semantic Kernel; `IPromptSanitizer` introduced;
  DEFERRED-004 closed
- [x] Architecture Review completed — see `Docs/architecture-review-phase1-report.md`

**Phase 1.5 DoD: ✅ COMPLETE**

**Phase gate:** GO WITH CONDITIONS
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
  service registration (e.g. Semantic Kernel, `DefaultAzureCredential`)
- **Spec property name verification** — when writing code sketches in specs, verify
  property names against the actual DTO/entity. PR #52: spec used `f.RolloutStrategy`
  (the enum type name) instead of `f.StrategyType` (the property name) in
  `AiFlagAnalyzer.BuildPrompt`. Fix: always cross-reference the DTO file before
  publishing a spec with code samples.
- **Validator field naming in multi-validator controllers** — when a controller
  already has `_createValidator` / `_updateValidator`, new validators must be injected
  with explicit, action-scoped names (e.g. `_healthValidator`). Bare `_validator`
  is ambiguous and will not compile.
- `GeneratedRegex` attribute — prefer over `new Regex(...)` for patterns used in
  hot paths; compile-time generation avoids runtime allocation

---

## 🧩 Notes for AI Assistants

- Clean Architecture: Controller → Service → Evaluator → Strategy → Repository
- `Flag` does not cross the service boundary; evaluation intentionally passes
  immutable `FeatureEvaluationContext` into `IBanderasService.IsEnabledAsync`
- `IBanderasRepository.GetAllAsync` accepts `EnvironmentType? environment = null`;
  null means no environment filter (cross-environment query for health analysis)
- `FlagResponse.StrategyConfig` is `string?` — null guard required before sanitizing
- `AiAnalysisUnavailableException` extends `Exception` (not `BanderasException`) —
  middleware catches it explicitly before the generic handler
- Semantic Kernel and `DefaultAzureCredential` are excluded from `Testing` environment
- Integration test factory registers `StubAiFlagAnalyzer` — no live Azure calls in CI
- `UnavailableAiFlagAnalyzer` handles missing Azure OpenAI endpoint outside Testing;
  non-AI endpoints still start, AI health analysis returns 503 ProblemDetails
- Connection string uses `Host=postgres` — do not change to `localhost`
- Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design`
  with `PrivateAssets=all`
- Azure resources: Key Vault and App Insights in `rg-banderas-dev`;
  OpenAI (`aoai-banderas-dev`) in East US; App Insights (`appi-banderas-dev`) in West US
- GPT model deployment name: `gpt-5-mini` inside `aoai-banderas-dev`
