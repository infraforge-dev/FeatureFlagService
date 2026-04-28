# Specification: AI Response Semantic Validation

**Document:** `Docs/Decisions/fix-ai-response-validation/spec.md`  
**Status:** Draft  
**Branch:** `fix/ai-response-validation`  
**PR:** TBD  
**Phase:** 2 Prep — Remaining Gate Conditions (closes KI-008)  
**Depends on:** None  
**Author:** Jose / Claude Architect Session  
**Date:** 2026-04-28  

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

As an engineer consuming `POST /api/flags/health`, I want the API to verify that the AI model's response is **semantically correct** before returning `200 OK` — so that I never receive a partially complete, inconsistently shaped, or status-invalid health report without knowing something went wrong.

---

## Background and Goals

`AiFlagAnalyzer.AnalyzeAsync` currently deserializes the Azure OpenAI response into `FlagHealthAnalysisResponse` and returns it immediately.

It verifies that the JSON parses without throwing, but nothing more.

These silent failure modes are currently undetected:

| Failure | What Happens Today |
|---|---|
| Model returns empty flags list | `200 OK` with zero assessments |
| Model omits one or more flags | `200 OK` with a partial picture |
| Model uses an undocumented status value | `200 OK` with an unhandled enum string |
| Model returns a null or empty summary | `200 OK` with no narrative |

The audit in `Docs/architecture-review-phase1-report.md` classified this as **Medium severity** and flagged it as a **Phase 2 gate condition**.

This spec closes **KI-008**.

### Goals

- Validate AI response semantics at the boundary.
- Throw `AiAnalysisUnavailableException` on validation failure.
- Return clean `503` responses.
- Never return corrupt `200 OK` responses.
- Add integration test coverage for the AI-unavailable path.
- Make zero changes to the service layer or above.

---

## Design Decisions

### DD-1 — Validation lives inside `AiFlagAnalyzer`, not above it

| Option | Verdict |
|---|---|
| Validate inside `AiFlagAnalyzer` | ✅ Chosen — belongs at the boundary |
| Validate in `BanderasService` | ❌ Rejected — leaks AI concerns into the application layer |
| Validate in the controller | ❌ Rejected — too late; corrupt data has already crossed two layers |

`AiFlagAnalyzer` is the Anti-Corruption Layer.

Nothing crosses it without inspection.

The application layer has no awareness that AI is involved, and it should stay that way.

---

### DD-2 — Private method, not a separate class or interface

| Option | Verdict |
|---|---|
| `private ValidateResponse(...)` inside `AiFlagAnalyzer` | ✅ Chosen — only ever needed here |
| Separate `AiResponseValidator` class | ❌ Rejected — a seam for no benefit |
| `IAiResponseValidator` interface | ❌ Rejected — YAGNI; nothing else needs it |

---

### DD-3 — Coverage check over count check

| Option | Verdict |
|---|---|
| `response.Flags.Count == flags.Count` | ❌ Rejected — brittle; duplicates cause false negatives |
| Coverage check via name matching | ✅ Chosen — verifies every flag is accounted for |

The model could return duplicates or omit flags.

Count equality fails both cases for the wrong reasons.

Coverage check is what actually matters.

---

### DD-4 — Two error surfaces: operator messages in logs, safe message to caller

`AiAnalysisUnavailableException` carries a specific diagnostic message naming exactly what failed.

Middleware strips it and returns a safe, generic `503` body.

Operators get precision. Callers get stability.

Never mix the two.

---

## Architecture Overview

```text
BanderasService
    │
    └── IAiFlagAnalyzer.AnalyzeAsync(flags, threshold)
            │
            ├── calls Azure OpenAI
            ├── deserializes JSON
            ├── ValidateResponse(response, flags)   ← NEW private method
            │       ├── summary null/empty?         → throw (operator message)
            │       ├── flags list empty?           → throw (operator message)
            │       ├── coverage check              → throw (names missing flag)
            │       └── invalid status value?       → throw (names bad value)
            │
            ├── on throw → AiAnalysisUnavailableException
            │       └── GlobalExceptionMiddleware → 503 ProblemDetails (safe)
            │                                     → logs → full detail
            └── on success → FlagHealthAnalysisResponse returned clean
```

---

## Scope

### Modified Files

| File | Change |
|---|---|
| `Banderas.Infrastructure/AI/AiFlagAnalyzer.cs` | Add `ValidateResponse` private method; call after deserialization |
| `Banderas.Tests.Integration/Fixtures/BanderasApiFactory.cs` | Add `ThrowingAiFlagAnalyzer` factory path |
| `Banderas.Tests.Integration/AnalyzeFlagsEndpointTests.cs` | Add AC-7 test case |

### No Changes To

- `BanderasService.cs`
- `GlobalExceptionMiddleware.cs`
- `AiAnalysisUnavailableException.cs`
- Any controller
- Any DTO
- Any validator
- Any domain file

---

## Acceptance Criteria

### AC-1 — Null or empty summary is rejected

Given `summary` is null or empty:

- `AiAnalysisUnavailableException` is thrown.
- Exception message contains `"summary"`.
- Caller receives `503 application/problem+json`.

