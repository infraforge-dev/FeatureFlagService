# Evaluation Engine — Implementation Spec

**Branch:** `feature/evaluation-engine`  
**Phase:** 0 — Foundation  
**Status:** Ready for implementation
**Implementation notes:** `docs/Decisions/evaluation-engine/implementation-notes.md`

[Pull Request #22](https://github.com/amodelandme/Bandera/pull/22)

---

## 1. Purpose of This Document

This is the implementation spec for the Bandera evaluation engine. It is intended to be read by Claude Code at the start of the implementation session before any code is written.

It covers every class, interface, design decision, and wiring detail needed to implement the evaluation layer from scratch — without ambiguity.

> **SCOPE**  
> This spec covers: `FeatureEvaluator`, `PercentageStrategy`, `RoleStrategy`, `NoneStrategy`, `DependencyInjection` extension methods, and the `Bandera` orchestration class.  
> It does **not** cover EF Core setup, controllers, or Swagger — those are separate sessions.

---

## 2. Architecture Context

Before writing any code, understand the dependency rules that govern this project. Violating these breaks the Clean Architecture boundary.

| Layer | Responsibility |
|---|---|
| `Bandera.Domain` | Entities, enums, value objects, interfaces. Zero outward dependencies. |
| `Bandera.Application` | Use cases, service interfaces, evaluator, strategies. Depends on Domain only. |
| `Bandera.Infrastructure` | EF Core, repositories. Depends on Domain + Application. |
| `Bandera.Api` | Controllers, Program.cs, DI wiring. Depends on Application + Infrastructure. |
| `Bandera.Tests` | Unit tests. Depends on Domain + Application. |

> **RULE**  
> `IRolloutStrategy` lives in **Domain**. `FeatureEvaluator`, `PercentageStrategy`, `RoleStrategy`, `NoneStrategy`, and `IBanderaService` live in **Application**. `DependencyInjection` extension methods live in Application and Infrastructure respectively. Do not move any of these.

---

## 3. Interfaces — Already Defined

The following interfaces are already committed to the repo. Do **NOT** redefine or modify them. All new code must implement or consume them as-is.

### 3.1 IRolloutStrategy (Domain)

**Location:** `Bandera.Domain/Interfaces/IRolloutStrategy.cs`

```csharp
namespace Bandera.Domain.Interfaces;

public interface IRolloutStrategy
{
    RolloutStrategy StrategyType { get; }
    bool Evaluate(Flag flag, FeatureEvaluationContext context);
}
```

> **NOTE**  
> `StrategyType` is a property on the interface — not in the original stub. **Add it.**  
> It is required so `FeatureEvaluator` can build a dictionary keyed by `RolloutStrategy` enum value. Without it, the registry dispatch pattern cannot work.

---

### 3.2 IBanderaService (Application)

**Location:** `Bandera.Application/Interfaces/IBanderaService.cs`

```csharp
namespace Bandera.Application.Interfaces;

public interface IBanderaService
{
    Flag GetFlag(string name, EnvironmentType environment);
    bool IsEnabled(string flagName, FeatureEvaluationContext context);
}
```

---

### 3.3 IBanderaRepository (Domain) — CREATE THIS FILE

**Location:** `Bandera.Domain/Interfaces/IBanderaRepository.cs`

This interface belongs in Domain so Infrastructure can implement it without creating an illegal dependency. This is the standard Clean Architecture repository pattern.

```csharp
namespace Bandera.Domain.Interfaces;

public interface IBanderaRepository
{
    Flag? GetByName(string name, EnvironmentType environment);
    IReadOnlyList<Flag> GetAll(EnvironmentType environment);
}
```

---

## 4. Classes to Implement

### 4.1 PercentageStrategy

| Property | Value |
|---|---|
| File location | `Bandera.Application/Strategies/PercentageStrategy.cs` |
| Implements | `IRolloutStrategy` |
| `StrategyType` property | `RolloutStrategy.Percentage` |
| Purpose | Deterministically assign users to a rollout percentage using SHA256 hashing |

**Config shape (JSON stored in `Flag.StrategyConfig`):**

```json
{ "percentage": 30 }
```

**Algorithm — step by step:**

1. Deserialize `Flag.StrategyConfig` into a `PercentageConfig` record.
2. If config is null or `Percentage` is outside 0–100, return `false` (fail closed).
3. Build input string: `$"{context.UserId}:{flag.Name}"`
4. Hash that string using `SHA256.HashData(Encoding.UTF8.GetBytes(input))`.
5. Convert first 4 bytes to `uint` via `BitConverter.ToUInt32(hashBytes, 0)`.
6. Compute `bucket = hashValue % 100`.
7. Return `bucket < (uint)config.Percentage`.

> **WHY `uint` and not `int`**  
> `BitConverter.ToUInt32` returns an unsigned 32-bit integer. Use `uint` throughout the comparison. A signed `int` can produce negative values after conversion, and a negative bucket number makes the less-than comparison silently incorrect. `uint` eliminates that class of bug entirely.

> **WHY include `flag.Name` in the hash input**  
> Hashing `UserId` alone means the same 30% of users are in every rollout. Including the flag name ensures each flag produces an independent user distribution. User A might be in bucket 12 for flag X but bucket 74 for flag Y.

**`PercentageConfig` record:**

Define as a private nested record inside `PercentageStrategy`. Keep it internal to the Application layer.

```csharp
private sealed record PercentageConfig(int Percentage);
```

**Full implementation:**

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.Interfaces;
using Bandera.Domain.ValueObjects;

namespace Bandera.Application.Strategies;

public sealed class PercentageStrategy : IRolloutStrategy
{
    public RolloutStrategy StrategyType => RolloutStrategy.Percentage;

    public bool Evaluate(Flag flag, FeatureEvaluationContext context)
    {
        var config = JsonSerializer.Deserialize<PercentageConfig>(flag.StrategyConfig);

        if (config is null || config.Percentage is < 0 or > 100)
            return false;

        var input = $"{context.UserId}:{flag.Name}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var bucket = BitConverter.ToUInt32(hashBytes, 0) % 100;

        return bucket < (uint)config.Percentage;
    }

    private sealed record PercentageConfig(int Percentage);
}
```

---

### 4.2 RoleStrategy

| Property | Value |
|---|---|
| File location | `Bandera.Application/Strategies/RoleStrategy.cs` |
| Implements | `IRolloutStrategy` |
| `StrategyType` property | `RolloutStrategy.RoleBased` |
| Purpose | Grant access to users whose roles intersect with the configured allowed roles |

**Config shape (JSON stored in `Flag.StrategyConfig`):**

```json
{ "roles": ["admin", "beta-tester"] }
```

**Algorithm — step by step:**

1. Deserialize `Flag.StrategyConfig` into a `RoleConfig` record.
2. If config is null or `config.Roles` is null or empty, return `false` (fail closed).
3. Build a `HashSet<string>` from `config.Roles` using `StringComparer.OrdinalIgnoreCase`.
4. Return `context.UserRoles.Any(role => allowedRoles.Contains(role))`.

> **OR logic — not AND**  
> The strategy uses OR logic: the user must have at least ONE of the configured roles. AND logic is rare in practice and usually signals that role granularity should be improved upstream. OR is the correct default for a feature flag system.

> **WHY `HashSet` + `OrdinalIgnoreCase`**  
> `HashSet` gives O(1) lookups vs O(n) for `List` — important when a user has many roles. `OrdinalIgnoreCase` handles mismatches like `"Admin"` vs `"admin"` that are common between identity providers and config files. Two separate bugs prevented by one line.

**Full implementation:**

```csharp
using System.Text.Json;
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.Interfaces;
using Bandera.Domain.ValueObjects;

namespace Bandera.Application.Strategies;

public sealed class RoleStrategy : IRolloutStrategy
{
    public RolloutStrategy StrategyType => RolloutStrategy.RoleBased;

    public bool Evaluate(Flag flag, FeatureEvaluationContext context)
    {
        var config = JsonSerializer.Deserialize<RoleConfig>(flag.StrategyConfig);

        if (config is null || config.Roles is null || config.Roles.Count == 0)
            return false;

        var allowedRoles = new HashSet<string>(
            config.Roles,
            StringComparer.OrdinalIgnoreCase);

        return context.UserRoles.Any(role => allowedRoles.Contains(role));
    }

    private sealed record RoleConfig(List<string> Roles);
}
```

---

### 4.3 NoneStrategy (Passthrough)

When `RolloutStrategy` is `None`, the flag is on for everyone. Rather than handling this as a special case inside `FeatureEvaluator` (which would be a conditional, not a strategy), implement a `NoneStrategy` that always returns `true`.

| Property | Value |
|---|---|
| File location | `Bandera.Application/Strategies/NoneStrategy.cs` |
| Implements | `IRolloutStrategy` |
| `StrategyType` property | `RolloutStrategy.None` |
| Purpose | Passthrough — flag is on, no targeting rules apply |

```csharp
using Bandera.Domain.Entities;
using Bandera.Domain.Interfaces;
using Bandera.Domain.ValueObjects;

namespace Bandera.Application.Strategies;

public sealed class NoneStrategy : IRolloutStrategy
{
    public RolloutStrategy StrategyType => RolloutStrategy.None;

    public bool Evaluate(Flag flag, FeatureEvaluationContext context) => true;
}
```

> **DESIGN NOTE**  
> `NoneStrategy` keeps `FeatureEvaluator` free of conditionals. Every `RolloutStrategy` value maps to a strategy class. The evaluator never needs to know about special cases — it just dispatches. This is the Open/Closed Principle in practice: add behavior by adding classes, not by modifying existing ones.

---

### 4.4 FeatureEvaluator

| Property | Value |
|---|---|
| File location | `Bandera.Application/Evaluation/FeatureEvaluator.cs` |
| Purpose | Dispatch evaluation to the correct `IRolloutStrategy` based on `Flag.StrategyType` |
| DI Lifetime | Singleton — stateless, safe to share across requests |
| Dependencies | `IEnumerable<IRolloutStrategy>` — injected by DI container |

**How registry dispatch works:**

The constructor receives all registered `IRolloutStrategy` implementations via `IEnumerable<IRolloutStrategy>`. It builds a dictionary keyed by each strategy's `StrategyType` property. At evaluation time it looks up the correct strategy in O(1) and delegates.

```csharp
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.Interfaces;
using Bandera.Domain.ValueObjects;

namespace Bandera.Application.Evaluation;

public sealed class FeatureEvaluator
{
    private readonly Dictionary<RolloutStrategy, IRolloutStrategy> _strategies;

    public FeatureEvaluator(IEnumerable<IRolloutStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.StrategyType);
    }

    public bool Evaluate(Flag flag, FeatureEvaluationContext context)
    {
        if (!flag.IsEnabled)
            return false;

        if (!_strategies.TryGetValue(flag.StrategyType, out var strategy))
            return false; // unknown strategy = fail closed

        return strategy.Evaluate(flag, context);
    }
}
```

> **SHORT CIRCUIT**  
> The check `if (!flag.IsEnabled) return false` must come before strategy dispatch. A disabled flag should never reach a strategy. This is intentional and must not be removed.

> **UNKNOWN STRATEGY = FAIL CLOSED**  
> If the dictionary does not contain the flag's `StrategyType` (e.g. a future enum value not yet implemented), the evaluator returns `false`. Do not throw an exception here — a missing strategy should disable the feature silently, not crash the request.

---

### 4.5 Bandera

| Property | Value |
|---|---|
| File location | `Bandera.Application/Services/BanderaService.cs` |
| Implements | `IBanderaService` |
| DI Lifetime | **Scoped** — depends on `IBanderaRepository` which is Scoped |
| Dependencies | `IBanderaRepository`, `FeatureEvaluator` |
| Purpose | Orchestrate: fetch flag from repository, pass to evaluator, return result |

> **LIFETIME WARNING**  
> `Bandera` must be **Scoped**, not Singleton. It depends on `IBanderaRepository` which wraps EF Core `DbContext` — a Scoped service. If `Bandera` were Singleton, the repository would be captured inside it and never released. ASP.NET Core will throw an `InvalidOperationException` at startup if this is wrong.

```csharp
using Bandera.Application.Evaluation;
using Bandera.Application.Interfaces;
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.Interfaces;
using Bandera.Domain.ValueObjects;

