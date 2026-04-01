# FeatureFlagService

**Azure-native. .NET-first. AI-assisted feature flag management.**

A production-quality feature flag evaluation service built for .NET teams on Azure —
designed from the ground up as an open-core alternative to LaunchDarkly and Unleash,
with AI-assisted flag analysis and a first-class .NET SDK as core product features.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://img.shields.io/github/actions/workflow/status/amodelandme/FeatureFlagService/ci.yml?label=CI&logo=github)](https://github.com/amodelandme/FeatureFlagService/actions)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)
[![Phase](https://img.shields.io/badge/Phase-1%20MVP-blue)](#roadmap)

---

## Table of Contents

- [Why This Exists](#-why-this-exists)
- [What Makes This Different](#-what-makes-this-different)
- [Architecture](#-architecture)
- [Key Design Decisions](#-key-design-decisions)
- [Features](#-features)
- [Getting Started](#-getting-started)
- [API Overview](#-api-overview)
- [Error Handling](#-error-handling)
- [AI-Assisted Development Workflow](#-ai-assisted-development-workflow)
- [Planned AI Features](#-planned-ai-features)
- [Tech Stack](#-tech-stack)
- [Roadmap](#-roadmap)
- [Contributing](#-contributing)

---

## 🎯 Why This Exists

Mid-market engineering teams running .NET on Azure are underserved by the feature flag market:

| Tool | The Problem |
|------|------------|
| **LaunchDarkly** | Powerful, but pricing scales aggressively. SDK is language-agnostic — not .NET-first. |
| **Unleash** | Open source, but limited .NET SDK support and no Azure-native integration story. |
| **Azure App Configuration** | Feature flags are a secondary concern — no rollout strategies, targeting rules, or evaluation analytics. |

**FeatureFlagService is built to fill that gap.** Self-hostable, MIT-licensed, and designed specifically for .NET teams on Azure — with AI-assisted flag analysis built in from the start, not bolted on later.

> The target demo: clone the repo, run `docker compose up`, have a working flag service with a .NET SDK, and ask *"which of my flags need attention?"* — all in under 15 minutes.

---

## ✨ What Makes This Different

**🏗️ Azure-native by design**
Key Vault, Application Insights, Container Apps, and Azure OpenAI are designed in from Phase 1.5 onward — not integrations added as an afterthought.

**🎯 .NET-first**
A production-quality NuGet SDK ships alongside the service. ASP.NET Core teams get middleware extensions, action filter attributes, and service registration helpers — all idiomatic .NET.

**🤖 AI-assisted flag management**
Natural language flag health analysis, stale flag detection, rollout risk reasoning, and evaluation debugging are core product features — powered by Azure OpenAI and Semantic Kernel.

**🔓 Open core**
Self-hostable under MIT. No vendor lock-in. Managed hosting and enterprise features are the business model — not the open source license.

---

## 🏗️ Architecture

FeatureFlagService follows Clean Architecture with strict unidirectional dependencies. Every layer has one job and knows nothing about the layers above it.

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
└──────────────────────────────┬──────────────────────────────┘
                               │ DTOs only — Flag entity never crosses this line
┌──────────────────────────────▼──────────────────────────────┐
│              APPLICATION LAYER (IFeatureFlagService)         │
│  • Orchestrates use cases                                    │
│  • Owns DTO ↔ domain entity mapping                          │
│  • Sanitizes inputs before evaluation                        │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
┌──────────────▼──────────┐     ┌──────────────▼──────────────┐
│   EVALUATION ENGINE      │     │   DATA ACCESS LAYER          │
│   (FeatureEvaluator)     │     │   (IFeatureFlagRepository)   │
│                          │     │                              │
│  Registry dispatch →     │     │  EF Core + Npgsql            │
│  IRolloutStrategy        │     │  jsonb for StrategyConfig    │
│  • NoneStrategy          │     │  Soft delete via archiving   │
│  • PercentageStrategy    │     │  Partial unique index        │
│  • RoleStrategy          │     └──────────────────────────────┘
└──────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────────────┐
│                    DOMAIN LAYER                              │
│  Flag entity • FeatureEvaluationContext • RolloutStrategy    │
│  No outward dependencies — the innermost layer              │
└─────────────────────────────────────────────────────────────┘
```

### Project Structure

```
FeatureFlagService/
├── FeatureFlag.Domain/          # Entities, enums, value objects, interfaces
│   └── Exceptions/              # Domain exception hierarchy (FlagNotFoundException etc.)
├── FeatureFlag.Application/     # Use cases, service, evaluator, strategies, validators
│   ├── Evaluation/              # FeatureEvaluator — registry dispatch pattern
│   ├── Services/                # FeatureFlagService — orchestration
│   ├── Strategies/              # NoneStrategy, PercentageStrategy, RoleStrategy
│   └── Validators/              # FluentValidation v12 — CreateFlag, UpdateFlag, Evaluate
├── FeatureFlag.Infrastructure/  # EF Core, Postgres, repositories
├── FeatureFlag.Api/             # Controllers, middleware, OpenAPI, DI composition root
│   └── Middleware/              # GlobalExceptionMiddleware
└── FeatureFlag.Tests/           # xUnit unit tests
```

### Request Flow — Flag Evaluation

```
POST /api/evaluate
       │
       ▼
GlobalExceptionMiddleware (outermost layer — catches anything that escapes)
       │
       ▼
EvaluationController
  └─► ValidateAsync(EvaluationRequest)  ──► 400 if invalid
       │
       ▼
FeatureFlagService.IsEnabledAsync()
  ├─► IFeatureFlagRepository.GetByNameAsync()  ──► FlagNotFoundException if null → 404
  ├─► Check Flag.IsEnabled  ──► return false immediately if disabled
  └─► FeatureEvaluator.Evaluate(flag, context)
         │
         ▼
    Strategy Registry [RolloutStrategy enum → IRolloutStrategy]
         │
         ├── None       → always true
         ├── Percentage → SHA-256 hash(userId + flagName) % 100 < threshold
         └── RoleBased  → HashSet<string>(OrdinalIgnoreCase) intersection
         │
         ▼
    bool result → { "isEnabled": true/false }
```

---

## 🧠 Key Design Decisions

### Registry Dispatch Pattern
`FeatureEvaluator` builds a `Dictionary<RolloutStrategy, IRolloutStrategy>` at startup from DI-registered strategies. At evaluation time it's an O(1) dictionary lookup — no switch statements, no conditionals. Adding a new strategy is one new class and one DI registration. The evaluator never changes.

### Deterministic SHA-256 Bucketing
Percentage rollouts hash `userId + flagName` with SHA-256 into a 0–99 bucket. `string.GetHashCode()` is explicitly avoided — it's randomized per-process in .NET by design, which would flip users between enabled/disabled on every restart. SHA-256 is stable, deterministic, and consistent across machines.

### DTO Boundary at the Service Interface
`IFeatureFlagService` speaks entirely in DTOs. The `Flag` domain entity never crosses the service boundary. Controllers are completely decoupled from the domain model and stay stable when domain logic changes.

### Domain Exception Hierarchy
Named exceptions (`FlagNotFoundException`, `DuplicateFlagNameException`) extend `FeatureFlagException`, which carries an HTTP status code. `GlobalExceptionMiddleware` is the single catch-all — domain exceptions map to their declared 4xx status codes; anything unexpected gets logged in full and returns a safe generic 500. Controllers contain only the happy path.

### FluentValidation v12 — Manual Validation
`FluentValidation.AspNetCore` auto-validation is deprecated. All validation uses explicit `ValidateAsync()` calls in controllers. Validators run at the HTTP boundary before any service code executes. A shared `InputSanitizer` is called in both validators and the service layer to handle the immutable DTO problem.

### Soft Deletes with Partial Unique Index
Flags are never hard-deleted. `IsArchived = true` removes them from active queries. A partial unique index on `(Name, Environment)` filtered to `IsArchived = false` enforces name uniqueness without blocking recreation of previously archived flags.

---

## 🚀 Features

### ✅ Available Now (Phase 1)

| Feature | Details |
|---------|---------|
| **Percentage rollouts** | Deterministic SHA-256 bucketing — same user always gets the same result across servers and restarts |
| **Role-based targeting** | Enable features for specific user roles with case-insensitive matching |
| **Environment isolation** | Flags scoped independently to Development, Staging, and Production |
| **Input validation** | FluentValidation v12 with two-point sanitization on all write paths |
| **Standardized error responses** | RFC 9457 `ProblemDetails` shape on every error — consistent contract for consumers |
| **Self-documenting API** | Enriched OpenAPI spec with Scalar UI at `/scalar/v1` |
| **AI PR Reviewer** | Claude-powered code review on every PR — checks Clean Architecture, FluentValidation v12 patterns, and project conventions |
| **CI pipeline** | GitHub Actions — format gate, zero-warnings build, unit tests on every push |

### 🔜 Coming Soon

| Phase | Feature |
|-------|---------|
| **1.5** | Azure Key Vault for secrets management |
| **1.5** | Azure Application Insights — evaluation telemetry |
| **1.5** | AI flag health analysis endpoint (Azure OpenAI + Semantic Kernel) |
| **3** | JWT authentication and RBAC |
| **5** | User targeting, time-based activation, gradual rollout |
| **6** | Redis caching layer |
| **7** | .NET NuGet SDK — middleware, action filters, service registration |

---

## ⚡ Getting Started

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [VS Code](https://code.visualstudio.com/) + [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers) *(recommended)*

### One-Command Quickstart

```bash
git clone https://github.com/amodelandme/FeatureFlagService.git
cd FeatureFlagService
docker compose up -d
```

The API starts at `http://localhost:5000`.
Interactive docs at `http://localhost:5000/scalar/v1`.

> PostgreSQL starts alongside the API — no separate database setup required.

### Dev Container (Recommended)

The repo ships with a fully configured devcontainer:

1. Open the repo in VS Code
2. Click **Reopen in Container** when prompted
3. Run `docker compose up -d` from the integrated terminal
4. Hit **F5** to start the API

### Apply Migrations

```bash
dotnet ef database update \
  --project FeatureFlag.Infrastructure \
  --startup-project FeatureFlag.Api
```

### Run Tests

```bash
# Unit tests only
dotnet test --filter "Category!=Integration"

# All tests (requires running Postgres)
dotnet test
```

---

## 📡 API Overview

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/flags?environment=Production` | List all active flags for an environment |
| `GET` | `/api/flags/{name}?environment=Production` | Get a flag by name and environment |
| `POST` | `/api/flags` | Create a new flag |
| `PUT` | `/api/flags/{name}?environment=Production` | Update a flag's strategy and enabled state |
| `DELETE` | `/api/flags/{name}?environment=Production` | Archive a flag (soft delete) |
| `POST` | `/api/evaluate` | Evaluate a flag for a user context |

Full interactive documentation is available at `/scalar/v1` when running locally.

### Evaluate a Flag

```http
POST /api/evaluate
Content-Type: application/json

{
  "flagName": "new-checkout-flow",
  "userId": "user-abc-123",
  "userRoles": ["beta-tester"],
  "environment": "Production"
}
```

```json
{
  "isEnabled": true
}
```

### Create a Percentage Rollout

```http
POST /api/flags
Content-Type: application/json

{
  "name": "new-checkout-flow",
  "isEnabled": true,
  "environment": "Production",
  "strategyType": "Percentage",
  "strategyConfig": "{\"percentage\": 25}"
}
```

---

## 🛡️ Error Handling

All error responses follow [RFC 9457](https://tools.ietf.org/html/rfc9457) `ProblemDetails`:

```json
{
  "type": "about:blank",
  "title": "Not Found",
  "status": 404,
  "detail": "No feature flag with name 'dark-mode' was found.",
  "instance": "/api/flags/dark-mode"
}
```

`GlobalExceptionMiddleware` is the single catch-all in the pipeline. Controllers contain only the happy path — no try/catch anywhere in the API layer.

| Exception | HTTP Status | When |
|-----------|-------------|------|
| `FlagNotFoundException` | 404 | Flag name not found in the requested environment |
| `DuplicateFlagNameException` | 409 | Flag with that name already exists |
| Unhandled exception | 500 | Logged in full server-side; safe generic message returned to consumer |

---

## 🤖 AI-Assisted Development Workflow

This project is built using a structured two-agent AI workflow. The goal is not to have AI write the code — it's to use AI as a force multiplier at every stage of the engineering process while keeping a human in the loop for all architectural decisions.

```
┌─────────────────────────────────┐
│   ARCHITECT AGENT (Claude.ai)   │
│                                 │
│  • Reasons through tradeoffs    │
│  • Produces implementation spec │
│  • Updates living docs          │
│                                 │
│  Output: Docs/Decisions/*.md    │
└────────────────┬────────────────┘
                 │  spec committed to repo
                 ▼
┌─────────────────────────────────┐
│  ENGINEERING AGENT (Claude Code) │
│                                 │
│  • Reads spec                   │
│  • Implements to DoD            │
│  • build → test → fix → repeat  │
│  • Documents deviations         │
│                                 │
│  Output: working code + impl    │
│          notes                  │
└────────────────┬────────────────┘
                 │  PR opened
                 ▼
┌─────────────────────────────────┐
│  AI REVIEWER (GitHub Actions)   │
│                                 │
│  • Reviews diff via Claude API  │
│  • Checks Clean Architecture    │
│  • Flags FluentValidation v12   │
│    misuse, missing tokens, etc. │
│  • Posts structured PR comment  │
└─────────────────────────────────┘
```

Every significant feature starts with a design conversation, produces a spec in `Docs/Decisions/`, and is implemented against that spec. Deviations are captured in `implementation-notes.md` alongside each spec.

This mirrors spec-driven development practices used by mature engineering teams — the AI tooling makes it faster to execute, not an excuse to skip the discipline.

**The `Docs/Decisions/` folder is a first-class artifact.** It captures not just *what* was built but *why* — the tradeoffs considered, the alternatives rejected, the constraints that shaped each decision.

---

## 🧠 Planned AI Features

The AI-assisted *development* workflow above is separate from AI features built *into the product*:

**Phase 1.5 — Natural Language Flag Health Analysis**
Ask *"which flags haven't been evaluated in 30 days?"* or *"which production flags are currently serving less than 5% of users?"* — powered by Azure OpenAI and Semantic Kernel.

**Phase 4 — Anomaly Detection**
Automatic detection of unusual evaluation patterns — sudden drops in flag exposure, strategies producing unexpected distributions, stale flags accumulating in production.

**Phase 5 — Smart Rollout Recommendations**
*"Based on current traffic patterns, you could safely increase this flag from 10% to 25%."*

**Phase 7 — Natural Language Flag Creation**
Describe a flag in plain English and have the service configure the correct strategy — *"Gradually roll out the new payment flow to 10% of users in production."*

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 10, ASP.NET Core |
| Database | PostgreSQL 16 via Npgsql + EF Core |
| Validation | FluentValidation v12 |
| API Docs | Scalar UI + Microsoft.AspNetCore.OpenApi |
| CI/CD | GitHub Actions |
| AI Reviewer | Claude API (claude-sonnet-4-6) |
| Containerization | Docker + Docker Compose |
| Dev Environment | VS Code Dev Containers |
| Code Formatting | CSharpier 1.x |
| Testing | xUnit |

---

## 🗺️ Roadmap

```
Phase 0   ✅  Foundation          Domain, strategies, persistence, full CRUD API
Phase 1   🔄  MVP Completion      Validation, error handling, CI, unit + integration tests
Phase 1.5 🔜  Azure + AI          Key Vault, App Insights, AI analysis endpoint
Phase 2       Testing             Full coverage, contract tests, integration test suite
Phase 3       Auth & Security     JWT, RBAC, rate limiting, audit trail
Phase 4       Observability       Evaluation telemetry, "why was this flag ON?" endpoint
Phase 5       Advanced Strategies User targeting, time-based activation, gradual rollout
Phase 6       Performance         Redis caching, horizontal scaling validation
Phase 7   ⭐  .NET SDK            NuGet package, middleware, action filters   ← key milestone
Phase 8       Production          CD to Azure, AKS, SLA baseline
Phase 9       Open Core Launch    Public Docker image, managed hosting option
```

See [`Docs/roadmap.md`](Docs/roadmap.md) for the full phase breakdown with task-level detail.

---

## 🤝 Contributing

Contributions are welcome. This project is actively developed and looking for collaborators
who want to build production-quality .NET tooling and get real experience with Clean
Architecture, AI integration, and Azure-native development.

### What We're Looking For

- .NET / C# developers (any level — this is a good learning project)
- Azure developers interested in Key Vault, App Insights, or Container Apps
- Developers interested in AI integration (Azure OpenAI, Semantic Kernel)
- Anyone who wants to build a real NuGet SDK from scratch

### How to Contribute

1. **Check the open issues** — look for `good first issue` or `help wanted` labels
2. **Read the architecture docs** — `Docs/architecture.md` explains every layer and the rules that govern them
3. **Fork and branch** — branch off `dev` using the conventions below
4. **Write a spec first** — significant features require a spec in `Docs/Decisions/` before implementation. Open an issue to discuss first if you're unsure
5. **Open a PR into `dev`** — not `main`

### Branch and Commit Conventions

```bash
# Branch prefixes
feature/  fix/  refactor/  docs/  test/

# Commit format (Conventional Commits)
feat: add time-based activation strategy
fix: correct percentage bucket overflow on uint boundary
docs: update architecture decision for caching layer
```

### Build Requirements

PRs must pass the CI pipeline:

```bash
# These must all pass before opening a PR
dotnet build FeatureFlagService.sln          # 0 errors, 0 warnings
dotnet test --filter "Category!=Integration" # all unit tests green
dotnet csharpier check .                     # 0 formatting violations
```

### Project Conventions

- **No try/catch in controllers** — `GlobalExceptionMiddleware` handles everything
- **Controllers return only the happy path** — throw domain exceptions from the service layer
- **DTOs at the service boundary** — `Flag` entity never crosses `IFeatureFlagService`
- **FluentValidation v12 only** — no `.Transform()`, no `FluentValidation.AspNetCore`
- **CSharpier is the formatting authority** — run `dotnet csharpier format .` before committing
- **Specs before code** — significant work requires a `Docs/Decisions/` spec

---

*Built by [@amodelandme](https://github.com/amodelandme).
If this is useful to you or you want to collaborate, open an issue or start a discussion.*
