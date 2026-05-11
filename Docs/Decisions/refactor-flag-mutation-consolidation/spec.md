# Specification: Consolidate `Flag` Mutation Methods by Concern

**Document:** Docs/Decisions/refactor-flag-mutation-consolidation/spec.md
**Status:** Draft
**Branch:** refactor/flag-mutation-consolidation
**PR:** TBD
**Phase:** Phase 2 — Testing & Reliability (Flag DDD analysis backlog item)
**Depends on:** None (typed `StrategyConfig` VO refactor, PR #62, already merged)
**Author:** Developer
**Date:** 2026-05-11

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

**As** a future contributor extending `Flag` (adding variations, targeting rules, or a separate `FlagEnvironmentConfig` aggregate),
**I want** the entity's mutation surface to be organized by domain concern rather than by individual field,
**so that** I can reason about *what kind of change is happening* rather than mechanically wiring per-field setters, and so that the partial-mutation bug class (e.g. "did the caller intend to keep strategy unchanged?") is eliminated by construction.

---

## Background and Goals

`Flag` today exposes four mutation methods:

| Method | Mutates | Production caller |
|---|---|---|
| `SetEnabled(bool)` | `IsEnabled`, `UpdatedAt` | none (test-only) |
| `UpdateStrategy(RolloutStrategy, StrategyConfig)` | `StrategyType`, `StrategyConfig`, `UpdatedAt` | none (test-only) |
| `Update(bool, RolloutStrategy, StrategyConfig)` | `IsEnabled`, `StrategyType`, `StrategyConfig`, `UpdatedAt` | `BanderasService.UpdateFlagAsync` |
| `UpdateName(string)` | `Name`, `UpdatedAt` | none (test-only) |

`SetEnabled` and `UpdateStrategy` are field-shaped methods left over from an earlier draft of the domain model; the application layer never calls them. `Update` is the atomic rollout-config mutation that production actually uses. `UpdateName` is a distinct rename concern.

The DDD analysis (`Docs/Decisions/flag-ddd-analysis-backlog.md`) identified that **`SetEnabled` + `UpdateStrategy` + `Update` are three slices of the same domain concern**: *changing how this flag rolls out for this environment.* The concern-level operation is "reconfigure the rollout"; the per-field methods are an artifact of CRUD thinking that bled into the domain.

**Goals:**

1. Reduce three mutation entry points to one whose name describes the *concern*, not the *fields*.
2. Eliminate the public surface area for partial rollout updates (no caller can flip `IsEnabled` without restating strategy + config — removing a class of "stale strategy" bugs in any future caller).
3. Preserve `UpdateName` as a separate concern (rename ≠ reconfigure).
4. Zero changes to the public HTTP API, DTO contracts, or `IBanderasService` signatures.
5. Preserve archived-state-terminal and config/strategy-consistency invariants exactly as they exist today.

---

## Design Decisions

### DD-1 — Rename `Update` to `Reconfigure`; delete `SetEnabled` and `UpdateStrategy`

**Decision:** The single rollout-mutation method on `Flag` is named `Reconfigure(bool isEnabled, RolloutStrategy strategyType, StrategyConfig strategyConfig)`. `SetEnabled` and `UpdateStrategy` are deleted.

**Why a rename rather than keeping `Update`:** The backlog item is explicit — "separate by concern not field." `Update` is concern-blind; it reads as a generic CRUD verb. `Reconfigure` names the domain operation: *change this flag's rollout configuration as a single atomic act.* Naming is the load-bearing signal that the consolidation happened; without the rename, a future reader sees four mutation methods (now three) and assumes the same field-shaped thinking still applies.

**Tradeoff:** One application call site (`BanderasService.UpdateFlagAsync` line 191) and four test files must be updated. Diff cost is small; clarity gain is durable.

**Rejected alternative — `UpdateRollout`:** Less clear that the operation is *atomic and complete*, not a partial patch. `Reconfigure` carries the "starting over from the supplied values" connotation that matches the semantics (full replacement of the rollout triple).

**Rejected alternative — keep `Update` and just delete the two field-shaped methods:** Half the refactor. The remaining method's name would still suggest the deleted methods are valid patterns ("if `Update` exists, why not `SetEnabled`?"). The rename closes the loop.

### DD-2 — `UpdateName` stays a distinct concern, not folded into `Reconfigure`

**Decision:** `UpdateName(string name)` remains a separate public method.

**Why:** Rename is a metadata/display concern with different operational cadence (rare, often human-triggered for clarity), different validation rules (regex + uniqueness at the boundary), and different audit semantics (rename history is a separate question from rollout history). Coupling it to `Reconfigure` would force callers to restate the rollout config to change a name, or force `Reconfigure` to accept an optional name — either choice corrupts the concern boundary.

**Tradeoff:** No atomic "rename + reconfigure" entry point. There is no production caller for that combined operation today; YAGNI applies.

### DD-3 — No public API surface changes

**Decision:** `IBanderasService`, request/response DTOs, controller signatures, and HTTP status codes are untouched. This is a pure domain-layer rename + delete with one downstream application-layer call-site update.

**Why:** The change is a refactor of the domain's internal vocabulary, not a behavior change. Keeping the public API stable means no integration test needs to change to *prove the same behavior*, and the PR diff stays focused on the domain-model intent.

**Tradeoff:** Integration tests that happen to call `Flag` directly (currently `FlagConcurrencyTokenTests`) still need a one-line edit. That's not a public API change — it's a test that reaches into the domain entity as a setup fixture.

### DD-4 — Fold deleted-method tests into the surviving method's tests

**Decision:** `FlagArchivedInvariantTests` currently has four archived-guard tests (one per mutation method). After this refactor it has two: one for `Reconfigure`, one for `UpdateName`. The deleted method tests are removed, not migrated.

**Why:** The archived-state invariant is the same domain rule applied to every mutation. Testing it once per surviving method gives full coverage of the production surface; testing it on a deleted method tests nothing. The `StrategyConfig` mismatch test on `UpdateStrategy` similarly folds into the `Reconfigure` mismatch test — same rule, same surface.

**Tradeoff:** Test count drops slightly. The lessons-learned note from 2026-05-05 ("test what clients see, not what guards throw") supports this — we are not reducing observable coverage, just the count of identical guards exercised at a private surface.

---

## Architecture Overview

No new components. No layer boundary changes. No diagram required.

The mutation surface of the `Flag` aggregate root is reduced from four methods to two:

```
Before                                After
──────                                ─────
Flag                                  Flag
├── SetEnabled(bool)                  ├── Reconfigure(bool, RolloutStrategy, StrategyConfig)
├── UpdateStrategy(RS, SC)            ├── UpdateName(string)
├── Update(bool, RS, SC)              └── Archive()
├── UpdateName(string)
└── Archive()
```

All existing invariants are preserved on the surviving methods:
- Archived-state-terminal guard fires first.
- `Reconfigure` enforces `strategyConfig.ValidatedFor == strategyType` — same `FlagDomainException` as today.
- `UpdatedAt` is set exactly once per mutation.
- `Archive()` is unchanged.

---

## Scope

Files modified:

1. `Banderas.Domain/Entities/Flag.cs` — delete `SetEnabled`, delete `UpdateStrategy`, rename `Update` to `Reconfigure`. No signature change on the surviving method beyond the name.
2. `Banderas.Application/Services/BanderasService.cs` — single call site at line 191 changes from `flag.Update(...)` to `flag.Reconfigure(...)`.
3. `Banderas.Tests/Domain/FlagArchivedInvariantTests.cs` — remove the `SetEnabled` and `UpdateStrategy` archived-guard tests; rename the `Update` test to `Reconfigure`; remove the corresponding "non-archived succeeds" companions for the deleted methods. Keep `UpdateName` and `Archive` tests intact.
4. `Banderas.Tests/Domain/ValueObjects/StrategyConfigTests.cs` — rename `flag.Update(...)` call to `flag.Reconfigure(...)`; remove the `UpdateStrategy` mismatch test (now redundant with the `Reconfigure` mismatch test).
5. `Banderas.Tests.Integration/FlagConcurrencyTokenTests.cs` — replace `flagA.SetEnabled(true)` with `flagA.Reconfigure(true, /* preserve current strategy/config */)`. The test demonstrates the optimistic concurrency token; the specific mutation chosen is incidental.

Files **not** modified:

- `Banderas.Application/Interfaces/IBanderasService.cs` and DTOs — public service contract unchanged.
- `Banderas.Api/Controllers/BanderasController.cs` — unchanged.
- `Banderas.Infrastructure/Repositories/BanderasRepository.cs` and `FlagConfiguration.cs` — unchanged. EF Core sees the same private setters and the same property surface.
- `Banderas.Tests.Integration/*` other than `FlagConcurrencyTokenTests` — no changes; PUT/POST/DELETE end-to-end tests continue to exercise the surviving `Reconfigure` path through the service.
- `Requests/smoke-test.http` — HTTP surface unchanged.
- Foundation docs (`architecture.md`, `current-state.md`, `roadmap.md`) — updated separately by `/post-work` after implementation review, not by this spec.

---

## Acceptance Criteria

**AC-1 — `Reconfigure` updates the rollout triple atomically.**
*Given* a non-archived `Flag` with `IsEnabled = false`, `StrategyType = None`, and a default `StrategyConfig`,
*when* `flag.Reconfigure(true, RolloutStrategy.Percentage, percentageConfig)` is called,
*then* `IsEnabled == true`, `StrategyType == Percentage`, `StrategyConfig == percentageConfig`, and `UpdatedAt` advances. All four fields move in one observable step.

**AC-2 — `Reconfigure` honors the archived-terminal invariant.**
*Given* an archived `Flag`,
*when* `flag.Reconfigure(true, RolloutStrategy.None, anyConfig)` is called,
*then* `FlagDomainException` is thrown with the message `"Flag '{Name}' is archived and cannot be modified."` and no fields change.

**AC-3 — `Reconfigure` honors the config/strategy consistency invariant.**
*Given* a non-archived `Flag` and a `StrategyConfig` whose `ValidatedFor != strategyType`,
*when* `flag.Reconfigure(...)` is called with that mismatched pair,
*then* `FlagDomainException` is thrown with a message identifying the mismatch and no fields change.

**AC-4 — `UpdateName` is unaffected.**
*Given* a non-archived `Flag`,
*when* `flag.UpdateName("renamed")` is called,
*then* `Name == "renamed"` and `UpdatedAt` advances. *Given* an archived `Flag`, calling `UpdateName` throws `FlagDomainException`. *Given* a whitespace name, `UpdateName` throws `ArgumentException`.

**AC-5 — `SetEnabled` and `UpdateStrategy` are not callable.**
*When* the solution is compiled,
*then* no source file references `Flag.SetEnabled(` or `Flag.UpdateStrategy(`. Compilation fails if either is reintroduced as a public method.

**AC-6 — `PUT /api/flags/{name}` behavior is byte-for-byte unchanged.**
*Given* a valid `UpdateFlagRequest`,
*when* the integration test suite runs against the API,
*then* response status codes (200/400/404/409), `ProblemDetails` shapes, and persisted flag state match the existing test expectations exactly. No new integration assertions are required — existing PUT integration tests continue to pass without modification.

**AC-7 — Optimistic concurrency contract is preserved.**
*Given* two in-memory `Flag` instances loaded from the same row (`Version = N`),
*when* one calls `Reconfigure(...)` and saves successfully, and the other then calls `UpdateName(...)` and attempts to save,
*then* a `DbUpdateConcurrencyException` is raised on the loser. `FlagConcurrencyTokenTests` continues to demonstrate this exact contract.

**AC-8 — Test suite is green.**
*When* `dotnet test` runs both unit and integration projects,
*then* all tests pass. The unit test count may decrease by 3 (folded archived-guard tests + folded `UpdateStrategy` mismatch test); the integration test count is unchanged at 54.

---

## File Layout

```
Banderas.Domain/Entities/Flag.cs                          MOD  (delete 2 methods, rename 1)
Banderas.Application/Services/BanderasService.cs          MOD  (1 line, rename call)
Banderas.Tests/Domain/FlagArchivedInvariantTests.cs       MOD  (delete 4 tests, rename 2)
Banderas.Tests/Domain/ValueObjects/StrategyConfigTests.cs MOD  (delete 1 test, rename 1 call)
Banderas.Tests.Integration/FlagConcurrencyTokenTests.cs   MOD  (replace 1 call)
Docs/Decisions/refactor-flag-mutation-consolidation/spec.md  ADD  (this file)
```

---

## Technical Notes

- **No EF Core implications.** `Reconfigure` writes the same three private-setter properties (`IsEnabled`, `StrategyType`, `StrategyConfig`) that `Update` writes today. The `StrategyConfig` backing field + reconciling-getter pattern (KI/lesson 2026-05-07) is untouched.
- **No DI registration changes.** Nothing in `DependencyInjection.cs` references the deleted methods.
- **`InternalsVisibleTo("Banderas.Tests")`** already exists on `Banderas.Domain` — test access to the renamed method requires no changes.
- **`.editorconfig` IDE0008** — the call-site change in `BanderasService.UpdateFlagAsync` does not introduce a `var` declaration. The lessons-learned note from 2026-05-07 about `var` usage stays satisfied; no new style risks.
- **CSharpier** — no formatting concerns beyond standard one-line renames.
- **Build gate** — `dotnet build -p:TreatWarningsAsErrors=true` must remain green. There is no realistic CS0618/obsolete pathway; we are deleting, not deprecating.
- **No ADR required.** The decision is recorded here and in the DDD backlog (which will be checked off by `/post-work`).
- **Backlog hygiene.** `Docs/Decisions/flag-ddd-analysis-backlog.md` line 80 (`Consolidate SetEnabled() + UpdateStrategy() + Update()`) is checked off during `/post-work`.

---

## Out of Scope

- Adding `Description`, `Tags`, or `Variation` to `Flag` (separate DDD backlog items, future PRs).
- Splitting `Flag` into a definition aggregate and a `FlagEnvironmentConfig` aggregate (future, larger refactor).
- Any change to `IBanderasService` or the public HTTP API.
- Deprecating methods via `[Obsolete]` instead of deleting — the methods have no external consumers; deletion is correct.
- Adding new domain events (e.g. `FlagReconfigured`) — domain events are Phase 4 observability work.
- Changing `Archive()` semantics or signature.
- Changing optimistic concurrency token type or behavior.

---

## Learning Opportunities

1. **Naming as a domain-modeling signal.** The rename from `Update` to `Reconfigure` is the entire point of the refactor — it forces a future reader to think in terms of *the concern* rather than *the fields*. Cheap to do, durable to read. Worth noticing how much of the consolidation lives in the name versus in the deletes.
2. **Why folding tests is not the same as losing coverage.** Three archived-guard tests that exercise an identical guard against three deleted methods provide zero ongoing value. The single `Reconfigure` archived-guard test exercises the same code path on the only remaining surface. Coverage of the *invariant* is preserved; the redundant *exercise* of it is removed.
3. **Refactor-only PRs as a confidence signal.** With no public API change and no behavior change, the integration suite must pass *without test changes* (apart from `FlagConcurrencyTokenTests`, which reaches into the domain entity directly as setup). That is the strongest possible signal that the refactor is semantically equivalent.

---

## DX / Tooling Idea

N/A — pure internal refactor; no developer workflow surface is affected.

---

## Definition of Done

- [ ] `Banderas.Domain/Entities/Flag.cs` no longer declares `SetEnabled` or `UpdateStrategy`.
- [ ] `Banderas.Domain/Entities/Flag.cs` declares `Reconfigure(bool, RolloutStrategy, StrategyConfig)` with the exact body of the former `Update` method, including both invariant guards.
- [ ] `BanderasService.UpdateFlagAsync` calls `flag.Reconfigure(...)` instead of `flag.Update(...)`.
- [ ] No file in the repository (excluding this spec) contains the strings `flag.SetEnabled(`, `flag.UpdateStrategy(`, or `flag.Update(`.
- [ ] `FlagArchivedInvariantTests` contains an archived-guard test for `Reconfigure` and one for `UpdateName` (and `Archive`); tests for `SetEnabled`/`UpdateStrategy` are removed.
- [ ] `StrategyConfigTests` references `Reconfigure` only; the `UpdateStrategy` mismatch test is removed.
- [ ] `FlagConcurrencyTokenTests` continues to demonstrate the optimistic concurrency contract using `Reconfigure` (and/or `UpdateName`) — the test still fails on the loser with `DbUpdateConcurrencyException`.
- [ ] `dotnet build -p:TreatWarningsAsErrors=true` succeeds across all projects.
- [ ] `dotnet csharpier check .` reports no violations.
- [ ] `dotnet test` reports all unit tests pass (count may decrease by 3 vs. baseline of 158).
- [ ] `dotnet test` reports all 54 integration tests pass (count unchanged).
- [ ] No changes to `Requests/smoke-test.http`; manual smoke confirms PUT `/api/flags/{name}` still returns 200 with the expected `FlagResponse`.
- [ ] PR description references the DDD backlog item being closed.
