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
Phase 1  🔄  MVP Completion — validation, sanitization, testing, error handling
Phase 1.5 🆕  Azure Foundation + AI — Key Vault, App Insights, AI analysis endpoint
Phase 2      Testing & Reliability — full test coverage, contract tests
Phase 3      Auth & Security — JWT, RBAC, rate limiting, audit trail
Phase 4      Observability — evaluation telemetry, debugging endpoint, dashboards
Phase 5      Advanced Strategies — user targeting, time-based, gradual rollout
Phase 6      Performance — caching, Redis, horizontal scaling
Phase 7      .NET SDK — first-class SDK, NuGet package, SDK docs        ← key product milestone
Phase 8      Production Readiness — CI/CD, AKS deployment, SLA baseline
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
* [x] Dependency directions enforced

### Application Layer

* [x] `IFeatureFlagService` interface — async, CancellationToken throughout
* [x] `IRolloutStrategy` interface — StrategyType property for registry dispatch
* [x] `FeatureEvaluator` — registry dispatch pattern
* [x] `NoneStrategy`, `PercentageStrategy`, `RoleStrategy`
* [x] `FeatureFlagService` — async, orchestrates repository + evaluator
* [x] DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`,
      `EvaluationRequest`, `FlagMappings`
* [x] `DependencyInjection.cs` — `AddApplication()` extension method

### Infrastructure & API

* [x] EF Core + Npgsql (Postgres)
* [x] `FlagConfiguration` — Fluent API, jsonb for StrategyConfig
* [x] Partial unique index on `(Name, Environment)` filtered to non-archived flags
* [x] `FeatureFlagRepository` — async, CancellationToken on all EF Core calls
* [x] `InitialCreate` migration — generated and applied
* [x] `docker-compose.yml` — one-command local Postgres setup
* [x] `FeatureFlagsController` — full CRUD
* [x] `EvaluationController` — POST `/api/evaluate`
* [x] Swagger/OpenAPI at `/openapi/v1.json`
* [x] `JsonStringEnumConverter` wired — enums serialize as strings

### Dev Environment

* [x] DevContainer: .NET 10 SDK on ubuntu-24.04
* [x] Docker-outside-of-Docker
* [x] `dotnet-ef` in `.config/dotnet-tools.json`
* [x] Devcontainer networking — postStartCommand joins Docker Compose network

### Tests

* [x] `FeatureEvaluationContextTests` — 8/8 passing
* [x] Build: 0 warnings, 0 errors

---

## 🚀 Phase 1 — MVP Completion 🔄 Current Focus

### Architectural Cleanup ✅ Complete

* [x] Refactor `IFeatureFlagService` — DTOs only, no `Flag` entity in signatures
* [x] Mapping consolidated inside `FeatureFlagService`
* [x] Smoke test verified: POST, GET, PUT, DELETE all return correct responses

### Validation & Sanitization

* [ ] `InputSanitizer` — shared static helper, trims whitespace and strips control
      characters; called by validators and service layer
* [ ] `CreateFlagRequestValidator` — Name allowlist regex, env sentinel guard,
      StrategyConfig cross-field rules, 2000-char limit
* [ ] `UpdateFlagRequestValidator` — StrategyConfig cross-field rules, 2000-char limit
* [ ] `EvaluationRequestValidator` — UserId max 256, UserRoles max 50×100,
      env sentinel guard
* [ ] `FluentValidation.AspNetCore` auto-validation wired in `Program.cs`
* [ ] `InputSanitizer` called in `FeatureFlagService.IsEnabledAsync` and
      `CreateFlagAsync` — closes KI-003
* [ ] Name uniqueness check at the service layer before hitting the DB

### Error Handling

* [ ] Global exception middleware — replace per-controller try/catch blocks
* [ ] Standardized `ValidationProblemDetails` error response shape
* [ ] Route parameter guard for `{name}` on GET and PUT — closes KI-008

### Testing

* [ ] Unit tests for `PercentageStrategy`, `RoleStrategy`, `NoneStrategy`
* [ ] Unit tests for `FeatureEvaluator` — dispatch, missing strategy fallback
* [ ] Unit tests for all three validators — every acceptance criterion covered
* [ ] Integration tests for all API endpoints (including `/api/evaluate`)

### Developer Experience

* [ ] Commit `.http` request file for smoke testing (`requests/smoke-test.http`)
* [ ] Seed data for local development (dev/staging/prod flags)
* [ ] Evaluation decision logging
* [ ] Fix OpenAPI enum schema — enums currently render as `integer` in spec (cosmetic)

---

## ☁️ Phase 1.5 — Azure Foundation + AI Integration 🆕

> **This phase is the product's first competitive differentiator.**
> Azure-native and AI-assisted are not future features — they start here.

### Azure Key Vault

* [ ] Add `Azure.Extensions.AspNetCore.Configuration.Secrets` package
* [ ] Wire Key Vault as a configuration provider in `Program.cs`
* [ ] Move connection string and any future secrets out of `appsettings.json`
* [ ] Document Key Vault setup in `docs/decisions/azure-key-vault.md`

### Azure Application Insights

* [ ] Add `Microsoft.ApplicationInsights.AspNetCore` package
* [ ] Wire App Insights telemetry in `Program.cs`
* [ ] Add structured custom events for flag evaluation decisions
  * Event name: `FlagEvaluated`
  * Properties: `FlagName`, `Environment`, `StrategyType`, `Result`, `UserId` (hashed)
* [ ] Verify request telemetry, dependency tracking, and live metrics are active
* [ ] Document telemetry schema in `docs/decisions/observability-telemetry-schema.md`

### AI Analysis Endpoint

* [ ] Introduce `IAiFlagAnalyzer` interface in `FeatureFlag.Application/Interfaces/`
* [ ] Implement `AzureOpenAiFlagAnalyzer` in `FeatureFlag.Infrastructure/AI/`
* [ ] Add `AIController` — `POST /api/flags/analyze`
* [ ] Add `FlagAnalysisResponse` DTO:
  * `StaleFlags` — flags enabled for > 90 days with no recent evaluation activity
  * `RiskyConfigurations` — flags at 100% rollout with no role restriction, etc.
  * `Recommendations` — plain-language cleanup and improvement suggestions
* [ ] Semantic Kernel SDK wired in Infrastructure layer
* [ ] System prompt engineered and documented in `docs/decisions/ai-analysis-prompt.md`
* [ ] `IPromptSanitizer` interface — sanitizes string values before embedding in prompts
  * Targets: newline injection, instruction override patterns, role confusion attacks
  * Registered in DI, called by `AzureOpenAiFlagAnalyzer` before prompt construction
* [ ] `IAiFlagAnalyzer` mock implementation for local dev (no Azure OpenAI key required)
* [ ] Register `IAiFlagAnalyzer` in `DependencyInjection.cs` — swappable via config

### Azure Container Apps — Initial Deployment

* [ ] Add `Dockerfile` for the API project
* [ ] Add `Dockerfile` for local dev parity
* [ ] Deploy to Azure Container Apps (dev environment)
* [ ] Wire App Insights connection string from Key Vault in deployed environment
* [ ] Document deployment steps in `docs/decisions/azure-container-apps-deployment.md`
* [ ] Live URL committed to README

---

## 🧪 Phase 2 — Testing & Reliability

* [ ] Full unit test coverage for all strategies, evaluator, and validators
* [ ] Integration tests with real Postgres (test containers or in-memory)
* [ ] Contract tests for all API response shapes
* [ ] Test coverage for `IAiFlagAnalyzer` — mock + integration
* [ ] Handle invalid strategy configurations gracefully at evaluation time
* [ ] Structured error responses for all failure modes

---

## 🔐 Phase 3 — Authentication, Authorization & Rate Limiting

> **Phase 3 is a prerequisite for Phase 4 audit logging.**
> Identity must exist before it can be recorded.

### Auth Integration

* [ ] JWT bearer token authentication — Azure AD / Entra ID as identity provider
* [ ] Secure all flag management endpoints (`/api/flags`)
* [ ] Evaluation endpoint (`/api/evaluate`) — configurable: authenticated or anonymous
      depending on SDK integration requirements

### Authorization

* [ ] Role-based access:
  * `FlagAdmin` — create, update, archive flags across all environments
  * `FlagEditor` — create and update flags in non-Production environments
  * `FlagReader` — read-only access
* [ ] Environment-level access control — Production changes require elevated role

### Rate Limiting

* [ ] ASP.NET Core `AddRateLimiter` on `/api/evaluate` (hot path)
* [ ] Rate limit keyed on authenticated caller identity (not IP)
* [ ] Rate limit configuration in Key Vault

---

## 📊 Phase 4 — Observability & Debugging

> **Phase 1.5 lays the App Insights foundation. Phase 4 builds the product layer on top.**

### Evaluation Telemetry

* [ ] Evaluation count by flag, environment, strategy
* [ ] True/false distribution per flag
* [ ] p99 evaluation latency tracking
* [ ] Anomaly detection — flag suddenly flipping for large user cohort

### Audit Trail

* [ ] Log all flag mutations with caller identity (requires Phase 3)
* [ ] Store: who, what, when, previous value, new value
* [ ] Expose via `GET /api/flags/{name}/history`

### Debugging Endpoint

* [ ] `POST /api/flags/{name}/explain` — "Why was this flag ON/OFF for this user?"
* [ ] Returns evaluation trace: strategy used, config applied, decision rationale
* [ ] AI-enhanced explanation mode — natural language evaluation trace (stretch goal)

---

## ⚙️ Phase 5 — Advanced Rollout Strategies

* [ ] User targeting strategy — evaluate by specific user ID list
* [ ] Time-based activation — enable flag on a schedule
* [ ] Gradual rollout — increment percentage over time automatically
* [ ] Each new strategy requires a corresponding FluentValidation rule before
      the API accepts its configuration — mandatory by architectural convention

---

## ⚡ Phase 6 — Performance & Scaling

* [ ] In-memory caching of `Flag` entities in `FeatureFlagService`
* [ ] Redis distributed cache — for multi-instance deployments on AKS
* [ ] Cache invalidation on flag update and archive
* [ ] Optimize evaluation path — target sub-5ms p99 for cached evaluations
* [ ] Horizontal scaling validation — stateless API confirmed

---

## 🛠️ Phase 7 — .NET SDK 🚨 Key Product Milestone

> **This is what makes FeatureFlagService a product, not just a service.**
> No enterprise team will adopt a flag service without a first-class SDK.

* [ ] `FeatureFlag.Client` NuGet package
* [ ] `IFeatureFlagClient` interface — `IsEnabledAsync(flagName, context)`
* [ ] HTTP client backed by `/api/evaluate`
* [ ] Local evaluation mode — SDK holds a cached copy of flag config, evaluates
      without an HTTP call on the hot path
* [ ] Fail-closed by default — SDK returns `false` if service is unreachable
* [ ] `AddFeatureFlagClient()` extension method for ASP.NET Core DI
* [ ] Full SDK documentation with quickstart guide
* [ ] NuGet package published
* [ ] README demo: flag service running + SDK integrated in a sample app,
      AI analysis endpoint returning recommendations — under 15 minutes

---

## 🏭 Phase 8 — Production Readiness

* [ ] Full CI/CD pipeline — GitHub Actions
  * Build, test, lint on every PR
  * Deploy to Azure Container Apps on merge to main
* [ ] AKS deployment manifests — for teams self-hosting on Kubernetes
* [ ] Environment configuration management — all secrets in Key Vault
* [ ] EF Core migration strategy for production upgrades
* [ ] Backup and restore documentation for Postgres
* [ ] Migrate devcontainer to full docker-compose devcontainer setup (resolves KI-007)
* [ ] SLA baseline documented — target 99.9% uptime for hosted offering

---

## 🌐 Phase 9 — Open Core Launch

* [ ] Public GitHub repository — MIT license
* [ ] Self-hosted Docker image published to Docker Hub and GitHub Container Registry
* [ ] One-command local setup: `docker compose up` → working service in < 2 minutes
* [ ] Managed hosting offering (Azure Container Apps) — paid tier
* [ ] Landing page with live demo
* [ ] Technical blog post: architecture, AI analysis design, Azure-native decisions
* [ ] LinkedIn launch posts and community outreach (.NET, Azure developer communities)

---

## 📌 Current Focus

👉 **Phase 1 — MVP Completion**

Immediate next tasks:

1. Implement `InputSanitizer` and all three FluentValidation validators — closes KI-003
2. Add global exception middleware
3. Add route parameter guard for `{name}` on GET/PUT — closes KI-008
4. Unit tests for strategies, evaluator, and validators
5. Integration tests for all endpoints
6. Commit `.http` smoke test file

---

## 🧩 Notes for AI Assistants (Claude Context)

### Product Direction
* This is an Azure-native, .NET-first, AI-assisted feature flag platform
* Every architectural decision should be evaluated against: does this serve Azure
  deployment, .NET SDK ergonomics, or AI integration?
* Phase 1.5 is the first major competitive differentiator — do not skip or defer it

### Architecture Conventions
* Architecture follows Clean Architecture: Controller → Service → Evaluator → Strategy → Repository
* `IFeatureFlagService` speaks entirely in DTOs — no `Flag` entity crosses the service boundary
* Domain logic is intentionally strict (no public setters)
* Strategy pattern is central to extensibility
* Evaluation must remain deterministic and testable
* Connection string uses `Host=postgres` — do not change to `localhost`
* Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design`
  with `PrivateAssets=all`

