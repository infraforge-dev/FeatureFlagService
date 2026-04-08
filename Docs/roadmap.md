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

## 🔄 Phase 1 — MVP Completion (Current Focus)

### Validation & Sanitization ✅ Complete (PR #30)

* [x] `InputSanitizer` — shared `internal static` helper; trims whitespace, strips
      ASCII control characters
* [x] `CreateFlagRequestValidator` — name allowlist regex, env sentinel guard,
      StrategyConfig cross-field rules (via `.Must()` — FluentValidation v12)
* [x] `UpdateFlagRequestValidator` — StrategyConfig cross-field rules
* [x] `EvaluationRequestValidator` — FlagName, UserId, Environment validation
* [x] Manual `ValidateAsync` in controllers — no FluentValidation.AspNetCore

### Code Style Foundation ✅ Complete (PR #33)

* [x] `.editorconfig`, `.gitattributes`, `.csharpierrc.json`, `.csharpierignore`
* [x] `.config/dotnet-tools.json` — CSharpier pinned
* [x] All existing test classes decorated with `[Trait("Category", "Unit")]`

### CI Pipeline — Core Jobs ✅ Complete (PR #34)

> **Note:** CI/CD was originally scoped to Phase 8. The foundation pipeline
> (lint, format, build, test) was pulled forward to Phase 1 because enforcing
> code quality during active development has higher value than waiting until
> production readiness. CD (deployment to Azure) remains in Phase 8.

* [x] `.github/workflows/ci.yml` — `lint-format` and `build-test` parallel jobs
* [x] `dotnet csharpier check .` — format gate in `lint-format` job
* [x] `dotnet build -p:TreatWarningsAsErrors=true` — zero-warnings policy enforced
* [x] `dotnet test --filter "Category!=Integration"` — unit tests gate

### CI Pipeline — AI Reviewer ✅ Complete (PR #35)

* [x] `.github/workflows/ci.yml` — `ai-review` job fully implemented
* [x] `.github/prompts/ai-review-system.md` — system prompt in repo, read at runtime
* [x] `ANTHROPIC_API_KEY` secret added to GitHub repo
* [x] `ai-review` label created in GitHub repo
* [x] Fail-open behavior verified — transient API failures do not block merge

### Error Handling ✅ Complete (PR #36)

* [x] `FeatureFlagException` abstract base class in `Domain/Exceptions/`
* [x] `FlagNotFoundException` — 404
* [x] `DuplicateFlagNameException` — 409, constructor accepts `(string, EnvironmentType)`
* [x] `GlobalExceptionMiddleware` — single catch-all; domain exceptions → named 4xx;
      unexpected → `LogError` + safe 500
* [x] Middleware registered first in `Program.cs`
* [x] All controllers cleaned — zero `try/catch` blocks
* [x] All error responses return `ProblemDetails` with `Content-Type: application/problem+json`

### Input Validation Hardening ✅ Complete (PR #37)

* [x] `StrategyConfigRules` extracted — shared `internal static` class; closes KI-NEW-001
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

### Testing ✅ Complete (PRs #38, #39)

* [x] Unit tests for `PercentageStrategy`, `RoleStrategy`, `NoneStrategy`
* [x] Unit tests for `FeatureEvaluator` — dispatch, missing strategy fallback
* [x] Unit tests for all three validators — every acceptance criterion covered
* [x] 75/75 unit tests passing; 2 production bugs caught and fixed (PR #38)
* [x] Integration tests for all 6 endpoints via Testcontainers Postgres (PR #39)
* [x] 31/31 integration tests passing; 2 additional production bugs caught and fixed
* [x] `integration-test` CI job added; `ai-review` now depends on all three jobs
* [x] `EnvironmentRules.cs` introduced in Application layer — single source of truth
      for environment validation across validators and service boundary
* [x] `CreateFlagRequest.StrategyConfig` and `UpdateFlagRequest.StrategyConfig`
      changed to `string?` — matches actual wire contract for `RolloutStrategy.None`

### Developer Experience

* [ ] `.http` smoke test request file committed to repo (`requests/smoke-test.http`)
* [ ] Seed data for local development
* [ ] Evaluation decision logging

---

## 🌩️ Phase 1.5 — Azure Foundation + AI Integration ⭐

> Begins immediately after Phase 1 DoD is met.

* [ ] Azure Key Vault integration — secrets management for connection strings and API keys
* [ ] Application Insights — structured logging, request tracing, evaluation metrics
* [ ] `IAiFlagAnalyzer` interface + `AzureOpenAiFlagAnalyzer` implementation
* [ ] `POST /api/flags/analyze` endpoint — natural language flag health analysis
* [ ] `IPromptSanitizer` — prompt injection defense for flag data embedded in AI prompts
* [ ] Azure Container Apps deployment target documented

---

## 🧪 Phase 2 — Testing & Reliability

* [ ] Contract tests for API responses
* [ ] Test environment-specific flag behavior
* [ ] Code coverage gate in CI

---

## 🔐 Phase 3 — Authentication, Authorization & Rate Limiting

* [ ] JWT or OAuth authentication
* [ ] Role-based access to flag management endpoints
* [ ] Rate limiting (requires caller identity from Phase 3)
* [ ] Audit logging — who changed what (requires identity)

---

## 📊 Phase 4 — Observability & Debugging

* [ ] Log all feature evaluations
* [ ] Track evaluation counts, strategy usage, success/failure rates
* [ ] `GET /api/flags/{name}/trace` — "why was this flag ON/OFF?"
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

**Phase 1 — Developer Experience (final three tasks)**

1. `.http` smoke test file (`requests/smoke-test.http`)
2. Seed data for local development
3. Evaluation decision logging

Phase 1 DoD is met when all three are complete. Phase 1.5 begins immediately after.

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
- `EnvironmentRules` is the single source of truth for environment validation —
  `IsValid(...)` in validators, `RequireValid(...)` at the service boundary
- `SaveChangesAsync` in `FeatureFlagRepository` catches Postgres `23505` and rethrows as
  `DuplicateFlagNameException` — intentional TOCTOU handling, do not remove
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
- Any spec providing `JsonDocument` code must use `using JsonDocument doc = ...`
- Any spec with optional DTO fields must explicitly state nullability and wire contract

---

## 🔭 Long-Term Vision

- Full **Observability + Experimentation Platform**
- A/B testing with statistical significance tracking
- Natural language flag creation — *"Roll out the payment flow to 10% of production users"*
- Smart rollout recommendations based on evaluation patterns
- Integration with analytics pipelines and real-time dashboards
- Anomaly detection — automatic stale flag and unusual distribution alerts
