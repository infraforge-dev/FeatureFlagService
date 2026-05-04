# Specification: Remove `IsSeeded` from the `Flag` Domain Entity

**Document:** `Docs/Decisions/remove-isseeded-from-flag - PR# ##/spec.md`
**Status:** Draft
**Branch:** `refactor/remove-isseeded-from-flag`
**PR:** 60
**Phase:** Phase 2 — Testing & Reliability (Flag DDD invariants strand)
**Depends on:** None (PR #59 — archived state terminal — already shipped)
**Author:** Jose/Claude
**Date:** 2026-05-04

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

As a developer working on the Banderas domain layer, I want the `Flag` aggregate to
expose only its business-meaningful state — not infrastructure bookkeeping — so that
the domain remains a clean expression of "what a feature flag is" and future DDD
refactors (typed `StrategyConfig`, separated `FlagEnvironmentConfig`) can proceed
without inheriting an irrelevant property.

The end user experience is unchanged. This is a structural improvement that pays
forward into every subsequent change to `Flag`.

---

## Background and Goals

`Flag.IsSeeded` is a `bool` that records whether a row was inserted by
`DatabaseSeeder` versus a manual API call. It is read by exactly one component —
the seeder itself — to support `SEED_RESET=true`, which deletes previously-seeded
rows and re-creates the manifest baseline, while leaving manually-created flags
untouched.

The DDD analysis in `Docs/Decisions/flag-ddd-analysis-backlog.md` flagged this as
a **boundary violation**: a property describing how a row got into the database
has nothing to do with what a flag *is* in the business domain. It is invisible
on every DTO, irrelevant to every evaluation strategy, and ignored by every
controller. Its presence on the domain entity is a leak from infrastructure into
the domain layer.

This spec removes the leak. The bookkeeping survives at the persistence layer
(via an EF Core shadow property) but stops contaminating the domain API surface.

This is the next item on the Phase 2 "Strengthen `Flag` invariants" track,
following PR #59 (archived state terminal).

---

## Design Decisions

### 1. EF Core shadow property over column drop

**Decision:** Keep the `IsSeeded` column in the database. Reconfigure it as an EF
Core *shadow property* — a value tracked by the change tracker without a CLR
property on the entity.

**Why this over dropping the column entirely:**
The seeder uses `IsSeeded` to provide a real safety guarantee:
`SEED_RESET=true` deletes only rows it inserted, never manually-created flags
sitting in the same `(Name, Environment)` slot. If we dropped the column, the
reset path would have to delete by manifest match, which would also delete a
manually-edited flag occupying that slot. We would lose the "I touched it last,
I'll clean it up" safety property.

**Why this over a side table:**
A `seeded_flag_ids` table is overkill for one bool, adds a join to the seeder's
hot path on startup, and creates a second source of truth that can drift from
the `flags` table.

**Tradeoff accepted:** The seeder code becomes slightly more verbose. The
`f.IsSeeded` LINQ predicate becomes `EF.Property<bool>(f, "IsSeeded")`, and
inserts must set the value via `entry.Property("IsSeeded").CurrentValue = true`
after `AddAsync`. This cost is paid once, in one file, for permanent removal of
a domain leak.

### 2. Delete the `isSeeded` constructor overload outright

**Decision:** Remove the second `Flag` constructor (`(name, env, isEnabled,
strategyType, strategyConfig, isSeeded)`) entirely. The seeder uses the single
public constructor and stamps `IsSeeded=true` via the EF entry after insert.

**Why:** Once seeded-ness leaves the domain, the seeder param has nowhere to
live on the entity. Keeping the constructor as a vestigial relay would
re-introduce the same leak at a different surface. Deletion is cleaner.

**Tradeoff accepted:** The seeder must reach into EF tracking metadata
(`db.Entry(flag).Property("IsSeeded")`) instead of passing a clean ctor arg.
This is acceptable because the seeder is already deeply infrastructure-aware
and is the only caller paying the cost.

### 3. Generate a migration even if `Up`/`Down` are empty

**Decision:** Run `dotnet ef migrations add RemoveIsSeededFromFlagEntity` and
commit the result regardless of whether the generated `Up`/`Down` are empty.

**Why:** The model snapshot will change because the property's declaring source
moves from "entity property" to "shadow property", even though the column,
type, nullability, and default all stay identical. An empty migration that
keeps the snapshot in sync is the correct outcome and prevents the next
migration from regenerating an unrelated diff.

**If the generated migration is non-empty** (e.g. EF decides to drop and re-add
the column): edit the migration manually to no-op, since we explicitly do not
want a destructive column change.

**Tradeoff accepted:** One extra commit for what may be an empty file. The
alternative — letting the snapshot drift — is worse.

---

## Architecture Overview

No new components. No layer boundaries are crossed in new ways. This is a
relocation of a single piece of state from the domain layer to the
infrastructure layer.

```text
Before:                              After:
                                     
Banderas.Domain                      Banderas.Domain
  Flag                                 Flag
    ...                                  ...
    IsSeeded: bool   ◀── leaks         (no IsSeeded)
                                     
Banderas.Infrastructure              Banderas.Infrastructure
  FlagConfiguration                    FlagConfiguration
    Property(f => f.IsSeeded)            Property<bool>("IsSeeded")    ◀── shadow
  DatabaseSeeder                       DatabaseSeeder
    ctor: isSeeded: true                 entry.Property("IsSeeded")
    Where(f => f.IsSeeded)               Where(f => EF.Property<bool>(f, "IsSeeded"))
```

A Mermaid diagram is not warranted — the change is too narrow.

---

## Scope

**Files modified (4):**
- `Banderas.Domain/Entities/Flag.cs` — remove `IsSeeded` property; delete second constructor
- `Banderas.Infrastructure/Persistence/FlagConfiguration.cs` — convert mapping to shadow property
- `Banderas.Infrastructure/Seeding/DatabaseSeeder.cs` — rewrite three call sites + `SeedRecord.ToFlag()`
- `Banderas.Tests.Integration/SeedDataStartupTests.cs` — rewrite the `IsSeeded` assertion

**Files added (1):**
- New EF migration under `Banderas.Infrastructure/Migrations/` (timestamped name; expected near-empty)

**Files modified by test changes:**
- `Banderas.Tests/Domain/FlagTests.cs` — delete two test methods
- `Banderas.Tests.Integration/SeedDataStartupTests.cs` — add `SeedReset_DoesNotDeleteManuallyCreatedFlags`

**Files modified for docs (per PR conventions — docs travel with the feature branch):**
- `Docs/current-state.md` — update Phase 2 progress
- `Docs/Decisions/flag-ddd-analysis-backlog.md` — tick the "Remove `IsSeeded`" backlog item

**Untouched (verified):**
- All DTOs (`FlagResponse`, `CreateFlagRequest`, etc.) — `IsSeeded` was never on them
- All controllers, validators, the evaluator, and all strategies — never read `IsSeeded`
- `FlagMappings`, `BanderasService` — never read `IsSeeded`

---

## Acceptance Criteria

**AC-1 — Domain surface is clean**
Given the `Flag` class
When inspected for public members
Then `IsSeeded` does not appear as a property
And only one public constructor exists (no `isSeeded` parameter overload)

**AC-2 — Persistence preserves the column**
Given a database initialized via the new migration
When the schema is inspected
Then the `flags` table still has the `IsSeeded` column with default `false`
And no data is lost from existing rows

**AC-3 — Seeder insert path stamps shadow property**
Given a fresh database
When `DatabaseSeeder.SeedAsync(reset: false)` runs
Then every newly inserted row has shadow `IsSeeded = true`
And the row count matches the seed manifest length

**AC-4 — Reset deletes only previously-seeded rows**
Given a database containing both seeded rows and a manually-inserted flag
When `DatabaseSeeder.SeedAsync(reset: true)` runs
Then all rows where shadow `IsSeeded = true` are deleted and re-inserted
And the manually-inserted flag (shadow `IsSeeded = false`) survives

**AC-5 — Manual-slot conflict detection still works**
Given a manually-created flag occupying a `(Name, Environment)` slot listed in the seed manifest
When `DatabaseSeeder.SeedAsync(reset: true)` runs for that slot
Then the seeder skips that manifest entry, logs a warning, and does not overwrite the manual flag

**AC-6 — Existing test suite passes**
Given the full test suite
When `dotnet test` runs against `Banderas.Tests` and `Banderas.Tests.Integration`
Then all currently passing tests still pass (modulo the two `FlagTests` cases deleted in this spec)
And the new `SeedReset_DoesNotDeleteManuallyCreatedFlags` integration test passes

**AC-7 — Build is clean**
Given `dotnet build -p:TreatWarningsAsErrors=true`
Then it completes with zero warnings, zero errors
And `dotnet csharpier check .` reports no formatting violations

---

## File Layout

```
Banderas.Domain/
  Entities/
    Flag.cs                                   ◀── modified (remove property + ctor)

Banderas.Infrastructure/
  Persistence/
    FlagConfiguration.cs                      ◀── modified (shadow property)
  Seeding/
    DatabaseSeeder.cs                         ◀── modified (3 call sites + ToFlag)
  Migrations/
    YYYYMMDDHHMMSS_RemoveIsSeededFromFlagEntity.cs           ◀── new
    YYYYMMDDHHMMSS_RemoveIsSeededFromFlagEntity.Designer.cs  ◀── new
    BanderasDbContextModelSnapshot.cs         ◀── modified (regenerated)

Banderas.Tests/
  Domain/
    FlagTests.cs                              ◀── modified (delete 2 cases)

Banderas.Tests.Integration/
  SeedDataStartupTests.cs                     ◀── modified (rewrite + add 1 case)

Docs/
  current-state.md                            ◀── modified
  Decisions/
    flag-ddd-analysis-backlog.md              ◀── modified (tick item)
    remove-isseeded-from-flag - PR# ##/
      spec.md                                 ◀── this document
```

---

## Technical Notes

### Shadow property syntax reference

**Mapping** (in `FlagConfiguration.Configure`):
```csharp
builder.Property<bool>("IsSeeded").IsRequired().HasDefaultValue(false);
```

**Insert** (in `DatabaseSeeder`):
```csharp
EntityEntry<Flag> entry = await db.Flags.AddAsync(flag, ct);
entry.Property("IsSeeded").CurrentValue = true;
```

**Query — LINQ predicate:**
```csharp
db.Flags.Where(f => EF.Property<bool>(f, "IsSeeded"))
```

**Query — combined predicate:**
```csharp
db.Flags.AnyAsync(
    f => f.Name == record.Name
      && f.Environment == record.Environment
      && !f.IsArchived
      && !EF.Property<bool>(f, "IsSeeded"),
    ct);
```

### Migration command

Run from the repo root with the devcontainer up:

```bash
dotnet ef migrations add RemoveIsSeededFromFlagEntity \
  --project Banderas.Infrastructure \
  --startup-project Banderas.Api
```

If the generated `Up`/`Down` methods contain DropColumn/AddColumn directives,
**edit the migration to no-op both methods** before committing. The column must
not change shape.

### Build sequence

1. Edit `Flag.cs` (remove property + second ctor)
2. Edit `FlagConfiguration.cs` (shadow property)
3. Edit `DatabaseSeeder.cs` (all four call sites)
4. `dotnet build` — expect zero errors at this point if seeder rewrite is complete
5. Generate migration
6. Edit / delete two `FlagTests` cases
7. Rewrite `SeedDataStartupTests` assertion + add new test
8. `dotnet test` — full suite green
9. `dotnet csharpier check .`
10. Update docs

### Known pitfalls

- `EF.Property<T>(...)` only works inside an EF expression tree (LINQ-to-Entities). It cannot be invoked on a materialized entity in memory. For in-memory access, use `db.Entry(flag).Property("IsSeeded").CurrentValue`.
- After `await db.Flags.AddAsync(flag, ct)`, the returned `EntityEntry<Flag>` is the right place to stamp the shadow property — the entity is now tracked and shadow values are part of the change tracker, not the CLR object.
- `SeedDataStartupTests.cs` already loads `seededFlags` from the DbContext via a query. The rewritten assertion belongs inside that query (LINQ form) — converting it to an in-memory `Should().OnlyContain` over already-materialized entities would require switching to `dbContext.Entry(...)` per row. Prefer the LINQ form.

### ADR / boundary references

- DDD analysis source: `Docs/Decisions/flag-ddd-analysis-backlog.md` — backlog item "Remove `IsSeeded` from `Flag`"
- Architecture rule: `Docs/architecture.md` — domain entity protects business state only; seeding is an infrastructure concern (`InputSanitizer`-style boundary discipline applies to provenance markers, not just sanitization)
- PR convention (per memory): no Claude attribution footer in PRs; docs ride on the feature branch

---

## Out of Scope

The following are explicitly **deferred**:

- **Other `Flag` DDD backlog items.** Each is its own spec:
  - Consolidating `SetEnabled` / `UpdateStrategy` / `Update` by concern
  - Converting `StrategyConfig` from raw `string` to typed Value Objects
  - Enforcing config/strategy type consistency inside `Flag`
  - Adding `Description`, `Tags`, `Variation` to the definition
  - Splitting `FlagEnvironmentConfig` into its own aggregate root
- **API-level integration coverage for archived-flag `409` mapping** — deferred from PR #59; tracked separately in `current-state.md`.
- **GET-query `EnvironmentType` validation placement decision** — separate task on Phase 2 board.
- **Renaming the `IsSeeded` column** to snake_case or any other casing — column name stays `IsSeeded` to avoid a destructive migration.
- **`FlagResponse` / DTO changes** — `IsSeeded` was already absent from the API surface.

---

## Learning Opportunities

1. **EF Core shadow properties.** A first-class EF concept that lets infrastructure track per-row state without contaminating the domain model. The change tracker holds the value; the entity is unaware. Useful pattern for audit columns, tenant IDs, soft-delete flags, and provenance markers — exactly the kind of state that violates DDD if exposed on entities.
2. **`EF.Property<T>(...)` in LINQ-to-Entities.** A static method that exists only to be translated to SQL — calling it on a materialized entity throws. Reinforces the mental model of "LINQ-to-Entities is an expression tree, not C# code that runs in-process."
3. **Migration snapshot vs. column shape.** EF tracks the `ModelSnapshot` independently of the physical schema. A property moving from CLR to shadow can produce an empty `Up`/`Down` while still requiring a snapshot update — useful intuition for reading and editing migrations.

---

## DX / Tooling Idea

N/A — no developer pain-point in this slice. The seeder edits are mechanical and the migration command is already familiar.

---

## Definition of Done

- [ ] `IsSeeded` property removed from `Banderas.Domain/Entities/Flag.cs`
- [ ] Second `Flag` constructor (`isSeeded` overload) removed
- [ ] `FlagConfiguration` configures `IsSeeded` as a shadow property (`Property<bool>("IsSeeded")`)
- [ ] `DatabaseSeeder` updated at all four call sites:
  - [ ] Insert path in `SeedMissingAsync`
  - [ ] Insert path in `ResetSeedAsync`
  - [ ] Reset query (`Where(f => EF.Property<bool>(f, "IsSeeded"))`)
  - [ ] Manual-slot conflict check (`!EF.Property<bool>(f, "IsSeeded")`)
  - [ ] `SeedRecord.ToFlag()` no longer passes `isSeeded: true`
- [ ] EF migration `RemoveIsSeededFromFlagEntity` generated and committed; verified non-destructive
- [ ] `BanderasDbContextModelSnapshot.cs` regenerated and committed
- [ ] `FlagTests.Constructor_WithoutIsSeededParameter_DefaultsToFalse` deleted
- [ ] `FlagTests.Constructor_WithIsSeededParameter_SetsSeededState` deleted
- [ ] `SeedDataStartupTests` `IsSeeded` assertion rewritten via `EF.Property<bool>` in the LINQ query
- [ ] New integration test `SeedReset_DoesNotDeleteManuallyCreatedFlags` added and passing
- [ ] `dotnet build -p:TreatWarningsAsErrors=true` clean
- [ ] `dotnet csharpier check .` clean
- [ ] All previously-passing tests still pass (165 minus 2 deleted, plus 1 new = 164)
- [ ] `Docs/current-state.md` updated — Phase 2 progress reflects this change
- [ ] `Docs/Decisions/flag-ddd-analysis-backlog.md` — `Remove IsSeeded from Flag` checkbox ticked, with PR reference once the PR exists
