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
| 1.5 | Azure Foundation + AI Integration | ✅ Complete — Gate: GO WITH CONDITIONS |
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
* [x] Controllers wired, Scalar UI configured
* [x] Service interface boundary — `Flag` entity stays out of `IBanderasService` signatures

---

## 🎯 Phase 1 — MVP Completion ✅ Complete

* [x] `FluentValidation` v12 on all request DTOs
* [x] `InputSanitizer` — shared sanitization at HTTP boundary
* [x] Global exception middleware — RFC 9457 ProblemDetails
* [x] `RouteParameterGuard` — route parameter hardening
* [x] `DuplicateFlagNameException` with 409 Conflict handling
* [x] CI/CD pipeline — `lint-format`, `build-test`, `integration-test` parallel jobs
* [x] AI PR reviewer in CI (`ai-review` job, Claude API)
* [x] Unit tests — strategies, evaluator, validators
* [x] Integration tests — all endpoints via Testcontainers Postgres
* [x] Evaluation decision logging
* [x] NuGet locked restore
* [x] `DatabaseSeeder` — six seed flags, all three strategies
* [x] `Requests/smoke-test.http` — all endpoints covered

**Phase 1 DoD: ✅ COMPLETE**

---

## 🌩️ Phase 1.5 — Azure Foundation + AI Integration ✅ Complete

### Azure Infrastructure ✅ Provisioned

* [x] `rg-banderas-dev` — Azure Resource Group
* [x] `kv-banderas-dev` — Azure Key Vault
* [x] `appi-banderas-dev` — Azure Application Insights, West US
* [x] `aoai-banderas-dev` — Azure OpenAI resource, East US, Standard S0
* [x] `gpt-5-mini` model deployment — Standard tier

### Azure Key Vault Integration ✅ Complete (PR #50)

* [x] `AddAzureKeyVault()` wired in `Program.cs` before all service registrations
* [x] `DefaultAzureCredential` — `az login` locally, Managed Identity in production
* [x] Graceful fallback when `Azure:KeyVaultUri` is absent — local dev unaffected
* [x] Integration test factory uses `UseEnvironment("Testing")` — isolates from
      Azure credential chain

### Application Insights Integration ✅ Complete (PR #51)

* [x] `Microsoft.ApplicationInsights.AspNetCore` wired as telemetry sink
* [x] `flag.evaluated` custom event per evaluation
* [x] Connection string sourced from Key Vault (`ApplicationInsights--ConnectionString`)

### AI Flag Health Analysis Endpoint ✅ Complete (PR #52)

* [x] `POST /api/flags/health` — structured AI health analysis across all environments
* [x] `IPromptSanitizer` / `PromptSanitizer` — newline normalization, phrase redaction,
      role confusion defense, 500-char length cap; `GeneratedRegex` for compile-time regex
* [x] `IAiFlagAnalyzer` — Application interface; decoupled from Semantic Kernel
* [x] `AiFlagAnalyzer` — Infrastructure implementation via Semantic Kernel + Azure OpenAI
* [x] `FlagHealthConstants` — named constants, no magic numbers
* [x] `AiAnalysisUnavailableException` — graceful degradation to 503
* [x] `IBanderasRepository.GetAllAsync` — nullable `EnvironmentType?` param;
      null = cross-environment query (Option C `FlagQuery` deferred to Phase 4)
* [x] `FlagResponse.StrategyConfig` — corrected to `string?`
* [x] `GlobalExceptionMiddleware` — dedicated 503 catch + RFC URI type param
* [x] Semantic Kernel excluded from `Testing` environment; `StubAiFlagAnalyzer` in CI
* [x] `UnavailableAiFlagAnalyzer` — missing `AzureOpenAI:Endpoint` no longer blocks
      application startup; non-AI endpoints remain available
* [x] DEFERRED-004 closed (`IPromptSanitizer`)
* [x] 146/146 tests passing (107 unit + 39 integration)

### Architecture Review ✅ Complete

* [x] Technical health audit report — `Docs/architecture-review-phase1-report.md`
* [x] Strong seams, accumulating complexity, and debt inventory recorded
* [x] Gate decision recorded: GO WITH CONDITIONS

**Phase 1.5 DoD: ✅ COMPLETE**

**Carry-forward conditions before broad Phase 2 work:**

* [x] Remove Azure OpenAI as a hard startup dependency for non-AI endpoints
* [x] Explicitly document the `FeatureEvaluationContext` service-boundary exception
      as an intentional value-object boundary input
* [x] Add AI-unavailable 503 integration coverage
* [x] Add AI output-contract verification

---

