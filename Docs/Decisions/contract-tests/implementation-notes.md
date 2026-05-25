# API Response Contract Tests — Implementation Notes

**Session date:** 2026-05-17
**Branch:** feat/contract-tests
**Spec reference:** Docs/Decisions/contract-tests/spec.md
**Build status:** ✅ Passing — 0 warnings, 0 errors
**Tests:** 278/278 passing (203 unit + 75 integration)
**PR:** #65

---

## What Was Built

Five new integration tests in `ContractTests.cs` pin the JSON wire shape of every API
success response using raw `JsonDocument` assertions — not typed deserialization. Two
shared helper methods in `IntegrationTestBase` (`ReadProblemDetailsAsync` and
`ReadValidationProblemDetailsAsync`) were extended to assert camelCase field-name
presence on all error responses. A new `ReadRawJsonAsync` helper was added to
`IntegrationTestBase` as shared infrastructure for the contract tests and future tests
that need raw JSON access.

---

## Spec Gaps Resolved

None — the spec was complete and unambiguous throughout implementation.

---

## Deviations from Spec

None — implementation matched the spec exactly. All four design decisions were applied
as described.

---

## Key Decisions

**Double-read safety in `ReadProblemDetailsAsync`:** The updated helper reads the
response body twice — once into `JsonDocument` for field-name assertions, then again
via `ReadFromJsonAsync<ProblemDetails>` for typed value access. This is safe in
`WebApplicationFactory` tests because the response is memory-buffered, not a
one-shot stream. This would be unsafe against a real HTTP stream. The assumption was
flagged inline in the implementation notes during the HITL gate.

**`AssertFlagResponseShape` as a private static helper:** The 9 positional fields
common to all `FlagResponse` paths (create, get-single, get-all) were extracted into
a single `AssertFlagResponseShape` method. The 2 optional metadata fields
(`description`, `tags`) were separated into `AssertOptionalMetadataFields` because
AC-1 asserts specific values (null, empty array) while AC-2 and AC-3 only assert
presence. This split avoids over-constraining the shape assertions and matches the
spec's intent exactly.

**`ProblemDetails` contract assertions go into the base class helpers, not `ContractTests`:**
Because every error-path test across the entire suite calls `ReadProblemDetailsAsync`
or `ReadValidationProblemDetailsAsync`, placing the field-name assertions there means
existing tests get AC-6 and AC-7 coverage for free — without any test additions.
This is more reliable than a dedicated test: the assertion runs on every error path,
not just the one created for the contract test.

---

## File-by-File Changes

| File | Change | Lines |
|------|--------|-------|
| `Banderas.Tests.Integration/ContractTests.cs` | Created — 5 contract tests covering all 4 success response shapes | ~280 |
| `Banderas.Tests.Integration/Fixtures/IntegrationTestBase.cs` | Added `ReadRawJsonAsync`; added field-name assertions to `ReadProblemDetailsAsync` and `ReadValidationProblemDetailsAsync` | +20 |

No production files changed.

---

## Risks and Follow-Ups

- **Exhaustive field absence not yet pinned** — the current tests assert that documented
  fields are present; they do not assert that no undocumented fields exist. This was
  consciously deferred to Phase 7 when the SDK spec will define the canonical shape.
  At that point, a stricter contract test using `JsonElement.EnumerateObject()` to
  enumerate and compare all field names would be appropriate.

- **`description` always serialized as `null` when absent** — the contract test confirms
  `description` is `JsonValueKind.Null` (not absent). This is the correct contract, but
  it means any future change to `FlagResponse` that makes `description` use
  `[JsonIgnore(Condition = WhenWritingNull)]` would break this test. That's intentional —
  the test is the guard.

---

## How to Test

```bash
# From the worktree or the main working tree after merge:
dotnet test Banderas.Tests.Integration/Banderas.Tests.Integration.csproj \
  --filter "Category=Integration"

# To run only contract tests:
dotnet test Banderas.Tests.Integration/Banderas.Tests.Integration.csproj \
  --filter "FullyQualifiedName~ContractTests"
```

---

## Interview Lens

The core engineering decision here was using `JsonDocument` instead of typed
deserialization for contract assertions. The problem: `ReadFromJsonAsync<FlagResponse>`
with `PropertyNameCaseInsensitive = true` would silently pass if `strategyType` were
renamed to `strategy_type` on the wire — it would just map the value to the matching
C# property regardless of casing. The tradeoff is verbosity: `JsonDocument` assertions
are 3–5 lines per field vs one-line typed access. At the current scale this is fine.
At the scale of a full SDK with dozens of response types, you'd want a contract-test
framework (e.g., consumer-driven contract testing with Pact) that generates assertions
from a schema rather than writing them by hand. For now, `JsonDocument` with
`TryGetProperty` is the minimum viable solution that actually catches the bug class it
was designed for.

---

## Foundation Docs Updated

- [x] `Docs/current-state.md` — status summary, test counts, completed work, next tasks, lesson learned
- [x] `Docs/roadmap.md` — Phase 2 contract tests item checked, current focus updated
- [ ] `Docs/architecture.md` — no changes (no new layers, patterns, or external dependencies)

---

## Definition of Done — Status

- [x] ✅ `ContractTests.cs` created with `[Collection("Integration")]`, inheriting `IntegrationTestBase`
- [x] ✅ `ReadRawJsonAsync` helper added to `IntegrationTestBase`
- [x] ✅ `ReadProblemDetailsAsync` asserts `type`, `title`, `status`, `detail` camelCase field presence (AC-6)
- [x] ✅ `ReadValidationProblemDetailsAsync` asserts `errors` camelCase field presence (AC-7)
- [x] ✅ AC-1: `FlagResponse` shape pinned on POST create — 11 fields, enums as strings, `description` null, `tags` empty array
- [x] ✅ AC-2: `FlagResponse` shape pinned on GET single — 11 fields, `Content-Type: application/json`
- [x] ✅ AC-3: `FlagResponse[]` shape pinned on GET all — array element shape, `Content-Type: application/json`
- [x] ✅ AC-4: `EvaluationResponse` shape pinned — `isEnabled` field, boolean kind, `Content-Type: application/json`
- [x] ✅ AC-5: `FlagHealthAnalysisResponse` shape pinned — top-level fields and nested `FlagAssessment` fields, `Content-Type: application/json`
- [x] ✅ `dotnet build -p:TreatWarningsAsErrors=true` — 0 warnings, 0 errors
- [x] ✅ All new tests green; no existing tests broken (278/278)
- [x] ✅ CSharpier formatting passes (`dotnet csharpier check .`)
