# Enforce Archived State as Terminal — Implementation Notes

**Session date:** 2026-05-01
**Branch:** `dev`
**Spec reference:** `Docs/Decisions/enforce-archive-state-as-terminal - PR# 59/spec.md`
**Build status:** `dotnet build Banderas.sln` passed with 0 warnings and 0 errors
**Tests:** New `FlagArchivedInvariantTests` passing (10/10); full unit suite green (117/117)
**PR:** #59

## What Was Built

`Flag` now treats archival as a terminal state. Every mutation method on the entity (`SetEnabled`, `UpdateStrategy`, `UpdateName`, `Update`, `Archive`) carries a guard clause as its first statement: if `IsArchived` is `true`, the method throws a new `FlagDomainException` with a message that names the flag. The exception extends `BanderasException` with `StatusCode = 409`, so the existing `GlobalExceptionMiddleware` catches it and writes a `409 Conflict` ProblemDetails response without any middleware change.

The change is contained entirely within the Domain layer — no controllers, services, repositories, or migrations were touched.

## Why This Change

The DDD audit (`Docs/Decisions/flag-ddd-analysis-backlog.md`) flagged that `Flag` allowed silent mutation after archival: `SetEnabled`, `Update*`, and even a second `Archive()` would succeed, advance `UpdatedAt`, and persist. That is incorrect domain behavior — an archived flag is a historical record, and mutations are either caller bugs or out-of-band operations that need an authorized, audited code path (deferred to Phase 3). Encoding the rule on the entity itself, rather than in a service or validator, follows the "make illegal states unrepresentable" principle and keeps the invariant in the only place that owns it.

## Key Decisions

**Single `FlagDomainException` over per-violation exception types.** The message carries the specificity; the type carries the HTTP status. Adding a new invariant in the future is a `throw new FlagDomainException(...)` — no new middleware case, no new exception class. This matches how `DuplicateFlagNameException` works today (a single type per violation category, not per field).

**Guard `Archive()` itself, not just the other four mutations.** Calling `Archive()` on an already-archived flag is almost always a caller bug; making it a silent no-op would hide that. Throwing surfaces it.

**Archived-guard runs *before* the existing whitespace-name guard in `UpdateName`.** This is an observable behavior shift: `UpdateName("")` on an archived flag now throws `FlagDomainException` instead of `ArgumentException`. Documented in the spec; no existing caller depends on the previous order.

**Domain throws, middleware catches — no intermediary handling.** `FlagDomainException : BanderasException` plugs directly into the existing `BanderasException` catch block. The 409 case in `GetTitleForStatusCode` was already present from prior work on `DuplicateFlagNameException`, so AC-4 was a verification step, not a code change.

## File-by-File Changes

| File | Change |
|---|---|
| `Banderas.Domain/Exceptions/FlagDomainException.cs` | New sealed 409-mapped domain exception |
| `Banderas.Domain/Entities/Flag.cs` | Added `using Banderas.Domain.Exceptions;`; guard clause as first statement of `SetEnabled`, `UpdateStrategy`, `UpdateName`, `Update`, `Archive` |
| `Banderas.Tests/Domain/FlagArchivedInvariantTests.cs` | New unit-test class — 10 tests covering all 5 archived-guard paths and a happy-path baseline per method |
No controller, service, repository, or middleware changes. No migration.

## Test Notes

`FlagArchivedInvariantTests` carries `[Trait("Category", "Unit")]` at the class level and on each `[Fact]`, matching the convention in `FlagTests`. Each archived-state test follows the same shape: build a flag with `FlagBuilder.Build()`, call `Archive()`, capture the second mutation in an `Action` variable, and assert with `act.Should().Throw<FlagDomainException>()`.

The five happy-path tests (`*_WhenFlagIsNotArchived_Succeeds`) were added beyond the initial spec proposal of a single baseline. The reasoning: the guard clause is hand-edited into five separate methods, and a copy-paste error like `if (!IsArchived)` would slip past a single-method baseline. One short happy-path assert per method costs almost nothing and catches that class of bug.

