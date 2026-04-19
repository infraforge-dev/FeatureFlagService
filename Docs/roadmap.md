# Roadmap — Banderas

---

## Table of Contents

- [Product Vision](#-product-vision)
- [Phase Map](#-phase-map)
- [Phase 0 — Foundation](#-phase-0--foundation--complete)
- [Phase 1 — MVP Completion](#-phase-1--mvp-completion--complete)
- [Phase 1.5 — Azure Foundation + AI Integration](#-phase-15--azure-foundation--ai-integration--in-progress)
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

Banderas is being built to compete in the developer tooling market as the
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

| Phase | Name | Status |
|-------|------|--------|
| 0 | Foundation | ✅ Complete |
| 1 | MVP Completion | ✅ Complete |
| 1.5 | Azure Foundation + AI Integration | 🔄 In Progress |
| 2 | Testing & Reliability | Planned |
| 3 | Auth, Authorization & Rate Limiting | Planned |
| 4 | Observability & Debugging | Planned |
| 5 | Advanced Rollout Strategies | Planned |
| 6 | Performance & Scaling | Planned |
| 7 | .NET SDK | ⭐ Key Milestone |
| 8 | Production Readiness | Planned |
| 9 | Open Core Launch | Planned |

---

## 🏗️ Phase 0 — Foundation ✅ Complete

* [x] Domain entities, enums, value objects, interfaces
* [x] `FeatureEvaluator` — registry dispatch pattern
* [x] `PercentageStrategy`, `RoleStrategy`, `NoneStrategy`
* [x] EF Core + Postgres repository
* [x] Controllers wired, Swagger configured
* [x] Service interface boundary — `IBanderasService` speaks entirely in DTOs

---

## 🎯 Phase 1 — MVP Completion ✅ Complete

### Validation & Sanitization ✅ Complete (PR #30)

* [x] `FluentValidation` v12 on `CreateFlagRequest`, `UpdateFlagRequest`,
      `EvaluationRequest` — closes KI-003
* [x] `InputSanitizer` — shared sanitization at HTTP boundary
* [x] Manual `ValidateAsync` in controllers

### Code Style Foundation ✅ Complete (PR #33)

* [x] `.editorconfig`, `.csharpierignore`, CSharpier 1.x configured

### CI/CD Foundation ✅ Complete (PR #34)

* [x] `lint-format` and `build-test` parallel jobs
* [x] `TreatWarningsAsErrors=true`

### CI/CD AI Reviewer ✅ Complete (PR #35)

* [x] `ai-review` job — Claude API reviewer, activated by `ai-review` label,
      fail-open, shell injection fixed via `jq -n --arg`

### Error Handling ✅ Complete (PR #36)

* [x] `GlobalExceptionMiddleware` — RFC 9457 `ProblemDetails`
* [x] `application/problem+json` on all error responses
* [x] Domain exception hierarchy (`FlagNotFoundException`,
      `DuplicateFlagNameException`, `BanderasValidationException`)
* [x] All controllers cleaned — zero try/catch blocks

### Input Validation Hardening ✅ Complete (PR #37)

* [x] `StrategyConfigRules` extracted — shared validation logic
* [x] `RouteParameterGuard` — compiled regex allowlist for `{name}` route params
* [x] `ExistsAsync` + TOCTOU-safe `DbUpdateException` catch for 409 Conflict
* [x] Closes KI-008 and KI-NEW-001

### Unit Tests ✅ Complete (PR #38)

* [x] 81 unit tests — strategies, evaluator, validators
* [x] 2 silent production bugs caught and fixed
* [x] `AssemblyInfo.cs` — `InternalsVisibleTo("Banderas.Tests")`

### Integration Tests ✅ Complete (PR #39)

* [x] 32 integration tests — all 6 endpoints via Testcontainers Postgres
* [x] 2 additional production bugs caught and fixed
* [x] 113/113 tests passing

### Evaluation Decision Logging ✅ Complete (PR #48)

* [x] `EvaluationReason` enum — machine-readable log dimension on every entry
* [x] `HashUserId` — SHA256 surrogate, 8 hex chars; raw `UserId` never logged
* [x] `LogResult` — structured log output, consistent prefix
* [x] `IsEnabled(LogLevel.Information)` guard — CA1873 compliance on hot path
* [x] NuGet locked restore — `RestorePackagesWithLockFile=true`, `--locked-mode` in CI

### Seed Data for Local Development ✅ Complete (PR #49)

* [x] `IsSeeded` bool column — provenance marker, default `false`
* [x] `DatabaseSeeder` — six seed records, all three strategies, Dev and Staging envs
* [x] `MigrateAsync()` extension — schema migration before seeding in Development
* [x] `SEED_RESET` env var controls full reset mode

### Developer Experience ✅ Complete

* [x] `requests/smoke-test.http` — all 6 endpoints covered

**Phase 1 DoD: ✅ COMPLETE — 113/113 tests passing**

---

## 🌩️ Phase 1.5 — Azure Foundation + AI Integration 🔄 In Progress

### Azure Infrastructure ✅ Provisioned

* [x] `rg-banderas-dev` — Azure Resource Group
* [x] `kv-banderas-dev` — Azure Key Vault; `ConnectionStrings--DefaultConnection` secret enabled
* [x] `appi-banderas-dev` — Azure Application Insights, West US
* [x] `aoai-banderas-dev` — Azure OpenAI resource, East US, Standard S0
* [x] `gpt-5-mini` model deployment — Standard tier, inside `aoai-banderas-dev`

### Azure Key Vault Integration ✅ Complete (PR #50)

* [x] `AddAzureKeyVault()` wired in `Program.cs` before all service registrations
* [x] `DefaultAzureCredential` — `az login` locally, Managed Identity in production
* [x] Graceful fallback when `Azure:KeyVaultUri` is absent — local dev unaffected
* [x] `BanderasApiFactory.cs` updated — `UseEnvironment("Testing")` isolates
      integration tests from Azure credential chain
* [x] 113/113 tests passing after factory fix

### Application Insights Integration 🔲 Not Started (PR #51)

* [ ] `Microsoft.ApplicationInsights.AspNetCore` wired as telemetry sink
* [ ] Evaluation custom events — `EvaluationReason` fed into App Insights dimensions
* [ ] Structured logging via Application Insights custom dimensions
* [ ] App Insights connection string sourced from `appi-banderas-dev`

### AI Flag Health Analysis Endpoint 🔲 Not Started (PR #52)

* [ ] `IAiFlagAnalyzer` — natural language summary of flag status
* [ ] `IPromptSanitizer` — newline injection, instruction override, role confusion defense
* [ ] Azure OpenAI + Semantic Kernel integration
* [ ] `/api/flags/health` — AI-generated flag health summary endpoint
* [ ] Closes DEFERRED-004 (`IPromptSanitizer`)

### Architecture Review 🔲 Not Started

* [ ] Technical health audit document — `Docs/architecture-review-phase1.md`
* [ ] Strong seams, accumulating complexity, conscious debt inventory
* [ ] Published as blog post series (planned)

**Phase 1.5 DoD: complete when all three PRs merged + architecture review committed**

---

## 🧪 Phase 2 — Testing & Reliability

* [ ] Unit tests for domain logic edge cases
* [ ] Test environment-specific behavior
* [ ] Contract tests for API responses
* [ ] Handle invalid strategy configurations gracefully
* [ ] Return structured error responses for all failure modes

---

## 🔐 Phase 3 — Authentication, Authorization & Rate Limiting

* [ ] Add authentication (JWT or OAuth)
* [ ] Secure endpoints
* [ ] Role-based access to create/update flags and environment-specific actions
* [ ] Audit who changed what (basic tracking)
* [ ] Rate limiting on evaluation endpoint

---

## 📊 Phase 4 — Observability & Debugging

* [ ] Evaluation trace endpoint — "Why was this flag ON/OFF for user X?"
* [ ] Track evaluation counts, strategy usage, success/failure rates
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

* [ ] `Banderas.Client` NuGet package
* [ ] `IBanderasClient` — `IsEnabledAsync(string flagName, string userId, string[] roles)`
* [ ] ASP.NET Core middleware extensions — `UseBanderas()`
* [ ] Action filter attributes — `[RequireFlag("my-flag")]`
* [ ] Service registration helpers — `services.AddBanderasClient()`
* [ ] SDK documentation and quickstart guide
* [ ] NuGet publish via GitHub Actions on tag

---

## 🚀 Phase 8 — Production Readiness

* [ ] CD pipeline to Azure Container Apps
* [ ] Managed Identity assigned to Container App — grants Key Vault Secrets User role
* [ ] AKS deployment option documented
* [ ] SLA baseline established (p99 evaluation latency)
* [ ] Backup and migration strategy (EF Core)
* [ ] Full docker-compose devcontainer setup (resolves KI-007)
* [ ] Network hardening — Key Vault and App Insights on Selected Networks / private endpoints

---

## 🌐 Phase 9 — Open Core Launch

* [ ] Public repo prepared — `CONTRIBUTING.md`, issue templates, `good first issue` labels
* [ ] Self-hosted Docker image published to GitHub Container Registry
* [ ] Managed hosting offering documented
* [ ] Launch post and demo video
* [ ] Blog post series — architecture journey, AI-assisted dev workflow, lessons learned

---

## 🎯 Current Focus

**Phase 1.5 — Azure Foundation + AI Integration**

1. Application Insights integration (PR #51)
2. AI flag health analysis endpoint (PR #52)
3. Architecture Review Document before Phase 2

---

## 🧩 Notes for AI Assistants (Claude Context)

* Architecture follows Clean Architecture: Controller → Service → Evaluator → Strategy → Repository
* `IBanderasService` speaks entirely in DTOs — no `Flag` entity crosses the service boundary
* Domain logic is intentionally strict (no public setters)
* Strategy pattern is central to extensibility
* Evaluation must remain deterministic and testable
* Connection string uses `Host=postgres` — do not change to `localhost`
* Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design`
  with `PrivateAssets=all`
* Integration test factory uses `UseEnvironment("Testing")` — required to isolate
  tests from Azure Key Vault credential chain; do not remove
* Azure resources provisioned in `rg-banderas-dev`; OpenAI in East US, App Insights in West US
* GPT model deployment name: `gpt-5-mini` inside `aoai-banderas-dev`

When suggesting changes:

* Do not break domain encapsulation
* Prefer composability over conditionals
* Keep evaluation logic isolated from persistence
* Do not return `Flag` entities from `IBanderasService` — map to `FlagResponse` inside the service

---

## 🔭 Long-Term Vision

* Turn Banderas into a full **Observability + Experimentation Platform**
* A/B testing capabilities
* Integrate with analytics pipelines
* Real-time dashboards
* Managed hosting offering — open core business model
