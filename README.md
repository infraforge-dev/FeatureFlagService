# FeatureFlagService

A production-style feature flag service built with .NET 9 and EF Core. Supports deterministic 
percentage rollouts, role-based targeting, and environment isolation across Development, 
Staging, and Production.

Built as a portfolio project to demonstrate Clean Architecture, domain modeling, 
and extensible system design.

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Getting Started](#getting-started)
4. [Architecture](#architecture)
5. [Domain Model](#domain-model)
6. [Design Decisions](#design-decisions)
7. [Contributing](#contributing)

---

## Overview

Feature flags let you control which users see which features — without deploying new code. 
This service provides a backend engine for managing and evaluating those flags with support for:

- **Percentage rollouts** — gradually expose a feature to a portion of your user base
- **Role-based targeting** — enable features for specific user roles
- **Environment isolation** — flags behave independently across Development, Staging, and Production
- **Deterministic evaluation** — the same user always gets the same result

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- [Git](https://git-scm.com/)
- A SQL Server instance (local or cloud)

Optional but recommended:
- [GitHub Codespaces](https://github.com/features/codespaces) — devcontainer is preconfigured
- [VS Code](https://code.visualstudio.com/) with the C# Dev Kit extension

---

## Getting Started

**1. Clone the repository:**
```bash
git clone https://github.com/your-username/FeatureFlagService.git
cd FeatureFlagService
```

**2. Restore dependencies:**
```bash
dotnet restore FeatureFlagService.sln
```

**3. Run the API:**
```bash
dotnet run --project FeatureFlag.Api
```

**4. Explore the API:**

Navigate to `http://localhost:5227/openapi` to view the OpenAPI documentation.

---

## Architecture

This project follows Clean Architecture, with dependencies pointing inward toward the domain.
```
FeatureFlag.Api             → HTTP controllers, middleware, DI composition root
       ↓
FeatureFlag.Application     → Use cases, service interfaces, DTOs
       ↓
FeatureFlag.Domain          → Entities, enums, value objects, domain interfaces
       ↑
FeatureFlag.Infrastructure  → EF Core, repository implementations, external concerns
```

**Key principles:**
- Domain has no dependencies on any other layer
- Application depends only on Domain
- Infrastructure implements interfaces defined in Domain and Application
- Api is the composition root — it wires everything together

**Request flow:**
```
Client → Controller → IFeatureFlagService → FeatureEvaluator → IRolloutStrategy
```

---

## Domain Model

| Type | Name | Description |
|------|------|-------------|
| Entity | `Flag` | Core entity representing a feature flag with metadata, strategy, and environment |
| Value Object | `FeatureEvaluationContext` | Carries user ID, roles, and environment for a single evaluation request |
| Enum | `RolloutStrategy` | Defines strategy types: None, Percentage, RoleBased |
| Enum | `EnvironmentType` | Scopes flags to Development, Staging, or Production |

---

## Design Decisions

**Enum + JSON strategy config**
Strategy type is stored as an enum for type safety in code. Configuration details 
(e.g. percentage threshold, allowed roles) are stored as JSON for flexibility.

**Private setters with explicit mutation methods**
The domain entity never exposes public setters. All state changes go through 
named methods like `SetEnabled()` and `UpdateStrategy()`, making mutations 
intentional and traceable.

**Evaluation separated from persistence**
The `FeatureEvaluator` has no knowledge of the database. It receives a `Flag` 
and a `FeatureEvaluationContext` and returns a result — nothing more. This keeps 
evaluation logic fast, pure, and independently testable.

**Strategy pattern for rollout logic**
Each rollout strategy is an independent implementation of `IRolloutStrategy`. 
Adding a new strategy requires no changes to existing code — only a new class.

**Environment as enum, not string**
Environments are a closed set of known values. Using an enum prevents typos, 
enables compile-time safety, and maps cleanly to database strings via EF Core.

---

## Contributing

- Branch off `dev` for all work
- Use prefixes: `feature/`, `fix/`, `refactor/`, `docs/`, `test/`
- Keep commits focused and use conventional commit messages:
  - `feat:` new functionality
  - `fix:` bug corrections
  - `refactor:` structural improvements without behavior change
  - `test:` adding or updating tests
  - `docs:` documentation only
- Open a PR into `dev` — not `main`
- `main` receives merges from `dev` only after testing