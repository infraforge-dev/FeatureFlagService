# Roadmap — FeatureFlagService

---

## Table of Contents

- [Product Vision](#-product-vision)
- [Phase Map](#-phase-map)
- [Phase 0 — Foundation](#-phase-0--foundation--complete)
- [Phase 1 — MVP Completion](#-phase-1--mvp-completion--current-focus)
- [Phase 1.5 — Azure Foundation + AI Integration](#-phase-15--azure-foundation--ai-integration-)
- [Phase 2 — Testing & Reliability](#-phase-2--testing--reliability)
- [Phase 3 — Auth, Authorization & Rate Limiting](#-phase-3--authentication-authorization--rate-limiting)
- [Phase 4 — Observability & Debugging](#-phase-4--observability--debugging)
- [Phase 5 — Advanced Rollout Strategies](#-phase-5--advanced-rollout-strategies)
- [Phase 6 — Performance & Scaling](#-phase-6--performance--scaling)
- [Phase 7 — .NET SDK](#-phase-7--net-sdk--key-product-milestone)
- [Phase 8 — Production Readiness](#-phase-8--production-readiness)
- [Phase 9 — Open Core Launch](#-phase-9--open-core-launch)
- [Current Focus](#-current-focus)
- [Notes for AI Assistants](#-notes-for-ai-assistants-claude-context)
- [Long-Term Vision](#-long-term-vision)

---

## 🎯 Product Vision

**Azure-native. .NET-first. AI-assisted feature flag management.**

FeatureFlagService is being built to compete in the developer tooling market as the
feature flag platform for teams that live in the Microsoft ecosystem. The competitive
positioning is specific and deliberate:

- **Azure-native** — Key Vault, App Insights, Container Apps, and Azure OpenAI are
  not integrations bolted on later. They are designed in from Phase 1.5 onward.
- **.NET-first** — A production-quality .NET SDK ships alongside the service.
  ASP.NET Core teams should feel at home in under 15 minutes.
- **AI-assisted** — Natural language flag health analysis, stale flag detection,
  rollout risk reasoning, and evaluation debugging are core product features.
- **Open core** — Self-hostable, MIT licensed. Managed hosting and enterprise
  features are the business model. Not venture-dependent.

**The beachhead:** Mid-market engineering teams running .NET on Azure who find
LaunchDarkly expensive and Unleash under-supported. This is an underserved gap
in the current market.

**The demo that validates the product:** An engineer clones the repo, runs
`docker compose up`, has a working flag service with a .NET SDK, and asks it
*"which of my flags need attention?"* — all in under 15 minutes.

Every phase of this roadmap builds toward that demo.

---

## 🗺️ Phase Map

```
Phase 0  ✅  Foundation — domain, strategies, persistence, API
Phase 1  🔄  MVP Completion — validation, sanitization, CI, testing, error handling
Phase 1.5    Azure Foundation + AI — Key Vault, App Insights, AI analysis endpoint
Phase 2      Testing & Reliability — full test coverage, contract tests
Phase 3      Auth & Security — JWT, RBAC, rate limiting, audit trail
Phase 4      Observability — evaluation telemetry, debugging endpoint, dashboards
Phase 5      Advanced Strategies — user targeting, time-based, gradual rollout
Phase 6      Performance — caching, Redis, horizontal scaling
Phase 7      .NET SDK — first-class SDK, NuGet package, SDK docs        ← key product milestone
Phase 8      Production Readiness — CD to Azure, AKS deployment, SLA baseline
Phase 9      Open Core Launch — public repo, self-hosted Docker image, hosted offering
```

---

## 🧱 Phase 0 — Foundation ✅ Complete

### Core Domain & Architecture

* [x] Define `Flag` domain entity
* [x] Define `RolloutStrategy` enum (None, Percentage, RoleBased)
* [x] Define `EnvironmentType` enum (None = 0 sentinel, Development, Staging, Production)
* [x] Implement `FeatureEvaluationContext` value object
* [x] Enforce encapsulation (private setters, explicit mutation methods)
* [x] Clean Architecture project structure (Domain, Application, Infrastructure, Api, Tests)
* [x] Dependency directions enforced (Domain has no outward dependencies)

### Application Layer

* [x] Define `IFeatureFlagService` interface — async signatures with `CancellationToken`
* [x] Define `IRolloutStrategy` interface — includes `StrategyType` property for registry dispatch
* [x] Implement `FeatureEvaluator` — registry dispatch pattern, dictionary keyed by `RolloutStrategy`
* [x] Separate evaluation logic from domain

### Strategy Pattern

* [x] Implement `NoneStrategy` — passthrough, always returns true
* [x] Implement `PercentageStrategy` — deterministic SHA256 hashing into buckets
* [x] Implement `RoleStrategy` — config-driven, case-insensitive, fail-closed role matching

### Application Service & DTOs

* [x] `FeatureFlagService` — async, orchestrates repository + evaluator
* [x] DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`,
      `EvaluationResponse`, `FlagMappings`
* [x] `DependencyInjection.cs` — `AddApplication()` extension method

### API Layer

* [x] `FeatureFlagsController` — full CRUD: GET all, GET by name, POST, PUT, DELETE (soft archive)
* [x] `EvaluationController` — `POST /api/evaluate`
* [x] Scalar UI replacing Swagger — `/scalar/v1`
* [x] OpenAPI enrichment: `EnumSchemaTransformer`, `ApiInfoTransformer`, XML doc comments,
      `ProducesResponseType` attributes, `EvaluationResponse` DTO

### Infrastructure

* [x] EF Core + Npgsql — `FeatureFlagDbContext`, `FlagConfiguration` (Fluent API)
* [x] `StrategyConfig` stored as `jsonb`
* [x] Partial unique index on `(Name, Environment)` filtered to `IsArchived = false`
* [x] `FeatureFlagRepository` — implements `IFeatureFlagRepository`
* [x] `docker-compose.yml` at repo root — one-command local Postgres setup
* [x] `Docs/Decisions/` folder established for Architecture Decision Records

### Architectural Cleanup

* [x] `IFeatureFlagService` — no `Flag` entity in any method signature
* [x] All signatures use DTOs: `FlagResponse`, `CreateFlagRequest`, `UpdateFlagRequest`
* [x] `Flag` construction moved from controller into `FeatureFlagService.CreateFlagAsync`
* [x] `UpdateFlagAsync` accepts `UpdateFlagRequest` DTO

### Dev Environment

* [x] DevContainer: `devcontainers/base:ubuntu-24.04` + .NET 10 SDK feature
* [x] Docker-outside-of-Docker configured with `postStartCommand` network join
* [x] `.config/dotnet-tools.json` — `dotnet-ef` and `csharpier` tool manifests

---

## 🔄 Phase 1 — MVP Completion (Current Focus)

### Validation & Sanitization ✅ Complete (PR #30)

* [x] `InputSanitizer` — trims whitespace, strips ASCII control characters
* [x] `CreateFlagRequestValidator` — name allowlist regex, env sentinel guard,
      StrategyConfig cross-field rules (via `.Must()` — FluentValidation v12)
* [x] `UpdateFlagRequestValidator` — StrategyConfig cross-field rules
* [x] `EvaluationRequestValidator` — FlagName, UserId, Environment validation
* [x] Manual `ValidateAsync` in controllers — no FluentValidation.AspNetCore

### Code Style Foundation ✅ Complete (PR #33)

* [x] `.editorconfig` — LF line endings, naming conventions, Roslyn diagnostic severities
* [x] `.gitattributes` — LF normalization for all source file types
* [x] `.csharpierrc.json` — `printWidth: 100`
* [x] `.csharpierignore` — excludes Migrations, bin, obj, generated files
* [x] `.config/dotnet-tools.json` — CSharpier pinned to specific version
* [x] `.vscode/settings.json` — `formatOnSave: true`, CSharpier as default formatter
* [x] All existing test classes decorated with `[Trait("Category", "Unit")]`

### CI Pipeline — Core Jobs ✅ Complete (PR #34)

> **Note:** CI/CD was originally scoped to Phase 8. The foundation pipeline
> (lint, format, build, test) was pulled forward to Phase 1 because enforcing
> code quality during active development has higher value than waiting until
> production readiness. CD (deployment to Azure) remains in Phase 8.

* [x] `.github/workflows/ci.yml` — `lint-format` and `build-test` parallel jobs
* [x] Triggers: push to branch prefixes; PR targeting `dev` or `main`
* [x] `dotnet csharpier check .` — format gate in `lint-format` job
* [x] `dotnet build -p:TreatWarningsAsErrors=true` — zero-warnings policy enforced
* [x] `dotnet test --filter "Category!=Integration"` — unit tests only in Phase 1
* [x] Concurrency group scoped to workflow + PR number
* [x] Node 24 opt-in via `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24`

### CI Pipeline — AI Reviewer ✅ Complete (PR #35)

* [x] `Docs/Decisions/spec-ai-reviewer.md` — spec document complete
* [x] `.github/workflows/ci.yml` — `ai-review` job fully implemented
* [x] `.github/prompts/ai-review-system.md` — system prompt in repo, read at runtime
* [x] `ANTHROPIC_API_KEY` secret added to GitHub repo
* [x] `ai-review` label created in GitHub repo
* [x] Fail-open behavior verified — transient API failures do not block merge

### Error Handling ✅ Complete (PR #36)

* [x] `FeatureFlagException` abstract base class in `Domain/Exceptions/`
* [x] `FlagNotFoundException` — 404, thrown by service layer on null flag lookup
* [x] `DuplicateFlagNameException` — 409, constructor accepts `(string, EnvironmentType)`
* [x] `GlobalExceptionMiddleware` — single catch-all; domain exceptions → named 4xx;
      unexpected → `LogError` + safe 500
* [x] Middleware registered first in `Program.cs`
* [x] All controllers cleaned — zero `try/catch` blocks
* [x] All error responses return `ProblemDetails` with `Content-Type: application/problem+json`
* [x] AI reviewer system prompt Rule 8 updated — `try/catch` in controllers is reviewable error

### Input Validation Hardening ✅ Complete (PR #37)

* [x] `StrategyConfigRules` extracted — shared `internal static` class in
      `FeatureFlag.Application/Validators/`; closes KI-NEW-001
* [x] `BeValidPercentageConfig` and `BeValidRoleConfig` removed from both validators;
      both now call `StrategyConfigRules.*`
* [x] `FeatureFlagValidationException` — 400 domain exception for route parameter failures
* [x] `RouteParameterGuard` — compiled regex allowlist guard in `FeatureFlag.Api/Helpers/`;
      closes KI-008
* [x] `RouteParameterGuard.ValidateName(name)` called first in `GetByNameAsync`,
      `UpdateAsync`, and `ArchiveAsync`
* [x] `ExistsAsync` added to `IFeatureFlagRepository` and implemented with `AnyAsync`
* [x] `CreateFlagAsync` — sanitizes name, calls `ExistsAsync`, throws
      `DuplicateFlagNameException` before insert
* [x] `SaveChangesAsync` in repository intercepts Postgres `23505` unique constraint
      violation and rethrows as `DuplicateFlagNameException` — handles TOCTOU race condition
* [x] `GlobalExceptionMiddleware` `409` case verified present — no change required

### Testing 🔄 In Progress

* [x] Unit tests for `PercentageStrategy`, `RoleStrategy`, `NoneStrategy`
* [x] Unit tests for `FeatureEvaluator` — dispatch, missing strategy fallback
* [x] Unit tests for all three validators — every acceptance criterion covered
* [ ] Integration tests for all 6 endpoints (requires Postgres service container)

### Developer Experience

* [ ] `.http` smoke test request file committed to repo (`requests/smoke-test.http`)
* [ ] Seed data for local development
* [ ] Evaluation decision logging

---

## 🌩️ Phase 1.5 — Azure Foundation + AI Integration

> Begins immediately after Phase 1 DoD is met.

### Azure Key Vault

* [ ] `Azure.Extensions.AspNetCore.Configuration.Secrets` wired into `Program.cs`
* [ ] Connection string moved from `appsettings.Development.json` to Key Vault
* [ ] `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_CLIENT_SECRET` documented in `.env.example`

### Azure Application Insights

* [ ] `Microsoft.ApplicationInsights.AspNetCore` added
* [ ] Structured request telemetry — flag name, environment, strategy, result
* [ ] Evaluation decision log events emitted as custom Application Insights events
* [ ] Exception telemetry — captures unhandled exceptions with full context

### AI Flag Analysis Endpoint

* [ ] `IAiFlagAnalyzer` interface — `AnalyzeAsync(IReadOnlyList<FlagSummary>)`
* [ ] `AiFlagAnalyzer` implementation — Azure OpenAI via Semantic Kernel
* [ ] `IPromptSanitizer` — defends against prompt injection via flag data
* [ ] `POST /api/analysis` — returns natural language flag health summary
* [ ] System prompt committed to `Docs/Prompts/flag-analysis-system.md`

---

## 🧪 Phase 2 — Testing & Reliability

* [ ] Integration test project with Postgres service container
* [ ] Integration tests for all 6 endpoints
* [ ] Contract tests — API response shape regression coverage
* [ ] Test coverage baseline established and tracked in CI

---

## 🔐 Phase 3 — Authentication, Authorization & Rate Limiting

* [ ] JWT bearer authentication on management endpoints
* [ ] Role-based authorization — flag read vs flag write
* [ ] Evaluation endpoint optionally anonymous (SDK-compatible)
* [ ] `AddRateLimiter` on `/api/evaluate` keyed on authenticated identity

---

## 📊 Phase 4 — Observability & Debugging

* [ ] Structured audit events on all flag mutations
* [ ] `GET /api/flags/{name}/evaluation-trace` — "Why was this flag ON/OFF?"
* [ ] Anomaly detection — unusual evaluation pattern alerts
* [ ] Dashboard integration (Azure Monitor or Grafana)

---

## 🎯 Phase 5 — Advanced Rollout Strategies

* [ ] User targeting by ID
* [ ] Time-based activation (scheduled flags)
* [ ] Gradual rollout (time + percentage combined)
* [ ] Dynamic strategy registration (DI-driven)
* [ ] Strategy config validation framework

---

## ⚡ Phase 6 — Performance & Scaling

* [ ] In-memory caching layer between service and repository
* [ ] Redis cache option for distributed deployments
* [ ] Horizontal scaling validation — stateless API design confirmed
* [ ] Evaluation path latency baseline and regression tests

---

## 📦 Phase 7 — .NET SDK ⭐ Key Product Milestone

* [ ] `FeatureFlag.Client` NuGet package
* [ ] `IFeatureFlagClient` — `IsEnabledAsync(string flagName, string userId, string[] roles)`
* [ ] ASP.NET Core middleware extensions — `UseFeatureFlags()`
* [ ] Action filter attributes — `[RequireFlag("my-flag")]`
* [ ] Service registration helpers — `services.AddFeatureFlagClient()`
* [ ] SDK documentation and quickstart guide
* [ ] NuGet publish via GitHub Actions on tag

---

## 🚀 Phase 8 — Production Readiness

* [ ] CD pipeline to Azure Container Apps
* [ ] AKS deployment option documented
* [ ] SLA baseline established (p99 evaluation latency)
* [ ] Backup and migration strategy (EF Core)
* [ ] Full docker-compose devcontainer setup (resolves KI-007)
* [ ] README overhaul — product vision, market gap, architecture overview,
      getting started, AI-assisted dev workflow section, roadmap summary

---

## 🌐 Phase 9 — Open Core Launch

* [ ] Public repo prepared — `CONTRIBUTING.md`, issue templates, `good first issue` labels
* [ ] Self-hosted Docker image published to GitHub Container Registry
* [ ] Managed hosting offering documented
* [ ] Launch post and demo video

---

## 🎯 Current Focus

**Phase 1 — Testing & Developer Experience (final stretch)**

1. Integration tests — all 6 endpoints (own PR, own session)
2. `.http` smoke test file (`requests/smoke-test.http`)
3. Seed data for local development
4. Evaluation decision logging

Phase 1 DoD is met when all four are complete. Phase 1.5 begins immediately after.

---

## 🧩 Notes for AI Assistants (Claude Context)

- Architecture follows Clean Architecture: Api → Application → Domain ← Infrastructure
- `IFeatureFlagService` speaks entirely in DTOs — no `Flag` entity crosses the boundary
- Domain logic is intentionally strict — no public setters, explicit mutation methods
- Strategy pattern is central to extensibility — new strategies require zero changes to evaluator
- Evaluation must remain deterministic and testable
- `GlobalExceptionMiddleware` wraps the entire pipeline — controllers contain only happy path
- All error responses return `ProblemDetails` with `Content-Type: application/problem+json`
- `RouteParameterGuard.ValidateName(name)` is the first call in `GetByNameAsync`,
  `UpdateAsync`, and `ArchiveAsync` — do not remove or reorder
- `StrategyConfigRules` is the single source of truth for strategy config validation methods
- `SaveChangesAsync` in `FeatureFlagRepository` catches Postgres `23505` and rethrows as
  `DuplicateFlagNameException` — intentional TOCTOU handling, do not remove
- `ExistsAsync` checks non-archived flags only — archived flags do not block name reuse
- Connection string uses `Host=postgres` — do not change to `localhost`
- Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design`
  with `PrivateAssets=all`
- Do not use `FluentValidation.AspNetCore`, `AddFluentValidationAutoValidation()`,
  or `.Transform()` — all deprecated or removed in FluentValidation v12
- Any spec referencing ProblemDetails must specify `application/problem+json`
- Any spec with uniqueness checks must address TOCTOU and designate the correct layer
- Any spec providing `JsonDocument` code must use `using JsonDocument doc = ...`

---

## 🔭 Long-Term Vision

- Full **Observability + Experimentation Platform**
- A/B testing with statistical significance tracking
- Natural language flag creation — *"Roll out the payment flow to 10% of production users"*
- Smart rollout recommendations based on evaluation patterns
- Integration with analytics pipelines and real-time dashboards
- Anomaly detection — automatic stale flag and unusual distribution alerts
