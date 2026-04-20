# Banderas

**Azure-native. .NET-first. AI-assisted feature flag management.**

A production-quality feature flag evaluation service built for .NET teams on Azure —
designed from the ground up as an open-source alternative to LaunchDarkly and Unleash,
with AI-assisted flag analysis and a first-class .NET SDK as core product features.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![CI](https://img.shields.io/github/actions/workflow/status/amodelandme/Banderas/ci.yml?label=CI&logo=github)](https://github.com/amodelandme/Banderas/actions)
[![Tests](https://img.shields.io/badge/Tests-144%20passing-brightgreen?logo=github)](#testing)
[![Phase](https://img.shields.io/badge/Phase-1%20MVP%20%E2%80%94%20Final%20Stretch-blue)](#️-roadmap)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)

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
- [Planned AI Features](#-planned-ai-features)
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

**Banderas is built to fill that gap.** Self-hostable, MIT-licensed, and designed specifically for .NET teams on Azure — with AI-assisted flag analysis built in from the start, not bolted on later.

> **The target demo:** clone the repo, run `docker compose up`, have a working flag service with a .NET SDK, and ask *"which of my flags need attention?"* — all in under 15 minutes.

---

## ✨ What Makes This Different

**🏗️ Azure-native by design**
Key Vault, Application Insights, Container Apps, and Azure OpenAI are designed in from Phase 1.5 onward — not integrations added as an afterthought.

**🎯 .NET-first**
A production-quality NuGet SDK ships alongside the service. ASP.NET Core teams get middleware extensions, action filter attributes, and service registration helpers — all idiomatic .NET.

**🤖 AI-assisted flag management**
Natural language flag health analysis, stale flag detection, rollout risk reasoning, and evaluation debugging are core product features — powered by Azure OpenAI and Semantic Kernel.

**🔓 Open Source**
Self-hostable under MIT. No vendor lock-in. Managed hosting and enterprise features are the business model — not the open source license.

**🧪 Production-quality engineering**
75 unit tests covering all evaluation strategies, the registry dispatch engine, and every validator. CI runs format gating, zero-warnings builds, and full test suites on every push. An AI reviewer (Claude API) reviews every PR for Clean Architecture compliance.

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
│  • Validates input via FluentValidation v12                  │
│  • Returns DTOs only — zero domain knowledge                 │
│  • GlobalExceptionMiddleware wraps entire pipeline           │
│  • RouteParameterGuard — allowlist enforcement on all routes │
└──────────────────────────────┬──────────────────────────────┘
                               │ DTOs only — Flag entity never crosses this line
┌──────────────────────────────▼──────────────────────────────┐
│           APPLICATION LAYER (IBanderasService)      │
│  • Orchestrates use cases                                    │
│  • Owns DTO ↔ domain entity mapping                          │
│  • InputSanitizer — two-point sanitization at HTTP boundary  │
│  • Name uniqueness enforced before DB write                  │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
┌──────────────▼──────────┐     ┌──────────────▼──────────────┐
│   EVALUATION ENGINE      │     │   DATA ACCESS LAYER          │
│   (FeatureEvaluator)     │     │   (IBanderasRepository)   │
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
│   ├── Evaluation/              # FeatureEvaluator + IRolloutStrategy implementations
│   ├── Validators/              # FluentValidation v12 — CreateFlagRequest, UpdateFlagRequest, EvaluationRequest
│   └── Services/                # IBanderasService implementation
├── Banderas.Infrastructure/  # EF Core, Postgres, repository implementation
├── Banderas.Api/             # Controllers, middleware, DI composition root
│   └── Middleware/              # GlobalExceptionMiddleware, RouteParameterGuard
└── Banderas.Tests/           # 75 unit tests — xUnit + FluentAssertions v7
```

---

## 🔑 Key Design Decisions

### Clean Architecture with DTO Boundary Enforcement
The `Flag` domain entity never crosses the service layer boundary. Controllers work exclusively with DTOs (`FlagResponse`, `CreateFlagRequest`, etc.). This enforces a clean separation that prevents API contracts from becoming coupled to internal domain evolution.

### Strategy Pattern with Registry Dispatch
Rollout strategies (`NoneStrategy`, `PercentageStrategy`, `RoleStrategy`) are registered in a dictionary keyed by `RolloutStrategy` enum. `FeatureEvaluator` dispatches to the correct strategy at runtime — adding a new strategy requires zero changes to existing code.

### FluentValidation v12 (Manual Validation)
`FluentValidation.AspNetCore` is deprecated. All validators call `ValidateAsync()` explicitly in controllers before any service code runs. A shared `InputSanitizer` handles HTTP boundary sanitization. A shared `StrategyConfigRules` class keeps config validation logic DRY across create and update validators.

### RFC 9457 Problem Details (Error Responses)
Every error returns an `application/problem+json` response conforming to RFC 9457. A domain exception hierarchy (`BanderasException` → `FlagNotFoundException`, `DuplicateFlagNameException`, `BanderasValidationException`) maps cleanly to HTTP status codes via `GlobalExceptionMiddleware`.

### Route Parameter Hardening
`RouteParameterGuard` enforces an allowlist on all route parameters — flag names, environments, and strategy types. Requests with characters outside the allowlist are rejected at the middleware boundary before reaching controllers.

### Name Uniqueness with TOCTOU Race Protection
`ExistsAsync()` checks before insert catch the common case. For concurrent requests that slip through, `SaveChangesAsync` intercepts Postgres error code `23505` (unique constraint violation) and converts it to `DuplicateFlagNameException`. The DB catch lives in Infrastructure to avoid leaking EF Core dependencies upward.

### Soft Deletes with Partial Unique Index
Flags are never hard-deleted. `IsArchived = true` removes them from active queries. A partial unique index on `(Name, Environment)` filtered to `IsArchived = false` enforces name uniqueness without blocking recreation of previously archived flags.

### Deterministic Percentage Evaluation
`PercentageStrategy` uses SHA-256 to hash `userId + flagName` into a 0–99 bucket. The same user always gets the same result across servers and restarts — no sticky sessions or shared state required.

---

## 🚀 Features

### ✅ Available Now (Phase 1)

| Feature | Details |
|---------|---------|
| **Percentage rollouts** | Deterministic SHA-256 bucketing — same user always gets the same result across servers and restarts |
| **Role-based targeting** | Enable features for specific user roles with case-insensitive matching |
| **Environment isolation** | Flags scoped independently to Development, Staging, and Production |
| **Input validation** | FluentValidation v12 with two-point sanitization (`InputSanitizer` + validators) on all write paths |
| **Route parameter hardening** | `RouteParameterGuard` enforces character allowlists on all route parameters |
| **Name uniqueness** | TOCTOU-safe via `ExistsAsync` check + Postgres constraint intercept in Infrastructure |
| **Standardized error responses** | RFC 9457 `ProblemDetails` shape on every error (`application/problem+json`) |
| **Domain exception hierarchy** | `FlagNotFoundException` (404), `DuplicateFlagNameException` (409), `BanderasValidationException` (400) |
| **Self-documenting API** | Enriched OpenAPI spec with Scalar UI at `/scalar/v1` |
| **AI PR Reviewer** | Claude-powered code review on every PR — checks Clean Architecture, FluentValidation v12 patterns, and project conventions |
| **CI pipeline** | GitHub Actions — format gate (CSharpier), zero-warnings build, 75 unit tests on every push |

### 🔜 Coming Soon

| Phase | Feature |
|-------|---------|
| **1** | Integration tests — all 6 endpoints |
| **1** | `.http` smoke test file |
| **1** | Seed data + evaluation logging |
| **1.5** | Azure Key Vault for secrets management |
| **1.5** | Azure Application Insights — evaluation telemetry |
| **1.5** | AI flag health analysis endpoint (Azure OpenAI + Semantic Kernel) |
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

### One-Command Quickstart

```bash
git clone https://github.com/amodelandme/Banderas.git
cd Banderas
docker compose up -d
```

The API starts at `http://localhost:5000`.
Interactive docs at `http://localhost:5000/scalar/v1`.

> PostgreSQL starts alongside the API — no separate database setup required.

### Dev Container (Recommended)

The repo ships with a fully configured devcontainer including .NET 10, Claude Code, GitHub CLI, and Docker-outside-of-Docker:

1. Open the repo in VS Code
2. Click **Reopen in Container** when prompted
3. Run `docker compose up -d` from the integrated terminal
4. The devcontainer auto-joins the Postgres Docker network on start — `Host=postgres` resolves without any manual configuration

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
  "flagName": "dark-mode",
  "isEnabled": true,
  "environment": "Production",
  "strategy": "RoleBased"
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
  "strategyConfig": { "Percentage": 30 }
}
```

**Role-Based Targeting**
```json
{
  "name": "admin-dashboard",
  "environment": "Production",
  "isEnabled": true,
  "strategyType": "RoleBased",
  "strategyConfig": { "Roles": ["admin", "superuser"] }
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

### Unit Tests — 75/75 Passing

Tests live in `Banderas.Tests/` and cover all pure logic — strategies, the evaluator, and all validators. Integration tests covering the full HTTP stack are in progress.

| Test Class | Count | What It Covers |
|------------|-------|----------------|
| `NoneStrategyTests` | 4 | Passthrough always returns `true` |
| `PercentageStrategyTests` | 9 | SHA-256 bucketing, boundary values, invalid config |
| `RoleStrategyTests` | 9 | Role matching, case insensitivity, fail-closed behavior |
| `FeatureEvaluatorTests` | 4 | Registry dispatch, missing strategy fallback |
| `CreateFlagRequestValidatorTests` | 17 | All create path validation rules |
| `UpdateFlagRequestValidatorTests` | 9 | All update path validation rules |
| `EvaluationRequestValidatorTests` | 10 | Evaluation request validation rules |
| **Total** | **75** | |

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

## 🔮 Planned AI Features

These are roadmapped features, not marketing claims. They will be implemented as the product matures:

| Feature | Phase | Description |
|---------|-------|-------------|
| **Flag health analysis** | 1.5 | Natural language analysis of flag state, age, and evaluation patterns |
| **Stale flag detection** | 1.5 | Identify flags that haven't been evaluated recently or are at 0% / 100% |
| **Rollout risk reasoning** | 1.5 | "Is it safe to roll this flag out to 100%?" — answered in plain English |
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
| **Testing** | xUnit + FluentAssertions v7 |
| **API Docs** | Scalar UI (replaces Swagger) |
| **Code Style** | CSharpier 1.x + `.editorconfig` |
| **CI/CD** | GitHub Actions |
| **AI (Dev Workflow)** | Claude API (Anthropic) — PR reviewer |
| **AI (Product, Planned)** | Azure OpenAI + Semantic Kernel |
| **Containerization** | Docker + Docker Compose |
| **Dev Environment** | VS Code Dev Containers |

---

## 🗺️ Roadmap

### Phase Progress

```
Phase 0  ✅  Foundation — domain, strategies, persistence, API
Phase 1  ✅  MVP Completion — validation, CI, error handling, unit tests ← (final stretch)
Phase 1.5    Azure Foundation + AI — Key Vault, App Insights, AI analysis endpoint
Phase 2      Testing & Reliability — integration tests, contract tests
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
- [x] Unit tests for all strategies and evaluator (75/75 passing)
- [x] CI pipeline — format gate + zero-warnings build
- [x] AI PR reviewer in CI
- [x] Integration tests for all 6 endpoints
- [x] `.http` smoke test file
- [x] Seed data for local development
- [ ] Evaluation decision logging

---

## 🤝 Contributing

This project is in active development. Contributions, feedback, and questions are welcome.

For contribution guidelines, see [CONTRIBUTING.md](CONTRIBUTING.md) *(coming in Phase 9)*.

For questions or architectural discussions, open an issue.

---