namespace Bandera.Application.Services;

public sealed class BanderaService : IBanderaService
{
    private readonly IBanderaRepository _repository;
    private readonly FeatureEvaluator _evaluator;

    public BanderaService(
        IBanderaRepository repository,
        FeatureEvaluator evaluator)
    {
        _repository = repository;
        _evaluator = evaluator;
    }

    public Flag GetFlag(string name, EnvironmentType environment)
    {
        return _repository.GetByName(name, environment)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");
    }

    public bool IsEnabled(string flagName, FeatureEvaluationContext context)
    {
        var flag = _repository.GetByName(flagName, context.Environment);

        if (flag is null || !flag.IsEnabled)
            return false;

        return _evaluator.Evaluate(flag, context);
    }
}
```

---

## 5. Dependency Injection Wiring

Each layer owns its own DI registration. `Program.cs` calls the extension methods — it never references concrete types directly.

### 5.1 Application Layer — DependencyInjection.cs

**Location:** `Bandera.Application/DependencyInjection.cs`

```csharp
using Bandera.Application.Evaluation;
using Bandera.Application.Interfaces;
using Bandera.Application.Services;
using Bandera.Application.Strategies;
using Bandera.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bandera.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Strategies — Singleton: stateless, safe to share across requests
        services.AddSingleton<IRolloutStrategy, NoneStrategy>();
        services.AddSingleton<IRolloutStrategy, PercentageStrategy>();
        services.AddSingleton<IRolloutStrategy, RoleStrategy>();

        // Evaluator — Singleton: depends only on Singleton strategies
        services.AddSingleton<FeatureEvaluator>();

        // Service — Scoped: depends on Scoped repository
        services.AddScoped<IBanderaService, BanderaService>();

        return services;
    }
}
```

---

### 5.2 Infrastructure Layer — DependencyInjection.cs (stub)

**Location:** `Bandera.Infrastructure/DependencyInjection.cs`

EF Core and `DbContext` are out of scope for this session. Create this stub so the solution compiles and the wiring pattern is in place.

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bandera.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TODO: services.AddDbContext<BanderaDbContext>(...)
        // TODO: services.AddScoped<IBanderaRepository, BanderaRepository>()

        return services;
    }
}
```