## 🧪 Phase 2 — Testing & Reliability

* [x] Enforce AI response semantics after deserialization
* [x] Formally keep `FeatureEvaluationContext` as the evaluation value-object
      exception to the DTO-only service-boundary convention
* [ ] Strengthen `Flag` invariants and direct domain tests before adding new input surfaces
* [ ] Contract tests for API responses
* [ ] Handle invalid strategy configurations gracefully
* [ ] Test environment-specific behavior edge cases
* [ ] Mutation testing baseline

---

## 🔐 Phase 3 — Authentication, Authorization & Rate Limiting

* [ ] JWT bearer token authentication on all flag management endpoints
* [ ] Role-based access to create/update flags and environment-specific actions
* [ ] Rate limiting on `/api/evaluate` keyed on authenticated caller identity
* [ ] Audit trail — who changed what

---

## 📊 Phase 4 — Observability & Debugging

* [ ] Evaluation trace endpoint — "Why was this flag ON/OFF for user X?"
* [ ] `FlagQuery` record — extensible query object for repository (upgrade from
      nullable `EnvironmentType?` param; covers archived, strategy type, date range)
* [ ] Agentic AI capabilities — AI-initiated flag disable/archive with guardrails
      (requires Phase 3 auth + audit logging from Phase 4)
* [ ] Track evaluation counts, strategy usage, success/failure rates
* [ ] Anomaly detection — unusual evaluation pattern alerts

---

## 🎯 Phase 5 — Advanced Rollout Strategies

* [ ] User targeting by ID
* [ ] Time-based activation (scheduled flags)
* [ ] Gradual rollout (time + percentage combined)
* [ ] Dynamic strategy registration (DI-driven)

---

## ⚡ Phase 6 — Performance & Scaling

* [ ] In-memory caching layer between service and repository
* [ ] Redis cache option for distributed deployments
* [ ] Horizontal scaling validation — stateless API design confirmed
* [ ] Cache analysis results for `POST /api/flags/health`

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
* [ ] Managed Identity assigned to Container App
* [ ] SLA baseline (p99 evaluation latency)
* [ ] Full docker-compose devcontainer setup (resolves KI-007)
* [ ] Network hardening — Key Vault and App Insights on Selected Networks

---

## 🌐 Phase 9 — Open Core Launch

* [ ] Public repo prepared — `CONTRIBUTING.md`, issue templates
* [ ] Self-hosted Docker image published to GitHub Container Registry
* [ ] Managed hosting offering documented
* [ ] Launch post and demo video

---

## 🎯 Current Focus

**Phase 2 Prep — Gate: GO WITH CONDITIONS**

1. Strengthen direct domain invariants and tests
2. Decide whether GET query environment validation should move earlier or remain
   explicitly documented as service-level validation

**Phase 1.5 closed with GO WITH CONDITIONS; AI output-contract condition is closed.**

---

## 🧩 Notes for AI Assistants (Claude Context)

* Architecture follows Clean Architecture: Controller → Service → Evaluator → Strategy → Repository
* `Flag` does not cross the service boundary; evaluation intentionally passes
  immutable `FeatureEvaluationContext` into `IBanderasService.IsEnabledAsync`
* `IBanderasRepository.GetAllAsync` accepts `EnvironmentType? environment = null`;
  null means no environment filter (cross-environment health analysis)
* `FlagResponse.StrategyConfig` is `string?` — null guard required before sanitizing
* `AiAnalysisUnavailableException` extends `Exception` (not `BanderasException`) —
  middleware catches it explicitly before the generic handler
* `AiFlagAnalyzer` validates deserialized model output before returning: non-empty
  summary, non-empty assessments, full input-flag coverage, and documented status values
* Semantic Kernel and `DefaultAzureCredential` excluded from `Testing` environment
* Missing Azure OpenAI endpoint registers `UnavailableAiFlagAnalyzer`; app startup
  stays healthy and AI health analysis returns 503
* Integration test factory registers `StubAiFlagAnalyzer` — no live Azure calls in CI
* Connection string uses `Host=postgres` — do not change to `localhost`
* Azure resources provisioned in `rg-banderas-dev`; OpenAI in East US, App Insights in West US
* GPT model deployment name: `gpt-5-mini` inside `aoai-banderas-dev`
* `FlagQuery` record (extensible repository query object) — tracked as Phase 4 upgrade
  from current nullable `EnvironmentType?` approach

---

## 🔭 Long-Term Vision

* Turn Banderas into a full **Observability + Experimentation Platform**
* A/B testing capabilities
* Real-time dashboards
* Managed hosting offering — open core business model
