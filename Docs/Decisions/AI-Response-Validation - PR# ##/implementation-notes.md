# AI Response Semantic Validation — Implementation Notes

**Session date:** 2026-04-28
**Branch:** `dev`
**Spec reference:** `Docs/Decisions/AI-Response-Validation - PR# ##/spec.md`
**Build status:** Approved by user; verified `dotnet build` passed with 0 warnings and 0 errors
**Tests:** 155/155 passing
**PR:** TBD

## What Was Built

`AiFlagAnalyzer` now treats the Azure OpenAI response as untrusted until it passes
semantic validation. After JSON deserialization, the analyzer rejects missing summaries,
missing or empty assessment lists, partial flag coverage, and undocumented status values
by throwing `AiAnalysisUnavailableException`. The API translates those failures into the
existing safe `503 application/problem+json` response, so callers no longer receive a
corrupt `200 OK` health report.

## Spec Gaps Resolved

The spec listed `GlobalExceptionMiddleware.cs` under "No Changes To", but AC-6 required
the safe 503 title to be `"Flag health analysis is currently unavailable."` and the
reviewed implementation updated the middleware and existing startup-resilience test to
match that public contract.

The spec called for an endpoint-level throwing-stub test, but semantic validation itself
lives behind `AiFlagAnalyzer.AnalyzeAsync`. The implementation added direct analyzer
coverage through a stubbed Semantic Kernel `IChatCompletionService` so each malformed
model-output case is tested without live Azure calls.

## Deviations from Spec

The implementation added `Banderas.Tests.Integration/AiFlagAnalyzerValidationTests.cs`,
which was not in the spec's file layout. This keeps the private `ValidateResponse(...)`
method private while still testing it through the analyzer's public boundary.

`GlobalExceptionMiddleware.cs` changed despite the "No Changes To" table. The change
aligns the middleware with AC-6, preserves a safe caller message, and logs the operator
diagnostic reason from `AiAnalysisUnavailableException`.

## Key Decisions

Validation remains inside `AiFlagAnalyzer` rather than becoming a shared service because
the output contract is specific to the AI anti-corruption boundary.

Status validation uses a private static `HashSet<string>` with
`StringComparer.OrdinalIgnoreCase`, matching the spec while avoiding casing fragility in
model output.

The coverage check compares returned assessment names against input flag names rather
than only comparing counts, so duplicate model rows cannot hide a missing flag.

## File-by-File Changes

| File | Change |
|---|---|
| `Banderas.Infrastructure/AI/AiFlagAnalyzer.cs` | Added valid-status set, `ValidateResponse(...)`, and the validation call after deserialization |
| `Banderas.Api/Middleware/GlobalExceptionMiddleware.cs` | Logs AI-unavailable diagnostics and returns the AC-6 503 title |
| `Banderas.Tests.Integration/AiFlagAnalyzerValidationTests.cs` | Added validation-path coverage for summary, empty list, missing flag, invalid status, and valid pass-through |
| `Banderas.Tests.Integration/AnalyzeFlagsEndpointTests.cs` | Added throwing-analyzer 503 test and non-AI endpoint unaffected test |
| `Banderas.Tests.Integration/Fixtures/BanderasApiFactory.cs` | Added `CreateClientWithThrowingAiFlagAnalyzer()` and local throwing analyzer stub |
| `Banderas.Tests.Integration/AiStartupResilienceTests.cs` | Updated expected 503 title to the AC-6 public contract |
| `Docs/current-state.md` | Closed KI-008, updated test counts, Phase 2 prep DoD, and lessons learned |
| `Docs/roadmap.md` | Checked off AI output-contract verification and updated current focus |
| `Docs/architecture.md` | Documented AI output validation at the infrastructure boundary |

## Risks and Follow-Ups

None blocking. A small prompt-copy typo appears in the reviewed diff (`"the  Banderas"`);
it has no behavioral impact, but can be cleaned up opportunistically in a later touch.

## How to Test

```bash
dotnet csharpier check .
dotnet build Banderas.sln -p:TreatWarningsAsErrors=true
dotnet test Banderas.sln --no-build
```

Verified results:

- CSharpier: checked 88 files
- Build: passed with 0 warnings and 0 errors
- Tests: 155/155 passing (107 unit + 48 integration)

## Interview Lens

The interesting decision was to validate the model response at the anti-corruption
boundary instead of in the service or controller. The tradeoff is that `AiFlagAnalyzer`
now owns a little more defensive logic, but the rest of the application never has to
understand AI failure modes or partial model output. At a larger scale, I would consider
a dedicated validator only if multiple AI adapters shared the same response contract.

## Foundation Docs Updated

- [x] `Docs/current-state.md`
- [x] `Docs/roadmap.md`
- [x] `Docs/architecture.md`

## Definition of Done — Status

- [x] `ValidateResponse` private method added to `AiFlagAnalyzer.cs`
- [x] All four checks implemented:
  - [x] Summary null or empty
  - [x] Empty list
  - [x] Coverage
  - [x] Invalid status values
- [x] Valid status values defined as `private static readonly HashSet<string>`
- [x] Name matching uses `StringComparer.OrdinalIgnoreCase`
- [x] `ValidateResponse` called after deserialization, before `return response`
- [x] `ThrowingAiFlagAnalyzer` stub added to test project
- [x] Integration test added: throwing stub returns `503` with correct shape
- [x] All existing 146 tests pass; current suite is 155/155 passing
- [x] `dotnet build` passes with zero warnings
- [x] `dotnet csharpier check .` passes
