# Specification: Evaluation Decision Logging — Phase 1

**Document:** `Docs/Decisions/evaluation-logging-PR#48/spec.md`
**Status:** Ready for Implementation
**Branch:** `feature/evaluation-logging`
**Phase:** 1 — Developer Experience
**Author:** Jose / Claude Architect Session
**Date:** 2026-04-09

---

## Table of Contents

- [User Story](#user-story)
- [Goals and Non-Goals](#goals-and-non-goals)
- [Architecture Context](#architecture-context)
- [Design Decisions](#design-decisions)
- [New Files](#new-files)
  - [EvaluationResult.cs](#evaluationresultcs)
- [Modified Files](#modified-files)
  - [BanderasService.cs](#banderacs)
- [New Test File](#new-test-file)
  - [BanderasServiceLoggingTests.cs](#banderaloggingtestscs)
- [Folder Structure After Implementation](#folder-structure-after-implementation)
- [No-Change Files](#no-change-files)
- [Acceptance Criteria](#acceptance-criteria)
- [Out of Scope](#out-of-scope)
- [Build and Format Sequence](#build-and-format-sequence)
- [Instructions for Claude Code](#instructions-for-claude-code)

---

## User Story

> As a developer running Banderas locally or in staging, I want every
> flag evaluation decision to produce a structured log entry so that I can
> understand why a flag evaluated to `true` or `false` without attaching a
> debugger or adding temporary code.

---

## Goals and Non-Goals

**Goals:**
- Produce a structured log entry for every completed evaluation in `IsEnabledAsync`
- Capture evaluation reason as a first-class machine-readable field — not inferred
  from message shape or the presence/absence of other fields
- Model evaluation outcomes as a discriminated union — typed, immutable, exhaustive
- Keep `FeatureEvaluator` pure — no logging, no side effects
- Log a hashed surrogate for `UserId` — not the raw value — to avoid PII in
  centralized telemetry and prevent high-cardinality dimension pressure
- Zero new NuGet packages for production code — `ILogger<T>` is already in the container
- Add `Microsoft.Extensions.Diagnostics.Testing` to `Banderas.Tests` for `FakeLogger<T>`
- Keep the new logging tests self-contained by using a tiny in-file repository fake
  instead of introducing a separate mocking-library dependency
- Phase 4-ready — `EvaluationResult` and `EvaluationReason` are the direct foundation
  for the trace endpoint
- Protect new logging behavior with targeted unit tests

**Non-Goals:**
- No database table for evaluation logs — that is Phase 4
- No changes to `IBanderasService` interface signatures
- No changes to `EvaluationController`
- No changes to `FeatureEvaluator`
- No Application Insights integration — that is Phase 1.5
- No logging of `FlagNotFoundException` as a union member — error path, not an
  evaluation outcome; a `LogWarning` before the throw is sufficient

---

## Architecture Context

Read this before writing any code.

```
POST /api/evaluate
      │
      ▼
EvaluationController          ← unchanged
      │
      ▼
Banderas             ← MODIFIED
  IsEnabledAsync()
      │
      ├─ Flag not found        → LogWarning → throw FlagNotFoundException
      │
      ├─ flag.IsEnabled=false  → build FlagDisabled result → LogResult → return false
      │
      └─ _evaluator.Evaluate() → build StrategyEvaluated result → LogResult → return bool
            │
            ▼
      FeatureEvaluator         ← UNCHANGED — pure, no ILogger, no side effects
```

**Functional principle in play:**
`FeatureEvaluator` is the pure core. `Banderas` is the imperative shell.
Side effects (logging) live in the shell only. The evaluator has no knowledge that
logging exists.

**Logging infrastructure:**
`ILogger<T>` is registered automatically by `WebApplication.CreateBuilder()`.
No `AddLogging()` call is needed in `DependencyInjection.cs`. No new production
packages required. When Application Insights is added in Phase 1.5, named message
template parameters automatically become queryable custom dimensions — no code
changes required.

---

## Design Decisions

### DD-1 — Discriminated Union with Shared `Reason` Field

`EvaluationResult` is modeled as `abstract record` with two `sealed record` subtypes:
`FlagDisabled` and `StrategyEvaluated`. Each subtype carries only the fields
relevant to its outcome.

`FlagDisabled` does not carry `StrategyType` or `IsEnabled` — it never reached a
strategy. The union enforces this at compile time.

`EvaluationReason` is added as a positional parameter on the **base** record so
every log entry carries a machine-readable reason field regardless of which branch
fired. Each subtype hardcodes its own reason — there is no way to construct a
`FlagDisabled` with `EvaluationReason.StrategyEvaluated`.

**Alternative rejected:** A flat `EvaluationResult` with nullable `StrategyType`
and nullable `IsEnabled` alongside a reason enum. Rejected because nullable
strategy-specific fields require defensive null checks at every callsite and remove
the type system's ability to communicate what happened. The union preserves
compile-time safety for outcome-specific data while still exposing `Reason` on
the base for shared querying.

---

### DD-2 — Logging in Service, Not in Evaluator

`FeatureEvaluator` is a Singleton with zero dependencies beyond its strategy registry.
Injecting `ILogger<FeatureEvaluator>` would couple a side-effectful concern to a
pure dispatch function. It would also create a blind spot: `FeatureEvaluator` never
sees the `FlagDisabled` path — the service short-circuits before calling it.
Logging in the evaluator would silently miss half the evaluation outcomes.

---

### DD-3 — Warning Before FlagNotFoundException, Not a Union Member

`FlagNotFoundException` is an error condition, not an evaluation decision. Log a
`LogWarning` immediately before throwing. This produces a log entry without
polluting the `EvaluationResult` model with an exceptional case.

---

### DD-4 — Structured Log Templates, Not Interpolated Strings

All `_logger` calls must use message templates with named parameters. Named
parameters become queryable custom dimensions in App Insights. Interpolated
strings produce a flat unstructured message that cannot be queried by field.

Both branches share the prefix `"Flag evaluation complete."` for consistent
App Insights filtering regardless of outcome.

```csharp
// CORRECT — named parameters become queryable dimensions in App Insights
_logger.LogInformation(
    "Flag evaluation complete. Flag={FlagName} Environment={Environment} " +
    "UserId={UserId} Reason={Reason} Result={Result} Strategy={StrategyType}",
    s.FlagName, s.Environment, HashUserId(s.UserId),
    s.Reason, s.IsEnabled ? "enabled" : "disabled", s.StrategyType);

// WRONG — destroys structured data
_logger.LogInformation($"Flag {s.FlagName} evaluated to {s.IsEnabled}");
```

---

### DD-5 — Switch Statement for LogResult + UnreachableException Default

`LogResult` is void-returning and side-effectful. A switch statement is the
correct tool — forcing a switch expression via discards would be obscure.

The `default` branch throws `UnreachableException`. `EvaluationResult` is
`abstract` but not `sealed` — the compiler cannot guarantee exhaustiveness. If a
third subtype is added without a corresponding log branch, the exception fires
immediately rather than silently producing no log entry.

---

### DD-6 — Hashed UserId Surrogate

Raw user IDs are PII. Once logs flow into Application Insights, raw IDs would be
stored in Azure telemetry and become subject to GDPR/CCPA erasure obligations.
They also create high-cardinality dimension pressure at scale.

`HashUserId` produces a short deterministic SHA256 fingerprint:

```csharp
private static string HashUserId(string userId)
{
    byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
    return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
}
```

- **Deterministic** — same user always produces same hash; log entries are
  correlatable within a session
- **Pseudonymized** — the raw ID is not logged, but this is not a security boundary;
  low-entropy IDs may still be guessable via dictionary attack
- **Fixed width** — the emitted value is always an 8-character lowercase hex string,
  which keeps the telemetry payload compact even though distinct users still produce
  distinct values

`HashUserId` is called inside `LogResult` — not when constructing the record.
The record carries the raw `UserId` so Phase 4 can choose independently what
to expose to callers. Privacy transformation is a logging concern, not a data
model concern.

`HashUserId` must be a `private static` method on `Banderas`. It must
not be added to `InputSanitizer` — sanitization is about cleaning control characters
at the HTTP boundary; pseudonymization is a separate concern and must not be mixed in.

---

### DD-7 — Targeted Unit Tests for Logging Behavior

The existing integration tests exercise HTTP outcomes. They do not verify which
log branch fires, whether `Reason` carries the correct value, or whether the hashed
UserId appears instead of the raw value. This new behavior is unprotected without
targeted unit tests.

`Banderas` is normally integration-tested because `FeatureEvaluator` is
a concrete sealed class. However, for logging branch tests we construct
`FeatureEvaluator` directly with a real `NoneStrategy` — no mocking required.
`IBanderasRepository` is replaced by a tiny in-file fake that implements only
the behavior these tests need. `ILogger<BanderasService>` is replaced by
`FakeLogger<BanderasService>` from `Microsoft.Extensions.Diagnostics.Testing`.

---

## New Files

### `EvaluationResult.cs`

**Location:** `Banderas.Application/Evaluation/EvaluationResult.cs`

```csharp
using Banderas.Domain.Enums;

namespace Banderas.Application.Evaluation;

/// <summary>
/// Machine-readable reason for a flag evaluation outcome.
/// Carried on every EvaluationResult subtype for structured log querying
/// and as the foundation for the Phase 4 trace endpoint.
/// </summary>
public enum EvaluationReason
{
    FlagDisabled,
    StrategyEvaluated,
}

/// <summary>
/// Discriminated union representing the outcome of a feature flag evaluation.
/// Each subtype carries only the data relevant to its specific outcome.
/// Reason is defined on the base so every log entry carries a queryable field
/// regardless of which branch fired.
/// </summary>
public abstract record EvaluationResult(
    string FlagName,
    EnvironmentType Environment,
    string UserId,
    EvaluationReason Reason
);

/// <summary>
/// The flag exists but IsEnabled is false. Strategy was never consulted.
/// </summary>
public sealed record FlagDisabled(
    string FlagName,
    EnvironmentType Environment,
    string UserId
) : EvaluationResult(FlagName, Environment, UserId, EvaluationReason.FlagDisabled);

/// <summary>
/// The flag is enabled and a rollout strategy produced the final decision.
/// </summary>
public sealed record StrategyEvaluated(
    string FlagName,
    EnvironmentType Environment,
    string UserId,
    bool IsEnabled,
    RolloutStrategy StrategyType
) : EvaluationResult(FlagName, Environment, UserId, EvaluationReason.StrategyEvaluated);
```

> **Why does each subtype hardcode its own `Reason`?**
> The subtype already *is* the reason — `FlagDisabled` can only ever mean
> `EvaluationReason.FlagDisabled`. Hardcoding it in the base constructor call means
> callers cannot accidentally pass the wrong reason. The type system enforces the
> contract with no runtime check required.

---

## Modified Files

### `BanderasService.cs`

**Location:** `Banderas.Application/Services/BanderasService.cs`

**Changes required:**
1. Add `using System.Diagnostics;` to the using block
2. Add `using Microsoft.Extensions.Logging;` to the using block
3. Add `using System.Security.Cryptography;` to the using block
4. Add `using System.Text;` to the using block
5. Add `ILogger<BanderasService>` constructor parameter and `_logger` field
6. Rewrite `IsEnabledAsync` to build and log an `EvaluationResult`
7. Add `private void LogResult(EvaluationResult result)` method
8. Add `private static string HashUserId(string userId)` method
9. Add `LogWarning` immediately before `throw new FlagNotFoundException`

**Constructor — updated:**

```csharp
private readonly IBanderasRepository _repository;
private readonly FeatureEvaluator _evaluator;
private readonly ILogger<BanderasService> _logger;

public BanderasService(
    IBanderasRepository repository,
    FeatureEvaluator evaluator,
    ILogger<BanderasService> logger)
{
    _repository = repository;
    _evaluator = evaluator;
    _logger = logger;
}
```

**`IsEnabledAsync` — full replacement:**

```csharp
public async Task<bool> IsEnabledAsync(
    string flagName,
    FeatureEvaluationContext context,
    CancellationToken ct = default)
{
    // Sanitize evaluation inputs before SHA256 hashing and HashSet lookups.
    var sanitizedContext = new FeatureEvaluationContext(
        userId: Validators.InputSanitizer.Clean(context.UserId) ?? context.UserId,
        userRoles: Validators.InputSanitizer.CleanCollection(context.UserRoles),
        environment: context.Environment
    );

    Flag? flag = await _repository.GetByNameAsync(flagName, sanitizedContext.Environment, ct);

    if (flag is null)
    {
        _logger.LogWarning(
            "Flag evaluation: not found. Flag={FlagName} Environment={Environment}",
            flagName,
            sanitizedContext.Environment);

        throw new FlagNotFoundException(flagName);
    }

    if (!flag.IsEnabled)
    {
        var result = new FlagDisabled(
            FlagName: flagName,
            Environment: sanitizedContext.Environment,
            UserId: sanitizedContext.UserId);

        LogResult(result);
        return false;
    }

    bool isEnabled = _evaluator.Evaluate(flag, sanitizedContext);

    var strategyResult = new StrategyEvaluated(
        FlagName: flagName,
        Environment: sanitizedContext.Environment,
        UserId: sanitizedContext.UserId,
        IsEnabled: isEnabled,
        StrategyType: flag.StrategyType);

    LogResult(strategyResult);
    return isEnabled;
}
```

**`LogResult` — new private method:**

```csharp
/// <summary>
/// Writes a structured log entry for a completed evaluation outcome.
/// UserId is hashed to a short SHA256 surrogate — never logged raw.
/// Each branch logs only the fields meaningful to that outcome.
/// </summary>
private void LogResult(EvaluationResult result)
{
    switch (result)
    {
        case FlagDisabled d:
            _logger.LogInformation(
                "Flag evaluation complete. Flag={FlagName} Environment={Environment} " +
                "UserId={UserId} Reason={Reason}",
                d.FlagName,
                d.Environment,
                HashUserId(d.UserId),
                d.Reason);
            break;

        case StrategyEvaluated s:
            _logger.LogInformation(
                "Flag evaluation complete. Flag={FlagName} Environment={Environment} " +
                "UserId={UserId} Reason={Reason} Result={Result} Strategy={StrategyType}",
                s.FlagName,
                s.Environment,
                HashUserId(s.UserId),
                s.Reason,
                s.IsEnabled ? "enabled" : "disabled",
                s.StrategyType);
            break;

        default:
            throw new UnreachableException(
                $"Unhandled EvaluationResult subtype: {result.GetType().Name}. " +
                "Add a logging branch for every new EvaluationResult subtype.");
    }
}
```

**`HashUserId` — new private static method:**

```csharp
/// <summary>
/// Returns a short deterministic SHA256 fingerprint of the raw UserId.
/// Deterministic: same input always produces same output — log entries are
/// correlatable across a session. Not reversible: raw ID cannot be recovered
/// from the hash.
/// </summary>
private static string HashUserId(string userId)
{
    byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
    return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
}
```

---

## New Test File

### `BanderasServiceLoggingTests.cs`

**Location:** `Banderas.Tests/Services/BanderasServiceLoggingTests.cs`

**New package — add to `Banderas.Tests.csproj`:**
```xml
<PackageReference Include="Microsoft.Extensions.Diagnostics.Testing" Version="9.*" />
```

> `FakeLogger<T>` captures log records in memory and exposes them via
> `FakeLogger.LatestRecord` and `FakeLogger.Collector.GetSnapshot()`. No mocking
> library is required for the repository — use a tiny in-file fake instead.

```csharp
using Banderas.Application.Evaluation;
using Banderas.Application.Services;
using Banderas.Application.Strategies;
using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.Interfaces;
using Banderas.Domain.ValueObjects;
using Microsoft.Extensions.Diagnostics.Testing;
using Microsoft.Extensions.Logging;

namespace Banderas.Tests.Services;

[Trait("Category", "Unit")]
public sealed class BanderasServiceLoggingTests
{
    private readonly TestBanderasRepository _repo;
    private readonly FeatureEvaluator _evaluator;
    private readonly FakeLogger<BanderasService> _fakeLogger;
    private readonly BanderasService _service;

    public BanderasServiceLoggingTests()
    {
        _repo = new TestBanderasRepository();

        // FeatureEvaluator is a concrete sealed class — construct directly.
        _evaluator = new FeatureEvaluator(new IRolloutStrategy[] { new NoneStrategy() });

        _fakeLogger = new FakeLogger<BanderasService>();
        _service = new BanderasService(_repo, _evaluator, _fakeLogger);
    }

    [Fact]
    public async Task IsEnabledAsync_DisabledFlag_LogsFlagDisabledReason()
    {
        // Arrange
        _repo.FlagToReturn = new Flag(
            "my-flag",
            EnvironmentType.Development,
            isEnabled: false,
            RolloutStrategy.None,
            null);

        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        await _service.IsEnabledAsync("my-flag", context);

        // Assert
        var record = _fakeLogger.LatestRecord;
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Contains("Reason=FlagDisabled", record.Message);
        Assert.DoesNotContain("Strategy", record.Message);
    }

    [Fact]
    public async Task IsEnabledAsync_EnabledFlag_LogsStrategyEvaluatedReason()
    {
        // Arrange
        _repo.FlagToReturn = new Flag(
            "my-flag",
            EnvironmentType.Development,
            isEnabled: true,
            RolloutStrategy.None,
            null);

        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        await _service.IsEnabledAsync("my-flag", context);

        // Assert
        var record = _fakeLogger.LatestRecord;
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Contains("Reason=StrategyEvaluated", record.Message);
        Assert.Contains("Strategy=None", record.Message);
    }

    [Fact]
    public async Task IsEnabledAsync_AnyOutcome_LogsHashedUserIdNotRaw()
    {
        // Arrange
        _repo.FlagToReturn = new Flag(
            "my-flag",
            EnvironmentType.Development,
            isEnabled: false,
            RolloutStrategy.None,
            null);

        const string rawUserId = "user-abc-123";
        var context = new FeatureEvaluationContext(rawUserId, [], EnvironmentType.Development);

        // Act
        await _service.IsEnabledAsync("my-flag", context);

        // Assert — raw UserId must not appear anywhere in the log message
        var record = _fakeLogger.LatestRecord;
        Assert.DoesNotContain(rawUserId, record.Message);
    }

    [Fact]
    public async Task IsEnabledAsync_FlagNotFound_LogsWarningBeforeException()
    {
        // Arrange
        _repo.FlagToReturn = null;

        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act + Assert — exception is expected
        await Assert.ThrowsAsync<FlagNotFoundException>(
            () => _service.IsEnabledAsync("missing-flag", context));

        // Warning log must have fired before the throw
        var record = _fakeLogger.LatestRecord;
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("missing-flag", record.Message);
    }

    private sealed class TestBanderasRepository : IBanderasRepository
    {
        public Flag? FlagToReturn { get; set; }

        public Task<Flag?> GetByNameAsync(
            string name,
            EnvironmentType environment,
            CancellationToken ct = default) => Task.FromResult(FlagToReturn);

        public Task<bool> ExistsAsync(
            string name,
            EnvironmentType environment,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Flag>> GetAllAsync(
            EnvironmentType environment,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task AddAsync(Flag flag, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
```

---

## Folder Structure After Implementation

```
Banderas.Application/
  Evaluation/
    EvaluationResult.cs         ← NEW (EvaluationReason enum + abstract record
                                       + FlagDisabled + StrategyEvaluated)
    FeatureEvaluator.cs         ← unchanged

  Services/
    BanderasService.cs       ← MODIFIED (constructor, IsEnabledAsync,
                                            LogResult, HashUserId)

Banderas.Tests/
  Services/
    BanderasServiceLoggingTests.cs   ← NEW (4 unit tests)
```

---

## No-Change Files

| File | Reason |
|------|--------|
| `FeatureEvaluator.cs` | Pure function — no side effects, no ILogger |
| `IBanderasService.cs` | Interface signatures unchanged |
| `EvaluationController.cs` | Thin controller — no logging concerns |
| `DependencyInjection.cs` (Application) | ILogger auto-registered by the host |
| `DependencyInjection.cs` (Infrastructure) | No changes required |
| All existing files in `Banderas.Tests` | Existing tests must not be modified |
| All files in `Banderas.Tests.Integration` | Integration tests not in scope |

---

## Acceptance Criteria

### AC-1 — `EvaluationResult.cs` exists and compiles

- [ ] `EvaluationReason` enum is defined in the same file with two members:
      `FlagDisabled` and `StrategyEvaluated`
- [ ] `EvaluationResult` is an `abstract record` with four positional parameters:
      `FlagName` (`string`), `Environment` (`EnvironmentType`), `UserId` (`string`),
      `Reason` (`EvaluationReason`)
- [ ] `FlagDisabled` is a `sealed record` inheriting `EvaluationResult`; carries no
      additional properties; hardcodes `EvaluationReason.FlagDisabled` in the base
      constructor call
- [ ] `StrategyEvaluated` is a `sealed record` inheriting `EvaluationResult`; carries
      `IsEnabled` (`bool`) and `StrategyType` (`RolloutStrategy`); hardcodes
      `EvaluationReason.StrategyEvaluated` in the base constructor call
- [ ] File is in namespace `Banderas.Application.Evaluation`
- [ ] File is at `Banderas.Application/Evaluation/EvaluationResult.cs`

### AC-2 — `Banderas` constructor is updated

- [ ] `using System.Diagnostics;` added to the using block
- [ ] `using Microsoft.Extensions.Logging;` added to the using block
- [ ] `using System.Security.Cryptography;` added to the using block
- [ ] `using System.Text;` added to the using block
- [ ] Constructor has three parameters: `IBanderasRepository`, `FeatureEvaluator`,
      `ILogger<BanderasService>`
- [ ] `_logger` stored as a `private readonly` field

### AC-3 — `IsEnabledAsync` builds and logs an `EvaluationResult`

- [ ] Sanitization block is unchanged from the current implementation
- [ ] Repository return value declared as `Flag?` — not `Flag` (prevents CS8600)
- [ ] When flag is not found: `LogWarning` fires with `FlagName` and `Environment`
      as named template parameters; `FlagNotFoundException` thrown immediately after
- [ ] When `flag.IsEnabled` is false: `FlagDisabled` record constructed with named
      positional syntax and passed to `LogResult`; method returns `false`
- [ ] When `_evaluator.Evaluate` runs: `StrategyEvaluated` record constructed with
      `IsEnabled = isEnabled` and `StrategyType = flag.StrategyType`; passed to
      `LogResult`; method returns `isEnabled`
- [ ] No log call uses string interpolation

### AC-4 — `LogResult` is correct

- [ ] Signature: `private void LogResult(EvaluationResult result)`
- [ ] Uses a `switch` statement dispatching on `EvaluationResult` subtypes
- [ ] `FlagDisabled` branch: `LogInformation` with named fields `FlagName`,
      `Environment`, `UserId` (hashed), `Reason`
- [ ] `StrategyEvaluated` branch: `LogInformation` with named fields `FlagName`,
      `Environment`, `UserId` (hashed), `Reason`, `Result`, `StrategyType`
- [ ] Both branches use the shared prefix `"Flag evaluation complete."`
- [ ] `default` branch throws `UnreachableException` naming the unhandled subtype
- [ ] `UserId` is never passed to `_logger` directly — always via `HashUserId`

### AC-5 — `HashUserId` is correct

- [ ] Signature: `private static string HashUserId(string userId)`
- [ ] Uses `SHA256.HashData(Encoding.UTF8.GetBytes(userId))`
- [ ] Returns `Convert.ToHexString(bytes)[..8].ToLowerInvariant()`
- [ ] Method is on `Banderas` — not on `InputSanitizer`

### AC-6 — `FeatureEvaluator` is unchanged

- [ ] `FeatureEvaluator.cs` is byte-for-byte identical to its current committed state
- [ ] No `ILogger` injected into `FeatureEvaluator`
- [ ] DI lifetime remains Singleton

### AC-7 — Build and existing tests pass

- [ ] `dotnet build Banderas.sln` → 0 errors, 0 warnings
- [ ] `dotnet test --filter "Category=Unit"` → 75 existing + 4 new = 79/79 passing
- [ ] `dotnet test --filter "Category=Integration"` → 31/31 passing (unchanged)
- [ ] `dotnet csharpier check .` → 0 violations

### AC-8 — New unit tests pass and cover logging behavior

- [ ] `BanderasServiceLoggingTests` at `Banderas.Tests/Services/BanderasServiceLoggingTests.cs`
- [ ] `Microsoft.Extensions.Diagnostics.Testing` added to `Banderas.Tests.csproj`
- [ ] All four tests carry `[Trait("Category", "Unit")]`
- [ ] Tests use a tiny in-file `IBanderasRepository` fake; no additional mocking
      library is introduced
- [ ] `DisabledFlag` test: asserts rendered log contains `Reason=FlagDisabled` and no
      `Strategy` field
- [ ] `EnabledFlag` test: asserts rendered log contains `Reason=StrategyEvaluated` and
      `Strategy=None`
- [ ] `HashedUserId` test: asserts raw UserId string does not appear in log message
- [ ] `FlagNotFound` test: asserts `LogLevel.Warning` fires before the exception

### AC-9 — Log output is observable locally

- [ ] `POST /api/evaluate` with valid enabled flag → console shows:
      `Flag evaluation complete. Flag=... Environment=... UserId=<8-char-hex> Reason=StrategyEvaluated Result=enabled Strategy=...`
- [ ] With a disabled flag → console shows:
      `Flag evaluation complete. Flag=... Environment=... UserId=<8-char-hex> Reason=FlagDisabled`
      (no `Result` or `Strategy` fields)
- [ ] With a nonexistent flag → console shows a `warn` entry:
      `Flag evaluation: not found. Flag=... Environment=...`

---

## Out of Scope

- No `EvaluationLog` database table — Phase 4
- No Application Insights SDK — Phase 1.5
- No `GET /api/flags/{name}/trace` endpoint — Phase 4
- No changes to `appsettings.json` log level configuration
- No Serilog or any third-party logging library
- No modifications to existing unit or integration test files

---

## Build and Format Sequence

Always run in this exact order:

```bash
dotnet build Banderas.sln
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet csharpier format .
dotnet csharpier check .
```

CSharpier is the final formatting authority.

---

## Instructions for Claude Code

Read this entire document before writing any code.

**Do not:**
- Add `ILogger` to `FeatureEvaluator`
- Modify `IBanderasService`
- Modify `EvaluationController`
- Modify any existing test file
- Use string interpolation in any `_logger` call
- Use `?` (nullable) on any property of `EvaluationResult` or its subtypes
- Pass raw `UserId` to any `_logger` call — always via `HashUserId`
- Add `HashUserId` or any privacy concern to `InputSanitizer`

**Do:**
- Create `EvaluationResult.cs` exactly as specified in [New Files](#new-files)
- Modify `BanderasService.cs` exactly as specified in [Modified Files](#modified-files)
- Create `BanderasServiceLoggingTests.cs` exactly as specified in [New Test File](#new-test-file)
- Add `Microsoft.Extensions.Diagnostics.Testing` to `Banderas.Tests.csproj`
- Use the in-file repository fake shown in the test spec; do not add a separate
  mocking-library dependency just for these tests
- Preserve the entire sanitization block in `IsEnabledAsync` — do not simplify or remove it
- Use named positional record syntax when constructing `FlagDisabled` and `StrategyEvaluated`
- Verify AC-9 manually in the devcontainer before marking complete

---

*Banderas | feature/evaluation-logging | Phase 1 — Developer Experience | v2*
