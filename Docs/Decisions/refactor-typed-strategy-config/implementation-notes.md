# Typed StrategyConfig Value Object -- Implementation Notes

**Session date:** 2026-05-07
**Branch:** `refactor/typed-strategy-config`
**Spec reference:** Docs/Decisions/refactor-typed-strategy-config/spec.md
**Build status:** Passing (0 warnings, 0 errors with TreatWarningsAsErrors=true)
**Tests:** 158/158 unit + 54/54 integration tests passing (212 total)
**PR:** TBD

## What Was Built

`Flag.StrategyConfig` was converted from a raw `string` to a typed `StrategyConfig`
sealed record Value Object. The `Flag` entity now enforces that config and strategy type
are always consistent -- a `Flag` with `StrategyType = Percentage` but a config validated
for `RoleBased` cannot exist. An `IStrategyConfigValidator` registry pattern (mirroring the
existing `IRolloutStrategy` dispatch) validates raw JSON into typed VOs at the application
boundary, and an EF Core Value Converter handles persistence without schema changes.

## Spec Gaps Resolved

None -- the spec was clean.

## Deviations from Spec

**EF Core materialization approach.** The spec described the converter reconstructing
`StrategyConfig` using the `internal` constructor with `ValidatedFor` derived from
`Flag.StrategyType` during materialization. In practice, EF Core Value Converters cannot
access sibling properties. The implementation uses a backing field (`_strategyConfig`)
with a lazy-reconciling property getter that fixes `ValidatedFor` from `StrategyType` on
first access. The domain invariant is preserved -- the getter reconciles before any
consumer sees the value.

**`StrategyConfigRules` became instance-based.** The spec showed `StrategyConfigRules`
as a static class delegating to the factory. Since `StrategyConfigFactory` is injected
via DI, `StrategyConfigRules` became an instance class constructed with the factory.
`CreateFlagRequestValidator` and `UpdateFlagRequestValidator` now accept
`StrategyConfigFactory` in their constructors.

## Key Decisions

1. **Backing field reconciliation over materialization interceptor.** The alternative was
   an EF Core `IMaterializationInterceptor` to fix `ValidatedFor` post-load. The backing
   field approach is simpler, requires no interceptor registration, and keeps the fix
   local to the `Flag` entity.

2. **Single cross-field validation rule in FluentValidation.** The original validators
   had separate `When` clauses per strategy type. The new validators use a single `Must()`
   that delegates to `StrategyConfigRules.BeValidStrategyConfig()`, which catches
   `BanderasValidationException` from the factory. This is cleaner and automatically
   supports new strategy types without touching the validators.

3. **`InternalsVisibleTo` for both Tests and Infrastructure.** The `internal` trusted
   constructor needs access from test helpers (`FlagBuilder`) and infrastructure
   (`DatabaseSeeder`, `StrategyConfigConverter`). Both assemblies are listed in
   `Banderas.Domain.csproj`.

## File-by-File Changes

### New Files

| File | Purpose |
|------|---------|
| `Banderas.Domain/ValueObjects/StrategyConfig.cs` | Sealed record VO with `ValidatedFor` and `RawJson` |
| `Banderas.Domain/Interfaces/IStrategyConfigValidator.cs` | Validator interface |
| `Banderas.Application/Validators/StrategyConfigFactory.cs` | Registry dispatch factory |
| `Banderas.Application/Validators/NoneConfigValidator.cs` | None strategy validator |
| `Banderas.Application/Validators/PercentageConfigValidator.cs` | Percentage strategy validator |
| `Banderas.Application/Validators/RoleBasedConfigValidator.cs` | RoleBased strategy validator |
| `Banderas.Infrastructure/Persistence/StrategyConfigConverter.cs` | EF Core ValueConverter |
| `Banderas.Tests/Domain/ValueObjects/StrategyConfigTests.cs` | VO + Flag guard clause tests (8) |
| `Banderas.Tests/Validators/StrategyConfigFactoryTests.cs` | Factory dispatch tests (7) |
| `Banderas.Tests/Validators/NoneConfigValidatorTests.cs` | None validator tests (7) |
| `Banderas.Tests/Validators/PercentageConfigValidatorTests.cs` | Percentage validator tests (12) |
| `Banderas.Tests/Validators/RoleBasedConfigValidatorTests.cs` | RoleBased validator tests (9) |

