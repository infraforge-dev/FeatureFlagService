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
* [x] Define `EnvironmentType` enum
* [x] Implement `FeatureEvaluationContext` value object
* [x] Enforce encapsulation (private setters, explicit mutation methods)
* [x] `Flag.Update()` — atomic update method (sets all fields + `UpdatedAt` once)
* [x] Clean Architecture project structure (Domain, Application, Infrastructure, Api, Tests)
* [x] Dependency directions enforced (Domain has no outward dependencies)
* [x] DevContainer on .NET 10 SDK (`devcontainers/base:ubuntu-24.04` + dotnet feature)
* [x] Docker-outside-of-Docker configured (host socket mounted, CLI feature added)
 
### Application Layer
 
* [x] Define `IFeatureFlagService` interface — async with `CancellationToken`
* [x] Define `IFeatureFlagRepository` interface — async with `CancellationToken`
* [x] Implement `FeatureEvaluator` — registry dispatch pattern
* [x] Separate evaluation logic from domain and persistence
* [x] DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`
* [x] `FeatureFlagService` — async orchestration of repository + evaluator
 
### Strategy Pattern
 
* [x] Create `IRolloutStrategy` interface (includes `StrategyType` for registry dispatch)
* [x] Implement `NoneStrategy` — passthrough, always returns true
* [x] Implement `PercentageStrategy` — deterministic SHA256 hashing into buckets
* [x] Implement `RoleStrategy` — config-driven, case-insensitive, fail-closed
 
### API Layer
 
* [x] `FeatureFlagsController` — GET all, GET by name, POST, PUT, DELETE (soft archive)
* [x] `EvaluationController` — POST `/api/evaluate`, 404 for unknown flags
* [x] Swagger/OpenAPI configured — accessible at `/openapi/v1.json`
* [x] `JsonStringEnumConverter` — enums serialize as strings in API responses
 
### Persistence
 
* [x] PostgreSQL via Docker Compose (`docker-compose.yml` at repo root)
* [x] EF Core with Npgsql provider
* [x] `FlagConfiguration` — Fluent API: enums as strings, `jsonb` for StrategyConfig
* [x] Partial unique index on `(Name, Environment)` filtered to non-archived flags
* [x] `FeatureFlagRepository` — async, `CancellationToken` on all EF Core calls
* [x] `IDesignTimeDbContextFactory` — deterministic migration tooling
* [x] `InitialCreate` migration — generated and verified
 
### Tests
 
* [x] `FeatureEvaluationContextTests` — constructor guards, equality, hash code
* [x] Build: 0 warnings, 0 errors
* [x] Tests: 8/8 passing
 
---
 
## 🚀 Phase 1 — MVP Completion (Current Focus)
 
### Validation
 
* [ ] Add `FluentValidation` to `CreateFlagRequest` and `UpdateFlagRequest` — closes KI-003
* [ ] Validate `StrategyConfig` structure at write time (not just at evaluation time)
* [ ] Enforce name uniqueness check at service layer before hitting the DB
 
### Testing
 
* [ ] Unit tests for `PercentageStrategy` (edge cases: 0%, 100%, boundary values)
* [ ] Unit tests for `RoleStrategy` (empty roles, case sensitivity, AND vs OR logic)
* [ ] Unit tests for `FeatureEvaluator` dispatch
* [ ] Integration tests for all six API endpoints
* [ ] Test environment-specific behavior (same flag name across environments)
 
### Error Handling
 
* [ ] Standardize API error responses (consistent error envelope)
* [ ] Add global exception middleware
* [ ] Handle `OperationCanceledException` gracefully (client disconnect)
 
### Developer Experience
 
* [ ] Improve Swagger endpoint descriptions and examples
* [ ] Add seed data for local dev (representative flags for each strategy type)
* [ ] Add structured logging for evaluation decisions
 
---
 
## 🧪 Phase 2 — Testing & Reliability
 
### Automated Testing
 
* [ ] Contract tests for evaluation endpoint
* [ ] Load tests for evaluation path (establish baseline latency)
* [ ] Chaos testing for database connectivity failures
 
### Error Handling Hardening
 
* [ ] Retry policy for transient database failures (Polly)
* [ ] Circuit breaker for downstream dependencies
 
---
 
## 🔐 Phase 3 — Authentication & Authorization
 
### Auth Integration
 
* [ ] Add authentication (JWT or OAuth2)
* [ ] Secure flag management endpoints (CRUD requires auth)
* [ ] Evaluation endpoint — decide: open or authenticated?
 
### Authorization
 
* [ ] Role-based access: separate read vs write permissions
* [ ] Environment-specific access (e.g., Production flags require elevated role)
* [ ] Audit trail — who changed what, when (basic change tracking)
 
---
 
## 📊 Phase 4 — Observability (Key Differentiator)
 
### Logging & Metrics
 
* [ ] Structured evaluation logs (flagName, userId, environment, result, strategy, latency)
* [ ] Prometheus metrics endpoint
* [ ] Track: evaluation counts, strategy usage, error rates
 
### Metrics Pipeline
 
* [ ] Event-based evaluation tracking (domain events)
* [ ] Prepare for external ingestion (message queue — RabbitMQ or Azure Service Bus)
 
### Debugging Tools
 
* [ ] `GET /api/evaluate/explain` — "Why was this flag ON/OFF for this user?"
* [ ] Return evaluation trace (which strategy ran, what config was used, what the result was)
 
---
 
## ⚙️ Phase 5 — Advanced Rollout Strategies
 
### New Strategies
 
* [ ] User targeting (allowlist by user ID)
* [ ] Time-based activation (active between two timestamps)
* [ ] Gradual rollout (percentage ramp over time)
 
### Strategy System Improvements
 
* [x] Dynamic strategy registration — DI-driven registry dispatch (already implemented)
* [ ] Strategy config validation framework — per-strategy JSON schema validation
* [ ] Strategy config versioning — handle config shape changes without breaking existing flags
 
---
 
## 🌐 Phase 6 — Multi-Environment & Scaling
 
### Environment Enhancements
 
* [ ] Promotion workflow (dev → staging → prod flag copy)
* [ ] Environment-specific overrides without duplicating flags
 
### Performance
 
* [ ] In-memory caching layer for flag reads (low TTL, high throughput)
* [ ] Redis caching option for distributed deployments
* [ ] Benchmark evaluation path — target < 5ms p99
 
### Scalability
 
* [ ] Stateless API design validation (confirm no server-side session state)
* [ ] Horizontal scaling verification
* [ ] Connection pool tuning for Postgres under load
 
---
 
## 🧠 Phase 7 — Developer Tooling & UX
 
### Internal Tooling
 
* [ ] Minimal management UI (flag list, toggle, environment filter)
* [ ] CLI tool for managing flags (`featureflags create`, `featureflags toggle`)
 
### API Enhancements
 
* [ ] Bulk evaluation endpoint (evaluate multiple flags in one request)
* [ ] API versioning (`/api/v1/`, `/api/v2/`)
* [ ] SDK package (NuGet) for .NET consumers
 
---
 
## 🧭 Phase 8 — Production Readiness
 
### DevOps
 
* [x] Local Docker Compose setup (`docker-compose.yml` at repo root)
* [ ] Production Dockerfile for the API
* [ ] CI/CD pipeline (GitHub Actions — build, test, migrate, deploy)
* [ ] Azure deployment: Azure Container Apps + Azure Database for PostgreSQL Flexible Server
 
### Data & Safety
 
* [ ] Automated backup strategy for Postgres
* [ ] Migration safety checklist (review before production deploys)
* [ ] Zero-downtime migration patterns
 
### Documentation
 
* [ ] Finalize API documentation (full Swagger examples)
* [ ] Architecture diagrams
* [ ] Onboarding guide (clone, configure, run in < 5 minutes)
 
---
 
## 🗺️ Long-Term Vision
 
* Turn this into a full **Observability + Experimentation Platform**
* A/B testing capabilities with statistical significance tracking
* Analytics pipeline integration
* Real-time evaluation dashboards
* Multi-tenant support (serve multiple applications from one instance)
 
---
 
## 📌 Current Focus
 
👉 **Phase 1 — MVP Completion**
 
Next recommended tasks:
 
1. Add `FluentValidation` to request DTOs — closes KI-003
2. Add integration tests for all six endpoints
3. Standardize API error responses
4. Add seed data for local development
5. Add structured logging for evaluation decisions
 
---
 
## 🧩 Notes for AI Assistants (Claude Context)
 
* Architecture follows clean separation: Controller → Service → Evaluator → Strategy → Repository
* Domain logic is intentionally strict (no public setters, explicit mutation methods only)
* Strategy pattern is central to extensibility — new strategies are one class + one DI registration
* Evaluation must remain deterministic, synchronous, and isolated from persistence
* All repository and service methods are async with `CancellationToken`
* `appsettings.Development.json` is intentionally committed — local Docker defaults only
* Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design` with `PrivateAssets=all`
* See `docs/current-state.md` for known issues before modifying existing layers
 
When suggesting changes:
 
* Do not break domain encapsulation
* Prefer composability over conditionals
* Keep evaluation logic isolated from persistence
* Do not add authentication, caching, or advanced strategies until their phase is reached