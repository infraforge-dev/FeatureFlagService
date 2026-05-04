# Remove `IsSeeded` from `Flag` Domain Entity — Implementation Notes

**Session date:** 2026-05-04
**Branch:** `refactor/remove-isseeded-from-flag`
**Spec reference:** `Docs/Decisions/remove-isseeded-from-flag - PR# 60/spec.md`
**Build status:** `dotnet build -p:TreatWarningsAsErrors=true` passed with 0 warnings and 0 errors
**Tests:** Full suite green — 164 passing (165 − 2 deleted + 1 new)
**PR:** #60

## What Was Built

`Flag` no longer carries an `IsSeeded` property. The bookkeeping that distinguishes seeder-inserted rows from manually-created flags now lives in the persistence layer as an EF Core *shadow property* on `FlagConfiguration`. The `flags.IsSeeded` column is unchanged on disk; the value is tracked by EF's change tracker without a CLR property on the entity.

`DatabaseSeeder` was rewritten at four call sites: insert paths in `SeedMissingAsync` and `ResetSeedAsync` now stamp `entry.Property("IsSeeded").CurrentValue = true` on the returned `EntityEntry<Flag>`, and the reset query plus manual-slot conflict check use `EF.Property<bool>(f, "IsSeeded")` inside their LINQ predicates. The second `Flag` constructor (`isSeeded` overload) was deleted outright; `SeedRecord.ToFlag()` now calls the single public constructor.

The change is contained to the Domain and Infrastructure layers. No controllers, services, DTOs, or middleware were touched.

## Why This Change

The DDD audit (`Docs/Decisions/flag-ddd-analysis-backlog.md`) flagged `IsSeeded` as a boundary violation: a property describing *how a row got into the database* has nothing to do with *what a feature flag is* in the business domain. It was invisible on every DTO, irrelevant to every evaluation strategy, and read by exactly one component — the seeder itself, to power the `SEED_RESET=true` safety guarantee.

That guarantee — "the seeder only deletes rows it inserted; manually-created flags survive a reset" — is real and worth preserving. So the goal was to relocate the bookkeeping rather than drop it. EF Core shadow properties are the textbook fit: a piece of per-row state tracked entirely by infrastructure, with no CLR surface on the entity.

This is the next item on the Phase 2 "Strengthen `Flag` invariants" track, following PR #59 (archived state terminal).

## Key Decisions

**Shadow property over column drop.** Dropping the column would have forced the reset path to delete by manifest match, which would also delete a manually-edited flag occupying the same `(Name, Environment)` slot. The "I touched it last, I'll clean it up" property would have been silently lost. Keeping the column and reconfiguring it as shadow preserves the guarantee at the cost of slightly more verbose seeder code — paid once, in one file.

**Delete the second constructor outright, don't relay.** Once `IsSeeded` left the domain, the seeder's `isSeeded: true` argument had nowhere to live. Keeping the constructor as a vestigial relay would have re-introduced the same leak at a different surface. The seeder reaches into EF tracking metadata via `entry.Property("IsSeeded").CurrentValue` — acceptable because the seeder is already deeply infrastructure-aware and is the only caller paying the cost.

**Generate the migration even if `Up`/`Down` are empty.** The model snapshot reflects the property's *declaring source* (entity vs. shadow) regardless of whether the column shape changes. EF generated empty `Up`/`Down` methods, which is the correct outcome — column, type, nullability, and default all stay identical. Committing the empty migration keeps the snapshot in sync and prevents the next migration from regenerating an unrelated diff.

**Test the safety guarantee end-to-end, not just the syntax.** The new `SeedReset_DoesNotDeleteManuallyCreatedFlagsAsync` integration test exercises AC-4 directly: insert a manual flag in a non-manifest slot, run `SeedAsync(reset: true)`, and assert the manual flag survives while seeded rows are deleted and re-inserted. A unit test of the LINQ predicate would only confirm the syntax compiles; the integration test confirms the *guarantee* still holds after the relocation.

