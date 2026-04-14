# Roadmap — Banderas

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
| 1 | MVP Completion | 🔄 Final stretch — 1 task remaining |
| 1.5 | Azure Foundation + AI Integration | ⭐ Next |
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

## 🎯 Phase 1 — MVP Completion 🔄 Current Focus

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
* [x] All controllers cleaned — zero `try/catch` blocks
* [x] All error responses return `ProblemDetails` with `Content-Type: application/problem+json`

### Input Validation Hardening ✅ Complete (PR #37)

* [x] `StrategyConfigRules` extracted — shared `internal static` class; closes KI-NEW-001
* [x] `BanderasValidationException` — 400 domain exception for route parameter failures
* [x] `RouteParameterGuard` — compiled regex allowlist guard in `Banderas.Api/Helpers/`;
      closes KI-008
* [x] `RouteParameterGuard.ValidateName(name)` called first in `GetByNameAsync`,
      `UpdateAsync`, and `ArchiveAsync`
* [x] `ExistsAsync` added to `IBanderasRepository` and implemented with `AnyAsync`
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

### Evaluation Decision Logging ✅ Complete (PR #48)

* [x] `EvaluationResult` discriminated union — `FlagDisabled` | `StrategyEvaluated`
* [x] `EvaluationReason` enum — explicit machine-readable log dimension on every entry
* [x] `HashUserId` — SHA256 surrogate, 8 hex chars; raw `UserId` never logged
* [x] `LogResult` — structured log output, consistent prefix, `UnreachableException` default
* [x] `IsEnabled(LogLevel.Information)` guard — CA1873 compliance on hot path
* [x] 16 new unit tests — `EvaluationResultTests`, `UserIdHashTests`,
      `BanderasServiceLoggingTests`
* [x] `Microsoft.Extensions.Diagnostics.Testing` `10.4.0` — `FakeLogger<T>`
* [x] `AssemblyInfo.cs` — `InternalsVisibleTo("Banderas.Tests")`

### NuGet Locked Restore ✅ Complete (rolled into PR #48)

* [x] `RestorePackagesWithLockFile=true` in `Directory.Build.props`
* [x] `packages.lock.json` committed for all projects
* [x] CI restore steps use `--locked-mode`
* [x] NuGet caching re-enabled in CI

### Seed Data for Local Development ✅ Complete (PR #49)

* [x] `IsSeeded` bool column added to `Flag` entity — provenance marker, default `false`
* [x] `Flag` constructor overload accepting `isSeeded` — existing callers unchanged
* [x] `AddIsSeededToFlag` migration — applied cleanly, existing rows receive `false`
* [x] `DatabaseSeeder` (`public sealed`) — per-record backfill in normal mode;
      `SEED_RESET=true` wipes `IsSeeded = true` rows and re-inserts; skips slots
      occupied by non-seeded active flags with a `Warning` log
* [x] Six seed records — all three strategies, Development and Staging environments
* [x] `MigrateAsync()` extension — runs schema migration before seeding in Development
* [x] Development startup block in `Program.cs` — migration then seed, guarded by
      `IsDevelopment()`; `SEED_RESET` read from environment variable
* [x] 113/113 tests passing (81 unit + 32 integration)

### Developer Experience

* [ ] `.http` smoke test request file committed to repo (`requests/smoke-test.http`)

---

## 🌩️ Phase 1.5 — Azure Foundation + AI Integration ⭐

> Begins immediately after Phase 1 DoD is met.

* [ ] Azure Key Vault — secrets management (connection strings, API keys)
* [ ] Azure Application Insights — structured telemetry sink
  (`EvaluationReason` from PR #48 feeds directly into this endpoint)
* [ ] AI flag health analysis endpoint — natural language summary of flag status
* [ ] Track evaluation counts, strategy usage, success/failure rates
* [ ] Anomaly detection — unusual evaluation pattern alerts
* [ ] Dashboard integration (Azure Monitor or Grafana)

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
* [ ] AKS deployment option documented
* [ ] SLA baseline established (p99 evaluation latency)
* [ ] Backup and migration strategy (EF Core)
* [ ] Full docker-compose devcontainer setup (resolves KI-007)
* [ ] README overhaul — product vision, market gap, architecture overview,
      getting started, AI-assisted dev workflow, roadmap summary

---

## 🌐 Phase 9 — Open Core Launch

* [ ] Public repo prepared — `CONTRIBUTING.md`, issue templates, `good first issue` labels
* [ ] Self-hosted Docker image published to GitHub Container Registry
* [ ] Managed hosting offering documented
* [ ] Launch post and demo video

---

## 🎯 Current Focus

**Phase 1 — Final Task**

1. `.http` smoke test file (`requests/smoke-test.http`)

Phase 1 DoD is met when this is complete. Phase 1.5 begins immediately after.

---

## 🧩 Notes for AI Assistants (Claude Context)

- Architecture follows Clean Architecture: Api → Application → Domain ← Infrastructure
- `IBanderasService` speaks entirely in DTOs — no `Flag` entity crosses the boundary
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
- `SaveChangesAsync` in `BanderasRepository` catches Postgres `23505` and rethrows as
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
- `IsSeeded` must never appear on `FlagResponse` or any DTO — internal infrastructure only
- `DatabaseSeeder` is `public sealed` — required for DI resolution from `Banderas.Api`
  across the assembly boundary; `internal` causes `CS0122`
- `MigrateAsync()` runs before `SeedAsync()` in the Development startup block —
  order is load-bearing; do not swap
- Do not set `IsSeeded = true` anywhere outside `DatabaseSeeder`
- Reset mode (`SEED_RESET=true`) deletes only `IsSeeded = true` rows — never touches
  manually created flags

---

## 🔭 Long-Term Vision

- Full **Observability + Experimentation Platform**
- A/B testing with statistical significance tracking
- Natural language flag creation — *"Roll out the payment flow to 10% of production users"*
- Smart rollout recommendations based on evaluation patterns
- Integration with analytics pipelines and real-time dashboards
- Anomaly detection — automatic stale flag and unusual distribution alerts
