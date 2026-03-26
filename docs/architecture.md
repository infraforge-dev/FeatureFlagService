# Architecture — FeatureFlagService

## 🧭 Overview

FeatureFlagService is a modular backend system designed to evaluate feature flags in a deterministic, extensible, and environment-aware manner.

The system follows a **layered architecture with strong separation of concerns**, enabling:

* Testable evaluation logic
* Flexible rollout strategies
* Clear boundaries between domain, application, and infrastructure

---

## 🏗️ High-Level Architecture

```text
[ Client ]
     ↓
[ Controllers (API Layer) ]
  DTOs in, DTOs out — no domain knowledge
     ↓
[ Application Layer (IFeatureFlagService) ]
  Speaks entirely in DTOs — Flag entity never crosses this boundary
     ↓
[ Evaluation Engine (FeatureEvaluator) ]
  Pure logic — works with domain entities internally
     ↓
[ Strategy Layer (IRolloutStrategy) ]
     ↓
[ Data Access Layer (Repository / EF Core) ]
     ↓
[ Database (Postgres) ]
```

---

## 🧱 Architectural Layers

### 1. API Layer (Controllers)

**Responsibility:**

* Handle HTTP requests
* Validate input
* Return appropriate responses

**Key Characteristics:**

* Thin controllers — no business logic, no domain knowledge
* Delegates all work to application layer via `IFeatureFlagService`
* Receives and returns DTOs only — never touches domain entities
* Swagger/OpenAPI enabled at `/openapi/v1.json`

---

### 2. Application Layer (`IFeatureFlagService`)

**Responsibility:**

* Orchestrates use cases
* Coordinates between domain, evaluator, and repository
* Owns the DTO ↔ domain entity mapping boundary

**Key Characteristics:**

* `IFeatureFlagService` interface speaks entirely in DTOs
* `Flag` entity is constructed and mapped inside `FeatureFlagService` — never exposed to callers
* `ToResponse()` mapping is called inside the service, not in controllers
* Acts as the hard boundary between the API world and the domain world

**Boundary Rule:**

> `Flag` domain entity must never appear in any `IFeatureFlagService` method signature.
> The controller layer must never call `.ToResponse()` directly.

---

### 3. Evaluation Engine (`FeatureEvaluator`)

**Responsibility:**

* Determines whether a feature flag is enabled for a given context
* Delegates decision-making to the appropriate strategy

**Key Characteristics:**

* Pure logic — highly testable, no side effects
* No direct database access
* Registry dispatch pattern — `Dictionary<RolloutStrategy, IRolloutStrategy>`
* Accepts `FeatureEvaluationContext` (value object from domain)

**Precondition (KI-002):**

> Callers must check `Flag.IsEnabled` before calling `Evaluate`. The evaluator is a
> pure strategy dispatcher, not a policy enforcer. This contract is documented via
> XML doc comment but not enforced by a guard clause. If a second caller is introduced,
> reconsider adding the guard.

---

### 4. Strategy Layer (`IRolloutStrategy`)

**Responsibility:**

* Encapsulates rollout logic for a specific strategy type

**Implementations:**

* `NoneStrategy` — passthrough, always returns true
* `PercentageStrategy` — deterministic SHA256 hashing into 100 buckets
* `RoleStrategy` — config-driven, case-insensitive, fail-closed role matching

**Key Characteristics:**

* Follows Strategy Pattern — open for extension, closed for modification
* Each strategy is registered as a Singleton (stateless, safe to share)
* Each strategy is independently testable
* New strategies require zero changes to `FeatureEvaluator`

---

### 5. Domain Layer

**Core Entities:**

* `Flag` — encapsulates business rules, private setters, explicit mutation methods

**Value Objects:**

* `FeatureEvaluationContext` — immutable, `IEquatable<T>`, guard clauses on construction

**Enums:**

* `RolloutStrategy` (None, Percentage, RoleBased)
* `EnvironmentType` (None = 0 sentinel, Development, Staging, Production)

**Interfaces:**

* `IRolloutStrategy` — strategy contract
* `IFeatureFlagRepository` — persistence contract

**Responsibility:**

* Encapsulate business rules
* Protect invariants via private setters and explicit update methods

**Key Principle:**

> The domain should never be in an invalid state.

---

### 6. Data Access Layer (Repository + EF Core)

**Responsibility:**

* Persist and retrieve `Flag` entities

**Key Characteristics:**

* Postgres via Npgsql
* `FlagConfiguration` uses Fluent API: enums stored as strings, `StrategyConfig` as `jsonb`
* Partial unique index on `(Name, Environment)` filtered to `IsArchived = false` — archived flags are invisible to the uniqueness constraint
* Repository filters out archived flags on all read operations
* Abstracted via `IFeatureFlagRepository` — infrastructure detail hidden from domain and application layers

---

## 🔄 Request Flow (Evaluation Example)

1. Client sends `POST /api/evaluate` with `EvaluationRequest` DTO
2. `EvaluationController` receives request — constructs `FeatureEvaluationContext`
3. Controller calls `IFeatureFlagService.IsEnabledAsync(flagName, context)`
4. `FeatureFlagService` retrieves `Flag` entity from repository
5. Service checks `Flag.IsEnabled` — returns false immediately if disabled
6. Service passes `Flag` + context to `FeatureEvaluator.Evaluate`
7. Evaluator looks up strategy by `Flag.StrategyType` in registry
8. Strategy evaluates and returns bool result
9. Service returns bool to controller
10. Controller returns `{ "isEnabled": true/false }` to client