### Validation & Sanitization Conventions
* FluentValidation auto-validation runs at the HTTP boundary — controllers never see
  invalid requests
* `InputSanitizer` is the single source of truth for string sanitization — never inline
  equivalent logic
* Any non-HTTP input surface (CLI, seed data, test helpers) must call `InputSanitizer`
  independently
* Any new `IRolloutStrategy` implementation requires a corresponding validator rule in
  `CreateFlagRequestValidator` and `UpdateFlagRequestValidator`
* Do not sanitize `StrategyConfig` content — JSON stored verbatim; only length and
  structure are validated

### Security Conventions
* See `docs/decisions/adr-input-security-model.md` before modifying the security boundary
* `IPromptSanitizer` (Phase 1.5) is a separate concern from `InputSanitizer` — HTTP
  boundary sanitization does not substitute for prompt injection defense
* Raw SQL via `FromSqlRaw()` with string concatenation is prohibited — use
  `FromSqlInterpolated()` if raw SQL becomes necessary

### Known Issues
* KI-002 — `FeatureEvaluator.Evaluate` has an implicit precondition (documented,
  not enforced; see architecture.md)
* KI-007 — Devcontainer networking requires Postgres to start first (mitigated,
  deferred to Phase 8)
* KI-008 — Route parameters on GET/PUT lack allowlist validation (Phase 1 fix)

---

## 🗺️ Long-Term Vision

Turn FeatureFlagService into a **full Observability + Experimentation Platform**
for .NET teams on Azure:

* A/B testing and experiment analysis
* Real-time flag evaluation dashboards in App Insights
* Natural language querying of flag history and evaluation patterns
* SDK ecosystem: .NET, Python, JavaScript — in that order
* Salesforce integration — feature flags for Apex and Flow (future workstream)