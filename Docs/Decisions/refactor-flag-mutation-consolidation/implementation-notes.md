# Consolidate `Flag` Mutation Methods by Concern — Implementation Notes

**Session date:** 2026-05-11
**Branch:** refactor/flag-mutation-consolidation
**Spec reference:** Docs/Decisions/refactor-flag-mutation-consolidation/spec.md
**Build status:** ✅ `dotnet build -p:TreatWarningsAsErrors=true` — 0 warnings, 0 errors
**Tests:** 153/153 unit + 54/54 integration passing (207/207 total)
**PR:** TBD

---

## What Was Built

The `Flag` aggregate now exposes a single concern-named rollout mutation,
`Reconfigure(bool, RolloutStrategy, StrategyConfig)`, in place of the previous
trio `SetEnabled` / `UpdateStrategy` / `Update`. The two field-shaped methods
(`SetEnabled`, `UpdateStrategy`) had no production callers and were deleted; the
atomic `Update` method was renamed to `Reconfigure` to declare full atomic
replacement, not partial patching. `UpdateName` and `Archive` are unchanged.
The public HTTP API, DTO contracts, and `IBanderasService` signatures are all
identical to before — this is a pure domain-layer rename + delete with a
single application-layer call-site update.

## Spec Gaps Resolved

None. The spec was specific enough to execute without stopping for clarification.

## Deviations from Spec

- **Test count delta is 5, not 3 as the spec predicted.** The spec foresaw three
  folded tests (two archived-guards + one mismatch). The actual drops also include
  the two "non-archived succeeds" companions for `SetEnabled` and `UpdateStrategy`,
  which no longer have a method to exercise. The surviving
  `Reconfigure_WhenFlagIsNotArchived_Succeeds` test asserts all three fields
  (`IsEnabled`, `StrategyType`, `StrategyConfig`), so observable coverage of the
  success path is preserved. No coverage gap — the prediction was simply off by
  two trivial tests.

## Key Decisions

- **`Reconfigure` over `Configure`.** `Configure` reads as first-time setup and
  blurs with the .NET DI/extension idiom (`services.Configure<T>`, `IConfigureOptions`).
  The `re-` prefix signals that initial configuration happens in the constructor
  and every subsequent call is a replacement, not a setup.
- **XML doc rewritten, not just transplanted.** The former `Update` doc began
  "Atomically updates the enabled state and rollout strategy" — a description of
  fields. The new `Reconfigure` doc reads "Atomically replaces the rollout
  configuration (enabled state, strategy type, and strategy config) in a single
  operation" — a description of the concern. Small change, large signal.
- **Concurrency test mutation kept neutral.** `FlagConcurrencyTokenTests` calls
  `flagA.Reconfigure(true, RolloutStrategy.None, flagA.StrategyConfig)` —
  reusing the loaded `StrategyConfig` rather than constructing a new one. The
  test exists to demonstrate the optimistic concurrency token; the specific
  mutation is incidental and the no-op-on-strategy form keeps the test focused
  on the version-collision behavior rather than a strategy change.
- **Backlog item closed in place.** `Docs/Decisions/flag-ddd-analysis-backlog.md`
  line 80 is now `[X]` with a one-line provenance note pointing back to this PR,
  matching the prior `IsSeeded` removal's convention.

## File-by-File Changes

