# FeatureFlagService
A production-style feature flag management service built with .NET and EF Core, supporting percentage rollouts, role-based targeting, and environment isolation.

## Table of Contents

1. [Overview](#overview)
2. [Design Decisions](#design-decisions)
3. [Architecture](#architecture)
4. [Domain Model](#domain-model)
5. [Usage](#usage)
6. [Contributing](#contributing)

## Overview

This project explores the design and implementation of a backend feature flag system intended for internal developer tooling scenarios. 
It emphasizes deterministic evaluation, flexible rollout strategies, and clear separation of concerns between domain, service, and persistence layers.

## Design Decisions

- **Enum + JSON strategy config:** Type safety in code, flexible DB representation.
- **Private setters & explicit update methods:** Forces controlled mutations, safer domain modeling.
- **Feature evaluation separated from domain:** Supports multiple rollout strategies and unit testing.
- **Environment as enum:** Clarity in code, mapped as string in database for future flexibility.
- **Swagger/OpenAPI annotations:** Ensures self-documenting API.

## Architecture

Controllers
    ↓
IFeatureFlagService
    ↓
FeatureEvaluator
    ↓
IRolloutStrategy (PercentageStrategy, RoleStrategy)
    ↓
Repository

## Domain Model

- `FeatureFlag` – Core entity representing a feature with metadata, strategy, and environment.
- `RolloutStrategy` – Enum representing strategy types (None, Percentage, RoleBased).
- `EnvironmentType` – Enum for Development, Staging, Production.
- `FeatureEvaluationContext` – Context containing user and environment info for evaluation.

## Usage

1. Clone the repo.
2. Open in GitHub Codespaces (or VS Code).
3. Run the API:
   ```bash
   dotnet run --project FeatureFlag.Api

   ---

### 9. Contributing / Branch Workflow

```markdown
## Contributing

- Work in feature branches: `feature/*`
- Merge into `dev` branch for integration
- Merge `dev` into `main` after testing