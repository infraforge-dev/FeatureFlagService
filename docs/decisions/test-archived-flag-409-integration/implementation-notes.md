# Archived-Flag Integration Test Coverage — Implementation Notes

**Session date:** 2026-05-05
**Branch:** test/archived-flag-409-integration
**Spec reference:** docs/decisions/test-archived-flag-409-integration/spec.md
**Build status:** Passing — 0 warnings, 0 errors
**Tests:** 169/169 passing (115 unit + 54 integration)
**PR:** TBD

## What Was Built

Four integration tests that close the deferred gap from PR #59. Three tests verify
that the HTTP API returns 404 when clients attempt to mutate archived flags (PUT,
DELETE, and evaluate), documenting that the repository's `!f.IsArchived` filter is the
primary enforcement mechanism. One synthetic test verifies the `FlagDomainException` →
409 ProblemDetails middleware contract via a test-only endpoint registered through
`IStartupFilter` in `BanderasApiFactory`.

## Spec Gaps Resolved

None — implementation matched the spec exactly.

## Deviations from Spec

The spec's Technical Note 1 suggested `app.MapGet("/test/throw-domain-exception", ...)`
inside `ConfigureWebHost`. The implementation used `IStartupFilter` with
`IApplicationBuilder.Map()` instead, because `WebApplicationFactory` does not expose
`WebApplication`'s minimal API `MapGet` from the `ConfigureWebHost` override. The
`IStartupFilter` approach achieves the same result — a test-only endpoint that throws
`FlagDomainException` downstream of `GlobalExceptionMiddleware` — without requiring
access to the `WebApplication` instance.

## Key Decisions

1. **`IStartupFilter` with `next(app)` first** — calling `next(app)` before registering
   the test middleware ensures it is added at the end of the pipeline, downstream of
   `GlobalExceptionMiddleware`. This means the exception bubbles back up through the
   real middleware, giving a genuine integration test of the 409 mapping.

2. **`IApplicationBuilder.Map()` for path branching** — used `Map("/test/...", branch
   => branch.Run(...))` to isolate the test endpoint to a specific path without
   interfering with the rest of the pipeline. Unmatched paths pass through normally.

3. **Asserting `detail` content, not exact message** — the 409 middleware test asserts
   that `detail` contains `"test-flag"` and `"archived and cannot be modified"` rather
   than an exact string match, making the test resilient to minor message rewording.

## File-by-File Changes

| File | Change |
|------|--------|
| `Banderas.Tests.Integration/Fixtures/BanderasApiFactory.cs` | Added `using` for `Banderas.Domain.Exceptions` and `Microsoft.AspNetCore.Builder`. Registered `TestDomainExceptionEndpointFilter` as `IStartupFilter`. Added private `TestDomainExceptionEndpointFilter` class that maps `GET /test/throw-domain-exception` and throws `FlagDomainException`. |
| `Banderas.Tests.Integration/FlagEndpointTests.cs` | Added `UpdateFlag_ArchivedFlag_Returns404ProblemDetailsAsync` and `ArchiveFlag_AlreadyArchived_Returns404ProblemDetailsAsync`. Both follow the create-then-archive-then-mutate pattern. |
| `Banderas.Tests.Integration/EvaluationEndpointTests.cs` | Added `Evaluate_ArchivedFlag_Returns404Async`. Creates a flag, archives it, then attempts evaluation. |
| `Banderas.Tests.Integration/ArchivedFlag409MiddlewareTests.cs` | **New file.** Single test class with `FlagDomainException_Returns409ConflictProblemDetailsAsync` that hits the synthetic test endpoint and asserts 409 status, `application/problem+json` content type, `"Conflict"` title, and exception message in `detail`. |

## Risks and Follow-Ups

- **Test count drift** — current-state.md previously listed 117 unit tests but the
  actual count is 115. This was pre-existing (not caused by this PR). The doc has been
  updated to reflect the actual count.
- **`IStartupFilter` ordering with minimal hosting** — the current approach works
  because `next(app)` is called first, placing the test middleware at the end of the
  pipeline. If the app's startup pipeline changes significantly (e.g., switching to a
  different hosting model), the test endpoint ordering should be re-verified.

## How to Test

```bash
# Run just the 4 new tests
dotnet test Banderas.Tests.Integration \
  --filter "UpdateFlag_ArchivedFlag|ArchiveFlag_AlreadyArchived|Evaluate_ArchivedFlag|FlagDomainException_Returns409" \
  --verbosity normal

# Run full suite
dotnet test Banderas.sln --verbosity normal
```

## Interview Lens

The most interesting decision was how to integration-test a middleware mapping that no
real HTTP endpoint currently triggers. The repository layer filters archived flags
before the domain guard fires, so `PUT`/`DELETE` on an archived flag returns 404, not
409. But the `FlagDomainException` → 409 middleware path exists as a defense-in-depth
contract. Rather than coupling the test to repository internals or bypassing the filter,
we registered a synthetic test-only endpoint via `IStartupFilter` that throws the
domain exception directly. This keeps the test honest — it exercises the real middleware
pipeline without misrepresenting the API's actual behavior. At higher scale with more
exception types, I'd consider a parameterized test fixture that registers multiple
synthetic endpoints from a list of exception-to-status mappings, turning the middleware
contract into a data-driven test suite.

## Foundation Docs Updated

- [x] `Docs/current-state.md` — status summary, test counts, test section, current
  focus, lessons learned
- [x] `Docs/roadmap.md` — Phase 2 checklist items, phase map status, current focus
- [ ] `Docs/architecture.md` — no changes needed (no new production components)

## Definition of Done — Status

- [x] `PUT` on archived flag returns 404 — `UpdateFlag_ArchivedFlag_Returns404ProblemDetailsAsync`
- [x] `DELETE` on already-archived flag returns 404 — `ArchiveFlag_AlreadyArchived_Returns404ProblemDetailsAsync`
- [x] `POST /api/evaluate` on archived flag returns 404 — `Evaluate_ArchivedFlag_Returns404Async`
- [x] Synthetic `FlagDomainException` → 409 ProblemDetails shape — `FlagDomainException_Returns409ConflictProblemDetailsAsync`
- [x] All existing tests still pass (169/169)
- [x] `dotnet build -p:TreatWarningsAsErrors=true` passes
- [x] CSharpier format check passes
- [x] No production code modified