| File | Change |
|---|---|
| `Banderas.Domain/Entities/Flag.cs` | Deleted `SetEnabled(bool)` and `UpdateStrategy(RolloutStrategy, StrategyConfig)`. Renamed `Update(...)` → `Reconfigure(...)`. Rewrote the XML doc to describe the concern instead of the fields. Reflowed the signature across three lines to satisfy CSharpier on the longer method name. No behavior change on the surviving method body. |
| `Banderas.Application/Services/BanderasService.cs` | `UpdateFlagAsync` now calls `flag.Reconfigure(...)`. The adjacent comment changed from "Single atomic update" to "Atomic rollout reconfiguration" — same intent, named for the concern. |
| `Banderas.Tests/Domain/FlagArchivedInvariantTests.cs` | Dropped the four tests that exercised the deleted methods (`SetEnabled` and `UpdateStrategy`, both archived-guard and success). Renamed the `Update`-shaped tests to `Reconfigure`. File shrinks from 10 tests to 5 (Reconfigure × 2, UpdateName × 2, Archive × 2 — minus one because `Reconfigure_WhenFlagIsArchived_ThrowsFlagDomainException` is one test, not two). |
| `Banderas.Tests/Domain/ValueObjects/StrategyConfigTests.cs` | Renamed `FlagUpdate_WithMismatchedConfig_...` to `FlagReconfigure_WithMismatchedConfig_...`. Deleted the redundant `FlagUpdateStrategy_WithMismatchedConfig_...` test — same rule, no longer a separate surface to exercise. |
| `Banderas.Tests.Integration/FlagConcurrencyTokenTests.cs` | Replaced `flagA.SetEnabled(true)` with `flagA.Reconfigure(true, RolloutStrategy.None, flagA.StrategyConfig)`. Test continues to demonstrate the optimistic concurrency contract: `flagA` saves first, `flagB.UpdateName("renamed-by-loser")` then races and the repository surfaces `FlagConcurrencyException` → 409 on the loser. |
| `Docs/architecture.md` | Updated the Domain Integrity principle to name `Reconfigure` / `UpdateName` / `Archive` as the canonical mutation surface, and to state the surface is named by concern, not by field. (Also includes the prior env-validation ratification edit from earlier in the session — bundled on this branch because it was uncommitted in the main tree.) |
| `Docs/current-state.md` | Added "Phase 2 — Consolidate Flag Mutation Methods by Concern: ✅ Complete" to the Status Summary. Updated the Flag-completed bullet to name `Reconfigure(...)`. Updated the archived-terminal description (3 methods, not 5). Updated the test count summary (153 unit, was 158). Added the 2026-05-11 Lessons Learned entry. Bumped the Immediate Next Tasks list to the next DDD backlog item (`Description` / `Tags`). |
| `Docs/roadmap.md` | Checked off the consolidation item under the Phase 2 `Flag` invariants list. Extended the Phase 2 progress sentence. |
| `Docs/Decisions/flag-ddd-analysis-backlog.md` | Checked off the consolidation backlog item with a one-line provenance note. |
| `Docs/Decisions/refactor-flag-mutation-consolidation/spec.md` | Carried into the worktree from the main tree (where it was written during `/spec`). |
| `Docs/Decisions/refactor-flag-mutation-consolidation/implementation-notes.md` | This file. |

## Risks and Follow-Ups

- **No EF Core / migration risk.** `Reconfigure` writes the same three private-setter
  properties that `Update` wrote. The schema is unchanged. The `StrategyConfig`
  backing-field + reconciling-getter pattern is untouched.
- **No public API risk.** Every external surface (HTTP endpoints, DTOs,
  `IBanderasService` signatures, OpenAPI document) is byte-for-byte identical.
  The PUT integration suite passing without test changes is the strongest possible
  proof of semantic equivalence.
- **Future readers — be cautious about reintroducing `SetEnabled`.** If a future
  feature genuinely requires partial mutation (e.g. a "quick toggle" endpoint that
  flips enabled without touching strategy), the right move is to add a new
  concern-named method (`ToggleEnabled`?) with explicit semantics — not to
  resurrect `SetEnabled`. The 2026-05-11 Lessons Learned entry codifies this.
- **DDD backlog also closes the typed-config item.** Reading the backlog file, the
  typed `StrategyConfig` Value Object entry (line 81) was still unchecked despite
  PR #62 having shipped. Not touched in this PR — separate hygiene concern.

## How to Test

**Domain unit tests** (cover AC-1 through AC-5):

```bash
dotnet test Banderas.Tests/Banderas.Tests.csproj --filter "FullyQualifiedName~FlagArchivedInvariantTests|FullyQualifiedName~StrategyConfigTests"
```

Expected: all archived-guard, success, and config-mismatch tests pass; no test
references `SetEnabled` or `UpdateStrategy`.

