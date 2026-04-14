# Specification: Unit Tests — Phase 1

**Document:** `Docs/Decisions/unit-tests - PR#38/spec.md`
**Status:** Ready for Implementation
**Branch:** `test/unit-and-integration-tests`
**Phase:** 1 — Testing & Developer Experience
**Author:** Jose / Claude Architect Session
**Date:** 2026-04-04

[Pull Request #38](https://github.com/amodelandme/Bandera/pull/38)

---

## Table of Contents

- [User Story](#user-story)
- [Goals and Non-Goals](#goals-and-non-goals)
- [Test Project Setup](#test-project-setup)
- [Folder Structure](#folder-structure)
- [Conventions](#conventions)
- [Helper: FlagBuilder](#helper-flagbuilder)
- [NoneStrategyTests](#nonestrategyTests)
- [PercentageStrategyTests](#percentagestrategyTests)
- [RoleStrategyTests](#roleStrategyTests)
- [FeatureEvaluatorTests](#featureevaluatortests)
- [CreateFlagRequestValidatorTests](#createflagrequestvalidatortests)
- [UpdateFlagRequestValidatorTests](#updateflagrequestvalidatortests)
- [EvaluationRequestValidatorTests](#evaluationrequestvalidatortests)
- [Acceptance Criteria](#acceptance-criteria)
- [CI Verification](#ci-verification)
- [Out of Scope](#out-of-scope)
- [Instructions for Claude Code](#instructions-for-claude-code)

---

## User Story

> As a developer on Bandera, I want a comprehensive suite of unit tests
> for the evaluation strategies, evaluator, and request validators so that I can
> confidently refactor and extend the system without introducing silent regressions.

---

## Goals and Non-Goals

**Goals:**
- Test all evaluation strategy implementations in isolation
- Test the `FeatureEvaluator` registry dispatch and fail-closed fallback
- Test every validation rule in all three request validators
- Have all tests run in CI via the existing `build-test` job (no database required)

**Non-Goals:**
- Do not test `Bandera` — it orchestrates a real repository; leave for integration tests
- Do not test `BanderaRepository` — requires a live database; integration tests only
- Do not test controllers directly — integration tests only
- Do not modify `FeatureEvaluationContextTests.cs` — already covers its own domain

---

## Test Project Setup

### NuGet Packages

Add the following to `Bandera.Tests/Bandera.Tests.csproj`:

```xml
<PackageReference Include="FluentAssertions" Version="7.*" />
```

`xunit`, `Microsoft.NET.Test.Sdk`, and `xunit.runner.visualstudio` are already
present. Do not add or change any other packages.

### Project References

The test project must reference `Bandera.Domain` and `Bandera.Application`.
Verify these are already present. Do not add a reference to `Bandera.Infrastructure`
or `Bandera.Api`.

```xml
<ProjectReference Include="..\Bandera.Domain\Bandera.Domain.csproj" />
<ProjectReference Include="..\Bandera.Application\Bandera.Application.csproj" />
```

---

## Folder Structure

Create the following files. Do not create any other files or folders.

```
Bandera.Tests/
├── Helpers/
│   └── FlagBuilder.cs                         ← NEW
├── Strategies/
│   ├── NoneStrategyTests.cs                   ← NEW
│   ├── PercentageStrategyTests.cs             ← NEW
│   └── RoleStrategyTests.cs                   ← NEW
├── Evaluation/
│   └── FeatureEvaluatorTests.cs               ← NEW
└── Validators/
    ├── CreateFlagRequestValidatorTests.cs      ← NEW
    ├── UpdateFlagRequestValidatorTests.cs      ← NEW
    └── EvaluationRequestValidatorTests.cs     ← NEW
```

`FeatureEvaluationContextTests.cs` already exists at the root of the test project.
Do not move, rename, or modify it.

---

## Conventions

### Trait decoration
Every test class and every test method must carry `[Trait("Category", "Unit")]`.
The CI `build-test` job filters on this trait. Without it, tests do not run in CI.

```csharp
[Trait("Category", "Unit")]
public sealed class NoneStrategyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_Always_ReturnsTrue() { ... }
}
```

### Naming convention — `MethodName_StateUnderTest_ExpectedBehavior`

```
Evaluate_WhenPercentageIsZero_ReturnsFalse
Evaluate_WhenUserHasMatchingRole_ReturnsTrue
Validate_WhenNameIsEmpty_ReturnsInvalid
```

### Arrange-Act-Assert (AAA)

Every test body follows three clearly separated sections:

```csharp
// Arrange
var flag = FlagBuilder.Build(strategy: RolloutStrategy.None);

// Act
var result = strategy.Evaluate(flag, context);

// Assert
result.Should().BeTrue();
```

### FluentAssertions usage

Use FluentAssertions for all assertions. Do not use `Assert.True`, `Assert.Equal`,
or any other xUnit built-in assertion method.

```csharp
// Correct
result.Should().BeTrue();
result.Should().BeFalse();
validationResult.IsValid.Should().BeFalse();
validationResult.Errors.Should().ContainSingle(e => e.PropertyName == "Name");

// Incorrect — do not use
Assert.True(result);
Assert.False(result);
```

### No shared mutable state

Do not use constructor injection or `IClassFixture<T>` to share state between tests.
Each test method constructs its own objects. Strategies and validators are cheap to
instantiate.

---

## Helper: FlagBuilder

**File:** `Bandera.Tests/Helpers/FlagBuilder.cs`

A static factory that creates `Flag` instances with sensible defaults. Tests override
only the properties relevant to what they are testing.

```csharp
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;

namespace Bandera.Tests.Helpers;

internal static class FlagBuilder
{
    internal static Flag Build(
        string name = "test-flag",
        EnvironmentType environment = EnvironmentType.Development,
        bool isEnabled = true,
        RolloutStrategy strategy = RolloutStrategy.None,
        string? strategyConfig = null
    )
    {
        return new Flag(name, environment, isEnabled, strategy, strategyConfig);
    }
}
```

> **NOTE**
> `FlagBuilder` is `internal` — it is only used within the test project. Do not
> make it `public`.

> **NOTE**
> Check the `Flag` constructor signature in `Bandera.Domain/Entities/Flag.cs`
> before implementing. Match the parameter names and order exactly. If the
> constructor does not accept a nullable `string?` for `strategyConfig`, adjust
> the default to `string.Empty` instead.

---

## NoneStrategyTests

**File:** `Bandera.Tests/Strategies/NoneStrategyTests.cs`

`NoneStrategy` is a passthrough. It must always return `true` regardless of the
flag configuration or evaluation context. These tests confirm the contract.

### Tests to implement

**NT-1 — Always returns true for a default context**

```
Evaluate_WithDefaultContext_ReturnsTrue
```
Arrange a flag with `RolloutStrategy.None` and a basic `FeatureEvaluationContext`
(userId = `"user-1"`, no roles, `EnvironmentType.Development`).
Assert result is `true`.

**NT-2 — Always returns true with an empty UserId**

```
Evaluate_WithEmptyUserId_ReturnsTrue
```
Arrange a context where `UserId` is `"anon"` (use the shortest valid value —
do not use empty string; the constructor guards against it).
Assert result is `true`.

**NT-3 — Always returns true with no roles**

```
Evaluate_WithNoRoles_ReturnsTrue
```
Arrange a context with an empty roles array.
Assert result is `true`.

**NT-4 — Always returns true regardless of flag enabled state**

```
Evaluate_WhenFlagIsDisabled_StillReturnsTrue
```
> **IMPORTANT NOTE for Claude Code:** This test verifies that `NoneStrategy.Evaluate`
> itself returns `true` even when `flag.IsEnabled` is `false`. The `IsEnabled` check
> is the *service layer's* responsibility (enforced in `Bandera`), not the
> strategy's. This is a deliberate architectural decision documented in KI-002.

Arrange a flag built with `isEnabled: false`.
Call `NoneStrategy.Evaluate` directly.
Assert result is `true`.

---

## PercentageStrategyTests

**File:** `Bandera.Tests/Strategies/PercentageStrategyTests.cs`

`PercentageStrategy` uses SHA-256 hashing to assign users deterministically to a
0–99 bucket and returns true if that bucket falls below the configured percentage
threshold.

### Tests to implement

**PS-1 — Null config returns false**

```
Evaluate_WhenStrategyConfigIsNull_ReturnsFalse
```
Build a flag with `strategyConfig: null`.
Assert result is `false`.

**PS-2 — Empty string config returns false**

```
Evaluate_WhenStrategyConfigIsEmpty_ReturnsFalse
```
Build a flag with `strategyConfig: string.Empty`.
Assert result is `false`.

**PS-3 — Malformed JSON returns false**

```
Evaluate_WhenStrategyConfigIsNotJson_ReturnsFalse
```
Build a flag with `strategyConfig: "not-json"`.
Assert result is `false`.

**PS-4 — Missing percentage field returns false**

```
Evaluate_WhenPercentageFieldIsMissing_ReturnsFalse
```
Build a flag with `strategyConfig: """{"rollout": 50}"""` (valid JSON but wrong field name).
Assert result is `false`.

**PS-5 — Percentage of zero always returns false**

```
Evaluate_WhenPercentageIsZero_ReturnsFalse
```
Build a flag with `strategyConfig: """{"percentage": 0}"""`.
Run `Evaluate` for 10 different user IDs (`"user-0"` through `"user-9"`).
Assert every result is `false`.

**PS-6 — Percentage of 100 always returns true**

```
Evaluate_WhenPercentageIsOneHundred_ReturnsTrue
```
Build a flag with `strategyConfig: """{"percentage": 100}"""`.
Run `Evaluate` for 10 different user IDs (`"user-0"` through `"user-9"`).
Assert every result is `true`.

**PS-7 — Evaluation is deterministic**

```
Evaluate_CalledTwiceWithSameInput_ReturnsSameResult
```
Build a flag with `strategyConfig: """{"percentage": 50}"""`.
Call `Evaluate` twice with identical flag and context.
Assert both results are equal.

This is the most important test for this strategy — SHA-256 must produce the same
bucket for the same `userId:flagName` input on every call and every process restart.

**PS-8 — Percentage out of range (above 100) returns false**

```
Evaluate_WhenPercentageExceedsOneHundred_ReturnsFalse
```
Build a flag with `strategyConfig: """{"percentage": 150}"""`.
Assert result is `false`.

> **NOTE**
> The `PercentageStrategy` implementation clamps or rejects values outside 0–100
> and fails closed. Check the implementation — it may reject on `< 1` or on `< 0`.
> The boundary condition is: if the implementation treats `0` as valid (returning
> `false`) and `< 0` as invalid (also returning `false`), both behaviors are correct
> for this test — what matters is that they fail closed. Do not change the strategy
> implementation to match the test. Adjust the test to match the actual boundary
> the implementation enforces.

**PS-9 — Flag name is included in the hash input**

```
Evaluate_SameUserDifferentFlagNames_MayProduceDifferentResults
```
This test documents the design intent, not a strict pass/fail assertion.

Build two flags with the same `strategyConfig: """{"percentage": 50}"""` but
different names: `"flag-a"` and `"flag-b"`. Use the same `userId`.

Call `Evaluate` for each flag. Capture both boolean results.

Assert: at minimum, both calls complete without throwing. Optionally, log a warning
if both results are identical (this is possible by chance but unlikely at 50%).

> **NOTE FOR CLAUDE CODE:** Do not use `Assert` or FluentAssertions to assert the
> two results are *different* — that would be a flaky test. Instead, assert
> that neither call throws, and add a comment explaining the design intent:
> including the flag name in the hash input ensures independent bucketing per flag.

---

## RoleStrategyTests

**File:** `Bandera.Tests/Strategies/RoleStrategyTests.cs`

`RoleStrategy` deserializes a JSON config containing a `roles` array and returns
`true` if the user's roles contain at least one match (OR logic, case-insensitive).

### Tests to implement

**RS-1 — Null config returns false**

```
Evaluate_WhenStrategyConfigIsNull_ReturnsFalse
```
Build a flag with `strategyConfig: null`.
Assert result is `false`.

**RS-2 — Empty string config returns false**

```
Evaluate_WhenStrategyConfigIsEmpty_ReturnsFalse
```
Build a flag with `strategyConfig: string.Empty`.
Assert result is `false`.

**RS-3 — Malformed JSON returns false**

```
Evaluate_WhenStrategyConfigIsNotJson_ReturnsFalse
```
Build a flag with `strategyConfig: "not-json"`.
Assert result is `false`.

**RS-4 — Empty roles array in config returns false**

```
Evaluate_WhenConfigRolesArrayIsEmpty_ReturnsFalse
```
Build a flag with `strategyConfig: """{"roles": []}"""`.
Provide a context with `userRoles: ["Admin"]`.
Assert result is `false`.

> This verifies fail-closed behavior — an empty allowlist blocks everyone.

**RS-5 — User with a matching role returns true**

```
Evaluate_WhenUserHasMatchingRole_ReturnsTrue
```
Config: `"""{"roles": ["Admin", "Editor"]}"""`.
Context roles: `["Admin"]`.
Assert result is `true`.

**RS-6 — User with no matching role returns false**

```
Evaluate_WhenUserHasNoMatchingRole_ReturnsFalse
```
Config: `"""{"roles": ["Admin"]}"""`.
Context roles: `["Viewer"]`.
Assert result is `false`.

**RS-7 — Role matching is case-insensitive**

```
Evaluate_WhenRoleCaseDiffers_ReturnsTrue
```
Config: `"""{"roles": ["Admin"]}"""`.
Context roles: `["admin"]`.
Assert result is `true`.

This is a critical correctness test — identity providers frequently mismatch
casing between what the config stores and what the token provides.

**RS-8 — OR logic: user with one of multiple required roles returns true**

```
Evaluate_WhenUserHasOneOfManyRoles_ReturnsTrue
```
Config: `"""{"roles": ["Admin", "Editor", "Reviewer"]}"""`.
Context roles: `["Reviewer"]`.
Assert result is `true`.

**RS-9 — User with no roles returns false**

```
Evaluate_WhenUserHasNoRoles_ReturnsFalse
```
Config: `"""{"roles": ["Admin"]}"""`.
Context roles: `[]` (empty array).
Assert result is `false`.

---

## FeatureEvaluatorTests

**File:** `Bandera.Tests/Evaluation/FeatureEvaluatorTests.cs`

`FeatureEvaluator` builds a `Dictionary<RolloutStrategy, IRolloutStrategy>` from
injected strategies and dispatches at evaluation time. Tests confirm correct dispatch
and fail-closed fallback when a strategy is not registered.

### Constructing FeatureEvaluator in tests

`FeatureEvaluator` takes `IEnumerable<IRolloutStrategy>` in its constructor.
Instantiate it directly — no DI container needed:

```csharp
var evaluator = new FeatureEvaluator(new IRolloutStrategy[]
{
    new NoneStrategy(),
    new PercentageStrategy(),
    new RoleStrategy(),
});
```

For the missing-strategy test, construct the evaluator with only `NoneStrategy`
registered — then provide a flag whose `StrategyType` is `Percentage`.

### Tests to implement

**FE-1 — Dispatches to NoneStrategy and returns true**

```
Evaluate_WhenStrategyIsNone_ReturnsTrue
```
Evaluator: all three strategies registered.
Flag: `RolloutStrategy.None`, `strategyConfig: null`.
Context: basic context, any user.
Assert result is `true`.

**FE-2 — Dispatches to PercentageStrategy**

```
Evaluate_WhenStrategyIsPercentage_DelegatesToPercentageStrategy
```
Evaluator: all three strategies registered.
Flag: `RolloutStrategy.Percentage`, `strategyConfig: """{"percentage": 100}"""`.
Context: any user.
Assert result is `true`.

> Setting percentage to 100 guarantees a `true` return regardless of userId,
> making the assertion deterministic without needing to know the specific bucket.

**FE-3 — Dispatches to RoleStrategy**

```
Evaluate_WhenStrategyIsRoleBased_DelegatesToRoleStrategy
```
Evaluator: all three strategies registered.
Flag: `RolloutStrategy.RoleBased`, `strategyConfig: """{"roles": ["Admin"]}"""`.
Context: `userRoles: ["Admin"]`.
Assert result is `true`.

**FE-4 — Returns false (fail-closed) when strategy is not registered**

```
Evaluate_WhenStrategyNotRegistered_ReturnsFalse
```
Evaluator: instantiated with **only** `NoneStrategy` registered.
Flag: `RolloutStrategy.Percentage` (not in the registry).
Context: any user.
Assert result is `false`.

> This is the most important evaluator test. It verifies the fail-closed contract:
> an unknown strategy type must never accidentally grant access.

---

## CreateFlagRequestValidatorTests

**File:** `Bandera.Tests/Validators/CreateFlagRequestValidatorTests.cs`

Instantiate the validator directly — no DI:

```csharp
var validator = new CreateFlagRequestValidator();
var result = await validator.ValidateAsync(request);
```

For all invalid cases, also assert that `result.Errors` contains an error on the
expected property name. Use the `PropertyName` from the `ValidationFailure`.

### Tests to implement

**CV-1 — Empty name is invalid**

```
Validate_WhenNameIsEmpty_ReturnsInvalid
```
`Name: ""`. Assert `IsValid == false`. Assert error on `"Name"`.

**CV-2 — Whitespace-only name is invalid**

```
Validate_WhenNameIsWhitespaceOnly_ReturnsInvalid
```
`Name: "   "`. Assert `IsValid == false`. Assert error on `"Name"`.

**CV-3 — Name exceeding 100 characters is invalid**

```
Validate_WhenNameExceedsMaxLength_ReturnsInvalid
```
`Name: new string('a', 101)`. Assert `IsValid == false`. Assert error on `"Name"`.

**CV-4 — Name with invalid characters is invalid**

Use `[Theory]` + `[InlineData]`:

```
Validate_WhenNameContainsInvalidCharacters_ReturnsInvalid
```

```csharp
[Theory]
[InlineData("my flag")]       // space
[InlineData("flag!")]         // exclamation
[InlineData("flag.name")]     // dot
[InlineData("flag/name")]     // slash
[Trait("Category", "Unit")]
public async Task Validate_WhenNameContainsInvalidCharacters_ReturnsInvalid(string name)
```

Assert `IsValid == false` for each. Assert error on `"Name"`.

**CV-5 — Valid name with padded whitespace passes**

```
Validate_WhenNameHasPaddedWhitespace_ReturnsValid
```
`Name: " dark-mode "`. The validator runs the regex on the *cleaned* value.
Assert `IsValid == true`.

> This test confirms the validator accepts padded input — the service layer will
> strip the whitespace before storing. Do not change this behavior.

**CV-6 — Valid name with hyphens and underscores passes**

```
Validate_WhenNameUsesAllowedCharacters_ReturnsValid
```
`Name: "my-flag_v2"`. Assert `IsValid == true`.

**CV-7 — Environment sentinel (None) is invalid**

```
Validate_WhenEnvironmentIsNone_ReturnsInvalid
```
`Environment: EnvironmentType.None`. Valid name and strategy.
Assert `IsValid == false`. Assert error on `"Environment"`.

**CV-8 — Valid environments pass**

Use `[Theory]` + `[InlineData]`:

```csharp
[Theory]
[InlineData(EnvironmentType.Development)]
[InlineData(EnvironmentType.Staging)]
[InlineData(EnvironmentType.Production)]
[Trait("Category", "Unit")]
public async Task Validate_WhenEnvironmentIsValid_ReturnsValid(EnvironmentType env)
```

All three must pass with `IsValid == true`. Use `RolloutStrategy.None` and a valid name.

**CV-9 — StrategyType None with non-empty StrategyConfig is invalid**

```
Validate_WhenStrategyIsNoneButConfigIsProvided_ReturnsInvalid
```
`StrategyType: RolloutStrategy.None`, `StrategyConfig: """{"percentage":50}"""`.
Assert `IsValid == false`. Assert error on `"StrategyConfig"`.

**CV-10 — StrategyType None with null StrategyConfig passes**

```
Validate_WhenStrategyIsNoneAndConfigIsNull_ReturnsValid
```
`StrategyType: RolloutStrategy.None`, `StrategyConfig: null`.
Assert `IsValid == true`.

**CV-11 — StrategyType Percentage with valid config passes**

```
Validate_WhenStrategyIsPercentageWithValidConfig_ReturnsValid
```
`StrategyConfig: """{"percentage": 50}"""`. Assert `IsValid == true`.

**CV-12 — StrategyType Percentage with missing config is invalid**

```
Validate_WhenStrategyIsPercentageWithNullConfig_ReturnsInvalid
```
`StrategyType: RolloutStrategy.Percentage`, `StrategyConfig: null`.
Assert `IsValid == false`. Assert error on `"StrategyConfig"`.

**CV-13 — StrategyType Percentage with invalid config structure is invalid**

```
Validate_WhenStrategyIsPercentageWithInvalidConfig_ReturnsInvalid
```
`StrategyConfig: """{"roles": ["Admin"]}"""` (wrong shape for Percentage).
Assert `IsValid == false`. Assert error on `"StrategyConfig"`.

**CV-14 — StrategyType RoleBased with valid config passes**

```
Validate_WhenStrategyIsRoleBasedWithValidConfig_ReturnsValid
```
`StrategyConfig: """{"roles": ["Admin", "Editor"]}"""`. Assert `IsValid == true`.

**CV-15 — StrategyType RoleBased with null config is invalid**

```
Validate_WhenStrategyIsRoleBasedWithNullConfig_ReturnsInvalid
```
`StrategyType: RolloutStrategy.RoleBased`, `StrategyConfig: null`.
Assert `IsValid == false`. Assert error on `"StrategyConfig"`.

**CV-16 — StrategyType RoleBased with invalid config structure is invalid**

```
Validate_WhenStrategyIsRoleBasedWithInvalidConfig_ReturnsInvalid
```
`StrategyConfig: """{"percentage": 50}"""` (wrong shape for RoleBased).
Assert `IsValid == false`. Assert error on `"StrategyConfig"`.

**CV-17 — StrategyConfig exceeding 2000 characters is invalid**

```
Validate_WhenStrategyConfigExceedsMaxLength_ReturnsInvalid
```
`StrategyConfig: new string('x', 2001)`. Assert `IsValid == false`. Assert error on `"StrategyConfig"`.

> Use `RolloutStrategy.None` and ensure `StrategyConfig` is non-empty — the 2000-char
> rule applies before the None-must-be-empty rule triggers. To exercise the length rule,
> use `RolloutStrategy.Percentage` as the `StrategyType` so the config field is expected.

---

## UpdateFlagRequestValidatorTests

**File:** `Bandera.Tests/Validators/UpdateFlagRequestValidatorTests.cs`

`UpdateFlagRequest` does not have a `Name` or `Environment` field — those come from
the route. The validator covers `StrategyType` and `StrategyConfig` only.

Instantiate directly: `var validator = new UpdateFlagRequestValidator();`

### Tests to implement

**UV-1 — StrategyType None with non-empty StrategyConfig is invalid**

```
Validate_WhenStrategyIsNoneButConfigIsProvided_ReturnsInvalid
```
Assert `IsValid == false`. Assert error on `"StrategyConfig"`.

**UV-2 — StrategyType None with null StrategyConfig passes**

```
Validate_WhenStrategyIsNoneAndConfigIsNull_ReturnsValid
```
Assert `IsValid == true`.

**UV-3 — StrategyType Percentage with valid config passes**

```
Validate_WhenStrategyIsPercentageWithValidConfig_ReturnsValid
```
`StrategyConfig: """{"percentage": 75}"""`. Assert `IsValid == true`.

**UV-4 — StrategyType Percentage with null config is invalid**

```
Validate_WhenStrategyIsPercentageWithNullConfig_ReturnsInvalid
```
Assert `IsValid == false`. Assert error on `"StrategyConfig"`.

**UV-5 — StrategyType Percentage with invalid config structure is invalid**

```
Validate_WhenStrategyIsPercentageWithInvalidConfig_ReturnsInvalid
```
`StrategyConfig: """{"roles": ["Admin"]}"""`.
Assert `IsValid == false`. Assert error on `"StrategyConfig"`.

**UV-6 — StrategyType RoleBased with valid config passes**

```
Validate_WhenStrategyIsRoleBasedWithValidConfig_ReturnsValid
```
`StrategyConfig: """{"roles": ["Admin"]}"""`. Assert `IsValid == true`.

**UV-7 — StrategyType RoleBased with null config is invalid**

```
Validate_WhenStrategyIsRoleBasedWithNullConfig_ReturnsInvalid
```
Assert `IsValid == false`. Assert error on `"StrategyConfig"`.

**UV-8 — StrategyType RoleBased with invalid config structure is invalid**

```
Validate_WhenStrategyIsRoleBasedWithInvalidConfig_ReturnsInvalid
```
`StrategyConfig: """{"percentage": 50}"""`.
Assert `IsValid == false`. Assert error on `"StrategyConfig"`.

**UV-9 — StrategyConfig exceeding 2000 characters is invalid**

```
Validate_WhenStrategyConfigExceedsMaxLength_ReturnsInvalid
```
`StrategyType: RolloutStrategy.Percentage`, `StrategyConfig: new string('x', 2001)`.
Assert `IsValid == false`. Assert error on `"StrategyConfig"`.

---

## EvaluationRequestValidatorTests

**File:** `Bandera.Tests/Validators/EvaluationRequestValidatorTests.cs`

Instantiate directly: `var validator = new EvaluationRequestValidator();`

### Tests to implement

**EV-1 — Empty FlagName is invalid**

```
Validate_WhenFlagNameIsEmpty_ReturnsInvalid
```
Assert `IsValid == false`. Assert error on `"FlagName"`.

**EV-2 — FlagName exceeding 100 characters is invalid**

```
Validate_WhenFlagNameExceedsMaxLength_ReturnsInvalid
```
`FlagName: new string('a', 101)`.
Assert `IsValid == false`. Assert error on `"FlagName"`.

**EV-3 — Empty UserId is invalid**

```
Validate_WhenUserIdIsEmpty_ReturnsInvalid
```
Assert `IsValid == false`. Assert error on `"UserId"`.

**EV-4 — UserId exceeding 256 characters is invalid**

```
Validate_WhenUserIdExceedsMaxLength_ReturnsInvalid
```
`UserId: new string('a', 257)`.
Assert `IsValid == false`. Assert error on `"UserId"`.

**EV-5 — Environment sentinel (None) is invalid**

```
Validate_WhenEnvironmentIsNone_ReturnsInvalid
```
`Environment: EnvironmentType.None`. Valid FlagName and UserId.
Assert `IsValid == false`. Assert error on `"Environment"`.

**EV-6 — Null UserRoles is invalid**

```
Validate_WhenUserRolesIsNull_ReturnsInvalid
```
`UserRoles: null`. Assert `IsValid == false`. Assert error on `"UserRoles"`.

**EV-7 — Empty UserRoles array is valid**

```
Validate_WhenUserRolesIsEmpty_ReturnsValid
```
`UserRoles: []`.
Assert `IsValid == true`.

> An empty roles array is a legitimate state — the user is authenticated but has
> no roles. This is different from `null`, which signals a missing payload field.

**EV-8 — UserRoles exceeding 50 entries is invalid**

```
Validate_WhenUserRolesExceedsMaxCount_ReturnsInvalid
```
`UserRoles: Enumerable.Range(0, 51).Select(i => $"role-{i}").ToList()`.
Assert `IsValid == false`. Assert error on `"UserRoles"`.

**EV-9 — Individual role exceeding 100 characters is invalid**

```
Validate_WhenSingleRoleExceedsMaxLength_ReturnsInvalid
```
`UserRoles: [new string('a', 101)]`.
Assert `IsValid == false`. Assert error on `"UserRoles[0]"`.

> FluentValidation's `RuleForEach` generates property names in the format
> `"UserRoles[0]"`, `"UserRoles[1]"`, etc. Check the actual property name in
> the `ValidationFailure` and assert accordingly.

**EV-10 — Valid request with all fields passes**

```
Validate_WhenAllFieldsAreValid_ReturnsValid
```
```csharp
var request = new EvaluationRequest
{
    FlagName = "dark-mode",
    UserId = "user-42",
    Environment = EnvironmentType.Production,
    UserRoles = ["Admin", "Editor"]
};
```
Assert `IsValid == true`.

---

## Acceptance Criteria

The implementation is complete when **all** of the following are true:

- [ ] `Bandera.Tests.csproj` references `FluentAssertions Version="7.*"`
- [ ] `FlagBuilder.cs` exists in `Bandera.Tests/Helpers/`
- [ ] All 7 test files exist in their specified locations
- [ ] Every test class and method carries `[Trait("Category", "Unit")]`
- [ ] All assertions use FluentAssertions — no `Assert.*` calls remain
- [ ] All test methods follow the `MethodName_StateUnderTest_ExpectedBehavior` naming convention
- [ ] `NoneStrategyTests`: all 4 tests pass
- [ ] `PercentageStrategyTests`: all 9 tests pass
- [ ] `RoleStrategyTests`: all 9 tests pass
- [ ] `FeatureEvaluatorTests`: all 4 tests pass
- [ ] `CreateFlagRequestValidatorTests`: all 17 tests pass
- [ ] `UpdateFlagRequestValidatorTests`: all 9 tests pass
- [ ] `EvaluationRequestValidatorTests`: all 10 tests pass
- [ ] `dotnet test Bandera.sln --filter "Category=Unit"` exits 0
- [ ] `dotnet build Bandera.sln -warnaserror` exits 0 with 0 warnings
- [ ] `dotnet csharpier check .` exits 0

**Total expected test count: 62 tests minimum.**

> The exact count may vary slightly if `[Theory]` tests are counted individually
> by the runner. The minimum is 62 distinct test methods.

---

## CI Verification

The existing `build-test` CI job already runs:

```yaml
dotnet test Bandera.sln --no-restore --filter "Category!=Integration"
```

This will pick up all `[Trait("Category", "Unit")]` tests automatically. No changes
to the CI YAML are required for this PR.

After the PR is merged, confirm the `build-test` job passes in GitHub Actions.

---

## Out of Scope

The following are explicitly **not** part of this PR:

- `Bandera` unit tests (requires mocked `IBanderaRepository`)
- Integration tests for any endpoint
- Seed data
- Evaluation logging
- `.http` smoke test file
- Any changes to `src/` production code
- Any changes to `FeatureEvaluationContextTests.cs`

---

## Instructions for Claude Code

Read the following files in order before writing any code:

1. `CLAUDE.md`
2. `Docs/roadmap.md`
3. `Docs/current-state.md`
4. `Docs/architecture.md`
5. This file

then implement in this order:

1. Add `FluentAssertions` to `Bandera.Tests/Bandera.Tests.csproj`
2. Create `Bandera.Tests/Helpers/FlagBuilder.cs`
3. Create `Bandera.Tests/Strategies/NoneStrategyTests.cs`
4. Create `Bandera.Tests/Strategies/PercentageStrategyTests.cs`
5. Create `Bandera.Tests/Strategies/RoleStrategyTests.cs`
6. Create `Bandera.Tests/Evaluation/FeatureEvaluatorTests.cs`
7. Create `Bandera.Tests/Validators/CreateFlagRequestValidatorTests.cs`
8. Create `Bandera.Tests/Validators/UpdateFlagRequestValidatorTests.cs`
9. Create `Bandera.Tests/Validators/EvaluationRequestValidatorTests.cs`
10. Run `dotnet build Bandera.sln -warnaserror` — fix all warnings before proceeding
11. Run `dotnet test Bandera.sln --filter "Category=Unit"` — all tests must pass
12. Run `dotnet format Bandera.sln` followed by `dotnet csharpier format .`

**DO NOT:**
- Modify any file in `src/` (Bandera.Domain, Application, Infrastructure, Api)
- Modify `FeatureEvaluationContextTests.cs`
- Use `Assert.*` — use FluentAssertions only
- Add `FluentValidation.AspNetCore` or any package not listed in this spec
- Use `.Transform()` in any validator code — it was removed in FluentValidation v12
- Add `try/catch` blocks anywhere in the test project

---

*Bandera | test/unit-and-integration-tests | Phase 1*