---

### 5.3 Program.cs — Composition Root

**Location:** `Bandera.Api/Program.cs`

Replace the two commented-out lines that already exist in the file:

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

---

## 6. Folder Structure After Implementation

```
Bandera.Application/
  DependencyInjection.cs
  Evaluation/
    FeatureEvaluator.cs
  Interfaces/
    IBanderaService.cs
  Services/
    BanderaService.cs
  Strategies/
    NoneStrategy.cs
    PercentageStrategy.cs
    RoleStrategy.cs

Bandera.Domain/
  Interfaces/
    IRolloutStrategy.cs          ← UPDATE: add StrategyType property
    IBanderaRepository.cs    ← CREATE

Bandera.Infrastructure/
  DependencyInjection.cs         ← CREATE (stub only)
```

---

## 7. Design Decisions — Quick Reference

| Decision | Rationale |
|---|---|
| Registry dispatch over `switch` | Adding a new strategy requires only a new class and one DI registration line. `FeatureEvaluator` never changes. Satisfies the Open/Closed Principle. |
| SHA256 over `GetHashCode()` | `GetHashCode()` is randomized per process in .NET. It produces different values across restarts. SHA256 is stable, deterministic, and trusted. |
| `uint` for bucket comparison | SHA256 bytes converted to `int` can be negative. A negative bucket silently breaks the less-than comparison. `uint` is always non-negative — safe by type. |
| `UserId + FlagName` in hash | Hashing `UserId` alone means the same users are always in every rollout. Including the flag name creates an independent distribution per flag. |
| OR logic in `RoleStrategy` | Most role-based flags mean "any of these roles." AND logic usually signals that role design needs improvement upstream, not that the strategy should be more complex. |
| `HashSet` for role lookup | O(1) lookup vs O(n) for `List`. `OrdinalIgnoreCase` handles provider mismatches like `"Admin"` vs `"admin"` silently and correctly. |
| Fail closed on bad config | Both strategies return `false` when config is null, malformed, or out of range. A broken flag disables the feature — it does not grant access. |
| `NoneStrategy` as a class | Avoids a conditional in `FeatureEvaluator`. Every enum value maps to a strategy. The evaluator stays free of branching logic. |
| Scoped service lifetime | `Bandera` depends on `IBanderaRepository` which wraps EF Core `DbContext` — a Scoped service. Matching lifetimes prevents the captive dependency bug. |
| `IBanderaRepository` in Domain | Infrastructure implements it. Domain defines it. Application consumes it. This is the Dependency Inversion Principle — high-level policy does not depend on low-level detail. |