---

## 🔄 Request Flow (CRUD Example — Create)

1. Client sends `POST /api/flags` with `CreateFlagRequest` DTO
2. `FeatureFlagsController` receives request — passes DTO directly to service
3. `FeatureFlagService.CreateFlagAsync` constructs `Flag` entity internally from DTO
4. Service calls `IFeatureFlagRepository.AddAsync` and `SaveChangesAsync`
5. Service maps `Flag` → `FlagResponse` via `FlagMappings.ToResponse()`
6. Controller returns `201 Created` with `FlagResponse` body

---

## 🧠 Key Design Principles

### Separation of Concerns

Each layer has a single responsibility and minimal knowledge of others.

---

### DTO Boundary at the Service Interface

`IFeatureFlagService` is the hard boundary between the API world and the domain world.
DTOs cross the boundary inward. Domain entities never cross the boundary outward.
This keeps controllers stable when the domain evolves, and keeps domain logic
independent of serialization concerns.

---

### Strategy Pattern for Extensibility

New rollout strategies can be added without modifying `FeatureEvaluator` or any
existing strategy. Implement `IRolloutStrategy`, register it in DI — done.

---

### Deterministic Evaluation

Feature evaluation must always return the same result for the same input.
`PercentageStrategy` uses SHA256 hashing to ensure determinism across restarts.

---

### Domain Integrity

All mutations go through controlled methods to prevent invalid state. No public setters
on domain entities. `Flag.Update()` sets all related fields atomically.

---

### Testability First

* Evaluation logic is isolated from persistence
* Strategies are independently testable (pure functions)
* `IFeatureFlagRepository` is an interface — swappable in tests
* `IFeatureFlagService` is an interface — controllers are independently testable

---

## ⚖️ Design Tradeoffs

### DTO Boundary vs Convenience

**Decision:** `IFeatureFlagService` speaks entirely in DTOs — no `Flag` entity in signatures.

**Pros:**
* Controllers have zero domain knowledge — stable API layer
* Mapping consolidated in one place — easier to reason about
* Domain can evolve without breaking the API contract

**Cons:**
* Slight overhead — mapping `Flag → FlagResponse` inside the service
* `EnvironmentType` enum still appears on the interface — acceptable for now, revisit if duplication becomes a problem

---

### Enum + JSON Strategy Configuration

**Decision:** `StrategyConfig` stored as `jsonb`, deserialized at evaluation time.

**Pros:**
* Flexible — each strategy defines its own config shape
* No schema migrations required when a strategy's config changes

**Cons:**
* No validation at write time — misconfiguration fails silently at evaluation (KI-003)
* **Planned fix:** FluentValidation on request DTOs in Phase 1

---

### Repository Pattern over Direct DbContext

**Pros:**
* Abstraction for testing — swappable in unit tests
* Decouples persistence detail from application logic

**Cons:**
* Additional complexity
* Can be overkill for small systems — accepted cost for portfolio quality

---

### Layered Architecture vs Simplicity

**Pros:**
* Scales well — each layer can evolve independently
* Easier to reason about — one layer, one job

**Cons:**
* More boilerplate
* Slower initial development — accepted cost for long-term maintainability

---

## 🔌 Extensibility Points

* Add new `IRolloutStrategy` implementations — zero changes to evaluator required
* Introduce caching layer between service and repository
* Replace repository with external service if needed
* Add event-driven evaluation tracking (Phase 4)
* Swap Postgres for another DB — only `FlagConfiguration` and `DependencyInjection` in Infrastructure need updating

---

## 🚀 Future Architecture Considerations

### Caching Layer (Phase 6)

* In-memory or Redis between `FeatureFlagService` and `FeatureFlagRepository`
* Reduce DB lookups for frequently evaluated flags
* Cache invalidation on flag update/archive

---

### Event-Driven Observability (Phase 4)

* Emit evaluation events from `FeatureEvaluator` or `FeatureFlagService`
* Feed into analytics pipeline
* Enable "Why was this flag ON/OFF?" debugging endpoint

---

### Docker Compose Devcontainer (Phase 8)

* Replace current `postStartCommand` network join workaround (KI-007)
* Both devcontainer and Postgres start together on a shared network
* Eliminates startup ordering dependency

---

### Multi-Service Decomposition (Optional, Long-term)

* Feature management service (CRUD)
* Evaluation service (read-heavy, cacheable)
* Metrics/observability service

---

## 🧩 Notes for AI Assistants

* Do not introduce logic into controllers
* Do not bypass `FeatureEvaluator` — all rollout logic lives in strategies
* Preserve domain encapsulation — no public setters on `Flag`
* Prefer adding new strategies over modifying existing ones
* `IFeatureFlagService` must never expose `Flag` in any method signature
* `ToResponse()` must be called inside `FeatureFlagService` — never in controllers
* Connection string uses `Host=postgres` — do not change to `localhost`
* See KI-002 before adding new callers to `FeatureEvaluator.Evaluate`
* See KI-003 before accepting `StrategyConfig` without validation

---

## 📌 Summary

This system is designed to behave like a **miniature production-grade platform**, not just a CRUD API.

Its strength lies in:

* Clear layer boundaries — each layer speaks its own language (DTOs at API, entities at domain)
* Deterministic logic — same input always produces same output
* Extensibility through composition — new strategies require zero changes to existing code

The architecture prioritizes long-term maintainability over short-term simplicity.