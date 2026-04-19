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
**Phase 1.5 — Application Insights Integration (PR #51): 🔲 Not started**
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
- `Banderas` service — async, orchestrates repository + evaluator + logging
- `IBanderasService` — async signatures with `CancellationToken`, full CRUD + evaluation
- `DependencyInjection.cs` — `AddApplication()` extension method
- DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`,
  `EvaluationResponse`, `FlagMappings`
- `InputSanitizer` — single source of truth for HTTP boundary sanitization
- `EnvironmentRules` — single source of truth for environment validation
- `StrategyConfigRules` — extracted shared validation logic across validators
- FluentValidation v12 on all request DTOs — `CreateFlagRequestValidator`,
  `UpdateFlagRequestValidator`, `EvaluationRequestValidator`
- Evaluation decision logging — `EvaluationReason` enum, `HashUserId` (SHA256 surrogate),
  `LogResult` structured output, `IsEnabled(LogLevel.Information)` guard (CA1873)

### Infrastructure Layer

- `BanderasRepository` — EF Core + Npgsql, full async CRUD + soft archive
- `BanderasDbContext` — EF Core 10, Npgsql provider
- `RouteParameterGuard` — compiled regex allowlist for `{name}` route parameters
- `GlobalExceptionMiddleware` — RFC 9457 `ProblemDetails`, `application/problem+json`
- OpenAPI enrichment — Scalar UI, `EnumSchemaTransformer`, `ApiInfoTransformer`,
  XML doc comments, `[ProducesResponseType]` attributes
- `WebApplicationExtensions.MigrateAsync()` — runs `db.Database.MigrateAsync()`
  in a scoped service provider; called in Development startup block before seeder
- `Program.cs` Development startup block — runs migration then seeder on every
  startup; `SEED_RESET` env var controls reset mode

### Azure Infrastructure (Phase 1.5 — Provisioned)

- `rg-banderas-dev` — Azure Resource Group, West US
- `kv-banderas-dev` — Azure Key Vault; `ConnectionStrings--DefaultConnection` secret enabled
- `appi-banderas-dev` — Azure Application Insights, West US
- `aoai-banderas-dev` — Azure OpenAI resource, East US, Standard S0
- `gpt-5-mini` model deployment — deployed inside `aoai-banderas-dev`, Standard tier

### Phase 1.5 — Key Vault Integration (PR #50) ✅ Complete

- `Azure.Extensions.AspNetCore.Configuration.Secrets` + `Azure.Identity` added to
  `Banderas.Api.csproj`
- `Program.cs` — `AddAzureKeyVault()` called with `DefaultAzureCredential` before
  all service registrations; guarded by `string.IsNullOrWhiteSpace(keyVaultUri)` for
  graceful local fallback
- `appsettings.json` — `"Azure": { "KeyVaultUri": "" }` placeholder added
- `appsettings.Development.json` — `Azure:KeyVaultUri` set to `kv-banderas-dev` URI;
  local Docker connection string retained as fallback
- `BanderasApiFactory.cs` — `UseEnvironment("Testing")` added to prevent Key Vault
  credential chain from firing during integration tests; `DatabaseSeeder.SeedAsync()`
  called explicitly in `InitializeAsync` since seeder no longer runs under "Testing"
  environment
- 113/113 tests passing after integration test factory fix

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
- 113/113 passing
- `AssemblyInfo.cs` — `InternalsVisibleTo("Banderas.Tests")`

### Developer Experience

- `requests/smoke-test.http` — all 6 endpoints covered ✅
- `DatabaseSeeder` — six seed flags available immediately on `docker compose up`

---

## 🚧 What Is Not Yet Built — Phase 1.5 Remaining

- [ ] Application Insights integration (PR #51) — structured telemetry sink;
  `EvaluationReason` from PR #48 feeds directly into custom events
- [ ] AI flag health analysis endpoint (PR #52) — natural language summary of
  flag status via Azure OpenAI + Semantic Kernel; `IPromptSanitizer` introduced
  alongside `IAiFlagAnalyzer`

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

1. Application Insights integration spec + implementation (PR #51)
2. AI flag health analysis endpoint spec + implementation (PR #52)
3. Architecture Review Document — technical health audit before Phase 2

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
- [ ] Application Insights integration — structured telemetry, evaluation custom events
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
- Key Vault secret naming: `--` maps to `:` in `IConfiguration` automatically

---

## 🧩 Notes for AI Assistants

- Follow Clean Architecture — dependencies point inward toward Domain
- Work within the established layer boundaries (Api → Application → Domain ← Infrastructure)
- `IBanderasService` speaks entirely in DTOs — never return `Flag` from the service
- All evaluation logic must remain deterministic and isolated from persistence
- `appsettings.Development.json` is intentionally committed — local Docker defaults only
- Connection string uses `Host=postgres` — do not change to `localhost`
- Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design`
  with `PrivateAssets=all`
- Integration test factory uses `UseEnvironment("Testing")` — do not change this;
  it prevents Key Vault credential chain from firing during tests
- Azure resources are provisioned in `rg-banderas-dev`, West US (App Insights) /
  East US (OpenAI)
- GPT model deployment name: `gpt-5-mini` inside `aoai-banderas-dev`
