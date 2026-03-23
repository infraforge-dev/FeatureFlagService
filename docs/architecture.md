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
     ↓
[ Application Layer (IFeatureFlagService) ]
     ↓
[ Evaluation Engine (FeatureEvaluator) ]
     ↓
[ Strategy Layer (IRolloutStrategy) ]
     ↓
[ Data Access Layer (Repository / EF Core) ]
     ↓
[ Database ]
```

---

## 🧱 Architectural Layers

### 1. API Layer (Controllers)

**Responsibility:**

* Handle HTTP requests
* Validate input
* Return appropriate responses

**Key Characteristics:**

* Thin controllers (no business logic)
* Delegates all work to application layer
* Swagger/OpenAPI enabled

---

### 2. Application Layer (`IFeatureFlagService`)

**Responsibility:**

* Orchestrates use cases
* Coordinates between domain, evaluator, and repository

**Key Characteristics:**

* Contains business workflows, not domain rules
* Acts as the boundary between API and core logic

---

### 3. Evaluation Engine (`FeatureEvaluator`)

**Responsibility:**

* Determines whether a feature flag is enabled
* Delegates decision-making to strategies

**Key Characteristics:**

* Pure logic (highly testable)
* No direct database access
* Accepts `FeatureEvaluationContext`

---

### 4. Strategy Layer (`IRolloutStrategy`)

**Responsibility:**

* Encapsulates rollout logic

**Implementations:**

* `PercentageStrategy`
* `RoleStrategy`

**Key Characteristics:**

* Follows Strategy Pattern
* Easily extensible without modifying core logic
* Each strategy is independently testable

---

### 5. Domain Layer

**Core Entities:**

* `Flag`
* `FeatureEvaluationContext`

**Enums:**

* `RolloutStrategy`
* `EnvironmentType`

**Responsibility:**

* Encapsulate business rules
* Protect invariants via:

  * Private setters
  * Explicit update methods

**Key Principle:**

> The domain should never be in an invalid state.

---

### 6. Data Access Layer (Repository + EF Core)

**Responsibility:**

* Persist and retrieve data

**Key Characteristics:**

* Uses EF Core for ORM
* Maps domain entities to database schema
* Abstracted via repository pattern

---

## 🔄 Request Flow (Evaluation Example)

1. Client requests feature evaluation
2. Controller receives request
3. Controller calls `IFeatureFlagService`
4. Service retrieves `FeatureFlag` from repository
5. Service passes flag + context to `FeatureEvaluator`
6. Evaluator selects appropriate `IRolloutStrategy`
7. Strategy evaluates and returns result
8. Result returned to client

---

## 🧠 Key Design Principles

### Separation of Concerns

Each layer has a single responsibility and minimal knowledge of others.

---

### Strategy Pattern for Extensibility

New rollout strategies can be added without modifying existing logic.

---

### Deterministic Evaluation

Feature evaluation must always return the same result for the same input.

---

### Domain Integrity

All mutations go through controlled methods to prevent invalid state.

---

### Testability First

* Evaluation logic is isolated
* Strategies are independently testable
* Minimal side effects

---

## ⚖️ Design Tradeoffs

### Enum + JSON Strategy Configuration

**Pros:**

* Type safety in code
* Flexible configuration storage

**Cons:**

* Requires careful validation
* Potential runtime errors if misconfigured

---

### Repository Pattern over Direct DbContext

**Pros:**

* Abstraction for testing
* Decouples persistence

**Cons:**

* Additional complexity
* Can be overkill for small systems

---

### Layered Architecture vs Simplicity

**Pros:**

* Scales well
* Easier to reason about

**Cons:**

* More boilerplate
* Slower initial development

---

## 🔌 Extensibility Points

* Add new `IRolloutStrategy` implementations
* Introduce caching layer between service and repository
* Replace repository with external service if needed
* Add event-driven evaluation tracking

---

## 🚀 Future Architecture Considerations

### Caching Layer

* In-memory or Redis
* Reduce DB lookups for hot flags

---

### Event-Driven Observability

* Emit events for each evaluation
* Feed into analytics pipeline

---

### Multi-Service Decomposition (Optional)

* Feature management service
* Evaluation service
* Metrics/observability service

---

## 🧩 Notes for AI Assistants

* Do not introduce logic into controllers
* Do not bypass `FeatureEvaluator`
* All rollout logic must live in strategies
* Preserve domain encapsulation (no public setters)
* Prefer adding new strategies over modifying existing ones

---

## 📌 Summary

This system is designed to behave like a **miniature production-grade platform**, not just a CRUD API.

Its strength lies in:

* Clear boundaries
* Deterministic logic
* Extensibility through composition

The architecture prioritizes long-term maintainability over short-term simplicity.
