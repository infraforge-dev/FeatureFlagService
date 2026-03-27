# Roadmap — FeatureFlagService

## 🎯 Project Goal

Build a production-style feature flag service that supports:

* Deterministic evaluation
* Multiple rollout strategies
* Environment isolation
* Extensibility for future observability and experimentation features

---

## 🧱 Phase 0 — Foundation ✅ Complete

### Core Domain & Architecture

* [x] Define `Flag` domain entity
* [x] Define `RolloutStrategy` enum (None, Percentage, RoleBased)
* [x] Define `EnvironmentType` enum (None = 0 sentinel, Development, Staging, Production)
* [x] Implement `FeatureEvaluationContext` value object (`IEquatable<T>`, guard clauses, immutable roles)
* [x] Enforce encapsulation (private setters, explicit mutation methods)
* [x] Clean Architecture project structure (Domain, Application, Infrastructure, Api, Tests)
* [x] Dependency directions enforced (Domain has no outward dependencies)

### Application Layer

* [x] Define `IFeatureFlagService` interface — async signatures with `CancellationToken`
* [x] Define `IRolloutStrategy` interface — includes `StrategyType` property for registry dispatch
* [x] Implement `FeatureEvaluator` — registry dispatch pattern, dictionary keyed by `RolloutStrategy`
* [x] Separate evaluation logic from domain

### Strategy Pattern

* [x] Implement `NoneStrategy` — passthrough, always returns true
* [x] Implement `PercentageStrategy` — deterministic SHA256 hashing into buckets
* [x] Implement `RoleStrategy` — config-driven, case-insensitive, fail-closed role matching

### Application Service & DTOs