## File-by-File Changes

| File | Change |
|---|---|
| `Banderas.Domain/Entities/Flag.cs` | Removed `IsSeeded` property and assignment; deleted the second constructor (`isSeeded` overload); the single primary constructor remains |
| `Banderas.Infrastructure/Persistence/FlagConfiguration.cs` | Replaced `Property(f => f.IsSeeded)` with `Property<bool>("IsSeeded")` — same `IsRequired().HasDefaultValue(false)` shape |
| `Banderas.Infrastructure/Seeding/DatabaseSeeder.cs` | Added `using Microsoft.EntityFrameworkCore.ChangeTracking;`; rewrote two insert paths to stamp `entry.Property("IsSeeded").CurrentValue = true`; rewrote reset `Where` and conflict-check predicates to use `EF.Property<bool>(f, "IsSeeded")`; `SeedRecord.ToFlag()` now calls the single-arg constructor |
| `Banderas.Infrastructure/Migrations/20260504205809_RemoveIsSeededFromFlagEntity.cs` | New migration — `Up`/`Down` are empty by design; non-destructive |
| `Banderas.Infrastructure/Migrations/20260504205809_RemoveIsSeededFromFlagEntity.Designer.cs` | New designer file accompanying the migration |
| `Banderas.Tests/Domain/FlagTests.cs` | Deleted entirely — both surviving test cases were `IsSeeded`-specific (the file held `Constructor_WithoutIsSeededParameter_DefaultsToFalse` and `Constructor_WithIsSeededParameter_SetsSeededState`) |
| `Banderas.Tests.Integration/SeedDataStartupTests.cs` | Rewrote the seeded-flags assertion to filter via `EF.Property<bool>(f, "IsSeeded")` inside the LINQ query; tightened `using` directives; added `SeedReset_DoesNotDeleteManuallyCreatedFlagsAsync` covering AC-4 end-to-end |

No controller, service, DTO, validator, evaluator, or middleware changes.

## Test Notes

The `SeedDataStartupTests` rewrite kept the assertion inside the LINQ-to-Entities query (`.Where(f => EF.Property<bool>(f, "IsSeeded"))`) rather than materializing all flags and switching to `dbContext.Entry(...)` per row. The spec called this out explicitly as a pitfall — `EF.Property<T>` is an expression-tree marker, not a runtime accessor, and the LINQ form is the cleaner of the two.

`SeedReset_DoesNotDeleteManuallyCreatedFlagsAsync` deliberately places the manual flag in `EnvironmentType.Production`, a slot the seed manifest never targets, so the test isolates the "seeded vs. manual" distinction from the "manifest slot conflict" path that AC-5 covers separately. The test asserts three things: the manual survivor is present, exactly 6 rows have `IsSeeded = true`, and exactly 1 row has `IsSeeded = false`.

