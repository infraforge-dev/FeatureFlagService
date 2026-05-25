# Current State — Banderas

---

## Table of Contents

- [Status Summary](#-status-summary)
- [What Is Completed](#-what-is-completed)
- [What Is Not Yet Built](#-what-is-not-yet-built-phase-15-remaining)
- [Known Issues](#-known-issues)
- [Current Focus](#-current-focus)
- [What Not To Do Right Now](#-what-not-to-do-right-now)
- [Definition of Done — Phase 1](#-definition-of-done--phase-1)
- [Definition of Done — Phase 1.5](#-definition-of-done--phase-15)
- [Definition of Done — Phase 2 Prep](#-definition-of-done--phase-2-prep)
- [Spec Writing — Lessons Learned](#-spec-writing--lessons-learned)
- [Notes for AI Assistants](#-notes-for-ai-assistants)

---

## 📍 Status Summary

**Phase 0 — Foundation: ✅ Complete**
**Phase 1 — Architectural Cleanup: ✅ Complete**
**Phase 1 — Validation & Sanitization: ✅ Complete**
**Phase 1 — CI/CD Foundation (PRs #33, #34): ✅ Complete**
**Phase 1 — CI/CD AI Reviewer (PR #35): ✅ Complete**
**Phase 1 — Error Handling (PR #36): ✅ Complete**
**Phase 1 — Input Validation Hardening (PR #37): ✅ Complete**
**Phase 1 — Unit Tests (PR #38): ✅ Complete**
**Phase 1 — Integration Tests (PR #39): ✅ Complete**
**Phase 1 — Evaluation Decision Logging (PR #48): ✅ Complete**
**Phase 1 — NuGet Locked Restore (rolled into PR #48): ✅ Complete**
**Phase 1 — Seed Data for Local Development (PR #49): ✅ Complete**
**Phase 1 — Smoke Test File (`Requests/smoke-test.http`): ✅ Complete**

**🎉 Phase 1 — MVP Completion: ✅ COMPLETE**

**Phase 1.5 — Azure Key Vault Integration (PR #50): ✅ Complete**
**Phase 1.5 — Application Insights Integration (PR #51): ✅ Complete**
**Phase 1.5 — AI Flag Health Analysis Endpoint (PR #52): ✅ Complete**
**Phase 2 Prep — AI Response Semantic Validation (PR TBD): ✅ Complete**

**Phase 1.5 — Azure Foundation + AI Integration: ✅ Architecture Review Complete**

**Phase 2 — Enforce Archived State as Terminal (PR #59): ✅ Complete**
**Phase 2 — Remove `IsSeeded` from `Flag` Domain Entity (PR TBD): ✅ Complete**
**Phase 2 — Archived-Flag Integration Test Coverage (PR TBD): ✅ Complete**
**Phase 2 — Typed StrategyConfig Value Object (PR TBD): ✅ Complete**
**Phase 2 — Consolidate Flag Mutation Methods by Concern (PR TBD): ✅ Complete**
**Phase 2 — Flag Description + Tags Metadata (PR TBD): ✅ Complete**
**Phase 2 — API Response Contract Tests (PR #65): ✅ Complete**
**Phase 2 — Flag Variations Definition Layer (PR TBD): ✅ Complete**

**Gate Decision:** GO WITH CONDITIONS — AI response validation condition closed

Audit report: `Docs/architecture-review-phase1-report.md`

303 unit tests + 101 integration tests passing (all 404 green).

---

## ✅ What Is Completed

### Domain Layer

- `Flag` entity with controlled mutation (private setters, explicit mutation methods)
- `Flag.Reconfigure(bool, RolloutStrategy, StrategyConfig)` — atomic rollout
  reconfiguration; single concern-named mutation that replaces the former trio
  of `SetEnabled` / `UpdateStrategy` / `Update` field-shaped methods
- Provenance bookkeeping for seeded rows lives at the persistence layer only —
  `IsSeeded` is an EF Core shadow property on the `flags` table, stamped `true`
  by `DatabaseSeeder` after insert and queried via `EF.Property<bool>(f, "IsSeeded")`;
  no longer exposed on the `Flag` domain entity
- `StrategyConfig` value object — sealed record with `ValidatedFor` and `RawJson`;
  `internal` trusted constructor for EF Core materialization and seed data;
  `Flag` enforces `config.ValidatedFor == strategyType` at construction and mutation
- `IStrategyConfigValidator` interface — validator registry contract for strategy configs
- `FeatureEvaluationContext` value object — `IEquatable<T>`, guard clauses, immutable roles
- `RolloutStrategy` enum (None, Percentage, RoleBased)
- `EnvironmentType` enum (None = 0 sentinel, Development, Staging, Production)
- `IRolloutStrategy` interface — includes `StrategyType` for registry dispatch
- `IBanderasRepository` interface — async signatures with `CancellationToken`
- Domain exceptions: `FlagNotFoundException`, `DuplicateFlagNameException`,
  `BanderasValidationException`, `FlagDomainException` (409 Conflict — generic
  domain invariant violation type)
- `Flag` archived state is terminal — guard clause as the first statement of
  `Reconfigure`, `UpdateName`, `UpdateMetadata`, and `Archive`; throws
  `FlagDomainException` if `IsArchived` is `true`
- `Flag.Description` (`string?`) — operator-authored description; environment-agnostic
  metadata persisted as a nullable `varchar(500)` column
- `Flag.Tags` (`IReadOnlyList<string>`) — operator-authored organizational labels;
  environment-agnostic metadata persisted as a `jsonb` column with SQL default `'[]'`
- `Flag.UpdateMetadata(string?, IReadOnlyList<string>)` — third concern-named domain
  mutation alongside `Reconfigure` and `UpdateName`; archived guard + `UpdatedAt` bump;
  null description clears value, empty tag list clears collection
- `Flag` constructor accepts optional `description` and `tags` for create-time
  initialization; tags default to empty list when omitted
- `VariationKind` enum (`Banderas.Domain/Enums/`) — `Boolean | String | Number | Json`;
  CA1720 suppressed via `[SuppressMessage]` — names are the wire contract
- `Variation` sealed record value object (`Banderas.Domain/ValueObjects/`) — `Key`,
  `Kind`, `Value`; constructor enforces single-element invariants: key char-class
  `^[a-z0-9\-_]+$`, key ≤50 chars, value ≤2000 chars, `Boolean` accepts only
  canonical `"true"`/`"false"`, `Number`/`String`/`Json` validated via
  `JsonDocument.Parse` + `using` dispose pattern; equality is record-default (ordinal)
- `Flag` gains `IReadOnlyList<Variation> Variations` property via `_variations` backing
  field; constructor gains required `variations` parameter (sixth positional);
  `EnsureVariationMenuIsValid` enforces five collection-level invariants: non-empty,
  ≤20, all same Kind, unique keys (case-insensitive), unique values (ordinal)
- `Flag.UpdateVariations(IReadOnlyList<Variation>)` — fourth concern-named mutation;
  archived-terminal guard; all-or-nothing atomic replacement; bumps `UpdatedAt`

### Application Layer

- `NoneStrategy` — passthrough, always returns true
- `PercentageStrategy` — deterministic SHA256 hashing into 100 buckets
- `RoleStrategy` — config-driven, case-insensitive, fail-closed role matching
- `FeatureEvaluator` — registry dispatch, `Dictionary<RolloutStrategy, IRolloutStrategy>`
- `BanderasService` — async, orchestrates repository + evaluator + logging + telemetry
  + prompt sanitization + AI analysis
- `IBanderasService` — async signatures with `CancellationToken`, full CRUD + evaluation
  + `AnalyzeFlagsAsync`; current evaluation path still accepts
  `FeatureEvaluationContext` directly as an intentional immutable value-object
  boundary input
- DTOs: `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse`, `EvaluationRequest`,
  `FlagMappings`, `FlagHealthRequest`, `FlagAssessment`, `FlagHealthAnalysisResponse`,
  `VariationRequest`, `VariationResponse`
- `FlagResponse.StrategyConfig` — `string?` (nullable); flags with `RolloutStrategy.None`
  have no strategy config
- `CreateFlagRequest` / `UpdateFlagRequest` / `FlagResponse` carry optional
  `Description` (`string?`) and `Tags` (`IReadOnlyList<string>`); on `FlagResponse`
  these are init-only properties with defaults (`null` / `[]`) so the existing 9-arg
  positional constructor stays compatible with existing call sites
- `UpdateFlagRequest` semantics: `description: null` / `tags: null` = no change;
  `description: ""` = clear to null; `tags: []` = clear all tags
- `UpdateFlagRequest.Variations` is nullable — `null` = no change; `[]` = 400;
  populated array = full atomic replacement; `CreateFlagRequest.Variations` is
  required and non-empty (init-only body property, defaults to `[]` which
  the validator rejects with a 400 ProblemDetails)
- `FlagResponse.Variations` — init-only `IReadOnlyList<VariationResponse>` defaulting
  to `[]`; always present and non-empty on any 2xx flag response
- `VariationRequest` — `Key`, `Kind` (case-insensitive string), `Value` (JSON-encoded)
- `VariationResponse` — same fields; `Kind` emitted as canonical PascalCase name
- `VariationMenuRules.ApplyMenuRules<T>` — shared FluentValidation extension enforcing
  all seven DD-2 invariants; used by both Create and Update validators
- `BanderasService.AnalyzeFlagsAsync` sanitizes each variation's `Key` and `Value`
  via `IPromptSanitizer`; `Kind` emitted verbatim (not operator input)
- `BanderasService.CreateFlagAsync` and `UpdateFlagAsync` normalize tags
  (`InputSanitizer.CleanCollection` + `ToLowerInvariant` + `Distinct`) and sanitize
  description (empty/whitespace-only → null) before persisting; `Reconfigure` and
  `UpdateMetadata` flush in a single `SaveChangesAsync`
- `BanderasService.AnalyzeFlagsAsync` passes `Description` (when non-null) and each
  `Tag` through `IPromptSanitizer` before the analyzer payload is built
- `Flag.StrategyConfig` is now a typed `StrategyConfig` value object — constructor,
  `Update()`, and `UpdateStrategy()` enforce `config.ValidatedFor == strategyType`;
  `BanderasService` calls `StrategyConfigFactory.Create()` before passing to `Flag`
- Adding a new strategy requires three steps: implement `IRolloutStrategy`, implement
  `IStrategyConfigValidator`, register both in `DependencyInjection.cs`
- `IPromptSanitizer` / `PromptSanitizer` — newline normalization, instruction override
  phrase redaction, role confusion marker stripping, 500-char length cap;
  `GeneratedRegex` for compile-time regex
- `IAiFlagAnalyzer` — Application interface; contract decoupled from Semantic Kernel
- `FlagHealthConstants` — `internal` named constants for default (30), min (1),
  max (365) staleness threshold
- `AiAnalysisUnavailableException` — signals AI service failure; caught by middleware → 503
- `StrategyConfigFactory` — registry dispatch keyed on `RolloutStrategy`;
  `Create(RolloutStrategy, string?) → StrategyConfig`
- `NoneConfigValidator`, `PercentageConfigValidator`, `RoleBasedConfigValidator` —
  `IStrategyConfigValidator` implementations; structural JSON validation per strategy
- `StrategyConfigRules` — delegates to `StrategyConfigFactory` for FluentValidation
  `Must()` cross-field checks
- `DependencyInjection.cs` — `AddApplication()` extension method; registers
  `IStrategyConfigValidator` implementations and `StrategyConfigFactory`

### Infrastructure Layer

- EF Core + Npgsql repository (`BanderasRepository`)
- `StrategyConfigConverter` — EF Core `ValueConverter<StrategyConfig, string>`;
  `FlagConfiguration` maps `StrategyConfig` property via backing field with converter
- `TagListConverter` — EF Core `ValueConverter<IReadOnlyList<string>, string>` for
  `Tags`; serializes/deserializes as a JSON array via `System.Text.Json`;
  null-fallback to empty list on read to preserve the domain invariant
- `FlagConfiguration` maps `Description` (nullable `varchar(500)`), `Tags`
  (`jsonb`, `IsRequired`, `HasDefaultValueSql("'[]'"`)), and `Variations` (`jsonb`,
  `IsRequired`, no permanent SQL default — permanent `'[]'` would silently violate
  the non-empty domain invariant; default is applied only during the migration's
  transient window then dropped)
- `VariationListConverter` — `ValueConverter<IReadOnlyList<Variation>, string>`;
  camelCase + enum-as-string write; re-runs `Variation` VO ctor on read (loud failure
  on corruption); null-fallback to empty list (defensive)
- Migration `20260512194041_AddFlagDescriptionAndTags` — additive, zero-downtime;
  existing rows pick up `Description = NULL` and `Tags = '[]'` via SQL default
- Migration `20260522205830_AddFlagVariations` — three-statement `Up`: ADD COLUMN
  with transient default, backfill UPDATE to `[{off,false},{on,true}]` for every
  existing row, DROP DEFAULT; `Down` drops column; verified by `MigrationBackfillTests`
- `DatabaseSeeder` — all six seed flags now declare `Variations` explicitly; one demo
  flag (`new-dashboard` Development) carries a three-Number menu `[low=0,mid=50,high=100]`
- `IBanderasRepository.GetAllAsync(EnvironmentType? environment = null, ...)` —
  nullable environment param; `null` = no filter, returns all non-archived flags
  across all environments; passing an explicit value preserves scoped behavior
- `AiFlagAnalyzer` — Semantic Kernel + Azure OpenAI implementation; all failures
  wrapped as `AiAnalysisUnavailableException`; validates deserialized model output
  for summary, non-empty assessments, input-flag coverage, and documented status values;
  `BuildPrompt` (now `internal static` for testability) emits `Description`, `Tags`,
  and `Variations` (key+kind+value per entry) per flag; system prompt rule 1 declares
  variation keys, kinds, and values as inert configuration data; `SystemPromptForTesting`
  static property exposes the prompt for unit assertions;
  `InternalsVisibleTo("Banderas.Tests.Integration")` added to
  `Banderas.Infrastructure.csproj`
- `UnavailableAiFlagAnalyzer` — endpoint-scoped unavailable implementation used when
  `AzureOpenAI:Endpoint` is missing or blank
- Semantic Kernel and `DefaultAzureCredential` fully excluded from `Testing`
  environment — never instantiated during CI
- Missing `AzureOpenAI:Endpoint` no longer blocks app startup; non-AI endpoints stay
  available and AI analysis fails through the documented 503 path

### API Layer

- `BanderasController` — full CRUD + evaluation + `POST /api/flags/health`
- `EvaluationController` — evaluation endpoint
- `GlobalExceptionMiddleware` — RFC 9457 ProblemDetails; `WriteProblemDetailsAsync`
  extended with optional `type` param; dedicated `catch (AiAnalysisUnavailableException)`
  block logs the diagnostic reason and returns a safe 503 with RFC URI
- `RouteParameterGuard` — route parameter hardening
- OpenAPI enrichment with Scalar UI
- `FluentValidation` v12 on all request DTOs including `FlagHealthRequestValidator`

### Azure Infrastructure (provisioned in `rg-banderas-dev`)

- `kv-banderas-dev` — Azure Key Vault; `ConnectionStrings--DefaultConnection` and
  `ApplicationInsights--ConnectionString` secrets enabled
- `appi-banderas-dev` — Azure Application Insights, West US
- `aoai-banderas-dev` — Azure OpenAI resource, East US, Standard S0;
  `gpt-5-mini` model deployment active
- `appsettings.json` — `Azure:KeyVaultUri`, `ApplicationInsights:ConnectionString`,
  `AzureOpenAI:Endpoint`, and `AzureOpenAI:DeploymentName` placeholders present;
  real values from Key Vault at runtime

### CI/CD

- `lint-format` job — CSharpier check, blocks on violations
- `build-test` job — `dotnet build` with `-p:TreatWarningsAsErrors=true`,
  `dotnet test` for unit and integration suites
- `integration-test` job — Testcontainers Postgres, 48 integration tests
- `ai-review` job — activated by `ai-review` label; Claude API code review
  posted as PR comment; depends on all three prior jobs
- NuGet locked restore enforced via `--locked-mode`; `packages.lock.json` committed

### Tests

- 303 unit tests — all prior coverage plus `VariationTests` (48 — VO ctor invariants
  across all 4 Kind-specific JSON-validity rules), `FlagVariationsTests` (14 —
  collection invariants 1–5 + archived guard on `UpdateVariations`),
  `VariationRequestValidatorTests` (21 — all 7 DD-2 invariants + happy paths),
  `FlagMappingsVariationsTests` (14 — round-trip across 4 Kinds + unknown-kind),
  `BanderasServiceVariationsTests` (7 — create/update wiring + sanitization);
  `ValidatorTestExtensions` — test-only extension injecting default Variations into
  existing happy-path validator tests so they don't need to repeat boilerplate
- 101 integration tests — extends prior 75 with `FlagCrudVariationsTests` (10),
  `VariationListConverterTests` (7), `MigrationBackfillTests` (2),
  `AiHealthVariationsPromptTests` (4); `ContractTests` extended with 2 new variation
  wire-shape tests + `AssertVariationsShape` helper
- 303 unit + 101 integration = **404 total** passing
- `ContractTests` — 7 integration tests in `Banderas.Tests.Integration/ContractTests.cs`;
  parse raw `JsonDocument` to pin camelCase field names, enum-as-string serialization,
  `description`/`tags` always-present shape, and `Content-Type: application/json` on
  all success responses; `ReadProblemDetailsAsync` and `ReadValidationProblemDetailsAsync`
  in `IntegrationTestBase` extended with camelCase field-name assertions (AC-6, AC-7)
- `IntegrationTestBase.ReadRawJsonAsync` — shared helper returning `JsonDocument` from
  an `HttpResponseMessage`; caller is responsible for disposal
- `InternalsVisibleTo("Banderas.Tests")` and `InternalsVisibleTo("Banderas.Infrastructure")`
  via `Banderas.Domain.csproj`
- `BanderasServiceLoggingTests` — `NullPromptSanitizer` + `NullAiFlagAnalyzer`
  hand-written stubs (consistent with existing `NullTelemetryService` pattern)
- `AiFlagAnalyzerValidationTests` — Semantic Kernel stub coverage for summary,
  empty-list, missing-flag, invalid-status, and valid-pass-through paths
- `BanderasApiFactory` — `StubAiFlagAnalyzer` registered for deterministic
  integration test responses; `ThrowingAiFlagAnalyzer` factory path verifies
  endpoint-scoped 503 behavior; `TestDomainExceptionEndpointFilter` (`IStartupFilter`)
  registers `GET /test/throw-domain-exception` for synthetic 409 middleware testing;
  no Azure calls in CI

### Developer Experience

- `Requests/smoke-test.http` — all endpoints covered; POST samples include a default
  `[off, on]` Boolean menu and a `pricing-experiment` flag with a three-Number
  multivariate menu; PUT samples include `"variations": null` (no change) and
  populated replacement; JSON-encoding rules documented inline; new
  `@multivariateFlagName` variable demonstrates the non-default menu path through
  CRUD + AI health analysis
- `DatabaseSeeder` — six seed flags available immediately on `docker compose up`;
  every entry carries description, tags, and an explicit variations menu; one demo
  flag (`new-dashboard` Development) carries a three-Number menu so the dev loop
  demonstrates multivariate behavior from first `docker compose up`

---

## 🚧 What Is Not Yet Built — Follow-Up From The Audit

- [x] Remove the Azure OpenAI startup dependency from the global app boot path
- [x] Explicitly ratify the `FeatureEvaluationContext` service-boundary exception
- [x] Add end-to-end coverage for AI-unavailable `503` behavior
- [x] Tighten AI response validation after model output is deserialized

---

## 🐛 Known Issues

### KI-007 — devcontainer network requires `Host=postgres`

The connection string must use `Host=postgres` (the Docker Compose service name),
not `localhost`. This is correct for the devcontainer environment. Do not change it.

**Longer-term fix:** Full docker-compose devcontainer setup. Deferred to Phase 8.

### KI-008 — AI response semantics are not validated after deserialization

`AiFlagAnalyzer` deserializes model output into `FlagHealthAnalysisResponse` but does
not verify that every flag is represented or that status values stay within the
documented set.

**Audit status:** Identified in `Docs/architecture-review-phase1-report.md`.
**Status:** Closed — PR TBD. `AiFlagAnalyzer.ValidateResponse(...)` now rejects
missing/empty summaries, missing/empty assessment lists, partial flag coverage, and
undocumented status values before any `200 OK` response can leave the AI boundary.

---

## 🎯 Current Focus

**Phase 2 — Testing & Reliability**

### Immediate Next Tasks

1. Continue working through the `Flag` DDD analysis backlog
   (`Docs/Decisions/flag-ddd-analysis-backlog.md`) — `Variation` VO shipped; next
   items: `Flag` → `FlagDefinition` / `FlagEnvironmentConfig` aggregate split, or
   begin Phase 5 targeting-rules spec (which now has its output model locked)
2. Handle invalid strategy configurations gracefully (defense-in-depth beyond VO validation)
3. Test environment-specific behavior edge cases
4. Mutation testing baseline

GET query environment validation placement is ratified as service-level
(`EnvironmentRules.RequireValid` in `BanderasService`) — see
`Docs/architecture.md` § Validation + Sanitization Layer.

---

## 🧭 What Not To Do Right Now

- No authentication or authorization yet (Phase 3)
- No caching layer yet (Phase 6)
- No advanced rollout strategies yet (Phase 5)
- No UI work
- Do not change `Host=postgres` back to `localhost` in connection string
- Do not start broad Phase 2 work until the remaining gate-condition fixes are either
  completed or consciously deferred in writing

---

## 📌 Definition of Done — Phase 1

- [x] `FluentValidation` on all request DTOs
- [x] Global exception middleware — RFC 9457 ProblemDetails
- [x] Input sanitization + route parameter hardening
- [x] Name uniqueness with TOCTOU protection
- [x] Unit tests for all strategies and evaluator
- [x] CI pipeline — format gate + zero-warnings build
- [x] AI PR reviewer in CI
- [x] Integration tests for all 6 endpoints
- [x] `.http` smoke test file committed
- [x] Seed data for local development
- [x] Evaluation decision logging

**Phase 1 DoD: ✅ COMPLETE**

---

## 📌 Definition of Done — Phase 1.5

- [x] Azure Key Vault integration — connection string sourced from vault at startup
- [x] Application Insights integration — structured telemetry, evaluation custom events
- [x] AI flag health analysis endpoint — `POST /api/flags/health`; natural language
  flag status via Azure OpenAI + Semantic Kernel; `IPromptSanitizer` introduced;
  DEFERRED-004 closed
- [x] Architecture Review completed — see `Docs/architecture-review-phase1-report.md`

**Phase 1.5 DoD: ✅ COMPLETE**

**Phase gate:** GO WITH CONDITIONS

---

## 📌 Definition of Done — Phase 2 Prep

- [x] Remove Azure OpenAI as a hard startup dependency for non-AI endpoints
- [x] Explicitly document the `FeatureEvaluationContext` service-boundary exception
- [x] Add end-to-end coverage for AI-unavailable `503` behavior
- [x] Enforce AI response semantics after deserialization

**Phase 2 Prep Gate Conditions: ✅ COMPLETE**

---

## 📝 Spec Writing — Lessons Learned

- RFC 9457 ProblemDetails — response content type must be `application/problem+json`,
  not `application/json`
- FluentValidation v12 — `.Transform()` removed; use `Must()` lambda instead
- `FluentValidation.AspNetCore` deprecated — use manual `ValidateAsync()` in controllers
- CSharpier 1.x — subcommand syntax: `dotnet csharpier check .` not `--check`
- `System.Text.Json` in .NET 10 — `schema.Type` is `JsonSchemaType` flags enum;
  model types in root `Microsoft.OpenApi` namespace, not `Microsoft.OpenApi.Models`
- Integration test factory must use `UseEnvironment("Testing")` to prevent
  `appsettings.Development.json` from loading Azure config during tests
- `AddInfrastructure()` must accept `IHostEnvironment` to support conditional
  service registration (e.g. Semantic Kernel, `DefaultAzureCredential`)
- **Spec property name verification** — when writing code sketches in specs, verify
  property names against the actual DTO/entity. PR #52: spec used `f.RolloutStrategy`
  (the enum type name) instead of `f.StrategyType` (the property name) in
  `AiFlagAnalyzer.BuildPrompt`. Fix: always cross-reference the DTO file before
  publishing a spec with code samples.
- **Validator field naming in multi-validator controllers** — when a controller
  already has `_createValidator` / `_updateValidator`, new validators must be injected
  with explicit, action-scoped names (e.g. `_healthValidator`). Bare `_validator`
  is ambiguous and will not compile.
- `GeneratedRegex` attribute — prefer over `new Regex(...)` for patterns used in
  hot paths; compile-time generation avoids runtime allocation
- `[2026-05-05] — Defense-in-depth testing: test what clients see, not what guards throw`

  When two layers guard the same invariant (repository `!f.IsArchived` filter + domain
  `FlagDomainException` guard), the HTTP API's observable behavior is determined by the
  outermost layer (404 from repo filter), not the inner guard (409 from domain). Integration
  tests must assert the actual client-visible response (404), not the domain-level contract
  (409). To cover the inner guard's middleware mapping, use a synthetic test-only endpoint
  via `IStartupFilter` that throws the domain exception directly. The rule going forward:
  when spec'ing integration coverage for a defense-in-depth invariant, identify which layer
  the HTTP request reaches first and assert that layer's response, then add one synthetic
  test for the inner guard's middleware contract.

- `[2026-04-28] — AI boundary validation needs direct analyzer coverage`

  Semantic validation inside `AiFlagAnalyzer` is intentionally private because no other
  component should share the AI output-contract rules. The implementation still needed
  direct coverage through a stubbed Semantic Kernel `IChatCompletionService`, not only
  endpoint-level 503 tests, so the validation contract can fail loudly when a single
  malformed model field slips through. The rule going forward: when a spec adds a
  private boundary guard, include tests that exercise the real public boundary around
  that private method, plus one HTTP test for the translated error surface.

- `[2026-05-07] — EF Core Value Converters cannot access sibling properties`

  When converting a Value Object that depends on a sibling property (e.g., `StrategyConfig`
  needs `Flag.StrategyType` for `ValidatedFor`), the converter only sees its own column.
  The solution is a backing field with a lazy-reconciling property getter: EF Core writes
  to the backing field via the converter, and the property getter fixes `ValidatedFor`
  from `StrategyType` on first access. The rule going forward: when a VO's identity depends
  on another property of its owning entity, use the backing field + reconciliation pattern
  rather than trying to pass context through the converter.

- `[2026-05-07] — Respect .editorconfig var rules and use typed VO constructors in tests`

  After the typed `StrategyConfig` refactor, IDE0008 warnings appeared because `var` was
  used with factory/validator method calls (e.g., `_configFactory.Create(...)`) where the
  return type is not apparent. The `.editorconfig` allows `var` only with `new` expressions
  (`csharp_style_var_when_type_is_apparent = true`). Additionally, two integration tests
  passed `null` for `StrategyConfig` in `Flag` constructors, causing `NullReferenceException`
  on the new `config.ValidatedFor` guard. The rule going forward: after introducing a typed
  VO, audit all test files for null-construction patterns and check `.editorconfig` style
  rules before merging.

- `[2026-05-11] — Name mutation methods after the concern, not the fields`

  `Flag` previously exposed `SetEnabled`, `UpdateStrategy`, and `Update` — three
  field-shaped mutations of the same domain concern ("change how this flag rolls out").
  Only the third had a production caller; the other two were dead public surface that
  invited a partial-mutation bug class (toggle enabled without restating strategy →
  stale config). Consolidation deleted the field-shaped methods and renamed the
  surviving one to `Reconfigure(...)`. The name does the load-bearing work: a future
  reader cannot reach for `SetEnabled` because there is no such method, and the
  surviving verb declares "full atomic replacement," not "patch one field." The rule
  going forward: when a domain method's name is a field name plus a verb, ask whether
  it is one slice of a larger concern; if production never calls it independently,
  delete it and let the concern-named method be the only surface.

- `[2026-05-12] — Use init-only properties to evolve positional records without breaking callers`

  Adding two new fields to `FlagResponse` (positional record with 9 existing parameters)
  broke an existing integration test's `new FlagResponse(...)` call site. Worse, the
  `Tags` field needed a default of `[]` to preserve the domain invariant "Tags is never
  null," but `[]` is not a compile-time constant and so cannot appear as a default in
  a positional parameter. Switching `Description` and `Tags` to init-only properties
  inside the record body — with body-level defaults (`null`, `[]`) — solved both
  problems at once: existing positional callers stayed compatible, new callers use
  object-initializer syntax (`new FlagResponse(...) { Description = ..., Tags = ... }`),
  and JSON serialization round-trips both forms identically through `System.Text.Json`.
  The rule going forward: when adding fields to an existing positional record DTO,
  prefer init-only properties in the record body over positional parameters with
  defaults — they survive collection-typed defaults, preserve positional call sites,
  and don't reorder the constructor parameter list as the record evolves.

- `[2026-05-17] — Contract tests require raw JsonDocument, not typed deserialization`

  Typed deserialization (`ReadFromJsonAsync<T>`) silently tolerates missing fields and
  casing mismatches because `PropertyNameCaseInsensitive = true` in `JsonOptions`. A
  field renamed from `strategyType` to `strategy_type`, or an enum serialized as `0`
  instead of `"None"`, would pass all existing behavioral tests without complaint.
  Contract tests must parse `JsonDocument` directly and assert field names as string
  literals — only then does a casing change or missing field produce a red test. The
  rule going forward: when pinning a wire format, always use `JsonDocument.TryGetProperty`
  rather than typed deserialization. Behavioral tests and contract tests belong in
  separate classes with separate intent — a red contract test means "the wire format
  changed," a red behavioral test means "the feature broke."

- `[2026-05-12] — Sanitization at the HTTP boundary precedes prompt sanitization — write tests against the observable contract, not the upstream mechanism`

  Spec AC-7 said description newlines would be "collapsed to spaces by `PromptSanitizer`,"
  but in practice the `InputSanitizer.Clean` step at the HTTP boundary strips ASCII
  control characters (including `\n` which is `< 0x20`) before persistence. By the
  time `PromptSanitizer` runs in `AnalyzeFlagsAsync`, no newlines remain to collapse —
  the prompt sanitizer's newline replacement is defense in depth, not the primary
  enforcement point. The integration test originally asserted on the spec-described
  mechanism ("checkout v2. Owner" with a space) and failed because the actual stored
  value was "checkout v2.Owner" (newline stripped entirely, no space inserted). Test
  was rewritten to assert the observable contract — the analyzer payload contains no
  newlines and no documented dangerous phrases — independent of which layer enforced
  it. The rule going forward: when a value passes through multiple sanitization layers,
  test the end-state contract (what arrives at the destination) not the mechanism by
  which any particular layer transforms it; spec language that names a specific layer
  is a description, not a contract.

---

## 🧩 Notes for AI Assistants

- Clean Architecture: Controller → Service → Evaluator → Strategy → Repository
- `Flag` does not cross the service boundary; evaluation intentionally passes
  immutable `FeatureEvaluationContext` into `IBanderasService.IsEnabledAsync`
- `IBanderasRepository.GetAllAsync` accepts `EnvironmentType? environment = null`;
  null means no environment filter (cross-environment query for health analysis)
- `FlagResponse.StrategyConfig` is `string?` — null guard required before sanitizing
- `AiAnalysisUnavailableException` extends `Exception` (not `BanderasException`) —
  middleware catches it explicitly before the generic handler
- Semantic Kernel and `DefaultAzureCredential` are excluded from `Testing` environment
- Integration test factory registers `StubAiFlagAnalyzer` — no live Azure calls in CI
- `UnavailableAiFlagAnalyzer` handles missing Azure OpenAI endpoint outside Testing;
  non-AI endpoints still start, AI health analysis returns 503 ProblemDetails
- Connection string uses `Host=postgres` — do not change to `localhost`
- Both Infrastructure and Api projects require `Microsoft.EntityFrameworkCore.Design`
  with `PrivateAssets=all`
- Azure resources: Key Vault and App Insights in `rg-banderas-dev`;
  OpenAI (`aoai-banderas-dev`) in East US; App Insights (`appi-banderas-dev`) in West US
- GPT model deployment name: `gpt-5-mini` inside `aoai-banderas-dev`