---

### AC-2 — Empty flags list is rejected

Given `flags` is empty:

- `AiAnalysisUnavailableException` is thrown.
- Caller receives `503`.

---

### AC-3 — Partial response is rejected

Given 6 flags are sent and 4 are returned:

- `AiAnalysisUnavailableException` is thrown.
- Exception message names the missing flags.
- Caller receives `503`.

---

### AC-4 — Invalid status value is rejected

Given any flag has:

```json
{
  "status": "Unknown"
}
```

Then:

- `AiAnalysisUnavailableException` is thrown.
- Exception message names the bad value.
- Caller receives `503`.

---

### AC-5 — Valid response passes through unchanged

Given:

- Summary is non-empty.
- List is non-empty.
- All flags are covered.
- All status values are valid.

Then:

- `200 OK` is returned.
- `FlagHealthAnalysisResponse` is returned unmodified.

---

### AC-6 — 503 shape is RFC 9457 compliant

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Flag health analysis is currently unavailable.",
  "status": 503
}
```

**Content-Type:** `application/problem+json`

---

### AC-7 — Integration test: throwing stub returns 503

Given `ThrowingAiFlagAnalyzer` is registered via `ConfigureTestServices`:

- `POST /api/flags/health` returns `503`.
- Response has the correct ProblemDetails shape.

---

### AC-8 — Non-AI endpoints are unaffected

Given the AI analyzer throws on every call:

- `GET /api/flags` still returns `200`.

---

## File Layout

```text
Banderas.Infrastructure/
  AI/
    AiFlagAnalyzer.cs                  ← modified

Banderas.Tests.Integration/
  Fixtures/
    BanderasApiFactory.cs              ← modified
  AnalyzeFlagsEndpointTests.cs         ← modified
```

---

## Technical Notes

### Valid status values

Define valid statuses as a private static readonly `HashSet<string>`, not magic strings:

```csharp
private static readonly HashSet<string> ValidStatusValues =
    new(StringComparer.OrdinalIgnoreCase)
    { "Healthy", "Stale", "Misconfigured", "NeedsReview" };
```

---

### ThrowingAiFlagAnalyzer stub

```csharp
internal sealed class ThrowingAiFlagAnalyzer : IAiFlagAnalyzer
{
    public Task<FlagHealthAnalysisResponse> AnalyzeAsync(
        IReadOnlyList<FlagResponse> flags,
        int stalenessThresholdDays,
        CancellationToken cancellationToken = default) =>
        throw new AiAnalysisUnavailableException("Stub: AI analysis unavailable.");
}
```

---

### Call site inside `AnalyzeAsync`

After existing deserialization, before return:

```csharp
var response = JsonSerializer.Deserialize<FlagHealthAnalysisResponse>(json, JsonOptions)
    ?? throw new AiAnalysisUnavailableException("Failed to deserialize Azure OpenAI response.");

ValidateResponse(response, flags);

return response;
```

The existing catch block already rethrows correctly:

```csharp
catch (AiAnalysisUnavailableException)
{
    throw;
}
```

No changes are needed there.

---

### Name matching

Use `StringComparer.OrdinalIgnoreCase`.

Model output casing may vary.

---

## Out of Scope

| Item | Deferred To |
|---|---|
| Flag domain invariants / expanding `FlagTests.cs` | Phase 2, separate PR |
| `GET` query environment validation placement decision | Phase 2, separate decision |
| Retry logic on AI validation failure | Phase 4 |
| Structured telemetry event on validation failure | Phase 4 |
| Surfacing failure reason to caller | Intentionally never |

---

## Learning Opportunities

### 1. Anti-Corruption Layer

`AiFlagAnalyzer` is the customs agent.

Nothing crosses the AI boundary without inspection.

This is the pattern in action.

---

### 2. Fail-Closed vs. Fail-Open

Bad output is blocked.

`503` is returned.

The system stays up.

Only the AI endpoint fails, in isolation.

---

### 3. Separation of Error Surfaces

Exception messages are for operators.

Response bodies are for callers.

Middleware acts as the translator.

---

## DX / Tooling Idea

Add a comment block in `smoke-test.http` above `POST /api/flags/health` instructing the developer to temporarily set `AzureOpenAI:Endpoint` to an invalid value in `appsettings.Development.json` to trigger the `503` path locally.

No test harness needed.

---

## Definition of Done

- [ ] `ValidateResponse` private method added to `AiFlagAnalyzer.cs`
- [ ] All four checks implemented:
  - [ ] Summary null or empty
  - [ ] Empty list
  - [ ] Coverage
  - [ ] Invalid status values
- [ ] Valid status values defined as `private static readonly HashSet<string>`
- [ ] Name matching uses `StringComparer.OrdinalIgnoreCase`
- [ ] `ValidateResponse` called after deserialization, before `return response`
- [ ] `ThrowingAiFlagAnalyzer` stub added to test project
- [ ] Integration test added: throwing stub returns `503` with correct shape
- [ ] All existing 146 tests pass
- [ ] `dotnet build` passes with zero warnings
- [ ] `dotnet csharpier check .` passes
