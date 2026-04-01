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
Phase 1.5 🆕  Azure Foundation + AI — Key Vault, App Insights, AI analysis endpoint
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
* [x] DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`, `FlagMappings`
* [x] `DependencyInjection.cs` — `AddApplication()` extension method

### API Layer

* [x] `FeatureFlagsController` — full CRUD: GET all, GET by name, POST, PUT, DELETE (soft archive)
* [x] `EvaluationController` — POST `/api/evaluate`
* [x] OpenAPI enrichment — Scalar UI, XML docs, `EnumSchemaTransformer`, `ApiInfoTransformer`

### Infrastructure & Persistence

* [x] EF Core + Npgsql setup — `FeatureFlagDbContext`
* [x] `FeatureFlagRepository` — async, full CRUD with soft-delete
* [x] Postgres `jsonb` for `StrategyConfig`
* [x] Partial unique index on `Name` for soft-delete support
* [x] EF Core migrations — initial schema
* [x] `docker-compose.yml` — one-command local Postgres setup

### Service Interface Boundary

* [x] `IFeatureFlagService` — no `Flag` entity in any method signature
* [x] All signatures use DTOs only
* [x] `Flag` construction moved from controller into `FeatureFlagService.CreateFlagAsync`

### Dev Environment

* [x] DevContainer: `devcontainers/base:ubuntu-24.04` + .NET 10 SDK feature
* [x] Docker-outside-of-Docker configured with `postStartCommand` network join
* [x] `.config/dotnet-tools.json` — `dotnet-ef` tool manifest

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

* [x] `.editorconfig` — LF line endings, naming conventions, Roslyn diagnostic
      severities, `generated_code = true` on Migrations
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
* [x] `ai-review` job present as commented stub — activated in PR #35

### CI Pipeline — AI Reviewer 🔄 In Progress (PR #35)

* [x] `spec-ai-reviewer.md` — spec document (forthcoming this session)
* [x] `.github/workflows/ci.yml` — uncomment and implement `ai-review` job
* [x] `.github/prompts/ai-review-system.md` — system prompt for Claude reviewer
* [x] `ANTHROPIC_API_KEY` secret added to GitHub repo

### Validation (remaining)

* [ ] Name uniqueness check at the service layer before hitting the DB

### Error Handling

* [x] Global exception middleware — replace per-controller try/catch
* [x] Standardized `ProblemDetails` error response shape
* [ ] Route parameter guard for `{name}` on GET/PUT — closes KI-008

### Testing

* [ ] Unit tests for `PercentageStrategy`, `RoleStrategy`, `NoneStrategy`
* [ ] Unit tests for `FeatureEvaluator` — dispatch, missing strategy fallback
* [ ] Unit tests for all three validators
* [ ] Integration tests for all 6 endpoints (Phase 2 gate — Postgres service container)

### Developer Experience

* [ ] `.http` smoke test request file committed to repo
* [ ] Seed data for local development
* [ ] Evaluation decision logging

---

## 🌩️ Phase 1.5 — Azure Foundation + AI Integration 🆕

> Begins immediately after Phase 1 DoD is met.

* [ ] Azure Key Vault integration — all secrets sourced from Key Vault at startup
* [ ] Azure Application Insights — structured telemetry for API requests and evaluations
* [ ] `IAiFlagAnalyzer` interface + `AzureOpenAiFlagAnalyzer` implementation
      (Semantic Kernel, Azure OpenAI backend)
* [ ] `IPromptSanitizer` — newline injection and instruction override defense
      (complements `InputSanitizer` at the HTTP boundary)
* [ ] `GET /api/flags/analysis` endpoint — natural language flag health queries
* [ ] Azure OpenAI deployment provisioned (gpt-4o or equivalent)

---

## 🧪 Phase 2 — Testing & Reliability

* [ ] Integration tests for all API endpoints with Postgres service container in CI
* [ ] NuGet caching in CI — `RestorePackagesWithLockFile=true` in `Directory.Build.props`,
      `packages.lock.json` committed, `cache: true` on `setup-dotnet@v4`
* [ ] Code coverage gate — Coverlet + ReportGenerator, 80% line coverage minimum
* [ ] Contract tests for `IFeatureFlagService`
* [ ] `StrategyConfigRules` internal static class — resolves KI-NEW-001 duplication

---

## 🔐 Phase 3 — Authentication, Authorization & Rate Limiting

* [ ] JWT bearer authentication on all management endpoints
* [ ] Role-based access control — admin vs read-only roles
* [ ] Rate limiting on `/api/evaluate` — `AddRateLimiter`, keyed on authenticated identity
* [ ] Audit trail — structured log events on all flag mutations
* [ ] Dependabot for NuGet packages
* [ ] CodeQL security scanning (free for public repos)

---

## 📡 Phase 4 — Observability & Debugging

* [ ] Evaluation telemetry pipeline — log every evaluation decision
* [ ] Debugging endpoint — `GET /api/flags/{name}/trace` with evaluation reasoning
* [ ] Anomaly detection — unusual evaluation patterns surfaced via AI analysis
* [ ] Application Insights dashboard — evaluation rates, strategy distribution, stale flags
* [ ] Return evaluation trace in evaluation response

---

## ⚙️ Phase 5 — Advanced Rollout Strategies

* [ ] User targeting (by ID)
* [ ] Time-based activation
* [ ] Gradual rollout (time + percentage combined)
* [ ] Dynamic strategy registration (DI-driven — Open/Closed compliant)
* [ ] Smart rollout recommendations via AI analysis

---

## 🌐 Phase 6 — Performance & Scaling

* [ ] Environment-specific flag overrides
* [ ] Promotion workflow (dev → staging → prod)
* [ ] In-memory caching layer with cache invalidation strategy
* [ ] Redis for distributed caching (horizontal scaling readiness)
* [ ] Optimize evaluation path for sub-millisecond hot path

---

## 📦 Phase 7 — .NET SDK ← Key Product Milestone

> No enterprise team will adopt a flag service without a first-class SDK.

* [ ] `FeatureFlag.Client` NuGet package
* [ ] `IFeatureFlagClient` interface — `IsEnabledAsync(flagName, context)`
* [ ] HTTP client backed by `/api/evaluate`
* [ ] Local evaluation mode — cached flag config, evaluates without HTTP on hot path
* [ ] Fail-closed by default — returns `false` if service is unreachable
* [ ] `AddFeatureFlagClient()` extension for ASP.NET Core DI
* [ ] Middleware extensions and action filter attributes
* [ ] Full SDK documentation with quickstart guide
* [ ] NuGet package published
* [ ] Natural language flag creation — describe a flag in plain English, system creates it

---

## 🏭 Phase 8 — Production Readiness

> CI (lint, format, build, test) was pulled forward to Phase 1.
> Phase 8 covers CD — automated deployment — and hardening.

* [ ] CD pipeline — GitHub Actions deploy to Azure Container Apps on merge to `main`
* [ ] AKS deployment manifests — for teams self-hosting on Kubernetes
* [ ] All secrets sourced from Key Vault (validates Phase 1.5 work at scale)
* [ ] EF Core migration strategy for zero-downtime production upgrades
* [ ] Backup and restore documentation for Postgres
* [ ] Migrate devcontainer to full docker-compose devcontainer setup — resolves KI-007
* [ ] SLA baseline documented — target 99.9% uptime for hosted offering
* [ ] AI reviewer fail-open → fail-closed hardening for PRs targeting `main`
* [ ] Switch AI reviewer fail behavior: retry with backoff, fail-closed on `main` only

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

Code style foundation and core CI pipeline are complete (PRs #33, #34).
Immediate next tasks:

1. Write `spec-ai-reviewer.md` — AI reviewer job for PR #35
2. Global exception middleware — replace per-controller try/catch
3. Route parameter guard for `{name}` on GET/PUT — closes KI-008
4. Name uniqueness check at the service layer
5. Unit tests for strategies, evaluator, and all three validators
6. Integration tests for all endpoints
7. Commit `.http` smoke test file
8. Seed data for local development
9. Evaluation decision logging

---

## 🧩 Notes for AI Assistants (Claude Context)

### Product Direction
* This is an Azure-native, .NET-first, AI-assisted feature flag platform
* Every architectural decision should be evaluated against: does this serve Azure
  deployment, .NET SDK ergonomics, or AI integration?

### Architecture
* Controller → Service → Evaluator → Strategy → Repository
* `IFeatureFlagService` speaks entirely in DTOs — no `Flag` entity crosses the service boundary
* Domain logic is intentionally strict (no public setters)
* Strategy pattern is central to extensibility — use registry dispatch, not switch statements
* Evaluation must remain deterministic and testable

### FluentValidation v12
* No `.Transform()` — use `.Must()` lambda instead
* No `AddValidatorsFromAssemblyContaining` — register validators explicitly with `AddScoped`
* No `FluentValidation.AspNetCore` — use manual `ValidateAsync()` in controllers

### Formatter conventions
* CSharpier is the final formatting authority — always run last
* `dotnet format` may precede CSharpier for Roslyn diagnostic fixes
* CSharpier 1.x syntax: `dotnet csharpier check .` and `dotnet csharpier format .`
* `**/Migrations/**` has `generated_code = true` — do not format or analyze

### Infrastructure
* Connection string uses `Host=postgres` — do not change to `localhost`
* Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design`
  with `PrivateAssets=all`
* `appsettings.Development.json` is intentionally committed — local Docker defaults only
* NuGet caching in CI requires `packages.lock.json` — deferred to Phase 2

### What not to do
* No auth yet (Phase 3)
* No caching layer yet (Phase 6)
* No advanced strategies yet (Phase 5)
* No App Insights yet (Phase 1.5)
* No CD to Azure yet (Phase 8)
* No UI work

---

## 🗺️ Long-Term Vision

* Full **Observability + Experimentation Platform** for .NET teams on Azure
* A/B testing capabilities with statistical significance reporting
* AI-powered flag lifecycle management — creation, health analysis, rollout recommendations
* Real-time evaluation dashboards
* Open core + managed hosting business model