### Modified Files

| File | Change |
|------|--------|
| `Banderas.Domain/Entities/Flag.cs` | `StrategyConfig` type string -> VO; backing field + reconciling getter; guard clauses on constructor/Update/UpdateStrategy |
| `Banderas.Domain/Banderas.Domain.csproj` | `InternalsVisibleTo` for Tests + Infrastructure |
| `Banderas.Application/Strategies/PercentageStrategy.cs` | `flag.StrategyConfig` -> `flag.StrategyConfig.RawJson` |
| `Banderas.Application/Strategies/RoleStrategy.cs` | Same |
| `Banderas.Application/Validators/StrategyConfigRules.cs` | Rewritten: instance class delegating to factory |
| `Banderas.Application/Validators/CreateFlagRequestValidator.cs` | Constructor injection of factory; single cross-field Must() rule |
| `Banderas.Application/Validators/UpdateFlagRequestValidator.cs` | Same |
| `Banderas.Application/Services/BanderasService.cs` | Calls `StrategyConfigFactory.Create()` before Flag construction/update |
| `Banderas.Application/DTOs/FlagMappings.cs` | Maps `flag.StrategyConfig.RawJson` |
| `Banderas.Application/DependencyInjection.cs` | Registers validators + factory |
| `Banderas.Infrastructure/Persistence/FlagConfiguration.cs` | Value Converter + backing field |
| `Banderas.Infrastructure/Seeding/DatabaseSeeder.cs` | Uses trusted constructor |
| `Banderas.Tests/Helpers/FlagBuilder.cs` | Uses trusted constructor |
| `Banderas.Tests/Domain/FlagArchivedInvariantTests.cs` | Updated to VO signatures |
| `Banderas.Tests/Strategies/PercentageStrategyTests.cs` | Via FlagBuilder (no direct changes) |
| `Banderas.Tests/Strategies/RoleStrategyTests.cs` | Via FlagBuilder |
| `Banderas.Tests/Strategies/NoneStrategyTests.cs` | Via FlagBuilder |
| `Banderas.Tests/Evaluation/FeatureEvaluatorTests.cs` | Via FlagBuilder |
| `Banderas.Tests/Validators/CreateFlagRequestValidatorTests.cs` | Constructor injection of factory |
| `Banderas.Tests/Validators/UpdateFlagRequestValidatorTests.cs` | Same |
| `Banderas.Tests/AI/BanderasServiceAnalysisTests.cs` | Factory + VO construction |
| `Banderas.Tests/Services/BanderasServiceLoggingTests.cs` | Factory + VO construction |

## Risks and Follow-Ups

1. **`Flag.StrategyConfig` getter reconciliation is lazy.** If `StrategyType` and
   `_strategyConfig.ValidatedFor` are both `None` (the default), no reconciliation
   occurs. For non-None strategies materialized from DB, the first property access
   creates a new `StrategyConfig` record. This is correct but subtle -- a future
   refactor should consider whether a post-materialization hook is cleaner.

2. ~~**Integration test coverage.** The EF Core Value Converter path (AC-11) is not
   covered by the unit test suite.~~ **Resolved:** All 54 integration tests pass.
   Two integration tests (`FlagConcurrencyTokenTests`, `SeedDataStartupTests`) were
   passing `null` for `StrategyConfig` in `Flag` constructors -- fixed to use
   `StrategyConfig.Create(RolloutStrategy.None, "{}")`.

