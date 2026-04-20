# AI Flag Health Analysis Endpoint — Implementation Notes

**Session date:** 2026-04-20
**Branch:** `feature/ai-flag-health-analysis`
**Spec reference:** `Docs/Decisions/AI-flag-health-analysis-endpoint - PR#52/spec-v2.md`
**Build status:** Passed — 0 warnings, 0 errors
**Tests:** 144/144 passing (107 unit + 37 integration)
**PR:** #52

---

## Table of Contents

- [What Was Built](#what-was-built)
- [Spec Issues Found and Fixed](#spec-issues-found-and-fixed)
- [Files Changed](#files-changed)
- [Key Decisions](#key-decisions)
- [NuGet Packages Added](#nuget-packages-added)
- [Test Notes](#test-notes)
- [Manual Step Required](#manual-step-required)

---

## What Was Built

`POST /api/flags/health` — an AI-generated health analysis of all feature flags across
all environments. The endpoint is analytical only: it reads flag state, sanitizes
user-controlled fields to defend against prompt injection, delegates to Azure OpenAI via
Semantic Kernel, and returns a structured `FlagHealthAnalysisResponse`. No flags are
modified. The AI service degrades gracefully to `503 Service Unavailable` if Azure OpenAI
is unavailable.

---

## Spec Issues Found and Fixed

Two issues were found in spec-v2 during implementation planning. Both are compile errors
that were corrected during implementation — not at author review time.

### Issue A — `f.RolloutStrategy` does not exist on `FlagResponse`

`AiFlagAnalyzer.BuildPrompt` projected `f.RolloutStrategy` in an anonymous object.
`RolloutStrategy` is the enum *type*, not a property name. The property is `StrategyType`.

**Fix:** `f.StrategyType` in `BuildPrompt`.

### Issue B — Controller snippet used `_validator` — ambiguous in context

The spec showed `_validator.ValidateAsync(request, ...)`. The existing controller already
had `_createValidator` and `_updateValidator`. A bare `_validator` field does not exist.

**Fix:** `IValidator<FlagHealthRequest>` injected as `_healthValidator`; used in the
health action only.

---

## Files Changed

### New files

| File | Purpose |
|---|---|
| `Banderas.Application/AI/FlagHealthConstants.cs` | Named constants for default (30), min (1), max (365) threshold — no magic numbers |
| `Banderas.Application/AI/IPromptSanitizer.cs` | Application interface — sanitize strings before embedding in prompts |
| `Banderas.Application/AI/IAiFlagAnalyzer.cs` | Application interface — contract for AI analysis, decoupled from SK |
| `Banderas.Application/AI/PromptSanitizer.cs` | Implementation — newline normalization, phrase redaction, 500-char cap; `GeneratedRegex` for compile-time regex |
| `Banderas.Application/DTOs/FlagHealthRequest.cs` | Optional `StalenessThresholdDays` (1–365) |
| `Banderas.Application/DTOs/FlagAssessment.cs` | Per-flag result: Name, Status, Reason, Recommendation |
| `Banderas.Application/DTOs/FlagHealthAnalysisResponse.cs` | Top-level response: Summary, Flags, AnalyzedAt, StalenessThresholdDays |
| `Banderas.Application/Exceptions/AiAnalysisUnavailableException.cs` | Signals AI service failure — caught by middleware → 503 |
| `Banderas.Application/Validators/FlagHealthRequestValidator.cs` | FluentValidation — validates threshold range using constants |
| `Banderas.Infrastructure/AI/AiFlagAnalyzer.cs` | SK + Azure OpenAI implementation; wraps all failures as `AiAnalysisUnavailableException` |
| `Banderas.Tests/AI/PromptSanitizerTests.cs` | 21 unit tests covering all 4 sanitization rules + edge cases |
| `Banderas.Tests/AI/BanderasServiceAnalysisTests.cs` | 5 unit tests — default threshold, caller threshold, null StrategyConfig, sanitization, 503 propagation |
| `Banderas.Tests.Integration/AnalyzeFlagsEndpointTests.cs` | 5 integration tests — 200 response shape, threshold, 400 validation, UTC timestamp |

### Modified files

| File | Change |
|---|---|
| `Banderas.Domain/Interfaces/IBanderasRepository.cs` | `GetAllAsync(EnvironmentType? environment = null, ...)` — null = no filter |
| `Banderas.Infrastructure/Persistence/BanderasRepository.cs` | Conditional environment filter in `GetAllAsync` |
| `Banderas.Infrastructure/DependencyInjection.cs` | Semantic Kernel + `AiFlagAnalyzer` registered inside `!IsEnvironment("Testing")` guard |
| `Banderas.Infrastructure/Banderas.Infrastructure.csproj` | Added `Azure.Identity`, `Microsoft.SemanticKernel`, `Microsoft.SemanticKernel.Connectors.AzureOpenAI` |
| `Banderas.Application/DTOs/FlagResponse.cs` | `StrategyConfig` changed from `string` to `string?` (DD-9) |
| `Banderas.Application/DTOs/FlagMappings.cs` | No change required — `string` → `string?` assignment is widening |
| `Banderas.Application/Interfaces/IBanderasService.cs` | `AnalyzeFlagsAsync` added |
| `Banderas.Application/Services/BanderasService.cs` | `IPromptSanitizer` + `IAiFlagAnalyzer` constructor params; `AnalyzeFlagsAsync` implemented |
| `Banderas.Application/DependencyInjection.cs` | `IPromptSanitizer` + `FlagHealthRequestValidator` registered |
| `Banderas.Api/Controllers/BanderasController.cs` | `_healthValidator` injected; `POST /api/flags/health` action added |
| `Banderas.Api/Middleware/GlobalExceptionMiddleware.cs` | `WriteProblemDetailsAsync` extended with `type` param; dedicated `catch (AiAnalysisUnavailableException)` block added |
| `Banderas.Api/appsettings.json` | `AzureOpenAI.Endpoint` and `AzureOpenAI.DeploymentName` placeholders added |
| `Banderas.Tests/Services/BanderasServiceLoggingTests.cs` | `NullPromptSanitizer` + `NullAiFlagAnalyzer` stubs added to satisfy new constructor; `GetAllAsync` signature updated in local repository stub |
| `Banderas.Tests.Integration/Fixtures/BanderasApiFactory.cs` | `StubAiFlagAnalyzer` registered in `ConfigureServices` — deterministic, no Azure calls |
| `Banderas.Tests.Integration/FlagEndpointTests.cs` | Null-forgiving operator added to `updated.StrategyConfig!` (consequence of DD-9) |
| `Requests/smoke-test.http` | `POST /api/flags/health` with empty body and `stalenessThresholdDays: 7` variants added |

---

## Key Decisions

### Magic numbers in constants (`FlagHealthConstants`)

`30`, `1`, and `365` appear in two places: `BanderasService` (default threshold) and
`FlagHealthRequestValidator` (min/max range). Both reference `FlagHealthConstants` —
`internal` to the Application layer. No bare literals in business logic or validators.
The `PromptSanitizer.MaxLength` (500) was already a `private const` in the spec and
remains so — it is not shared across files.

### `EnvironmentType?` over overload (DD-8)

`GetAllAsync` accepts a nullable `EnvironmentType? environment = null`. Null means
"no environment filter". All existing callers pass an explicit value — zero call sites
required updating. The overload option was rejected because repeated overloads for
evolving filter requirements produce an unmanageable interface over time. A `FlagQuery`
record is the Phase 4 upgrade path when more than two filter dimensions are needed.

### `FlagResponse.StrategyConfig` changed to `string?` (DD-9)

The original `string StrategyConfig` was incorrect. Flags with `RolloutStrategy.None`
have no strategy config; `Flag` stores `"{}"` by default, but `null` is a valid value
at the application boundary. AC-7 (null safety during sanitization) confirmed the need.
The change is widening — no existing code breaks. One null-forgiving operator added in
`FlagEndpointTests` where the test already asserts the value is present.

### `AiAnalysisUnavailableException` caught explicitly in middleware (DD-10)

The exception extends `Exception`, not `BanderasException`. Without a dedicated catch
it would fall to the generic 500 handler. The 503 response carries
`type: "https://tools.ietf.org/html/rfc9110#section-15.6.4"` — distinct from the
`"about:blank"` default used for domain errors. `WriteProblemDetailsAsync` was extended
with an optional `type` parameter (defaults to `"about:blank"`) rather than hardcoding
the URI in the exception class.

### Semantic Kernel fully excluded from Testing environment

`Kernel.CreateBuilder()`, `AddAzureOpenAIChatCompletion()`, and `DefaultAzureCredential()`
are all inside the `!environment.IsEnvironment("Testing")` block. They are never
instantiated during integration tests. `StubAiFlagAnalyzer` is registered by
`BanderasApiFactory.ConfigureServices` to provide deterministic responses.

### `BanderasService` constructor stubs (no mocking library)

The existing test style uses hand-written test doubles, not NSubstitute or Moq.
`NullPromptSanitizer` (pass-through) and `NullAiFlagAnalyzer` (throw `NotSupportedException`)
were added inline to `BanderasServiceLoggingTests` — consistent with the existing
`NullTelemetryService` pattern in the same file. No new package references required.

---

## NuGet Packages Added

| Package | Project | Reason |
|---|---|---|
| `Azure.Identity` `1.*` | `Banderas.Infrastructure` | `DefaultAzureCredential` for managed identity authentication to Azure OpenAI |
| `Microsoft.SemanticKernel` `1.*` | `Banderas.Infrastructure` | Kernel, `IChatCompletionService`, `ChatHistory` |
| `Microsoft.SemanticKernel.Connectors.AzureOpenAI` `1.*` | `Banderas.Infrastructure` | `AddAzureOpenAIChatCompletion`, `AzureOpenAIPromptExecutionSettings` |

---

## Test Notes

**26 new unit tests** across two new files:

- `PromptSanitizerTests` (21 tests) — covers all four sanitization rules: newline
  normalization, instruction override phrases, role confusion markers, and the 500-char
  length cap. Also covers null/whitespace input and clean pass-through.
- `BanderasServiceAnalysisTests` (5 tests) — default threshold, caller threshold, null
  `StrategyConfig` AC-7 safety, sanitization call verification, and `AiAnalysisUnavailableException`
  propagation.

**5 new integration tests** in `AnalyzeFlagsEndpointTests` — 200 response structure,
threshold pass-through, 400 validation (boundary values 0 and 366), and UTC `analyzedAt`.

All 113 pre-existing tests remain green. Total: **144 passing**.

---

## Manual Step Required

Add the Azure OpenAI endpoint to Key Vault before deploying:

```
Secret name:  AzureOpenAI--Endpoint
Value:        <endpoint URL from Azure Portal → Azure OpenAI resource → Keys and Endpoint>
Key Vault:    kv-banderas-dev
```

`DeploymentName` defaults to `"gpt-5-mini"` if not set. To override it:

```
Secret name:  AzureOpenAI--DeploymentName
Value:        <your deployment name>
Key Vault:    kv-banderas-dev
```

The application will throw `InvalidOperationException` at startup if `AzureOpenAI:Endpoint`
is missing and the environment is not `Testing`. All other endpoints remain unaffected.