**Integration suite** (covers AC-6 byte-for-byte API parity and AC-7 concurrency):

```bash
dotnet test Banderas.Tests.Integration/Banderas.Tests.Integration.csproj
```

Expected: 54/54 green. `FlagConcurrencyTokenTests.ConcurrentUpdate_SecondSave_ThrowsFlagConcurrencyExceptionAsync`
demonstrates the optimistic concurrency contract via `Reconfigure` + `UpdateName`.

**Manual smoke** (no expected change vs. baseline):

```bash
# In another terminal: docker compose up
# Then exercise the existing PUT path:
curl -X PUT http://localhost:5051/api/flags/checkout-flow \
  -H "Content-Type: application/json" \
  -d '{"environment":"Development","isEnabled":true,"strategyType":"None","strategyConfig":null}'
```

Expected: `200 OK` with `FlagResponse` body, identical to the pre-refactor baseline.

**Static check that no caller revived a deleted method:**

```bash
grep -rn "\.SetEnabled(\|\.UpdateStrategy(\|flag\.Update(" --include="*.cs" .
```

Expected: zero hits.

## Interview Lens

The interesting decision here was *what to keep and what to delete*, not *what to add*.
`Flag` had three public methods that all touched the same domain concern, but only
one had a production caller. Reading code from a DDD lens, the temptation is to
preserve all three "in case someone needs them" — which is exactly the YAGNI trap.
The cost of dead public surface is hidden but real: it invites future callers to
patch one field and forget the others (toggle enabled while leaving a now-mismatched
strategy untouched), and it tells future readers that the entity thinks in fields,
not concerns. Deleting the dead methods and renaming the survivor to `Reconfigure`
made the same domain rule unrepresentable in code rather than just documented. At
a larger scale (a public NuGet SDK, say), I'd deprecate first with `[Obsolete]` for
one release cycle before deletion; on an internal pre-1.0 codebase with one
in-tree caller, that ceremony is overhead without benefit.

## Foundation Docs Updated

- [x] `Docs/architecture.md` — Domain Integrity principle reflects the new mutation surface
- [x] `Docs/current-state.md` — Status Summary, Completed list, test counts, Next Tasks, Lessons Learned
- [x] `Docs/roadmap.md` — Phase 2 backlog checkbox + progress sentence
- [x] `Docs/Decisions/flag-ddd-analysis-backlog.md` — backlog item checked off
- [x] `Docs/Decisions/refactor-flag-mutation-consolidation/implementation-notes.md` — this file

## Definition of Done — Status

- [x] ✅ `SetEnabled` and `UpdateStrategy` removed from `Flag.cs`
- [x] ✅ `Update` renamed to `Reconfigure`; XML doc rewritten to name the concern
- [x] ✅ `BanderasService.UpdateFlagAsync` calls `flag.Reconfigure(...)`
- [x] ✅ No file in repo (excluding this spec) references `flag.SetEnabled(`, `flag.UpdateStrategy(`, or `flag.Update(` — verified by grep
- [x] ✅ `FlagArchivedInvariantTests` covers `Reconfigure`, `UpdateName`, `Archive`; deleted method tests removed
- [x] ✅ `StrategyConfigTests` references `Reconfigure` only; redundant `UpdateStrategy` mismatch test removed
- [x] ✅ `FlagConcurrencyTokenTests` continues to demonstrate the optimistic concurrency contract
- [x] ✅ `dotnet build -p:TreatWarningsAsErrors=true` succeeds across all projects (0/0)
- [x] ✅ `dotnet csharpier check .` reports no violations (104 files)
- [x] ✅ All unit tests pass (153/153 — 5 fewer than baseline, all due to deleted-method coverage being folded)
- [x] ✅ All integration tests pass (54/54, count unchanged)
- [x] ✅ `Requests/smoke-test.http` unchanged; PUT `/api/flags/{name}` returns the same `200 OK` `FlagResponse`
- [x] ✅ PR description (to be written by `/git-workflow`) will reference the DDD backlog item being closed
