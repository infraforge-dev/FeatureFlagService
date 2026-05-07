# Specification: Typed StrategyConfig Value Object

**Document:** Docs/Decisions/refactor-typed-strategy-config/spec.md
**Status:** Draft
**Branch:** `refactor/typed-strategy-config`
**PR:** TBD
**Phase:** Phase 2 — Testing & Reliability
**Depends on:** None
**Author:** Jose
**Date:** 2026-05-06

---

## Table of Contents

- [User Story](#user-story)
- [Background and Goals](#background-and-goals)
- [Design Decisions](#design-decisions)
- [Architecture Overview](#architecture-overview)
- [Scope](#scope)
- [Acceptance Criteria](#acceptance-criteria)
- [File Layout](#file-layout)
- [Technical Notes](#technical-notes)
- [Out of Scope](#out-of-scope)
- [Learning Opportunities](#learning-opportunities)
- [DX / Tooling Idea](#dx--tooling-idea)
- [Definition of Done](#definition-of-done)

---

## User Story

As a developer extending Banderas with new rollout strategies, I want `StrategyConfig`
to be a typed Value Object per strategy so that invalid configurations are impossible
to represent in the domain model and I get compile-time safety instead of runtime JSON
parsing failures.

---

## Background and Goals

### Problem

`StrategyConfig` is a raw `string` on the `Flag` entity. This creates three issues:

1. **No compile-time safety** — `Flag` happily accepts `{"roles":["Admin"]}` when
   `StrategyType` is `Percentage`. The mismatch is only caught at the HTTP boundary by
   FluentValidation, not by the domain itself. Any future non-HTTP input surface (CLI,
   seed data, tests) can silently create an inconsistent `Flag`.

2. **Duplicate parsing** — Each strategy (`PercentageStrategy`, `RoleStrategy`) privately
   defines its own config record (`PercentageConfig`, `RoleConfig`) and deserializes at
   evaluation time. The same JSON is parsed on every single evaluation call, and the
   config shape knowledge is locked inside each strategy class.

3. **Domain model gap** — The DDD analysis backlog explicitly calls out: "invalid config
   should be caught at Value Object construction, not at runtime." Today, `Flag` is an
   aggregate root that cannot enforce its own strategy/config consistency invariant.

### Goal

Move config parsing to Value Object construction so that a `Flag` with a mismatched
strategy type and config shape cannot exist. This closes the "make illegal states
unrepresentable" gap identified in the DDD backlog.

### Non-goal

This spec does not restructure `Flag` into definition vs. environment config (that is
a later backlog item). It only types the existing `StrategyConfig` property.

---

## Design Decisions

### Decision 1: Value Objects live in `Banderas.Domain/ValueObjects/`

**Options considered:**

| Option | Location | Verdict |
|--------|----------|---------|
| A | `Banderas.Domain/ValueObjects/` | **Chosen** |
| B | `Banderas.Application/Strategies/` | Rejected — domain can't reference them; `Flag` can't enforce consistency |
| C | Shared project | Rejected — over-engineering for this codebase |

**Rationale:** The entire point of this refactor is to let `Flag` enforce config/strategy
consistency at construction. The Value Object must be visible to the domain entity. This
is consistent with `FeatureEvaluationContext` already living in
`Banderas.Domain/ValueObjects/`.

---

### Decision 2: Single `StrategyConfig` record with validator registry (Option D)

**Options considered:**

| Option | Shape | Verdict |
|--------|-------|---------|
| A | Abstract base record with per-strategy subtypes | Rejected — Phase 5 (multivariate, targeting rules) will reshape the hierarchy; creates churn |
| B | Keep `string`, add parsing factory | Rejected — doesn't close the "illegal states" gap |
| C | `JsonDocument` wrapper | Rejected — not semantically typed |
| D | Single sealed record + `IStrategyConfigValidator` registry | **Chosen** |
| E | `IStrategyConfig` interface per strategy | Rejected — similar churn risk as Option A |
| F | Keep `string`, add `IsConfigValidated` stamp | Rejected — convention, not type guarantee |

**Rationale:** Option D was chosen for extensibility and Phase 5 forward-compatibility:

- **Mirrors the existing pattern** — `IRolloutStrategy` implementations are registered
  in DI and dispatched via `Dictionary<RolloutStrategy, IRolloutStrategy>`. A parallel
  `Dictionary<RolloutStrategy, IStrategyConfigValidator>` registry keeps the same shape.
- **`Flag` enforcement** — The entity checks `config.ValidatedFor == strategyType` and
  rejects mismatches via `FlagDomainException`. This is a type-system guarantee, not a
  downstream validator.
- **Open/closed** — Adding a new strategy requires: implement `IRolloutStrategy`,
  implement `IStrategyConfigValidator`, register both in DI. Zero changes to `Flag`,
  `StrategyConfig`, `StrategyConfigFactory`, `FeatureEvaluator`, or the EF Core converter.
- **Phase 5 safe** — `RawJson` stays as jsonb in Postgres. When multivariate variations
  and attribute-based targeting arrive, config shapes evolve inside the validators — the
  VO structure and `Flag` guard are unchanged.

**The `StrategyConfig` record:**

```csharp
public sealed record StrategyConfig
{
    public RolloutStrategy ValidatedFor { get; }
    public string RawJson { get; }

    // Public factory — only way to create a validated instance
    public static StrategyConfig Create(RolloutStrategy strategy, string rawJson)
        => new(strategy, rawJson);

    // Internal trusted constructor — EF Core materialization and seed data only
    internal StrategyConfig(RolloutStrategy validatedFor, string rawJson)
    {
        ValidatedFor = validatedFor;
        RawJson = rawJson ?? throw new ArgumentNullException(nameof(rawJson));
    }
}
```

Note: `StrategyConfig.Create()` is used by `StrategyConfigFactory` after validation
passes. Direct callers outside the factory should not exist in production code.

---

### Decision 3: EF Core Value Converter for persistence

**Options considered:**

| Option | Approach | Verdict |
|--------|----------|---------|
| A | Value Converter (`StrategyConfig ↔ string`) | **Chosen** |
| B | Owned Entity mapping | Rejected — overkill; jsonb column should stay as-is |
| C | Two separate columns | Rejected — `ValidatedFor` is always `Flag.StrategyType`; redundant |

**Rationale:** The Postgres column stays `jsonb`, no migration needed. On write, the
converter serializes `StrategyConfig.RawJson`. On read, the converter reconstructs
`StrategyConfig` using the trusted `internal` constructor — the data was validated on
the way in. `ValidatedFor` is derived from `Flag.StrategyType` during materialization.

---

## Architecture Overview

```text
Domain Layer (Banderas.Domain)
├── ValueObjects/StrategyConfig.cs          — sealed record(RolloutStrategy ValidatedFor, string RawJson)
│                                              Internal trusted constructor for DB materialization
├── Interfaces/IStrategyConfigValidator.cs  — validates raw JSON for a specific RolloutStrategy
├── Entities/Flag.cs                        — StrategyConfig property becomes typed VO;
│                                              constructor + Update() + UpdateStrategy() enforce
│                                              config.ValidatedFor == strategyType

Application Layer (Banderas.Application)
├── Validators/PercentageConfigValidator.cs — implements IStrategyConfigValidator
├── Validators/RoleBasedConfigValidator.cs  — implements IStrategyConfigValidator
├── Validators/NoneConfigValidator.cs       — implements IStrategyConfigValidator
├── Validators/StrategyConfigFactory.cs     — registry of validators, keyed by RolloutStrategy;
│                                              single entry point: Create(RolloutStrategy, string?) → StrategyConfig
├── Validators/StrategyConfigRules.cs       — delegates to factory for FluentValidation Must() checks
├── Services/BanderasService.cs             — calls StrategyConfigFactory.Create() before
│                                              passing config to Flag constructor/Update()
├── Strategies/PercentageStrategy.cs        — reads config from StrategyConfig.RawJson
├── Strategies/RoleStrategy.cs              — reads config from StrategyConfig.RawJson
├── DTOs/ (all request/response DTOs)       — UNCHANGED: still string? at API boundary

Infrastructure Layer (Banderas.Infrastructure)
├── Persistence/FlagConfiguration.cs        — EF Core Value Converter for StrategyConfig ↔ string
├── Persistence/StrategyConfigConverter.cs  — ValueConverter<StrategyConfig, string>
├── Seeding/DatabaseSeeder.cs               — SeedRecord.ToFlag() uses trusted constructor
```

**Key boundaries preserved:**

- DTOs stay as `string?` at the HTTP boundary — no API contract change
- `Flag` entity never crosses the service boundary (existing rule)
- jsonb column unchanged — no EF Core migration needed
- Strategies still fail-closed on malformed config (defense in depth)

**What does NOT change:**

- `IRolloutStrategy` interface
- `FeatureEvaluator` dispatch
- API request/response shapes
- Database schema

---

## Scope

### New Files

| File | Layer | Purpose |
|------|-------|---------|
| `Banderas.Domain/ValueObjects/StrategyConfig.cs` | Domain | Sealed record with `ValidatedFor` and `RawJson`; internal trusted constructor |
| `Banderas.Domain/Interfaces/IStrategyConfigValidator.cs` | Domain | Interface: `RolloutStrategy StrategyType` + `StrategyConfig Validate(string? rawJson)` |
| `Banderas.Application/Validators/StrategyConfigFactory.cs` | Application | Registry dispatch: `Create(RolloutStrategy, string?) → StrategyConfig` |
| `Banderas.Application/Validators/NoneConfigValidator.cs` | Application | `IStrategyConfigValidator` for `RolloutStrategy.None` |
| `Banderas.Application/Validators/PercentageConfigValidator.cs` | Application | `IStrategyConfigValidator` for `RolloutStrategy.Percentage` |
| `Banderas.Application/Validators/RoleBasedConfigValidator.cs` | Application | `IStrategyConfigValidator` for `RolloutStrategy.RoleBased` |
| `Banderas.Infrastructure/Persistence/StrategyConfigConverter.cs` | Infrastructure | EF Core `ValueConverter<StrategyConfig, string>` |
| `Banderas.Tests/Domain/ValueObjects/StrategyConfigTests.cs` | Tests | VO construction, guard clauses, equality |
| `Banderas.Tests/Validators/StrategyConfigFactoryTests.cs` | Tests | Factory registry dispatch, mismatch rejection |
| `Banderas.Tests/Validators/NoneConfigValidatorTests.cs` | Tests | None-specific validation |
| `Banderas.Tests/Validators/PercentageConfigValidatorTests.cs` | Tests | Percentage-specific validation |
| `Banderas.Tests/Validators/RoleBasedConfigValidatorTests.cs` | Tests | RoleBased-specific validation |

### Modified Files

| File | Layer | Change |
|------|-------|--------|
| `Banderas.Domain/Entities/Flag.cs` | Domain | `StrategyConfig` type: `string` → `StrategyConfig`; constructor and mutation methods enforce `config.ValidatedFor == strategyType` |
| `Banderas.Application/Strategies/PercentageStrategy.cs` | Application | Deserialize from `flag.StrategyConfig.RawJson` |
| `Banderas.Application/Strategies/RoleStrategy.cs` | Application | Deserialize from `flag.StrategyConfig.RawJson` |
| `Banderas.Application/Validators/StrategyConfigRules.cs` | Application | Delegate to `IStrategyConfigValidator` implementations |
| `Banderas.Application/Validators/CreateFlagRequestValidator.cs` | Application | StrategyConfig cross-field rules delegate through `StrategyConfigRules` to factory |
| `Banderas.Application/Validators/UpdateFlagRequestValidator.cs` | Application | Same as above |
| `Banderas.Application/Services/BanderasService.cs` | Application | Call `StrategyConfigFactory.Create()` before passing config to `Flag` |
| `Banderas.Application/DependencyInjection.cs` | Application | Register `IStrategyConfigValidator` implementations and `StrategyConfigFactory` |
| `Banderas.Application/DTOs/FlagMappings.cs` | Application | Map `flag.StrategyConfig.RawJson` → `FlagResponse.StrategyConfig` |
| `Banderas.Infrastructure/Persistence/FlagConfiguration.cs` | Infrastructure | Add Value Converter for `StrategyConfig` property |
| `Banderas.Infrastructure/Seeding/DatabaseSeeder.cs` | Infrastructure | Use trusted constructor for seed data |
| `Banderas.Tests/Helpers/FlagBuilder.cs` | Tests | Construct `StrategyConfig` VO instead of raw string |
| `Banderas.Tests/Strategies/PercentageStrategyTests.cs` | Tests | Update flag construction |
| `Banderas.Tests/Strategies/RoleStrategyTests.cs` | Tests | Update flag construction |
| `Banderas.Tests/Strategies/NoneStrategyTests.cs` | Tests | Update flag construction |
| `Banderas.Tests/Evaluation/FeatureEvaluatorTests.cs` | Tests | Update flag construction |
| `Banderas.Tests/Validators/CreateFlagRequestValidatorTests.cs` | Tests | Verify delegation to factory |
| `Banderas.Tests/Validators/UpdateFlagRequestValidatorTests.cs` | Tests | Verify delegation to factory |
| `Banderas.Tests/Domain/FlagArchivedInvariantTests.cs` | Tests | Update flag construction |

### Unchanged

| File | Why |
|------|-----|
| `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse` DTOs | API contract unchanged — `string?` at HTTP boundary |
| `IRolloutStrategy` interface | Signature unchanged — strategies still receive `Flag` |
| `FeatureEvaluator` | Dispatch unchanged — keyed on `Flag.StrategyType` |
| Database schema / migrations | jsonb column unchanged — Value Converter handles conversion |

---

## Acceptance Criteria

| ID | Given | When | Then |
|----|-------|------|------|
| AC-01 | A `CreateFlagRequest` with `StrategyType = Percentage` and `StrategyConfig = {"percentage": 50}` | The service creates the flag | `Flag.StrategyConfig` is a `StrategyConfig` VO with `ValidatedFor = Percentage` and `RawJson = {"percentage": 50}` |
| AC-02 | A `CreateFlagRequest` with `StrategyType = Percentage` and `StrategyConfig = {"roles": ["Admin"]}` | The request reaches FluentValidation | 400 returned — validation now delegates through `StrategyConfigRules` to the factory/validators |
| AC-03 | A `CreateFlagRequest` with `StrategyType = None` and `StrategyConfig = null` | The service creates the flag | `Flag.StrategyConfig` is a `StrategyConfig` VO with `ValidatedFor = None` and `RawJson = "{}"` |
| AC-04 | Code constructs a `Flag` with `StrategyType = RoleBased` but passes a `StrategyConfig` where `ValidatedFor = Percentage` | The `Flag` constructor executes | `FlagDomainException` is thrown — illegal state is unrepresentable |
| AC-05 | Code calls `Flag.Update()` with a mismatched `StrategyConfig.ValidatedFor` | `Update()` executes | `FlagDomainException` is thrown |
| AC-06 | Code calls `Flag.UpdateStrategy()` with a mismatched `StrategyConfig.ValidatedFor` | `UpdateStrategy()` executes | `FlagDomainException` is thrown |
| AC-07 | `PercentageConfigValidator.Validate()` receives null, empty, non-JSON, missing percentage field, or percentage outside 1-100 | Validation runs | `BanderasValidationException` is thrown |
| AC-08 | `RoleBasedConfigValidator.Validate()` receives null, empty, non-JSON, missing roles field, or empty roles array | Validation runs | `BanderasValidationException` is thrown |
| AC-09 | `NoneConfigValidator.Validate()` receives non-null, non-empty config | Validation runs | `BanderasValidationException` is thrown |
| AC-10 | `NoneConfigValidator.Validate()` receives null or empty string | Validation runs | Returns `StrategyConfig(ValidatedFor = None, RawJson = "{}")` |
| AC-11 | A `Flag` with `StrategyConfig(Percentage, {"percentage": 30})` is persisted via EF Core | Entity is saved and reloaded | `Flag.StrategyConfig.RawJson` equals `{"percentage": 30}` and `ValidatedFor` equals `Flag.StrategyType` on materialization |
| AC-12 | `PercentageStrategy.Evaluate()` is called on a flag with a valid `StrategyConfig` | Strategy reads `flag.StrategyConfig.RawJson` | Evaluation produces the same deterministic result as before the refactor |
| AC-13 | `RoleStrategy.Evaluate()` is called on a flag with a valid `StrategyConfig` | Strategy reads `flag.StrategyConfig.RawJson` | Same behavior — case-insensitive, fail-closed |
| AC-14 | A new strategy type is added in the future | Developer implements `IRolloutStrategy` + `IStrategyConfigValidator`, registers both in DI | Zero changes to `Flag`, `StrategyConfig`, `StrategyConfigFactory`, `FeatureEvaluator`, or the EF Core converter |
| AC-15 | `FlagMappings.ToResponse()` maps a flag with a typed `StrategyConfig` | Mapping runs | `FlagResponse.StrategyConfig` contains the raw JSON string — API response shape unchanged |
| AC-16 | All existing integration tests run | Test suite executes | All pass with no changes to expected HTTP request/response shapes |
| AC-17 | `DatabaseSeeder` creates seed flags | Application starts | Seed flags are created with valid `StrategyConfig` VOs — no runtime exceptions |

---

## File Layout

```text
Banderas.Domain/
├── Entities/
│   └── Flag.cs                              (modified)
├── Interfaces/
│   ├── IBanderasRepository.cs               (unchanged)
│   ├── IRolloutStrategy.cs                  (unchanged)
│   └── IStrategyConfigValidator.cs          (new)
└── ValueObjects/
    ├── FeatureEvaluationContext.cs           (unchanged)
    └── StrategyConfig.cs                    (new)

Banderas.Application/
├── DTOs/
│   ├── CreateFlagRequest.cs                 (unchanged)
│   ├── UpdateFlagRequest.cs                 (unchanged)
│   ├── FlagResponse.cs                      (unchanged)
│   └── FlagMappings.cs                      (modified)
├── Services/
│   └── BanderasService.cs                   (modified)
├── Strategies/
│   ├── NoneStrategy.cs                      (unchanged)
│   ├── PercentageStrategy.cs                (modified)
│   └── RoleStrategy.cs                      (modified)
├── Validators/
│   ├── CreateFlagRequestValidator.cs        (modified)
│   ├── UpdateFlagRequestValidator.cs        (modified)
│   ├── StrategyConfigRules.cs               (modified)
│   ├── StrategyConfigFactory.cs             (new)
│   ├── NoneConfigValidator.cs               (new)
│   ├── PercentageConfigValidator.cs         (new)
│   └── RoleBasedConfigValidator.cs          (new)
└── DependencyInjection.cs                   (modified)

Banderas.Infrastructure/
├── Persistence/
│   ├── FlagConfiguration.cs                 (modified)
│   └── StrategyConfigConverter.cs           (new)
└── Seeding/
    └── DatabaseSeeder.cs                    (modified)

Banderas.Tests/
├── Domain/
│   ├── FlagArchivedInvariantTests.cs        (modified)
│   └── ValueObjects/
│       ├── FeatureEvaluationContextTests.cs (unchanged)
│       └── StrategyConfigTests.cs           (new)
├── Evaluation/
│   └── FeatureEvaluatorTests.cs             (modified)
├── Helpers/
│   └── FlagBuilder.cs                       (modified)
├── Strategies/
│   ├── NoneStrategyTests.cs                 (modified)
│   ├── PercentageStrategyTests.cs           (modified)
│   └── RoleStrategyTests.cs                 (modified)
└── Validators/
    ├── CreateFlagRequestValidatorTests.cs   (modified)
    ├── UpdateFlagRequestValidatorTests.cs   (modified)
    ├── StrategyConfigFactoryTests.cs        (new)
    ├── NoneConfigValidatorTests.cs          (new)
    ├── PercentageConfigValidatorTests.cs    (new)
    └── RoleBasedConfigValidatorTests.cs     (new)
```

---

## Technical Notes

**Packages:** No new NuGet packages required. All changes use existing `System.Text.Json`,
`FluentValidation`, and `Microsoft.EntityFrameworkCore` APIs.

**EF Core Value Converter caveat:** EF Core Value Converters cannot inject services. The
read-path converter reconstructs `StrategyConfig` from the jsonb string using the
`internal` trusted constructor — skipping validation because the data was validated on
write. `ValidatedFor` is derived from `Flag.StrategyType` during materialization. This
requires the converter to be configured in `FlagConfiguration` where both properties are
accessible. Implementation may need a spike to determine whether a simple
`HasConversion()` call suffices or whether a post-materialization approach is needed
(e.g., `AfterSaveBehavior` or backing field mapping).

**Build sequence:**

1. Domain layer first — `StrategyConfig` VO, `IStrategyConfigValidator` interface
2. Application layer — validator implementations, factory, wire into DI
3. `Flag` entity — change property type, add guard clauses
4. `BanderasService` — call factory before `Flag` construction/update
5. Strategies — switch from `flag.StrategyConfig` (string) to `flag.StrategyConfig.RawJson`
6. `FlagMappings` — map `.RawJson` to response
7. Infrastructure — `StrategyConfigConverter`, `FlagConfiguration`, `DatabaseSeeder`
8. Tests — update `FlagBuilder`, strategy tests, add new VO/factory/validator tests
9. Verify all 169+ existing tests pass

**FluentValidation integration:** `StrategyConfigRules` will delegate to
`IStrategyConfigValidator` implementations. This means `CreateFlagRequestValidator` and
`UpdateFlagRequestValidator` will need the factory injected via constructor. Since
validators are already registered as `Scoped` services, constructor injection is
supported. The `Must()` lambdas will catch `BanderasValidationException` and return
`false`.

**`FlagBuilder` test helper:** Will use the trusted `internal` constructor directly —
test helpers have access via `InternalsVisibleTo("Banderas.Tests")`.

**`DatabaseSeeder`:** Lives in Infrastructure with access to internals. Uses the trusted
constructor for hardcoded, known-valid seed data.

---

## Out of Scope

| Item | Deferred To | Rationale |
|------|-------------|-----------|
| Multivariate flag support (variations collection) | Phase 5 | Config shapes will evolve significantly; `RawJson` is forward-compatible |
| Attribute-based targeting rules / clause engine | Phase 5 | Requires `FeatureEvaluationContext.Attributes` dictionary first |
| `FlagEnvironmentConfig` aggregate separation | Later Phase 2 backlog | Independent refactor |
| Consolidate `SetEnabled()` / `UpdateStrategy()` / `Update()` | Later Phase 2 backlog | Orthogonal to config typing |
| `Flag.Description` and `Tags` | Later Phase 2 backlog | Separate DDD backlog items |
| Deep typing of config internals (e.g., `PercentageConfig` domain type) | Phase 5 | Config internals will change with multivariate/targeting |
| Removing fail-closed JSON parsing from strategies | Never | Defense in depth |
| API contract changes to DTOs | Not planned | HTTP boundary stays `string?` |

---

## Learning Opportunities

1. **EF Core Value Converters** — Converting between a domain Value Object and a database
   column type without changing the schema. Understanding converter limitations (no DI, no
   access to other properties) is practical knowledge for DDD with EF Core.

2. **Registry/Factory pattern for open/closed extensibility** — The
   `StrategyConfigFactory` mirrors `FeatureEvaluator` dispatch:
   `Dictionary<RolloutStrategy, IStrategyConfigValidator>` from DI-injected
   `IEnumerable<IStrategyConfigValidator>`. Seeing the same pattern twice reinforces when
   and why it works.

3. **Make Illegal States Unrepresentable in C#** — Using a Value Object with controlled
   constructors to ensure only validated instances can exist. `public` factory for
   validated construction, `internal` constructor for trusted reconstruction — the type
   system enforces the business rule.

---

## DX / Tooling Idea

**New strategy scaffolding checklist.** After this refactor, adding a new strategy
requires exactly three steps: implement `IRolloutStrategy`, implement
`IStrategyConfigValidator`, register both in DI. A comment block at the top of
`DependencyInjection.cs` documenting this three-step recipe would make onboarding a new
strategy type a 10-minute task for any contributor.

---

## Definition of Done

- [ ] `StrategyConfig` sealed record exists in `Banderas.Domain/ValueObjects/` with `ValidatedFor` and `RawJson` properties
- [ ] `StrategyConfig` has an `internal` trusted constructor for EF Core materialization and seed data
- [ ] `IStrategyConfigValidator` interface exists in `Banderas.Domain/Interfaces/`
- [ ] `NoneConfigValidator`, `PercentageConfigValidator`, `RoleBasedConfigValidator` implementations exist in `Banderas.Application/Validators/`
- [ ] `StrategyConfigFactory` exists with registry dispatch keyed on `RolloutStrategy`
- [ ] All validators and factory registered in `DependencyInjection.cs`
- [ ] `Flag.StrategyConfig` property type is `StrategyConfig` (not `string`)
- [ ] `Flag` constructor, `Update()`, and `UpdateStrategy()` enforce `config.ValidatedFor == strategyType` — throw `FlagDomainException` on mismatch
- [ ] `PercentageStrategy` and `RoleStrategy` read from `flag.StrategyConfig.RawJson`
- [ ] `FlagMappings.ToResponse()` maps `StrategyConfig.RawJson` to `FlagResponse.StrategyConfig`
- [ ] `BanderasService.CreateFlagAsync()` and `UpdateFlagAsync()` call `StrategyConfigFactory.Create()` before passing to `Flag`
- [ ] EF Core Value Converter handles `StrategyConfig` <-> `string` — no database migration required
- [ ] `DatabaseSeeder` constructs valid `StrategyConfig` VOs for seed data
- [ ] `FlagBuilder` test helper updated to use `StrategyConfig` VO
- [ ] Unit tests for `StrategyConfig` VO construction and guard clauses
- [ ] Unit tests for each `IStrategyConfigValidator` implementation
- [ ] Unit tests for `StrategyConfigFactory` registry dispatch and mismatch rejection
- [ ] Unit tests for `Flag` constructor/mutation `FlagDomainException` on config mismatch
- [ ] All existing strategy, evaluator, and validator tests updated and passing
- [ ] All 169 existing tests pass — no regressions
- [ ] All integration tests pass — HTTP request/response shapes unchanged
- [ ] `dotnet build -p:TreatWarningsAsErrors=true` passes with zero warnings
- [ ] CSharpier formatting check passes