`FlagBuilder` was used as-is — no `BuildArchived()` helper added in this PR. The DX idea from the spec is left as a follow-up if archived-flag tests proliferate.

## Risks and Follow-Ups

- **No API-level integration test for the 409 mapping.** The middleware path (`FlagDomainException` → `409 Conflict` ProblemDetails) is verified by inspection only — `GetTitleForStatusCode` already includes the 409 case from prior work, and the catch block on `BanderasException` is unchanged. End-to-end coverage that issues a `PUT` or `DELETE` against an archived flag and asserts the response shape is deferred to a Phase 2 follow-up PR.
- **Unarchive is explicitly out of scope.** When it lands in Phase 3, it must be an auth-gated, audited operation — not a relaxed domain rule. The terminal guard added here is the correct default until that work happens.
- **`UpdateName` error-type shift.** Calling `UpdateName("")` on an archived flag now throws `FlagDomainException` instead of `ArgumentException`. No existing caller or test depends on the old order, but downstream consumers of the API who special-case the message text should be informed (none currently exist).

## How to Test

```bash
dotnet build Banderas.sln
dotnet test Banderas.sln --filter "FullyQualifiedName~FlagArchivedInvariantTests"
dotnet csharpier check .
```

## Interview Lens

The interesting decision here was *where* to put the rule. A pre-DDD instinct would be to add an `if (flag.IsArchived) throw ...` check in the service layer or as a FluentValidation rule on the request DTO. Both would work, and both would be wrong — they put the invariant in a place that can be bypassed. A new endpoint, a future bulk-import path, a seeding script, any of them could call `Flag.SetEnabled(...)` directly and silently mutate an archived flag. By encoding the rule on the entity, the only way to mutate an archived flag is to construct a new `Flag` — which the code path to do that does not exist.

The other useful constraint was the "exception hierarchy as a communication protocol" framing. The middleware does not need to know what `FlagDomainException` means; it only needs to know `BanderasException` and trust the `StatusCode` property. That decoupling means future invariant violations are a one-line addition: `throw new FlagDomainException("…")`. No middleware change, no new catch block, no new ProblemDetails mapping.

## Foundation Docs Updated

- [x] `Docs/current-state.md` — Phase 2 PR #59 line in Status Summary; `FlagDomainException` and archived-terminal invariant added under Domain Layer; test counts bumped to 165 (117 unit + 48 integration); Current Focus updated for Phase 2
- [x] `Docs/roadmap.md` — archived-terminal sub-bullet checked under Phase 2 "Strengthen `Flag` invariants"; Current Focus updated
- [x] `Docs/architecture.md` — no architecture changes required; `FlagDomainException` is a new subclass of the existing `BanderasException` hierarchy and the middleware path was already present
- [x] `Docs/Decisions/flag-ddd-analysis-backlog.md` — "Enforce archived state as terminal" flipped to `[X]` (PR #59)

## Definition of Done — Status

- [x] `FlagDomainException` exists in `Banderas.Domain/Exceptions/` and extends `BanderasException` with `StatusCode = 409`
- [x] Guard clause is the first statement in `SetEnabled()`
- [x] Guard clause is the first statement in `UpdateStrategy()`
- [x] Guard clause is the first statement in `UpdateName()`
- [x] Guard clause is the first statement in `Update()`
- [x] Guard clause is the first statement in `Archive()`
- [x] All 10 tests in `FlagArchivedInvariantTests.cs` pass (5 archived-guard + 5 happy-path)
- [x] `using Banderas.Domain.Exceptions;` added to top of `Flag.cs`
- [x] `[Trait("Category", "Unit")]` is present on the test class
- [x] `GlobalExceptionMiddleware.GetTitleForStatusCode` includes the `409` case (verified — already present)
- [x] `dotnet build` passes with zero warnings
- [x] `dotnet csharpier check .` passes
- [x] All existing tests continue to pass — no regressions (117/117 unit tests green)
