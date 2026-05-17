# Specification: API Response Contract Tests

**Document:** Docs/Decisions/contract-tests/spec.md
**Status:** Approved — Implemented
**Branch:** feat/contract-tests
**PR:** TBD
**Phase:** Phase 2 — Testing & Reliability
**Depends on:** None
**Author:** Banderas team
**Date:** 2026-05-17

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
- [Definition of Done](#definition-of-done)

---

## User Story

As an SDK author (and future consumer of this API), I want the JSON wire format of
every API response to be pinned by tests, so that serialization regressions — enum
format changes, field renames, casing changes, missing optional fields — are caught
before the contract breaks external consumers.

---

## Background and Goals

The existing integration tests verify behavioral correctness — status codes, field
values, error key presence — but none pin the raw JSON wire shape. A `FlagResponse`
field renamed from `strategyType` to `strategy_type`, an enum serialized as an integer
instead of a string, or `tags` omitted when empty would all pass the current suite.

Contract tests close this gap: they parse the raw `JsonDocument` instead of
deserializing into a typed model, asserting exact camelCase field names, enum string
values, and that optional fields (`description`, `tags`) are always present in the
JSON even when null or empty.

This matters now because the Phase 7 .NET SDK will consume these shapes, and pinning
them before SDK work begins is far cheaper than retrofitting after a breaking change
has already shipped.

---

## Design Decisions

### Decision 1: Raw `JsonDocument` vs typed deserialization

**Choice:** Contract tests parse `response.Content.ReadAsStringAsync()` into
`JsonDocument` and assert field names as string literals — not
`ReadFromJsonAsync<FlagResponse>`.

**Why:** Typed deserialization silently tolerates missing fields and casing mismatches
because `PropertyNameCaseInsensitive = true` in the test `JsonOptions`. A contract
test must fail if a field is absent or misspelled on the wire. `JsonDocument` does not
apply any tolerance — it sees the raw bytes.

**Tradeoff:** Slightly more verbose assertions; worth it because the tests now catch
exactly the class of bug they are designed to catch.

---

### Decision 2: New dedicated file vs extending existing tests

**Choice:** New file `ContractTests.cs` in `Banderas.Tests.Integration/`. The existing
files test behavior; this file tests shape.

**Why:** Mixing behavioral and contract assertions in the same test class blurs intent
and makes failures harder to diagnose. A red contract test means "the wire format
changed"; a red behavioral test means "the feature broke." Keeping them separate
preserves that signal.

**Tradeoff:** One more file in the integration project. Acceptable — the file has a
clear, bounded responsibility.

---

### Decision 3: Scope — which response shapes get contract tests

**Choice:** Pin all four distinct success response shapes:
- `FlagResponse` (POST create, GET single, GET all)
- `EvaluationResponse` (POST evaluate)
- `FlagHealthAnalysisResponse` including nested `FlagAssessment` (POST health)
- `ProblemDetails` and `ValidationProblemDetails` field names (via updated
  `IntegrationTestBase` helpers)

**Why:** These are the complete set of shapes an SDK consumer or external caller will
depend on. Partial pinning leaves gaps that grow over time.

**Tradeoff:** `ProblemDetails` field assertions go into the shared helper methods
rather than `ContractTests.cs`, since those helpers are already called by every error
path test across the suite.

---

### Decision 4: `Content-Type: application/json` assertion placement

**Choice:** Assert `Content-Type: application/json; charset=utf-8` inside each
contract test on success paths. Do not add it to `IntegrationTestBase`.

**Why:** Success content-type is part of the wire contract and belongs co-located with
the shape assertions. Error content-type (`application/problem+json`) is already
guarded by the existing `AssertProblemContentType` helper — no duplication needed.

**Tradeoff:** Some repetition across contract test methods. Acceptable — each test
should be independently readable without consulting a base class.

---

## Architecture Overview

No new production components, interfaces, or layers are introduced. This is purely
additive test coverage within the existing integration test project.

**Changes:**

- `Banderas.Tests.Integration/ContractTests.cs` — new test class
- `Banderas.Tests.Integration/Fixtures/IntegrationTestBase.cs` — adds one
  `ReadRawJsonAsync` helper method; adds camelCase field-name assertions to
  `ReadProblemDetailsAsync` and `ReadValidationProblemDetailsAsync`

No new NuGet packages. `System.Text.Json` is already an in-box dependency.

---

## Scope

### Files created

| File | Purpose |
|------|---------|
| `Banderas.Tests.Integration/ContractTests.cs` | New test class pinning all success response wire shapes |

### Files modified

| File | Change |
|------|--------|
| `Banderas.Tests.Integration/Fixtures/IntegrationTestBase.cs` | Add `ReadRawJsonAsync` helper; add field-name assertions to `ReadProblemDetailsAsync` and `ReadValidationProblemDetailsAsync` |

### Files not touched

All production projects (`Banderas.Api`, `Banderas.Application`, `Banderas.Domain`,
`Banderas.Infrastructure`) are unchanged. No DTO, controller, or serialization
configuration is modified.

---

## Acceptance Criteria

### AC-1: `FlagResponse` shape — POST create

**Given** a valid `POST /api/flags` request with no description or tags,
**When** the response is `201 Created`,
**Then** the JSON body contains exactly these camelCase fields: `id`, `name`,
`environment`, `isEnabled`, `isArchived`, `strategyType`, `strategyConfig`,
`createdAt`, `updatedAt`, `description`, `tags`; AND `environment` and `strategyType`
are `JsonValueKind.String`; AND `description` is `JsonValueKind.Null`; AND `tags` is
`JsonValueKind.Array` with length 0.

---

### AC-2: `FlagResponse` shape — GET single

**Given** an existing flag,
**When** `GET /api/flags/{name}?environment=Development` returns `200 OK`,
**Then** the JSON body contains the same 11 fields as AC-1, AND `Content-Type` header
is `application/json`.

---

### AC-3: `FlagResponse[]` shape — GET all

**Given** at least one existing flag,
**When** `GET /api/flags?environment=Development` returns `200 OK`,
**Then** the body is a `JsonValueKind.Array`; the first element contains the same 11
fields as AC-1; AND `Content-Type` header is `application/json`.

---

### AC-4: `EvaluationResponse` shape

**Given** a valid `POST /api/evaluate` request against an existing enabled flag,
**When** the response is `200 OK`,
**Then** the JSON body contains exactly one field: `isEnabled` of
`JsonValueKind.True` or `JsonValueKind.False`; AND `Content-Type` header is
`application/json`.

---

### AC-5: `FlagHealthAnalysisResponse` shape

**Given** a valid `POST /api/flags/health` request,
**When** the response is `200 OK`,
**Then** the JSON body contains: `summary` (`JsonValueKind.String`), `flags`
(`JsonValueKind.Array`), `analyzedAt` (`JsonValueKind.String`),
`stalenessThresholdDays` (`JsonValueKind.Number`); AND each element in `flags`
contains: `name`, `status`, `reason`, `recommendation` — all `JsonValueKind.String`;
AND `Content-Type` header is `application/json`.

---

### AC-6: `ProblemDetails` field names

**Given** any request that produces a `4xx` or `5xx` non-validation error,
**When** `ReadProblemDetailsAsync` processes the response,
**Then** the raw JSON contains camelCase fields `type`, `title`, `status`, `detail` —
all present as keys in the document.

---

### AC-7: `ValidationProblemDetails` field names

**Given** a request that produces a `400` validation error,
**When** `ReadValidationProblemDetailsAsync` processes the response,
**Then** the raw JSON contains the `errors` field as a camelCase key with at least one
entry.

---

## File Layout

```
Banderas.Tests.Integration/
├── ContractTests.cs                    ← new
└── Fixtures/
    └── IntegrationTestBase.cs          ← modified (ReadRawJsonAsync + field assertions)
```

---

## Technical Notes

- **`JsonDocument` disposal:** Wrap in `using var doc = JsonDocument.Parse(...)` — the
  document rents pooled memory and must be disposed after assertions complete.

- **Field presence check:** `JsonElement.TryGetProperty("fieldName", out var prop)`
  returns `false` for absent fields. Use `.Should().BeTrue()` on the return value to
  assert presence. Then use `prop.ValueKind` for type assertions.

- **Null field vs absent field:** For `description: null`, assert
  `TryGetProperty("description", out var d)` returns `true` AND
  `d.ValueKind == JsonValueKind.Null`. An absent field and a null field are different
  contracts.

- **Empty array field:** For `tags: []`, assert `TryGetProperty("tags", out var t)`
  returns `true` AND `t.ValueKind == JsonValueKind.Array` AND
  `t.GetArrayLength() == 0`.

- **Enum string assertion:** For `environment` and `strategyType`, assert
  `ValueKind == JsonValueKind.String`. The exact string value (e.g. `"Development"`)
  is already covered by behavioral tests; the contract test only pins that it is a
  string, not an integer.

- **`ReadRawJsonAsync` helper signature:**
  ```csharp
  protected static async Task<JsonDocument> ReadRawJsonAsync(HttpResponseMessage response)
  {
      string json = await response.Content.ReadAsStringAsync();
      return JsonDocument.Parse(json);
  }
  ```
  Caller is responsible for disposal (`using var doc = await ReadRawJsonAsync(response)`).

- **`Content-Type` assertion:**
  `response.Content.Headers.ContentType?.MediaType.Should().Be("application/json")`

- **No new packages:** `System.Text.Json` is in-box. `FluentAssertions` is already
  referenced in the integration test project.

- **Build sequence:** Only `Banderas.Tests.Integration` is affected. Run
  `dotnet test Banderas.Tests.Integration` to verify.

- **Trait consistency:** Apply `[Trait("Category", "Integration")]` at class level and
  on each `[Fact]`, consistent with the existing suite.

---

## Out of Scope

- **Field ordering** — JSON property order is not part of the contract; `System.Text.Json`
  does not guarantee order and consumers must not depend on it.
- **Exhaustive field absence ("no undocumented fields")** — asserting only documented
  fields exist is a stricter contract style; deferred until the Phase 7 SDK spec
  defines the canonical shape.
- **Response body on `204 No Content`** — PUT and DELETE return no body by design.
- **`Location` header format on `201`** — already covered in `FlagEndpointTests`.
- **AI response semantic validation** — already covered by `AiFlagAnalyzerValidationTests`;
  contract tests only pin the outer HTTP shape.
- **`Content-Type` negotiation** — no `Accept` header variation testing.
- **Snapshot / golden-file testing** — not introduced; assertions remain code-level.

---

## Learning Opportunities

1. **`JsonDocument` and `JsonElement` API** — .NET's low-allocation DOM parser;
   `TryGetProperty`, `GetProperty`, `ValueKind`, `GetArrayLength()`, and the
   `using`-disposal pattern for rented buffer memory. Contrast with typed
   deserialization: typed deserialization tolerates schema drift silently;
   `JsonDocument` does not.

2. **Behavioral tests vs contract tests** — behavioral tests assert *what a system
   does* (flag is created, response is 201); contract tests assert *how it
   communicates* (field names, types, serialization format). Both are necessary;
   mixing them produces tests that are hard to diagnose when they fail.

3. **`JsonStringEnumConverter` and its observable effect on the wire** — understanding
   that `RolloutStrategy.None` serializes as `"None"` (not `0`) because of a globally
   registered converter, and why a contract test asserting `ValueKind == JsonValueKind.String`
   on an enum field would catch a converter being accidentally removed.

---

## Definition of Done

- [ ] `Banderas.Tests.Integration/ContractTests.cs` created with `[Collection("Integration")]`,
      inheriting `IntegrationTestBase`
- [ ] `ReadRawJsonAsync` helper added to `IntegrationTestBase`
- [ ] `ReadProblemDetailsAsync` asserts `type`, `title`, `status`, `detail` camelCase
      field presence (AC-6)
- [ ] `ReadValidationProblemDetailsAsync` asserts `errors` camelCase field presence (AC-7)
- [ ] AC-1: `FlagResponse` shape pinned on POST create — all 11 fields, enums as
      strings, `description` null, `tags` empty array
- [ ] AC-2: `FlagResponse` shape pinned on GET single — 11 fields, `Content-Type: application/json`
- [ ] AC-3: `FlagResponse[]` shape pinned on GET all — array element shape,
      `Content-Type: application/json`
- [ ] AC-4: `EvaluationResponse` shape pinned — `isEnabled` field, boolean kind,
      `Content-Type: application/json`
- [ ] AC-5: `FlagHealthAnalysisResponse` shape pinned — top-level fields and nested
      `FlagAssessment` fields, `Content-Type: application/json`
- [ ] `dotnet build -p:TreatWarningsAsErrors=true` passes with no new warnings
- [ ] All new tests green; no existing tests broken
- [ ] CSharpier formatting passes (`dotnet csharpier check .`)
