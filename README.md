# Banderas

**Azure-native. .NET-first. AI-assisted feature flag management.**

A production-quality feature flag evaluation service built for .NET teams on Azure —
designed from the ground up as an open-source alternative to LaunchDarkly and Unleash,
with AI-assisted flag analysis and a first-class .NET SDK as core product features.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![CI](https://img.shields.io/github/actions/workflow/status/amodelandme/Banderas/ci.yml?label=CI&logo=github)](https://github.com/amodelandme/Banderas/actions)
[![Tests](https://img.shields.io/badge/Tests-146%20passing-brightgreen?logo=github)](#testing)
[![Phase](https://img.shields.io/badge/Phase-1.5%20Complete%20%E2%80%94%20GO%20WITH%20CONDITIONS-blue)](#️-roadmap)

---

## Table of Contents

- [Why This Exists](#-why-this-exists)
- [What Makes This Different](#-what-makes-this-different)
- [Architecture](#️-architecture)
- [Key Design Decisions](#-key-design-decisions)
- [Features](#-features)
- [Getting Started](#-getting-started)
- [API Overview](#-api-overview)
- [Error Handling](#️-error-handling)
- [Testing](#-testing)
- [AI-Assisted Development Workflow](#-ai-assisted-development-workflow)
- [AI Features](#-ai-features)
- [Tech Stack](#️-tech-stack)
- [Roadmap](#️-roadmap)
- [Contributing](#-contributing)

---

## 🎯 Why This Exists

Mid-market engineering teams running .NET on Azure are underserved by the feature flag market:

| Tool | The Problem |
|------|------------|
| **LaunchDarkly** | Powerful, but pricing scales aggressively. SDK is language-agnostic — not .NET-first. |
| **Unleash** | Open source, but limited .NET SDK support and no Azure-native integration story. |
| **Azure App Configuration** | Feature flags are a secondary concern — no rollout strategies, targeting rules, or evaluation analytics. |

**Banderas is built to fill that gap.** Self-hostable, open-core in direction, and designed specifically for .NET teams on Azure — with AI-assisted flag analysis built in from the start, not bolted on later.

> **The target demo:** clone the repo, run the local quickstart, have a working flag service with a .NET SDK, and ask *"which of my flags need attention?"* — all in under 15 minutes.

---

## ✨ What Makes This Different

**🏗️ Azure-native by design**
Key Vault, Application Insights, Container Apps, and Azure OpenAI are designed in from Phase 1.5 onward — not integrations added as an afterthought.

**🎯 .NET-first**
A production-quality NuGet SDK ships alongside the service. ASP.NET Core teams get middleware extensions, action filter attributes, and service registration helpers — all idiomatic .NET.

**🤖 AI-assisted flag management**
Natural language flag health analysis, stale flag detection, rollout risk reasoning, and evaluation debugging are core product features — powered by Azure OpenAI and Semantic Kernel.

**🔓 Open core direction**
Self-hostable by design. Managed hosting and enterprise features are the intended business model — not infrastructure lock-in.

**🧪 Production-quality engineering**
146 tests cover strategies, the registry dispatch engine, validators, service behavior, prompt sanitization, and HTTP integration paths. CI runs format gating, zero-warnings builds, unit tests, integration tests, and an optional AI reviewer for Clean Architecture compliance.

---

## 🏗️ Architecture

Banderas follows Clean Architecture with strict unidirectional dependencies. Every layer has one job and knows nothing about the layers above it.

```
┌─────────────────────────────────────────────────────────────┐
│                        CLIENT                               │
└──────────────────────────────┬──────────────────────────────┘
                               │ HTTP
┌──────────────────────────────▼──────────────────────────────┐
│                    API LAYER (Controllers)                   │
│  • Validates body DTOs via FluentValidation v12              │
│  • Returns DTOs; never exposes Flag entities                 │
│  • GlobalExceptionMiddleware wraps entire pipeline           │
│  • RouteParameterGuard — allowlist enforcement for names     │
└──────────────────────────────┬──────────────────────────────┘
                               │ DTOs + approved evaluation value object
┌──────────────────────────────▼──────────────────────────────┐
│             APPLICATION LAYER (IBanderasService)             │
│  • Orchestrates use cases                                    │
│  • Owns DTO ↔ domain entity mapping                          │
│  • InputSanitizer — validation + service-layer cleanup       │
│  • Name uniqueness enforced before DB write                  │
│  • IAiFlagAnalyzer boundary for AI health analysis           │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
┌──────────────▼──────────┐     ┌──────────────▼──────────────┐
│   EVALUATION ENGINE      │     │   DATA ACCESS LAYER          │
│   (FeatureEvaluator)     │     │   (IBanderasRepository)      │
│                          │     │                              │
│  Registry dispatch →     │     │  EF Core + Npgsql            │
│  IRolloutStrategy        │     │  jsonb for StrategyConfig    │
│  • NoneStrategy          │     │  Soft delete via archiving   │
│  • PercentageStrategy    │     │  Partial unique index on     │
│  • RoleStrategy          │     │  (Name, Environment)         │
└──────────────────────────┘     └──────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────────────┐
│                    DOMAIN LAYER                              │
│  Flag entity • FeatureEvaluationContext • RolloutStrategy    │
│  Exception hierarchy (FlagNotFoundException, etc.)           │
│  No outward dependencies — the innermost layer               │
└─────────────────────────────────────────────────────────────┘
```

### Project Structure

```
Banderas/
├── Banderas.Domain/          # Entities, enums, value objects, interfaces
│   └── Exceptions/              # Domain exception hierarchy (FlagNotFoundException, DuplicateFlagNameException, etc.)
├── Banderas.Application/     # Use cases, strategies, evaluator, DTOs, validators
│   ├── AI/                      # IAiFlagAnalyzer, IPromptSanitizer, health constants
│   ├── Evaluation/              # FeatureEvaluator + IRolloutStrategy implementations
│   ├── Validators/              # FluentValidation v12 request validators
│   └── Services/                # IBanderasService implementation
├── Banderas.Infrastructure/  # EF Core, Postgres, telemetry, Azure OpenAI implementation
├── Banderas.Api/             # Controllers, middleware, DI composition root
│   └── Middleware/              # GlobalExceptionMiddleware, RouteParameterGuard
├── Banderas.Tests/           # Unit tests — xUnit + FluentAssertions
└── Banderas.Tests.Integration/ # HTTP integration tests with Testcontainers Postgres
```

---

## 🔑 Key Design Decisions

### Clean Architecture with Entity Boundary Enforcement
The `Flag` domain entity never crosses the service layer boundary. CRUD and AI flows use DTOs (`FlagResponse`, `CreateFlagRequest`, `FlagHealthRequest`, etc.). Evaluation intentionally passes `FeatureEvaluationContext`, an immutable value object, into `IBanderasService` because it is the natural input to the pure evaluation core.

### Strategy Pattern with Registry Dispatch
Rollout strategies (`NoneStrategy`, `PercentageStrategy`, `RoleStrategy`) are registered in a dictionary keyed by `RolloutStrategy` enum. `FeatureEvaluator` dispatches to the correct strategy at runtime — adding a new strategy requires zero changes to existing code.

### FluentValidation v12 (Manual Validation)
`FluentValidation.AspNetCore` is deprecated. All validators call `ValidateAsync()` explicitly in controllers before any service code runs. A shared `InputSanitizer` handles HTTP boundary sanitization. A shared `StrategyConfigRules` class keeps config validation logic DRY across create and update validators.

### RFC 9457 Problem Details (Error Responses)
Every error returns an `application/problem+json` response conforming to RFC 9457. A domain exception hierarchy (`BanderasException` → `FlagNotFoundException`, `DuplicateFlagNameException`, `BanderasValidationException`) maps cleanly to HTTP status codes via `GlobalExceptionMiddleware`. AI availability failures use `AiAnalysisUnavailableException` and return `503`.

### Route Parameter Hardening
`RouteParameterGuard` enforces an allowlist on flag-name route parameters. Requests with characters outside the allowlist are rejected before service logic runs.

### Name Uniqueness with TOCTOU Race Protection
`ExistsAsync()` checks before insert catch the common case. For concurrent requests that slip through, `SaveChangesAsync` intercepts Postgres error code `23505` (unique constraint violation) and converts it to `DuplicateFlagNameException`. The DB catch lives in Infrastructure to avoid leaking EF Core dependencies upward.

### Soft Deletes with Partial Unique Index
Flags are never hard-deleted. `IsArchived = true` removes them from active queries. A partial unique index on `(Name, Environment)` filtered to `IsArchived = false` enforces name uniqueness without blocking recreation of previously archived flags.

### Deterministic Percentage Evaluation
`PercentageStrategy` uses SHA-256 to hash `userId + flagName` into a 0–99 bucket. The same user always gets the same result across servers and restarts — no sticky sessions or shared state required.

### Endpoint-Scoped AI Availability
Azure OpenAI integration is behind `IAiFlagAnalyzer`. Missing `AzureOpenAI:Endpoint` registers `UnavailableAiFlagAnalyzer`, so non-AI endpoints still start and only `POST /api/flags/health` returns the documented `503`.

---

## 🚀 Features

### ✅ Available Now

| Feature | Details |
|---------|---------|
| **Percentage rollouts** | Deterministic SHA-256 bucketing — same user always gets the same result across servers and restarts |
| **Role-based targeting** | Enable features for specific user roles with case-insensitive matching |
| **Environment isolation** | Flags scoped independently to Development, Staging, and Production |
| **Input validation** | FluentValidation v12 with two-point sanitization (`InputSanitizer` + validators) on all write paths |
| **Route parameter hardening** | `RouteParameterGuard` enforces character allowlists on flag-name route parameters |
| **Name uniqueness** | TOCTOU-safe via `ExistsAsync` check + Postgres constraint intercept in Infrastructure |
| **Standardized error responses** | RFC 9457 `ProblemDetails` shape on every error (`application/problem+json`) |
| **Domain exception hierarchy** | `FlagNotFoundException` (404), `DuplicateFlagNameException` (409), `BanderasValidationException` (400) |
| **Self-documenting API** | Enriched OpenAPI spec with Scalar UI at `/scalar/v1` |
| **Seed data** | Six local-development flags are available after development startup |
| **Evaluation telemetry** | Structured logs and Application Insights custom events for evaluation decisions |
| **Azure Key Vault integration** | Runtime secret loading via `Azure:KeyVaultUri` and `DefaultAzureCredential` |
| **Application Insights integration** | Azure-native telemetry sink with evaluation custom events |
| **AI flag health analysis** | `POST /api/flags/health` uses Azure OpenAI + Semantic Kernel behind `IAiFlagAnalyzer` |
| **Endpoint-scoped AI failure** | Missing Azure OpenAI endpoint leaves non-AI endpoints available and returns `503` only for AI analysis |
| **AI PR Reviewer** | Claude-powered code review on every PR — checks Clean Architecture, FluentValidation v12 patterns, and project conventions |
| **CI pipeline** | GitHub Actions — format gate (CSharpier), zero-warnings build, unit tests, integration tests, optional AI review |

### 🔜 Coming Soon

| Phase | Feature |
|-------|---------|
| **2** | AI response semantic validation after deserialization |
| **2** | Stronger direct `Flag` invariants and domain tests |
| **2** | Contract tests for API responses |
| **3** | JWT authentication and RBAC |
| **5** | User targeting, time-based activation, gradual rollout |
| **6** | Redis caching layer |
| **7** | .NET NuGet SDK — `UseBanderas()`, `[RequireFlag]`, `services.AddBanderasClient()` |

---

## ⚡ Getting Started

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [VS Code](https://code.visualstudio.com/) + [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers) *(recommended)*

### Local Quickstart

```bash
git clone https://github.com/amodelandme/Banderas.git
cd Banderas
docker compose up -d
Azure__KeyVaultUri="" dotnet run --project Banderas.Api --launch-profile http
```

The API starts at `http://localhost:5227`.
Interactive docs are available at `http://localhost:5227/scalar/v1`.

> `docker compose up -d` starts PostgreSQL. `dotnet run` starts the API. The
> `Azure__KeyVaultUri=""` override disables the development Key Vault setting for
> local runs that are not authenticated to Azure.

### Dev Container (Recommended)

The repo ships with a fully configured devcontainer including .NET 10, Claude Code, GitHub CLI, and Docker-outside-of-Docker:

1. Open the repo in VS Code
2. Click **Reopen in Container** when prompted
3. Run `docker compose up -d` from the integrated terminal
4. Run `Azure__KeyVaultUri="" dotnet run --project Banderas.Api --launch-profile http`
5. The devcontainer auto-joins the Postgres Docker network on start — `Host=postgres` resolves without any manual configuration

> **Note:** Start `docker compose up -d` before or immediately after opening the devcontainer.

---

## 📡 API Overview

All responses use `application/json`. All errors use `application/problem+json` (RFC 9457).

### Feature Flags

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/flags?environment=Development` | List all active flags in an environment |
| `GET` | `/api/flags/{name}?environment=Development` | Get a specific flag by name |
| `POST` | `/api/flags` | Create a new feature flag |
| `PUT` | `/api/flags/{name}?environment=Development` | Update an existing flag |
| `DELETE` | `/api/flags/{name}?environment=Development` | Archive a flag (soft delete) |

### Evaluation

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/evaluate` | Evaluate a flag for a specific user and context |

### Evaluation Request Example

```json
POST /api/evaluate
{
  "flagName": "dark-mode",
  "environment": "Production",
  "userId": "user-123",
  "userRoles": ["beta-tester", "admin"]
}
```

### Evaluation Response

```json
{
  "isEnabled": true
}
```

### AI Flag Health

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/flags/health` | Analyze all active flags across environments |

```json
POST /api/flags/health
{
  "stalenessThresholdDays": 7
}
```

### Strategy Config Examples

**Percentage Rollout (30% of users)**
```json
{
  "name": "checkout-v2",
  "environment": "Production",
  "isEnabled": true,
  "strategyType": "Percentage",
  "strategyConfig": "{\"percentage\": 30}"
}
```

**Role-Based Targeting**
```json
{
  "name": "admin-dashboard",
  "environment": "Production",
  "isEnabled": true,
  "strategyType": "RoleBased",
  "strategyConfig": "{\"roles\": [\"admin\", \"superuser\"]}"
}
```

---

## 🛡️ Error Handling

All errors return RFC 9457 `ProblemDetails` with `Content-Type: application/problem+json`.

| Scenario | Status | Type |
|----------|--------|------|
| Flag not found | `404` | `FlagNotFoundException` |
| Duplicate flag name | `409` | `DuplicateFlagNameException` |
| Validation failure | `400` | `BanderasValidationException` |
| Invalid route parameter | `400` | `RouteParameterGuard` rejection |
| AI analysis unavailable | `503` | `AiAnalysisUnavailableException` |
| Unexpected server error | `500` | Generic ProblemDetails |

**Example error response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Flag Not Found",
  "status": 404,
  "detail": "Flag 'dark-mode' not found in Production.",
  "instance": "/api/flags/dark-mode?environment=Production"
}
```

The exception hierarchy follows the **Open/Closed Principle** — new exception types extend `BanderasException` without modifying `GlobalExceptionMiddleware`.

---

## 🧪 Testing

### Test Suite — 146/146 Passing

Unit tests live in `Banderas.Tests/` and cover pure logic: strategies, evaluator behavior, validators, domain value objects, service orchestration, logging, prompt sanitization, and AI analysis orchestration.

Integration tests live in `Banderas.Tests.Integration/` and run the HTTP stack against Testcontainers PostgreSQL. They cover CRUD, evaluation, seed-data startup, AI health analysis, and the missing-Azure-OpenAI startup resilience path.

| Suite | Count | Coverage |
|-------|------:|----------|
| Unit | 107 | Domain, strategies, evaluator, validators, services, logging, prompt sanitization |
| Integration | 39 | API endpoints, ProblemDetails responses, seed data, AI health, startup resilience |
| **Total** | **146** | |

### A Bug Story Worth Telling

During the test session, two silent production bugs were discovered and fixed:

1. **Missing `JsonException` catch** — `PercentageStrategy` and `RoleStrategy` would throw an unhandled exception on malformed `StrategyConfig` JSON instead of failing closed.

2. **`System.Text.Json` case sensitivity** — `System.Text.Json` is case-sensitive by default. `StrategyConfig` JSON stored in Postgres uses `PascalCase` property names (`Percentage`, `Roles`). Without `PropertyNameCaseInsensitive = true`, *every* Percentage and RoleBased evaluation silently returned `false` — the flag appeared to be working but was always disabled for real users. Fixed with a static `JsonSerializerOptions` instance.

These bugs had no visible errors. They would have been invisible in production without tests.

### Running Tests

```bash
dotnet test Banderas.sln
```

---

## 🤖 AI-Assisted Development Workflow

This project uses a **two-agent AI development workflow** as a deliberate engineering practice — not just as a productivity hack:

```
┌─────────────────────────────────────────────────────────────────┐
│                     HUMAN ORCHESTRATOR                          │
│                    (Jose — Product Owner)                       │
└──────────────┬──────────────────────────────┬───────────────────┘
               │                              │
┌──────────────▼──────────┐    ┌──────────────▼──────────────────┐
│   ARCHITECT AGENT        │    │   ENGINEERING AGENT             │
│   Claude.ai (Project)    │    │   Claude Code (VS Code)         │
│                          │    │                                 │
│  Reads living docs →     │    │  Reads living docs →            │
│  Reasons through design  │    │  Reads spec →                   │
│  Writes spec.md          │    │  Implements feature             │
│  Flags interview moments │    │  Writes implementation notes    │
└─────────────────────────┘    └─────────────────────────────────┘
                                              │
                                ┌─────────────▼───────────────────┐
                                │   AI PR REVIEWER (GitHub CI)    │
                                │   Claude API — PR #35           │
                                │                                 │
                                │  Reviews every labeled PR for:  │
                                │  • Clean Architecture           │
                                │  • FluentValidation v12 rules   │
                                │  • Project conventions          │
                                │  Posts structured comments      │
                                └─────────────────────────────────┘
```

### Living Documentation

Three documents serve as the persistent memory across sessions:

| Document | Purpose |
|----------|---------|
| `Docs/architecture.md` | Structural source of truth — layer boundaries, design decisions |
| `Docs/current-state.md` | Where things stand right now — updated after every PR |
| `Docs/roadmap.md` | Phase-gated plan — where things are going |

Specs are written *before* implementation and committed to `Docs/Decisions/` as historical artifacts. They are never updated post-implementation.

---

## 🔮 AI Features

AI is split between capabilities available now and features planned for later phases:

| Feature | Phase | Description |
|---------|-------|-------------|
| **Flag health analysis** | 1.5 ✅ | Natural language analysis of flag state, age, and strategy configuration |
| **Stale flag detection** | 1.5 ✅ | Uses `UpdatedAt` and caller-supplied staleness threshold to flag stale candidates |
| **AI unavailable fallback** | 1.5 ✅ | Missing Azure OpenAI endpoint returns `503` only on the AI health endpoint |
| **AI response contract validation** | 2 | Enforce allowed statuses and one assessment per analyzed flag before returning `200` |
| **Rollout risk reasoning** | Future | "Is it safe to roll this flag out to 100%?" — answered in plain English |
| **Natural language flag creation** | Future | Describe a flag in English; get a fully configured flag back |
| **Anomaly detection** | Future | Alert when evaluation patterns change unexpectedly |
| **Evaluation debugging** | Future | "Why was this flag OFF for user X?" answered in plain English |

All AI features will use **Azure OpenAI** and **Semantic Kernel** — consistent with the Azure-native design principle.

---

## 🛠️ Tech Stack

| Category | Technology |
|----------|-----------|
| **Runtime** | .NET 10 / ASP.NET Core |
| **ORM** | EF Core 10 + Npgsql |
| **Database** | PostgreSQL 16 (Docker locally; Azure Database for PostgreSQL Flexible Server in production) |
| **Validation** | FluentValidation v12 |
| **Testing** | xUnit + FluentAssertions v8 |
| **API Docs** | Scalar UI (replaces Swagger) |
| **Code Style** | CSharpier 1.x + `.editorconfig` |
| **CI/CD** | GitHub Actions |
| **AI (Dev Workflow)** | Claude API (Anthropic) — PR reviewer |
| **AI (Product)** | Azure OpenAI + Semantic Kernel |
| **Containerization** | Docker + Docker Compose |
| **Dev Environment** | VS Code Dev Containers |

---

## 🗺️ Roadmap

### Phase Progress

```
Phase 0  ✅  Foundation — domain, strategies, persistence, API
Phase 1  ✅  MVP Completion — validation, CI, error handling, tests, telemetry
Phase 1.5 ✅  Azure Foundation + AI — Key Vault, App Insights, AI analysis endpoint
Phase 2      Testing & Reliability — AI contracts, domain invariants, API contracts
Phase 3      Auth & Security — JWT, RBAC, rate limiting, audit trail
Phase 4      Observability — evaluation telemetry, debugging endpoint, dashboards
Phase 5      Advanced Strategies — user targeting, time-based, gradual rollout
Phase 6      Performance — caching, Redis, horizontal scaling
Phase 7  ⭐  .NET SDK — first-class NuGet SDK, middleware extensions, action filters
Phase 8      Production Readiness — CD to Azure Container Apps, SLA baseline
Phase 9      Open Core Launch — public Docker image, hosted offering
```

### Phase 1 Definition of Done

- [x] FluentValidation on all request DTOs
- [x] Global exception middleware — RFC 9457 ProblemDetails
- [x] Input sanitization + route parameter hardening
- [x] Name uniqueness with TOCTOU protection
- [x] Unit tests for strategies, evaluator, validators, services, and AI helpers
- [x] CI pipeline — format gate + zero-warnings build
- [x] AI PR reviewer in CI
- [x] Integration tests for all current endpoints
- [x] `.http` smoke test file
- [x] Seed data for local development
- [x] Evaluation decision logging

### Phase 1.5 Definition of Done

- [x] Azure Key Vault integration
- [x] Application Insights integration
- [x] AI flag health analysis endpoint
- [x] Prompt sanitization before AI calls
- [x] AI unavailability maps to `503 ProblemDetails`
- [x] Missing Azure OpenAI endpoint does not block non-AI app startup
- [x] Architecture review completed — gate: GO WITH CONDITIONS

---

## 🤝 Contributing

This project is in active development. Contributions, feedback, and questions are welcome.

Contribution guidelines are planned for Phase 9.

For questions or architectural discussions, open an issue.

---