---

## 8. Instructions for Claude Code

Read the following before writing any code:

- `CLAUDE.md`
- `docs/roadmap.md`
- `docs/architecture.md`
- `docs/current-state.md`
- This file

Then implement in this order:

1. Update `IRolloutStrategy` in Domain — add the `StrategyType` property
2. Create `IBanderaRepository` in Domain
3. Implement `NoneStrategy`, `PercentageStrategy`, and `RoleStrategy` in `Application/Strategies/`
4. Implement `FeatureEvaluator` in `Application/Evaluation/`
5. Implement `Bandera` in `Application/Services/`
6. Create `DependencyInjection.cs` in Application with `AddApplication()` extension method
7. Create `DependencyInjection.cs` stub in Infrastructure with `AddInfrastructure()` extension method
8. Wire up `AddApplication()` and `AddInfrastructure()` in `Program.cs`
9. Verify the solution builds: `dotnet build Bandera.sln`

> **DO NOT**  
> Do not implement EF Core, `DbContext`, or the real repository — that is a separate session.  
> Do not add controllers or Swagger changes.  
> Do not modify `Flag.cs`, `FeatureEvaluationContext.cs`, or any existing domain entity.  
> Do not change `RolloutStrategy.cs` or `EnvironmentType.cs`.  
> Do not add NuGet packages without confirming first.

---

*Bandera | feature/evaluation-engine | Phase 0*