* [x] `FeatureFlagService` — async, orchestrates repository + evaluator
* [x] DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`, `FlagMappings`
* [x] `DependencyInjection.cs` — `AddApplication()` extension method

### API Layer

* [x] `FeatureFlagsController` — full CRUD: GET all, GET by name, POST, PUT, DELETE (soft archive)
* [x] `EvaluationController` — POST `/api/evaluate`
* [x] Configure Swagger/OpenAPI — accessible at `/openapi/v1.json`
* [x] `JsonStringEnumConverter` wired — enums serialize as strings throughout the stack

### Persistence

* [x] Set up EF Core with Npgsql (Postgres)
* [x] `FlagConfiguration` — Fluent API: enums as strings, `jsonb` for StrategyConfig
* [x] Partial unique index on `(Name, Environment)` filtered to non-archived flags
* [x] `FeatureFlagRepository` — async, `CancellationToken` on all EF Core calls
* [x] `InitialCreate` migration — generated and applied
* [x] `docker-compose.yml` — one-command local Postgres setup

### Dev Environment

* [x] DevContainer: `.NET 10 SDK` via `dotnet` feature on `ubuntu-24.04`
* [x] Docker-outside-of-Docker configured
* [x] `dotnet-ef` added to `.config/dotnet-tools.json` — installed via `dotnet tool restore`
* [x] Devcontainer networking: `postStartCommand` joins `featureflagservice_default` network automatically
* [x] Connection string uses `Host=postgres` (Docker Compose service name, not `localhost`)

### Tests

* [x] `FeatureEvaluationContextTests` — 8/8 passing
* [x] Build: 0 warnings, 0 errors

---

## 🚀 Phase 1 — MVP Completion (Current Focus)

### Architectural Cleanup ✅ Complete

* [x] Refactor `IFeatureFlagService` — remove all `Flag` entity references from signatures
* [x] Service interface now speaks entirely in DTOs (`FlagResponse`, `CreateFlagRequest`, `UpdateFlagRequest`)
* [x] `Flag` construction moved from controller into `FeatureFlagService.CreateFlagAsync`
* [x] `UpdateFlagAsync` accepts `UpdateFlagRequest` DTO instead of 5 primitive parameters
* [x] `FeatureFlagsController` — zero `.ToResponse()` calls, zero domain entity references
* [x] Mapping consolidated inside `FeatureFlagService` — `ToResponse()` called in exactly three places
* [x] Smoke test verified: POST, GET, PUT, DELETE all return correct responses

### Validation

* [ ] Add `FluentValidation` on `CreateFlagRequest`, `UpdateFlagRequest`, `EvaluationRequest` — closes KI-003
* [ ] Name uniqueness check at the service layer before hitting the DB

### Testing

* [ ] Unit tests for `PercentageStrategy`, `RoleStrategy`, `NoneStrategy`
* [ ] Unit tests for `FeatureEvaluator` — strategy dispatch, missing strategy fallback
* [ ] Integration tests for all API endpoints (including `/api/evaluate`)

### Developer Experience

* [ ] Commit `.http` request file for smoke testing and onboarding (`requests/smoke-test.http`)
* [ ] Add seed data for local development (dev/staging/prod flags)
* [ ] Add logging for evaluation decisions
* [ ] Fix OpenAPI enum schema — enums currently render as `integer` in the spec (cosmetic, not runtime)

### Error Handling

* [ ] Global exception middleware — replace per-controller try/catch blocks
* [ ] Standardize API error response shape

---

## 🧪 Phase 2 — Testing & Reliability

### Automated Testing

* [ ] Unit tests for domain logic edge cases
* [ ] Test environment-specific behavior
* [ ] Contract tests for API responses

### Error Handling

* [ ] Handle invalid strategy configurations gracefully
* [ ] Return structured error responses for all failure modes

---

## 🔐 Phase 3 — Authentication & Authorization

### Auth Integration

* [ ] Add authentication (JWT or OAuth)
* [ ] Secure endpoints

### Authorization

* [ ] Role-based access to:
  * Create/update flags
  * Environment-specific actions
* [ ] Audit who changed what (basic tracking)

---

## 📊 Phase 4 — Observability (Key Differentiator)

### Logging & Metrics

* [ ] Log all feature evaluations
* [ ] Track:
  * Evaluation counts
  * Strategy usage
  * Success/failure rates

### Metrics Pipeline (Future-facing)

* [ ] Design event-based evaluation tracking
* [ ] Prepare for external ingestion (e.g., message queue)

### Debugging Tools

* [ ] Add endpoint: "Why was this flag ON/OFF?"
* [ ] Return evaluation trace

---

## ⚙️ Phase 5 — Advanced Rollout Strategies

### New Strategies

* [ ] User targeting (by ID)
* [ ] Time-based activation
* [ ] Gradual rollout (time + percentage)

### Strategy System Improvements

* [ ] Dynamic strategy registration (DI-driven)
* [ ] Strategy config validation framework

---

## 🌐 Phase 6 — Multi-Environment & Scaling

### Environment Enhancements

* [ ] Environment-specific overrides
* [ ] Promotion workflow (dev → staging → prod)

### Performance

* [ ] Add caching layer (in-memory / Redis)
* [ ] Optimize evaluation path (low latency)

### Scalability

* [ ] Prepare for horizontal scaling
* [ ] Stateless API design validation

---

## 🧠 Phase 7 — Developer Tooling & UX

### Internal Tooling

* [ ] Build minimal UI (optional)
* [ ] CLI for managing flags

### API Enhancements

* [ ] Bulk operations
* [ ] Versioning support

---

## 🧭 Phase 8 — Production Readiness

### DevOps

* [ ] Dockerize application
* [ ] Set up CI/CD pipeline
* [ ] Environment configuration management
* [ ] Migrate devcontainer to full docker-compose devcontainer setup (resolves KI-007)

### Data & Safety

* [ ] Backup strategy
* [ ] Migration strategy (EF Core)

### Documentation

* [ ] Finalize API documentation
* [ ] Add architecture diagrams
* [ ] Add onboarding guide

---

## 🗺️ Long-Term Vision

* Turn this into a full **Observability + Experimentation Platform**
* Add A/B testing capabilities
* Integrate with analytics pipelines
* Provide real-time dashboards

---

## 📌 Current Focus

👉 **Phase 1 — MVP Completion**

Next recommended tasks:

1. Add `FluentValidation` to request DTOs — closes KI-003
2. Add global exception middleware
3. Add unit tests for strategies and evaluator
4. Add integration tests for all endpoints
5. Commit `.http` smoke test file

---

## 🧩 Notes for AI Assistants (Claude Context)

* Architecture follows clean separation: Controller → Service → Evaluator → Strategy → Repository
* `IFeatureFlagService` speaks entirely in DTOs — no `Flag` entity crosses the service boundary
* Domain logic is intentionally strict (no public setters)
* Strategy pattern is central to extensibility
* Evaluation must remain deterministic and testable
* Connection string uses `Host=postgres` — do not change to `localhost` (will not work in devcontainer)
* Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design` with `PrivateAssets=all`

When suggesting changes:

* Do not break domain encapsulation
* Prefer composability over conditionals
* Keep evaluation logic isolated from persistence
* Do not return `Flag` entities from `IFeatureFlagService` — map to `FlagResponse` inside the service