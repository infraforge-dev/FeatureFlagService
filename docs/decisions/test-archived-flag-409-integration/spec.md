# Specification: Archived-Flag Integration Test Coverage

**Document:** docs/decisions/test-archived-flag-409-integration/spec.md
**Status:** Draft
**Branch:** test/archived-flag-409-integration
**PR:** TBD
**Phase:** Phase 2 — Testing & Reliability
**Depends on:** None (PR #59 already merged)
**Author:** Developer
**Date:** 2026-05-05

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

## User Story

As a developer maintaining the Banderas API, I want integration tests that verify
the HTTP behavior when clients attempt to mutate archived flags, so that the archival
boundary is covered end-to-end and not just verified by code inspection.

## Background and Goals

PR #59 landed archived-as-terminal domain guards on all five `Flag` mutation methods
and 10 unit tests in `FlagArchivedInvariantTests`. API-level integration coverage was
explicitly deferred to a Phase 2 follow-up. The current-state doc calls this out as an
immediate next task.

**Goals:**

1. **Close the deferred gap from PR #59** — prove the HTTP surface behaves correctly
   when clients target archived flags.
2. **Document the architectural reality** — the repository filter (`!f.IsArchived` on
   all queries) is the primary guard, returning 404 before the domain exception can
   fire. Tests should assert what actually happens (404), not what the domain guards
   throw (409).
3. **Cover the middleware 409 contract** — even though the HTTP API does not currently
   trigger `FlagDomainException`, the middleware mapping exists and should have at
   least one integration test proving the ProblemDetails shape is correct for when
   future code paths reach it.

## Design Decisions

### DD-1: Test what the API actually returns, not what the domain guards throw

The repository filters archived flags with `!f.IsArchived`, so `PUT` and `DELETE` on
an archived flag return 404, not 409. Tests assert 404 — that is the real contract
clients see. Testing 409 via HTTP would require bypassing the repo filter, which
misrepresents the API's behavior.

**Tradeoff:** The `FlagDomainException` -> 409 middleware path is not exercised via a
real user flow. Acceptable because unit tests already cover the domain guards, and the
middleware mapping is covered separately (DD-2).

### DD-2: One dedicated middleware-level test for the 409 ProblemDetails shape

Register a test-only endpoint in `BanderasApiFactory` that throws
`FlagDomainException`, then assert the response is RFC 9457 ProblemDetails with
status 409, title "Conflict", and the exception message in `detail`.

**Tradeoff:** This is a synthetic test that does not exercise a real user flow. But it
is the only way to cover the middleware contract without coupling to implementation
details of the repository filter. The alternative (no 409 shape test) leaves the
middleware mapping verified only by inspection.

### DD-3: Include archived-flag evaluation in scope

`POST /api/evaluate` against an archived flag should also return 404 (repository
filter). This is a distinct user-visible behavior worth covering — a client might
reasonably try to evaluate a flag that was recently archived.

**Tradeoff:** Small scope increase (one test), but closes an obvious gap in
`EvaluationEndpointTests`.

## Architecture Overview

No new production components. No layer boundaries crossed. Purely additive test code.

**Test topology:**

```
BanderasApiFactory (existing)
├── Registers test-only endpoint: GET /test/throw-domain-exception
│   └── Throws FlagDomainException directly → middleware catches → 409 ProblemDetails
│
FlagEndpointTests (existing file, new tests)
├── PUT archived flag → 404
├── DELETE already-archived flag → 404
│
EvaluationEndpointTests (existing file, new tests)
├── POST /api/evaluate archived flag → 404
│
ArchivedFlag409MiddlewareTests (new file)
├── GET /test/throw-domain-exception → 409 ProblemDetails shape assertion
```

The test-only endpoint lives inside `BanderasApiFactory`'s `ConfigureWebHost`
override — it never ships in production.

## Scope

### Files modified

| File | Change |
|------|--------|
| `Banderas.Tests.Integration/Fixtures/BanderasApiFactory.cs` | Register test-only endpoint that throws `FlagDomainException` |
| `Banderas.Tests.Integration/FlagEndpointTests.cs` | Add 2 tests: PUT archived → 404, DELETE already-archived → 404 |
| `Banderas.Tests.Integration/EvaluationEndpointTests.cs` | Add 1 test: evaluate archived flag → 404 |

### Files created

| File | Purpose |
|------|---------|
| `Banderas.Tests.Integration/ArchivedFlag409MiddlewareTests.cs` | 1 test: synthetic `FlagDomainException` → 409 ProblemDetails shape |

### Files not touched

Zero changes to anything under `Banderas.Api/`, `Banderas.Application/`,
`Banderas.Domain/`, or `Banderas.Infrastructure/`.

## Acceptance Criteria

### AC-1: PUT on archived flag returns 404

- **Given** a flag exists and has been archived via
  `DELETE /api/flags/{name}?environment=Development`
- **When** a client sends `PUT /api/flags/{name}?environment=Development` with a
  valid `UpdateFlagRequest`
- **Then** the response is `404 Not Found` with `application/problem+json` content type

### AC-2: DELETE on already-archived flag returns 404

- **Given** a flag has already been archived
- **When** a client sends `DELETE /api/flags/{name}?environment=Development`
- **Then** the response is `404 Not Found` with `application/problem+json` content type

### AC-3: Evaluate archived flag returns 404

- **Given** a flag has been archived
- **When** a client sends `POST /api/evaluate` with that flag's name and environment
- **Then** the response is `404 Not Found` with `application/problem+json` content type

### AC-4: FlagDomainException middleware mapping produces correct 409 ProblemDetails

- **Given** a request hits an endpoint that throws `FlagDomainException`
- **When** the middleware catches it
- **Then** the response status is `409`
- **And** content type is `application/problem+json`
- **And** body contains `title: "Conflict"`, `status: 409`, and `detail` matching the
  exception message

## File Layout

```
Banderas.Tests.Integration/
├── Fixtures/
│   └── BanderasApiFactory.cs          (modified — test-only endpoint)
├── FlagEndpointTests.cs               (modified — +2 tests)
├── EvaluationEndpointTests.cs         (modified — +1 test)
└── ArchivedFlag409MiddlewareTests.cs   (new — +1 test)
```

## Technical Notes

1. **Test-only endpoint registration** — Use `app.MapGet("/test/throw-domain-exception", ...)`
   inside `BanderasApiFactory.ConfigureWebHost`. The endpoint throws
   `new FlagDomainException("Test flag 'x' is archived and cannot be modified.")`.
   This never appears in the production route table.

2. **Test setup pattern for archived flags** — Each test creates a flag via
   `POST /api/flags`, archives it via `DELETE /api/flags/{name}?environment=Development`,
   then attempts the mutation. This follows the existing create-then-archive pattern in
   `ArchiveFlag_Exists_Returns204AndExcludedFromGetAllAsync`.

3. **ProblemDetails deserialization** — Use `System.Text.Json` to deserialize the
   response body as `JsonDocument` and assert individual properties (`status`, `title`,
   `detail`, `type`). This avoids coupling to a specific ProblemDetails DTO. Existing
   integration tests already use this approach.

4. **No new NuGet packages required.**

5. **Test count impact** — Net +4 integration tests (48 → 52). Test naming follows the
   existing `Method_Scenario_ExpectedResultAsync` convention with
   `[Trait("Category", "Integration")]`.

## Out of Scope

- No production code changes — no controller `[ProducesResponseType]` additions, no
  repository changes, no domain changes
- No unarchive endpoint — deferred to Phase 3 (requires auth + audit trail per DD-4
  in PR #59)
- No typed `StrategyConfig` Value Objects — separate Phase 2 item
- No contract tests for non-archived API responses — separate Phase 2 backlog item
- No mutation testing — separate Phase 2 backlog item

## Learning Opportunities

1. **WebApplicationFactory test-only endpoints** — registering synthetic routes inside
   the test host to isolate middleware behavior without coupling to real service flows.
   A pattern that transfers to any ASP.NET Core project.

2. **Defense-in-depth testing strategy** — when two layers guard the same invariant
   (repository filter + domain guard), integration tests should assert what clients
   actually see (404), while targeted synthetic tests cover the secondary guard (409
   middleware mapping).

## Definition of Done

- [ ] `PUT` on archived flag returns 404 — integration test passing
- [ ] `DELETE` on already-archived flag returns 404 — integration test passing
- [ ] `POST /api/evaluate` on archived flag returns 404 — integration test passing
- [ ] Synthetic `FlagDomainException` → 409 ProblemDetails shape — integration test passing
- [ ] All existing tests still pass (165 + 4 = 169)
- [ ] `dotnet build -p:TreatWarningsAsErrors=true` passes
- [ ] CSharpier format check passes
- [ ] No production code modified