3. **IDE0008 `var` style compliance.** Follow-up fix replaced 11 `var` usages with
   explicit `StrategyConfig` type across `BanderasService.cs` and 4 test files to
   comply with `.editorconfig` (`csharp_style_var_elsewhere = false:warning`).

## How to Test

```bash
# Full unit test suite
dotnet test Banderas.Tests/Banderas.Tests.csproj

# Specific new test classes
dotnet test Banderas.Tests/Banderas.Tests.csproj --filter "FullyQualifiedName~StrategyConfigTests"
dotnet test Banderas.Tests/Banderas.Tests.csproj --filter "FullyQualifiedName~StrategyConfigFactoryTests"
dotnet test Banderas.Tests/Banderas.Tests.csproj --filter "FullyQualifiedName~NoneConfigValidatorTests"
dotnet test Banderas.Tests/Banderas.Tests.csproj --filter "FullyQualifiedName~PercentageConfigValidatorTests"
dotnet test Banderas.Tests/Banderas.Tests.csproj --filter "FullyQualifiedName~RoleBasedConfigValidatorTests"

# Warnings-as-errors build
dotnet build Banderas.sln -p:TreatWarningsAsErrors=true

# CSharpier formatting
dotnet csharpier check .
```

## Interview Lens

The central problem was making EF Core's Value Converter work with a Value Object whose
identity depends on a sibling property (`Flag.StrategyType`). Value Converters are
column-scoped -- they see one column, not the entity. The tradeoff was between a
`IMaterializationInterceptor` (clean but adds DI complexity and a global hook) and a
backing field with a lazy-reconciling property getter (local to the entity, zero DI, but
introduces a mutable backing field behind an immutable record). At this scale, the backing
field wins on simplicity. At a larger scale with many such VOs, the interceptor approach
would centralize the reconciliation logic and avoid spreading backing-field patterns across
the domain.

## Foundation Docs Updated

- [x] `Docs/current-state.md` -- Phase 2 status, domain/application/infra/test sections, lessons learned
- [x] `Docs/roadmap.md` -- Phase 2 checklist items, current focus
- [x] `Docs/architecture.md` -- StrategyConfig VO, IStrategyConfigValidator, extensibility points, design tradeoffs

## Definition of Done -- Status

- [x] `StrategyConfig` sealed record exists in `Banderas.Domain/ValueObjects/`
- [x] `StrategyConfig` has an `internal` trusted constructor
- [x] `IStrategyConfigValidator` interface exists in `Banderas.Domain/Interfaces/`
- [x] Three validator implementations exist in `Banderas.Application/Validators/`
- [x] `StrategyConfigFactory` exists with registry dispatch
- [x] All validators and factory registered in `DependencyInjection.cs`
- [x] `Flag.StrategyConfig` property type is `StrategyConfig`
- [x] `Flag` constructor, `Update()`, `UpdateStrategy()` enforce config match
- [x] Strategies read from `flag.StrategyConfig.RawJson`
- [x] `FlagMappings.ToResponse()` maps `StrategyConfig.RawJson`
- [x] `BanderasService` calls factory before `Flag` construction/update
- [x] EF Core Value Converter handles persistence -- no migration required
- [x] `DatabaseSeeder` uses valid VOs
- [x] `FlagBuilder` updated
- [x] Unit tests for VO construction (5)
- [x] Unit tests for each validator (12 + 9 + 7 = 28)
- [x] Unit tests for factory (7)
- [x] Unit tests for Flag guard clauses (3)
- [x] All existing tests updated and passing
- [x] 158/158 unit tests passing
- [x] 54/54 integration tests passing -- HTTP request/response shapes unchanged
- [x] `dotnet build -p:TreatWarningsAsErrors=true` -- 0 warnings
- [x] CSharpier check passes
- [x] IDE0008 `var` style compliance -- all method-call `var` replaced with explicit types per `.editorconfig`