`FlagTests.cs` was deleted as a file because both of its remaining cases were `IsSeeded`-specific. Other `Flag` invariant coverage lives in `FlagArchivedInvariantTests.cs` (PR #59) and the broader `Flag*` test classes under `Banderas.Tests/Domain/`. No domain coverage was lost.

## Risks and Follow-Ups

- **Production data is unchanged.** The migration's `Up` is empty — running it on an existing database is a no-op at the SQL level. Only the EF model snapshot moves. Verified by inspecting the generated migration before commit.
- **Shadow property is queryable, not direct-readable.** Any future code that wants to read `IsSeeded` on a materialized entity must go through `dbContext.Entry(flag).Property("IsSeeded").CurrentValue`. Today only the seeder needs that access — if a second consumer appears, that's a signal to rethink whether shadow is still the right home.
- **`BanderasDbContextModelSnapshot.cs` did not require regeneration.** EF's snapshot already represented the property in a form compatible with the shadow declaration; the diff was empty. Worth noting in case a future EF version produces a non-empty snapshot diff for the same operation.
- **Other `Flag` DDD backlog items remain open** — typed `StrategyConfig`, separated `FlagEnvironmentConfig`, consolidated mutation methods, etc. Each is its own spec; this PR does not touch them.

## How to Test

```bash
dotnet build -p:TreatWarningsAsErrors=true
dotnet test
dotnet csharpier check .

# Targeted verification of the new integration test
dotnet test Banderas.Tests.Integration \
  --filter "FullyQualifiedName~SeedReset_DoesNotDeleteManuallyCreatedFlagsAsync"
```

## Interview Lens

The interesting question here was *which layer owns provenance*. A row's origin (seeded vs. hand-edited) feels like state — it is a `bool`, after all — but it is state about the *infrastructure operation that produced the row*, not about the business object the row represents. Putting it on the domain entity gave it the same prominence as `IsEnabled` or `Environment`, which is wrong: a future engineer reading `Flag` for the first time would reasonably ask "what's this for? does it affect evaluation?" and find that it does not.

EF Core shadow properties are the right tool for exactly this category of state: audit columns, tenant IDs, soft-delete flags, provenance markers — anything tracked per-row by infrastructure that the business model neither knows nor cares about. The change tracker holds the value; the entity is unaware. The only caller that needs the value is the seeder, and the seeder is already deeply infrastructure-aware, so the asymmetry of "easy to read in LINQ, awkward to read in memory" lands on the right side.

The other useful framing was the "make illegal states unrepresentable" reverse: if removing a property from the entity makes some legitimate operation harder, that's a signal it might belong on the entity after all. Here the only operation that got harder was the seeder's stamping, and the seeder is the one place that should pay that cost. Confirmation that the relocation is correct, not just possible.

## Foundation Docs Updated

- [x] `Docs/current-state.md` — Phase 2 progress reflects this change; test counts updated to 164
- [x] `Docs/Decisions/flag-ddd-analysis-backlog.md` — "Remove `IsSeeded` from `Flag`" backlog item flipped to `[X]` with PR #60 reference
- [x] `README.md` — minor sync touch (test count / phase wording)
- [x] `Docs/architecture.md` — no architecture changes required; shadow properties are an existing EF-supported pattern within the established Infrastructure layer

## Definition of Done — Status

- [x] `IsSeeded` property removed from `Banderas.Domain/Entities/Flag.cs`
- [x] Second `Flag` constructor (`isSeeded` overload) removed
- [x] `FlagConfiguration` configures `IsSeeded` as a shadow property (`Property<bool>("IsSeeded")`)
- [x] `DatabaseSeeder` updated at all four call sites:
  - [x] Insert path in `SeedMissingAsync`
  - [x] Insert path in `ResetSeedAsync`
  - [x] Reset query (`Where(f => EF.Property<bool>(f, "IsSeeded"))`)
  - [x] Manual-slot conflict check (`!EF.Property<bool>(f, "IsSeeded")`)
  - [x] `SeedRecord.ToFlag()` no longer passes `isSeeded: true`
- [x] EF migration `RemoveIsSeededFromFlagEntity` generated and committed; verified non-destructive (empty `Up`/`Down`)
- [x] `BanderasDbContextModelSnapshot.cs` reconciled (no regeneration needed)
- [x] `FlagTests.Constructor_WithoutIsSeededParameter_DefaultsToFalse` deleted
- [x] `FlagTests.Constructor_WithIsSeededParameter_SetsSeededState` deleted
- [x] `SeedDataStartupTests` `IsSeeded` assertion rewritten via `EF.Property<bool>` in the LINQ query
- [x] New integration test `SeedReset_DoesNotDeleteManuallyCreatedFlagsAsync` added and passing
- [x] `dotnet build -p:TreatWarningsAsErrors=true` clean
- [x] `dotnet csharpier check .` clean
- [x] All previously-passing tests still pass — 164/164 green
- [x] `Docs/current-state.md` updated — Phase 2 progress reflects this change
- [x] `Docs/Decisions/flag-ddd-analysis-backlog.md` — `Remove IsSeeded from Flag` checkbox ticked, with PR #60 reference
