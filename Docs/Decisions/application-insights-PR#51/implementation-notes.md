# application-insights — Implementation Notes

**Session date:** 2026-04-19
**Branch:** `feature/application-insights`
**Spec reference:** `Docs/Decisions/application-insights-PR#51/spec.md`
**Build status:** Passed — 0 warnings, 0 errors
**Tests:** 113/113 passing
**PR:** #51

---

## Table of Contents

- [What Was Built](#what-was-built)
- [Files Changed](#files-changed)
- [Key Decisions](#key-decisions)
- [NuGet Packages Added](#nuget-packages-added)
- [Unit Test Fixture Fix](#unit-test-fixture-fix)
- [Manual Step Required](#manual-step-required)

---

## What Was Built

Application Insights telemetry wired into Banderas across two concerns:

1. **Auto-captured telemetry** — every HTTP request, unhandled exception, and EF Core/Postgres dependency is recorded by the SDK with zero manual instrumentation, enabled via `AddApplicationInsightsTelemetry()` in `Program.cs`.
2. **Custom evaluation events** — every flag evaluation emits a named `flag.evaluated` custom event with four structured dimensions (`FlagName`, `Result`, `Strategy`, `Environment`) queryable in KQL.

---

## Files Changed

| File | Change |
|---|---|
| `Banderas.Application/Telemetry/ITelemetryService.cs` | New — interface in Application layer |
| `Banderas.Infrastructure/Telemetry/ApplicationInsightsTelemetryService.cs` | New — `TelemetryClient`-backed implementation |
| `Banderas.Infrastructure/Telemetry/NullTelemetryService.cs` | New — no-op for Testing environment |
| `Banderas.Application/Services/BanderasService.cs` | `ITelemetryService` injected; `TrackEvaluation` called in both evaluation branches |
| `Banderas.Infrastructure/DependencyInjection.cs` | Added `IHostEnvironment` param; conditional registration of `ITelemetryService` |
| `Banderas.Api/Program.cs` | `AddApplicationInsightsTelemetry()` added before `Build()`; `builder.Environment` passed to `AddInfrastructure` |
| `Banderas.Api/appsettings.json` | `ApplicationInsights.ConnectionString` placeholder added |
| `Banderas.Infrastructure/Banderas.Infrastructure.csproj` | `Microsoft.ApplicationInsights` package added |
| `Banderas.Api/Banderas.Api.csproj` | `Microsoft.ApplicationInsights.AspNetCore` package added |
| `Banderas.Tests/Services/BanderasServiceLoggingTests.cs` | Local `NullTelemetryService` stub added to satisfy updated constructor |

---

## Key Decisions

### Interface in Application, implementation in Infrastructure

`ITelemetryService` lives in `Banderas.Application.Telemetry`. `BanderasService` depends only on the interface — it has no knowledge of the Application Insights SDK. This is the same Dependency Inversion pattern used for `IBanderasRepository`. Swapping Application Insights for OpenTelemetry requires zero changes to business logic.

### Custom Event over Trace

Flag evaluations are business facts with queryable dimensions. `TelemetryClient.TrackEvent("flag.evaluated", properties)` emits a Custom Event, enabling KQL queries like:

```kusto
customEvents
| where name == "flag.evaluated"
| where customDimensions.FlagName == "dark-mode"
| summarize count() by tostring(customDimensions.Result), bin(timestamp, 1h)
```

Traces cannot be aggregated this way. The existing `ILogger` calls are unchanged and continue to flow as Traces — the custom event is additive.

### `TrackEvaluation` called in `BanderasService`, not `FeatureEvaluator`

`FeatureEvaluator` is a pure function — no side effects, no dependencies. The call site is `BanderasService.IsEnabledAsync()` after `LogResult()` in both the `FlagDisabled` and `StrategyEvaluated` branches. The `FlagNotFoundException` path does not emit telemetry — exceptions are auto-captured by the SDK middleware.

### Singleton lifetime for `ITelemetryService`

`TelemetryClient` is thread-safe and maintains an internal buffer that flushes to Azure on a background timer. It must be a singleton. `ApplicationInsightsTelemetryService` wraps it with no mutable state, so Singleton is correct for both.

### `NullTelemetryService` for Testing environment

Registered when `IHostEnvironment.IsEnvironment("Testing")` is true. Integration tests already use `UseEnvironment("Testing")` from PR #39, so they resolve the no-op automatically. This is the Null Object Pattern — consuming code never checks whether telemetry is live or suppressed.

### Connection string from Key Vault

`appsettings.json` carries an empty placeholder. The real value is stored as `ApplicationInsights--ConnectionString` in `kv-banderas-dev`. The Key Vault provider (PR #50) loads it into `IConfiguration` at startup. `AddApplicationInsightsTelemetry()` reads `IConfiguration["ApplicationInsights:ConnectionString"]` automatically — no bridge code required. An empty string disables telemetry silently; no startup exception is thrown.

---

## NuGet Packages Added

| Package | Project | Reason |
|---|---|---|
| `Microsoft.ApplicationInsights` `2.*` | `Banderas.Infrastructure` | Provides `TelemetryClient` used in `ApplicationInsightsTelemetryService` |
| `Microsoft.ApplicationInsights.AspNetCore` `2.*` | `Banderas.Api` | Provides `AddApplicationInsightsTelemetry()` extension and ASP.NET Core middleware for auto-capture |

---

## Unit Test Fixture Fix

`BanderasServiceLoggingTests` constructs `BanderasService` directly and does not reference `Banderas.Infrastructure`. A private `NullTelemetryService` stub was added to the test class to satisfy the updated constructor — no new project reference or mocking library required.

---

## Manual Step Required

Add the Application Insights connection string to Key Vault before deploying:

```
Secret name:  ApplicationInsights--ConnectionString
Value:        <connection string from Azure Portal → Application Insights resource → Overview>
Key Vault:    kv-banderas-dev
```

The application starts and runs correctly without this secret — telemetry is silently disabled when the connection string is empty.
