# Application Insights Integration — Spec

**Document:** `Docs/Decisions/application-insights-pr51/spec.md`
**Branch:** `feature/application-insights`
**Phase:** 1.5 — Azure Foundation + AI Integration
**PR:** #51
**Status:** Ready for Implementation
**Author:** Jose / Claude Architect Session
**Date:** 2026-04-19

---

## Table of Contents

- [User Story](#user-story)
- [Background & Goals](#background--goals)
- [Non-Goals](#non-goals)
- [Design Decisions](#design-decisions)
  - [DD-1: ITelemetryService — Interface in Application, Implementation in Infrastructure](#dd-1-itelemetryservice--interface-in-application-implementation-in-infrastructure)
  - [DD-2: Custom Event over Trace for Evaluation Telemetry](#dd-2-custom-event-over-trace-for-evaluation-telemetry)
  - [DD-3: Connection String from Key Vault, Not Code](#dd-3-connection-string-from-key-vault-not-code)
  - [DD-4: Auto-Capture for Request, Exception, and Dependency Telemetry](#dd-4-auto-capture-for-request-exception-and-dependency-telemetry)
  - [DD-5: No ITelemetryService Call in FeatureEvaluator](#dd-5-no-itelemetryservice-call-in-featureevaluator)
- [Scope](#scope)
- [File Layout](#file-layout)
- [New Files](#new-files)
  - [ITelemetryService.cs](#itelemetryservicecs)
  - [ApplicationInsightsTelemetryService.cs](#applicationinsightstelemetryservicecs)
- [Modified Files](#modified-files)
  - [BanderasService.cs](#banderasservicecs)
  - [Infrastructure/DependencyInjection.cs](#infrastructuredependencyinjectioncs)
  - [Program.cs](#programcs)
  - [appsettings.json](#appsettingsjson)
  - [appsettings.Development.json](#appsettingsdevelopmentjson)
- [Acceptance Criteria](#acceptance-criteria)
  - [AC-1: ITelemetryService Interface](#ac-1-itelemetryservice-interface)
  - [AC-2: ApplicationInsightsTelemetryService Implementation](#ac-2-applicationinsightstelemetryservice-implementation)
  - [AC-3: BanderasService Calls ITelemetryService](#ac-3-banderasservice-calls-itelemetryservice)
  - [AC-4: Auto-Capture Wired in Program.cs](#ac-4-auto-capture-wired-in-programcs)
  - [AC-5: Connection String Configuration](#ac-5-connection-string-configuration)
  - [AC-6: NullTelemetryService for Testing](#ac-6-nulltelemetryservice-for-testing)
  - [AC-7: Build and Tests Pass](#ac-7-build-and-tests-pass)
- [Learning Opportunities](#learning-opportunities)
- [Known Constraints](#known-constraints)
- [Out of Scope](#out-of-scope)
- [Definition of Done](#definition-of-done)

---

## User Story

> As an engineer operating Banderas in Azure, I want structured telemetry flowing
> into Application Insights — including request traces, exceptions, Postgres query
> timings, and a custom `flag.evaluated` event per evaluation — so that I can
> monitor API health, debug failures, and understand which flags are evaluated most
> frequently, all from the Azure portal without reading raw logs.

---

## Background & Goals

PR #48 introduced structured evaluation decision logging via `ILogger<BanderasService>`.
Log output flows to the console — visible locally, gone on container restart, and
unsearchable across time.

Application Insights is an Azure-native telemetry sink that persists telemetry,
makes it queryable via Kusto (KQL), and provides dashboards for request health,
exception tracking, and dependency performance.

This PR wires Application Insights into Banderas with two goals:

1. **Auto-captured telemetry** — every HTTP request, unhandled exception, and
   outbound Postgres query is recorded with zero manual instrumentation.
2. **Custom evaluation events** — every flag evaluation emits a named
   `flag.evaluated` event with structured properties (flag name, result, strategy,
   environment) that can be queried by flag name over time.

---

## Non-Goals

- Moving the Azure OpenAI connection string to Key Vault — Phase 1.5 PR #52
- Kusto (KQL) query setup or Application Insights dashboard configuration
- Distributed tracing across multiple services (single-service for now)
- Any changes to existing `ILogger` usage — both coexist
- Serilog or any third-party logging library
- UI or API surface changes

---

## Design Decisions

---

### DD-1: ITelemetryService — Interface in Application, Implementation in Infrastructure

`TelemetryClient` lives in `Microsoft.ApplicationInsights` — a third-party SDK
package. Injecting it directly into `BanderasService` (Application layer) would
introduce an external infrastructure dependency into the core of the system,
violating the Clean Architecture dependency rule: Application and Domain must not
reference external packages.

**Solution:** Define `ITelemetryService` in the Application layer. Implement it
in Infrastructure using `TelemetryClient`. `BanderasService` depends only on the
interface — it has no knowledge of Application Insights.

This is the same pattern used for `IBanderasRepository` and EF Core. The
Application layer defines what it needs. Infrastructure delivers it.

**Why it matters for interviews:** This is the Dependency Inversion Principle
applied to telemetry. Swapping Application Insights for OpenTelemetry or a stub
requires zero changes to business logic.

---

### DD-2: Custom Event over Trace for Evaluation Telemetry

Application Insights has two relevant telemetry types for evaluation outcomes:

- **Trace** — a timestamped log line with a severity level. Passive narration.
  Queryable as free text in Application Insights Logs.
- **Custom Event** — a named business occurrence with structured key-value
  properties. Queryable by event name and property value in Application Insights.

Evaluation outcomes are business facts — "flag X was evaluated for user Y and
returned false using the Percentage strategy." They have specific, queryable
dimensions. Sending them as Custom Events means you can write:

```kusto
customEvents
| where name == "flag.evaluated"
| where customDimensions.FlagName == "dark-mode"
| summarize count() by tostring(customDimensions.Result), bin(timestamp, 1h)
```

Traces cannot be aggregated this way. Custom Events are the correct type.

The existing `ILogger` Trace telemetry is not removed — both coexist. `ILogger`
output flows into Application Insights as Traces automatically once the SDK is
wired. The custom event is additive, not a replacement.

---

### DD-3: Connection String from Key Vault, Not Code

The Application Insights connection string must not be committed to source code
or `appsettings.json`. It is added to `kv-banderas-dev` as:

```
Secret name:  ApplicationInsights--ConnectionString
```

The Application Insights SDK reads from `IConfiguration["ApplicationInsights:ConnectionString"]`
automatically when `AddApplicationInsightsTelemetry()` is called. Key Vault
loads the secret into `IConfiguration` at startup via the provider wired in
PR #50. No additional code is required to bridge Key Vault → SDK.

`appsettings.json` carries an empty placeholder:
```json
"ApplicationInsights": {
  "ConnectionString": ""
}
```

`appsettings.Development.json` carries the actual dev connection string for
local development when Key Vault is not available (optional — see Known Constraints).

---

### DD-4: Auto-Capture for Request, Exception, and Dependency Telemetry

`AddApplicationInsightsTelemetry()` in `Program.cs` automatically captures:

| Telemetry Type | What it records | Banderas example |
|---|---|---|
| Request | Every HTTP call — method, URL, status code, duration | `GET /api/flags/dark-mode → 200, 38ms` |
| Exception | Unhandled exceptions flowing through middleware | `FlagNotFoundException` on evaluate |
| Dependency | Outbound calls — Postgres queries via EF Core | `SELECT * FROM "Flags" → 12ms` |

No manual instrumentation is required for these three. They are free from the SDK.

---

### DD-5: No ITelemetryService Call in FeatureEvaluator

`FeatureEvaluator` is a pure function. It takes a `Flag` and a
`FeatureEvaluationContext` and returns a `bool`. It has no side effects and no
dependencies beyond its strategy registry. This design makes it fast, testable,
and easy to reason about.

`ITelemetryService.TrackEvaluation()` is called in `BanderasService.IsEnabledAsync()`
— after the evaluation result is known — not inside `FeatureEvaluator`. The
imperative shell (`BanderasService`) owns all side effects. The pure core
(`FeatureEvaluator`) remains untouched.

---

## Scope

| # | What | Layer | File(s) |
|---|---|---|---|
| 1 | `ITelemetryService` interface | Application | `Application/Telemetry/ITelemetryService.cs` |
| 2 | `ApplicationInsightsTelemetryService` | Infrastructure | `Infrastructure/Telemetry/ApplicationInsightsTelemetryService.cs` |
| 3 | `NullTelemetryService` (test double) | Infrastructure | `Infrastructure/Telemetry/NullTelemetryService.cs` |
| 4 | `BanderasService` — call `ITelemetryService` | Application | `Application/Services/BanderasService.cs` |
| 5 | Wire `AddApplicationInsightsTelemetry()` | Api | `Api/Program.cs` |
| 6 | Register `ITelemetryService` in DI | Infrastructure | `Infrastructure/DependencyInjection.cs` |
| 7 | Config placeholders | Api | `appsettings.json`, `appsettings.Development.json` |

---

## File Layout

```
Banderas.Application/
  Telemetry/
    ITelemetryService.cs                    ← NEW

Banderas.Infrastructure/
  Telemetry/
    ApplicationInsightsTelemetryService.cs  ← NEW
    NullTelemetryService.cs                 ← NEW

Banderas.Api/
  Program.cs                                ← MODIFIED
  appsettings.json                          ← MODIFIED
  appsettings.Development.json              ← MODIFIED (optional — see Known Constraints)

Banderas.Application/
  Services/
    BanderasService.cs                      ← MODIFIED

Banderas.Infrastructure/
  DependencyInjection.cs                    ← MODIFIED
```

---

## New Files

---

### ITelemetryService.cs

**Location:** `Banderas.Application/Telemetry/ITelemetryService.cs`

```csharp
using Banderas.Domain.Enums;

namespace Banderas.Application.Telemetry;

/// <summary>
/// Abstraction for emitting business telemetry events.
/// Implemented in Infrastructure — Application has no reference to any telemetry SDK.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Tracks a completed flag evaluation as a named business event.
    /// Called by BanderasService after every evaluation outcome is known.
    /// </summary>
    void TrackEvaluation(
        string flagName,
        bool result,
        RolloutStrategy strategy,
        EnvironmentType environment
    );
}
```

**Design note:** `TrackEvaluation` is void and synchronous. Telemetry emission is
a fire-and-forget concern — the evaluation response must not be delayed waiting
for a telemetry flush. The Application Insights SDK buffers events internally and
flushes on a background timer.

---

### ApplicationInsightsTelemetryService.cs

**Location:** `Banderas.Infrastructure/Telemetry/ApplicationInsightsTelemetryService.cs`

```csharp
using Banderas.Application.Telemetry;
using Banderas.Domain.Enums;
using Microsoft.ApplicationInsights;

namespace Banderas.Infrastructure.Telemetry;

/// <summary>
/// Application Insights implementation of ITelemetryService.
/// Emits a "flag.evaluated" custom event with structured dimensions.
/// </summary>
public sealed class ApplicationInsightsTelemetryService : ITelemetryService
{
    private readonly TelemetryClient _telemetryClient;

    public ApplicationInsightsTelemetryService(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient;
    }

    public void TrackEvaluation(
        string flagName,
        bool result,
        RolloutStrategy strategy,
        EnvironmentType environment)
    {
        var properties = new Dictionary<string, string>
        {
            ["FlagName"]    = flagName,
            ["Result"]      = result ? "enabled" : "disabled",
            ["Strategy"]    = strategy.ToString(),
            ["Environment"] = environment.ToString(),
        };

        _telemetryClient.TrackEvent("flag.evaluated", properties);
    }
}
```

**Custom event name:** `flag.evaluated` — lowercase with dot separator is the
convention for custom event names in Application Insights. Queryable in KQL as
`customEvents | where name == "flag.evaluated"`.

---

### NullTelemetryService.cs

**Location:** `Banderas.Infrastructure/Telemetry/NullTelemetryService.cs`

```csharp
using Banderas.Application.Telemetry;
using Banderas.Domain.Enums;

namespace Banderas.Infrastructure.Telemetry;

/// <summary>
/// No-op implementation of ITelemetryService.
/// Registered in the DI container when the environment is "Testing"
/// so integration tests do not require an Application Insights connection string.
/// </summary>
public sealed class NullTelemetryService : ITelemetryService
{
    public void TrackEvaluation(
        string flagName,
        bool result,
        RolloutStrategy strategy,
        EnvironmentType environment)
    {
        // Intentionally empty — telemetry is suppressed in test environments.
    }
}
```

---

## Modified Files

---

### BanderasService.cs

**Location:** `Banderas.Application/Services/BanderasService.cs`

**Changes required:**

1. Add `ITelemetryService` constructor parameter and `_telemetryService` field.
2. After `LogResult(result)` in `IsEnabledAsync`, call `_telemetryService.TrackEvaluation(...)`.
3. `TrackEvaluation` must not be called on the `FlagNotFoundException` path — telemetry
   tracks evaluation outcomes, not not-found errors. Exceptions are auto-captured by
   Application Insights middleware.

**Constructor — updated:**

```csharp
private readonly IBanderasRepository _repository;
private readonly FeatureEvaluator _evaluator;
private readonly ILogger<BanderasService> _logger;
private readonly ITelemetryService _telemetryService;

public BanderasService(
    IBanderasRepository repository,
    FeatureEvaluator evaluator,
    ILogger<BanderasService> logger,
    ITelemetryService telemetryService)
{
    _repository = repository;
    _evaluator = evaluator;
    _logger = logger;
    _telemetryService = telemetryService;
}
```

**IsEnabledAsync — evaluation paths updated:**

After the `LogResult(result)` call in the `FlagDisabled` branch:
```csharp
LogResult(result);
_telemetryService.TrackEvaluation(flagName, false, RolloutStrategy.None, sanitizedContext.Environment);
return false;
```

After the `LogResult(strategyResult)` call in the `StrategyEvaluated` branch:
```csharp
LogResult(strategyResult);
_telemetryService.TrackEvaluation(flagName, isEnabled, flag.StrategyType, sanitizedContext.Environment);
return isEnabled;
```

**Important:** `TrackEvaluation` is called after `LogResult` in both branches.
Telemetry emission order must not affect the return value.

---

### Infrastructure/DependencyInjection.cs

**Location:** `Banderas.Infrastructure/DependencyInjection.cs`

Register `ITelemetryService` conditionally based on environment:

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    // ... existing EF Core registration ...

    if (environment.IsEnvironment("Testing"))
    {
        services.AddSingleton<ITelemetryService, NullTelemetryService>();
    }
    else
    {
        services.AddSingleton<ITelemetryService, ApplicationInsightsTelemetryService>();
    }

    return services;
}
```

**Note:** `AddInfrastructure` must accept `IHostEnvironment` as a parameter if
it does not already. Update the call site in `Program.cs` accordingly.

**Lifetime:** `Singleton` — `TelemetryClient` is thread-safe and designed to be
a singleton. `ApplicationInsightsTelemetryService` wraps it and carries no
mutable state, so Singleton is correct.

---

### Program.cs

**Location:** `Banderas.Api/Program.cs`

Add Application Insights telemetry **before** `builder.Build()`:

```csharp
builder.Services.AddApplicationInsightsTelemetry();
```

This single call enables all auto-captured telemetry (requests, exceptions,
dependencies). The SDK reads `ApplicationInsights:ConnectionString` from
`IConfiguration` automatically — no manual configuration object required.

`AddApplicationInsightsTelemetry()` must be called before `builder.Build()`.
Order relative to other service registrations does not matter.

---

### appsettings.json

Add the Application Insights placeholder section:

```json
{
  "Azure": {
    "KeyVaultUri": ""
  },
  "ApplicationInsights": {
    "ConnectionString": ""
  },
  "ConnectionStrings": {
    "DefaultConnection": ""
  }
}
```

The empty string is the correct placeholder — the SDK treats an empty connection
string as "telemetry disabled" rather than throwing at startup.

---

### appsettings.Development.json

Add the dev connection string **only if** a dev Application Insights resource
exists. If one does not exist, omit this entry and telemetry will be silently
disabled in local development. This is acceptable — the service runs correctly
without telemetry.

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=<dev-key>;..."
  }
}
```

> **Note:** If a dev Application Insights resource does not exist, leave this
> section out of `appsettings.Development.json`. Do not commit a real production
> connection string here.

---

## Acceptance Criteria

---

### AC-1: ITelemetryService Interface

- [ ] File at `Banderas.Application/Telemetry/ITelemetryService.cs`
- [ ] Namespace: `Banderas.Application.Telemetry`
- [ ] Single method: `void TrackEvaluation(string flagName, bool result, RolloutStrategy strategy, EnvironmentType environment)`
- [ ] No reference to `Microsoft.ApplicationInsights` or any infrastructure package

---

### AC-2: ApplicationInsightsTelemetryService Implementation

- [ ] File at `Banderas.Infrastructure/Telemetry/ApplicationInsightsTelemetryService.cs`
- [ ] Namespace: `Banderas.Infrastructure.Telemetry`
- [ ] Implements `ITelemetryService`
- [ ] Injects `TelemetryClient` via constructor
- [ ] Calls `_telemetryClient.TrackEvent("flag.evaluated", properties)`
- [ ] Properties dictionary contains: `FlagName`, `Result` (`"enabled"` or `"disabled"`), `Strategy`, `Environment`
- [ ] `Result` is a string (`"enabled"` / `"disabled"`), not a bool — Application Insights custom dimensions are strings

---

### AC-3: BanderasService Calls ITelemetryService

- [ ] `ITelemetryService` injected via constructor
- [ ] `_telemetryService.TrackEvaluation(...)` called in the `FlagDisabled` path
- [ ] `_telemetryService.TrackEvaluation(...)` called in the `StrategyEvaluated` path
- [ ] `TrackEvaluation` is NOT called on the `FlagNotFoundException` path
- [ ] `TrackEvaluation` is called after `LogResult` in both branches
- [ ] `FeatureEvaluator.cs` is unchanged — no `ITelemetryService` reference

---

### AC-4: Auto-Capture Wired in Program.cs

- [ ] `builder.Services.AddApplicationInsightsTelemetry()` present in `Program.cs`
- [ ] Called before `builder.Build()`
- [ ] No `TelemetryConfiguration` or `ApplicationInsightsServiceOptions` manual
      configuration — defaults are sufficient for this phase

---

### AC-5: Connection String Configuration

- [ ] `appsettings.json` — `"ApplicationInsights": { "ConnectionString": "" }` placeholder present
- [ ] Secret `ApplicationInsights--ConnectionString` added to `kv-banderas-dev`
      (manual Azure Portal step — documented in PR description, not code)
- [ ] Key Vault loads the value into `IConfiguration["ApplicationInsights:ConnectionString"]`
      at startup via the provider wired in PR #50
- [ ] Application starts successfully when connection string is empty (local dev
      without Key Vault access) — SDK disables telemetry silently, no exception thrown

---

### AC-6: NullTelemetryService for Testing

- [ ] File at `Banderas.Infrastructure/Telemetry/NullTelemetryService.cs`
- [ ] Implements `ITelemetryService`
- [ ] `TrackEvaluation` body is empty (no-op)
- [ ] `NullTelemetryService` registered when `IHostEnvironment.IsEnvironment("Testing")` is true
- [ ] `ApplicationInsightsTelemetryService` registered for all other environments
- [ ] `BanderasApiFactory.cs` — no changes required; `UseEnvironment("Testing")` from
      PR #39 already ensures `NullTelemetryService` is resolved in integration tests

---

### AC-7: Build and Tests Pass

- [ ] `dotnet build Banderas.sln` → 0 errors, 0 warnings
- [ ] `dotnet test --filter "Category=Unit"` → all passing (count unchanged from PR #50 baseline)
- [ ] `dotnet test --filter "Category=Integration"` → 113/113 passing
- [ ] `dotnet csharpier check .` → 0 violations
- [ ] Application starts locally with `docker compose up` — no startup exception
      (telemetry disabled gracefully when connection string is empty)

---

## Learning Opportunities

**💡 TelemetryClient is a Singleton by design**
The Application Insights `TelemetryClient` maintains an internal buffer of telemetry
events and flushes them to Azure on a background timer (default: 30 seconds). It is
designed to be shared across the entire application lifetime — one instance per process.
Registering it as anything other than Singleton would create multiple buffers, leading
to event loss and unpredictable flush behavior.

**💡 The Null Object Pattern**
`NullTelemetryService` is an implementation of the Null Object Pattern — a class that
satisfies an interface contract by doing nothing. It eliminates the need for null checks
(`if (_telemetryService != null)`) throughout `BanderasService`. The consuming code
never needs to know whether telemetry is real or suppressed. This is the same reason
`ILogger<T>` uses `NullLogger<T>` in tests rather than passing `null`.

**💡 IConfiguration as an abstraction layer**
`AddApplicationInsightsTelemetry()` reads from `IConfiguration["ApplicationInsights:ConnectionString"]`
— not from a hardcoded key. The SDK does not care whether that value came from
`appsettings.json`, environment variables, or Key Vault. This is the Dependency
Inversion Principle applied to configuration: consumers depend on the abstraction
(`IConfiguration`), not the source.

**💡 Fire-and-forget telemetry**
`TrackEvaluation` is synchronous and void. The SDK buffers the event internally —
your code does not wait for a network call to Azure. This is intentional: telemetry
must never add latency to the hot path. The tradeoff is that events may be lost if
the process crashes before the next flush. For feature flag telemetry, this is an
acceptable tradeoff.

---

## Known Constraints

| Constraint | Detail | Tracked For |
|---|---|---|
| Dev Application Insights resource may not exist | If no dev resource exists, local telemetry is silently disabled. No exception. | Acceptable |
| Managed Identity not yet assigned | Local dev uses Key Vault via `az login`. Production Managed Identity is Phase 8. | Phase 8 |
| No KQL queries or dashboard setup | Out of scope for this PR. | Post-Phase 1.5 |
| TelemetryClient flush on shutdown | Default flush timeout is 5 seconds. Events buffered at shutdown time may be lost. | Phase 8 production hardening |

---

## Out of Scope

- Kusto (KQL) queries or Application Insights workbooks
- Distributed tracing across multiple services
- Serilog or structured logging library changes
- Any changes to existing `ILogger` calls in `BanderasService`
- Moving any other secrets to Key Vault (PR #52 scope)
- `GET /api/flags/{name}/trace` evaluation trace endpoint (Phase 4)

---

## Definition of Done

- [ ] `ITelemetryService` defined in Application layer with no infrastructure references
- [ ] `ApplicationInsightsTelemetryService` implemented in Infrastructure
- [ ] `NullTelemetryService` implemented and registered for Testing environment
- [ ] `BanderasService` calls `TrackEvaluation` on both evaluation outcome paths
- [ ] `AddApplicationInsightsTelemetry()` wired in `Program.cs`
- [ ] `ApplicationInsights--ConnectionString` secret added to `kv-banderas-dev`
- [ ] `appsettings.json` placeholder present
- [ ] 0 build errors, 0 warnings
- [ ] 113/113 tests passing
- [ ] CSharpier clean
- [ ] Application starts locally without exception when connection string is empty

---

*Banderas | feature/application-insights | Phase 1.5 — Azure Foundation | v1.0*
