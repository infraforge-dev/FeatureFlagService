# FeatureFlagService

**Azure-native. .NET-first. AI-assisted feature flag management.**

A production-quality feature flag evaluation service built for engineering teams
running .NET on Azure. Designed to compete in the space occupied by LaunchDarkly
and Flagsmith — but built specifically for the Microsoft ecosystem, with Azure
integrations and AI-assisted flag analysis as first-class features, not afterthoughts.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://img.shields.io/badge/build-passing-brightgreen)]()

---

## Table of Contents

- [Why This Exists](#why-this-exists)
- [Features](#features)
- [Getting Started](#getting-started)
- [API Overview](#api-overview)
- [Architecture](#architecture)
- [AI-Assisted Development Workflow](#ai-assisted-development-workflow)
- [Planned AI Features](#planned-ai-features)
- [Roadmap](#roadmap)
- [Contributing](#contributing)

---

## Why This Exists

Mid-market engineering teams running .NET on Azure are underserved by the current
feature flag market:

- **LaunchDarkly** is powerful but expensive — pricing scales aggressively past
  the free tier and the SDK is language-agnostic, not .NET-first.
- **Unleash** is open source but lacks first-class Azure integration and has
  limited .NET SDK support.
- **Azure App Configuration** handles feature flags as a secondary concern —
  it lacks rollout strategies, targeting rules, and evaluation analytics.

FeatureFlagService is being built to fill that gap: a self-hostable, MIT-licensed
platform that .NET teams on Azure can run in under 15 minutes and extend without
fighting the framework.

---

## Features

### Current (Phase 1)

- **Percentage rollouts** — deterministic SHA-256 bucketing ensures the same user
  always gets the same result, regardless of which server handles the request
- **Role-based targeting** — enable features for specific user roles with
  case-insensitive OR logic
- **Environment isolation** — flags are scoped to Development, Staging, or
  Production independently
- **Input validation** — FluentValidation v12 with sanitization on all write paths
- **Self-documenting API** — enriched OpenAPI spec with Scalar UI at `/scalar/v1`
- **Clean Architecture** — domain, application, infrastructure, and API layers
  with enforced dependency direction

### Coming Soon (Phase 1.5+)

- Azure Key Vault for secrets management
- Azure Application Insights for evaluation telemetry
- AI-assisted flag health analysis (natural language queries via Azure OpenAI)
- JWT authentication and RBAC
- .NET SDK — NuGet package for seamless ASP.NET Core integration
- Redis caching layer for high-throughput evaluation
- Self-hosted Docker image and managed hosting option

---

## Getting Started

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [VS Code](https://code.visualstudio.com/) with the
  [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
  *(recommended)*

### Run with Docker Compose

```bash
git clone https://github.com/amodelandme/FeatureFlagService.git
cd FeatureFlagService
docker compose up -d
```

The API starts at `http://localhost:5000`. Navigate to `http://localhost:5000/scalar/v1`
to explore the interactive API documentation.

> **Note:** PostgreSQL starts alongside the API. No separate database setup required.

### Run in the Dev Container

The repository ships with a fully configured devcontainer for VS Code:

1. Open the repo in VS Code
2. When prompted, click **Reopen in Container**
3. Run `docker compose up -d` from the integrated terminal to start PostgreSQL
4. Hit **F5** to run the API

### Apply Database Migrations

```bash
dotnet ef database update --project FeatureFlag.Infrastructure --startup-project FeatureFlag.Api
```

### Run Tests

```bash
dotnet test FeatureFlagService.sln
```

---

## API Overview

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/flags` | List all flags for an environment |
| `GET` | `/api/flags/{name}` | Get a flag by name and environment |
| `POST` | `/api/flags` | Create a new flag |
| `PUT` | `/api/flags/{name}` | Update a flag's strategy and state |
| `DELETE` | `/api/flags/{name}` | Archive a flag (soft delete) |
| `POST` | `/api/evaluate` | Evaluate a flag for a user context |

Full interactive documentation is available at `/scalar/v1` when running locally.
The raw OpenAPI spec is at `/openapi/v1.json`.

### Quick Example — Evaluate a Flag

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

---

## Architecture

This project follows Clean Architecture with dependencies pointing inward toward
the domain. No layer references anything above it.

```
FeatureFlag.Api             → HTTP controllers, OpenAPI, DI composition root
        ↓
FeatureFlag.Application     → Service interfaces, use cases, DTOs, validators
        ↓
FeatureFlag.Domain          → Entities, enums, value objects, domain interfaces
        ↑
FeatureFlag.Infrastructure  → EF Core, PostgreSQL, repository implementations
```

### Key Design Decisions

**Registry dispatch for rollout strategies**
Each strategy (`NoneStrategy`, `PercentageStrategy`, `RoleStrategy`) is registered
in DI and dispatched by key. Adding a new strategy requires no changes to existing
code — only a new class implementing `IRolloutStrategy`. This is the Open/Closed
Principle in practice.

**SHA-256 for deterministic bucketing**
Percentage rollouts use SHA-256 hashing on `userId + flagName` to assign users
to buckets. `HashCode.Combine()` is explicitly avoided — it is non-deterministic
across process restarts and would cause users to flip between enabled and disabled
on every deployment.

**DTOs at the service boundary**
`IFeatureFlagService` speaks entirely in DTOs — the `Flag` domain entity never
crosses the service boundary. This keeps the API layer independent of the domain
model and makes the service interface independently testable.

**FluentValidation v12 with manual validation**
`FluentValidation.AspNetCore` auto-validation middleware is deprecated. All
validation uses manual `ValidateAsync()` calls in controllers, which is the
correct v12 pattern.

**Soft deletes via archiving**
Flags are never hard-deleted. `IsArchived = true` removes them from active queries
while preserving evaluation history. A partial unique index on `(Name, Environment)`
filtered to non-archived flags enforces uniqueness without blocking re-creation.

### Request Flow

```
Client
  → Controller (validates input)
  → IFeatureFlagService (orchestrates)
  → IFeatureFlagRepository (fetches flag)
  → FeatureEvaluator (dispatches to strategy)
  → IRolloutStrategy (evaluates)
  → bool result
```

---

## AI-Assisted Development Workflow

This project is built using a structured AI-assisted engineering workflow. The
goal is not to have AI write the code — it is to use AI as a force multiplier
at every stage of the engineering process while keeping a human in the loop for
all architectural decisions.

### The Two-Agent Pattern

```
Architect Agent (Claude.ai)
  Goal: reason through design tradeoffs, produce implementation specs
  Output: Markdown spec committed to Docs/Decisions/

        ↓  spec handed off  ↓

Engineering Agent (Claude Code in VS Code)
  Goal: implement the spec until Definition of Done is met
  Tools: file system, dotnet CLI, test runner
  Loop: write → build → test → fix → repeat
```

Every significant feature starts with a design conversation, produces a spec
in `Docs/Decisions/`, and is implemented against that spec. Deviations from
the spec are captured in `implementation-notes.md` alongside the spec.

This mirrors the spec-driven development practices used by mature engineering
teams — the AI tooling makes it faster to execute, not an excuse to skip the
discipline.

### Why This Matters

The `Docs/Decisions/` folder is a first-class artifact of the project. It
captures not just *what* was built but *why* — the tradeoffs considered, the
alternatives rejected, and the constraints that shaped each decision. That's
the kind of engineering documentation that's almost universally absent from
portfolio projects and almost universally present at senior engineering teams.

---

## Planned AI Features

The AI-assisted *development* workflow is separate from the AI features built
*into the product*. The following are planned product features:

**Phase 1.5 — AI Flag Health Analysis**
A natural language analysis endpoint powered by Azure OpenAI and Semantic Kernel.
Ask questions like *"which flags haven't been evaluated in 30 days?"* or
*"which production flags are currently serving less than 5% of users?"* and
get back a plain-language answer with supporting data.

**Phase 4 — Anomaly Detection**
Automatic detection of unusual evaluation patterns — sudden drops in flag
exposure, strategies producing unexpected distributions, stale flags accumulating
in production.

**Phase 5 — Smart Rollout Recommendations**
AI-assisted rollout suggestions based on evaluation history: *"Based on current
traffic patterns, you could safely increase this flag from 10% to 25%."*

**Phase 7 — Natural Language Flag Creation**
Describe a flag in plain English — *"Gradually roll out the new payment flow to
10% of users in production"* — and have the system create the flag with the
correct strategy configuration.

---

## Roadmap

```
Phase 0   ✅  Foundation — domain, strategies, persistence, API
Phase 1   🔄  MVP Completion — validation, error handling, testing
Phase 1.5 🆕  Azure Foundation + AI — Key Vault, App Insights, AI analysis
Phase 2       Testing & Reliability — full coverage, contract tests
Phase 3       Auth & Security — JWT, RBAC, rate limiting, audit trail
Phase 4       Observability — evaluation telemetry, debugging, dashboards
Phase 5       Advanced Strategies — user targeting, time-based, gradual rollout
Phase 6       Performance — caching, Redis, horizontal scaling
Phase 7       .NET SDK — NuGet package, middleware, action filters    ← key milestone
Phase 8       Production Readiness — CI/CD, AKS, SLA baseline
Phase 9       Open Core Launch — public Docker image, hosted offering
```

See [`Docs/roadmap.md`](Docs/roadmap.md) for the full phase breakdown.

---

## Contributing

- Branch off `dev` for all work
- Use prefixes: `feature/`, `fix/`, `refactor/`, `docs/`, `test/`
- Follow conventional commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`
- Every feature requires a spec in `Docs/Decisions/` before implementation
- Build must pass with 0 errors and 0 warnings before opening a PR:
  ```bash
  dotnet build FeatureFlagService.sln
  dotnet test FeatureFlagService.sln
  ```
- Open PRs into `dev` — not `main`
