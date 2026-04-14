# Specification: Seed Data for Local Development (v2)

**Document:** `Docs/Decisions/seed-data/spec-v2.md`
**Branch:** `feature/phase1-finish-line`
**Phase:** 1 — Developer Experience
**Status:** Ready for Implementation
**Replaces:** v1 — revised after AI reviewer findings
**Author:** Jose / Claude Architect Session
**Date:** 2026-04-13

---

## Revision Log — v1 → v2

| Finding | v1 | v2 Fix |
|---------|-----|--------|
| No provenance marker | Reset targeted by Name + Environment — ambiguous | Added `IsSeeded` bool column; reset targets `IsSeeded = true` only |
| DD-2 and AC-2 contradicted each other | DD-2: skip all if any exist / AC-2: per-record backfill | Resolved to per-record backfill; self-healing behavior |
| Archived seed flags blocked re-insertion | Archived flags counted as "existing" — demo baseline never restored | Existence check targets non-archived rows only; archived seed flags are backfilled |
| `StrategyConfig = null` conflicts with domain | Domain normalizes `null` → `"{}"` | Manifest passes `"{}"` explicitly for all None strategy flags |
| Migration prerequisite unspecified | `MigrateAsync()` mentioned only as "if it exists" | `MigrateAsync()` added explicitly to Development startup block, before seeder |
| Reset could delete manually created flags | Delete by Name + Environment — no provenance distinction | Reset deletes only where `IsSeeded = true` — manual flags untouched |

---

## Table of Contents

- [User Story](#user-story)
- [Background & Goals](#background--goals)
- [Design Decisions](#design-decisions)
  - [DD-1: Automatic Startup Seeding, Development Only](#dd-1-automatic-startup-seeding-development-only)
  - [DD-2: Provenance Marker — IsSeeded](#dd-2-provenance-marker--isseeded)
  - [DD-3: Idempotency — Per-Record Backfill](#dd-3-idempotency--per-record-backfill)
  - [DD-4: Reset Mode — Target IsSeeded Only](#dd-4-reset-mode--target-isseeded-only)
  - [DD-5: Migration on Startup](#dd-5-migration-on-startup)
  - [DD-6: Layer Placement](#dd-6-layer-placement)
- [Scope](#scope)
- [Seed Records](#seed-records)
- [Acceptance Criteria](#acceptance-criteria)
  - [AC-1: IsSeeded Column and Migration](#ac-1-isseeded-column-and-migration)
  - [AC-2: DatabaseSeeder Class](#ac-2-databaseseeder-class)
  - [AC-3: Normal Mode — Per-Record Backfill](#ac-3-normal-mode--per-record-backfill)
  - [AC-4: Reset Mode — Wipe Seeded Rows and Re-insert](#ac-4-reset-mode--wipe-seeded-rows-and-re-insert)
  - [AC-5: Startup Wiring in Program.cs](#ac-5-startup-wiring-in-programcs)
  - [AC-6: DI Registration](#ac-6-di-registration)
  - [AC-7: Seed Record Coverage](#ac-7-seed-record-coverage)
  - [AC-8: Logging](#ac-8-logging)
- [File Layout](#file-layout)
- [Implementation Notes](#implementation-notes)
- [Out of Scope](#out-of-scope)
- [Definition of Done](#definition-of-done)

---

## User Story

> As a developer (or recruiter evaluating this project), I want a working set of
> feature flags automatically available when I run `docker compose up` — so I can
> explore and test the API immediately without any manual database setup.

---

## Background & Goals

Currently, a developer who clones the repo and starts the app has an empty database.
Every manual test requires creating flags by hand before anything meaningful can be
evaluated. This creates two problems:

1. **Onboarding friction** — new developers and recruiters cannot immediately explore
   the API. The "15-minute demo" promise in the product vision is broken.

2. **Dev data rot** — developers who have been working for days or weeks accumulate
   test data that no longer reflects a known clean state. There is no way to reset
   to a baseline without manually deleting rows.

This spec introduces a `DatabaseSeeder` that solves both problems. It seeds a curated
set of representative flags on startup (Development only) and supports a controlled
reset that targets only seeder-owned rows — leaving developer-created flags untouched.

---

## Design Decisions

### DD-1: Automatic Startup Seeding, Development Only

The seeder runs automatically during application startup. No manual CLI step is
required. This preserves the `docker compose up` onboarding experience described
in the product vision.

The seeder is guarded by `IHostEnvironment.IsDevelopment()` at the `Program.cs`
call site. It will not execute in Staging or Production under any circumstances.
The seeder itself has no knowledge of the environment — the guard is the caller's
responsibility.

**Rejected: EF Core `HasData()`** — baked into migrations, runs in all environments,
cannot support conditional or reset behavior without significant workarounds.

**Rejected: CLI command only** — requires a manual step after startup, breaking the
`docker compose up` onboarding flow.

**Rejected: Full database wipe on startup** — destroys developer-created flags on
container restart. Unacceptable data loss for active development.

---

### DD-2: Provenance Marker — IsSeeded

A new `bool` column `IsSeeded` is added to the `Flag` entity and persisted via
a new EF Core migration. The seeder stamps every record it creates with
`IsSeeded = true`. All existing rows and all manually created flags default to
`IsSeeded = false`.

This gives the seeder a reliable, unambiguous way to identify the rows it owns —
without relying on name matching, which cannot distinguish a seeded flag from a
developer flag that happens to share the same name.

**Migration default:** `false`. Existing rows are unaffected.

---

### DD-3: Idempotency — Per-Record Backfill

On every startup (normal mode), the seeder checks each seed record individually.
Records that already exist as active (non-archived) rows are skipped. Records that
are missing — because they were archived, deleted, or never inserted — are
re-inserted.

This is a **self-healing** behavior. If a developer archives a seeded flag, the
next startup restores it. The demo baseline is always recoverable without a full
reset.

**Rejected: Skip all seeding if any seed records exist** — fragile. A single
existing record suppresses all other inserts. Demo baseline degrades silently over
time.

---

### DD-4: Reset Mode — Target IsSeeded Only

Reset mode is triggered by the environment variable `SEED_RESET=true`. When active:

1. All rows where `IsSeeded = true` are deleted via `ExecuteDeleteAsync`
2. The full seed manifest is re-inserted unconditionally

Reset is scoped entirely to `IsSeeded = true` rows. Flags created manually by
developers are never deleted — regardless of whether their name matches a seed
record.

The variable is passed without modifying source code:

```bash
SEED_RESET=true docker compose up
```

---

### DD-5: Migration on Startup

The app does not currently migrate automatically. On a fresh `docker compose up`,
the database has no schema — the seeder would crash before it could insert anything.

`app.MigrateAsync()` is added to the Development startup block, immediately before
the seeder call. Schema is guaranteed to exist before seeding is attempted.

This guard is Development-only for the same reason as the seeder: in Production,
migrations are a controlled deployment step reviewed before the app starts — not
an automatic side effect of startup.

---

### DD-6: Layer Placement

| Concern | Layer | Location |
|---------|-------|----------|
| `Flag.IsSeeded` property | Domain | `Domain/Entities/Flag.cs` |
| EF Core column mapping | Infrastructure | `Infrastructure/Persistence/Configurations/FlagConfiguration.cs` |
| Migration | Infrastructure | `Infrastructure/Persistence/Migrations/` |
| `DatabaseSeeder` class | Infrastructure | `Infrastructure/Seeding/DatabaseSeeder.cs` |
| DI registration | Infrastructure | `Infrastructure/DependencyInjection.cs` |
| Startup trigger + guard | Api | `Banderas.Api/Program.cs` |

---

## Scope

| # | What | File(s) Affected |
|---|------|-----------------|
| 1 | `IsSeeded` property on `Flag` entity | `Domain/Entities/Flag.cs` |
| 2 | EF Core column mapping for `IsSeeded` | `Infrastructure/Persistence/Configurations/FlagConfiguration.cs` |
| 3 | New migration | `Infrastructure/Persistence/Migrations/` |
| 4 | `DatabaseSeeder` class | `Infrastructure/Seeding/DatabaseSeeder.cs` |
| 5 | DI registration | `Infrastructure/DependencyInjection.cs` |
| 6 | Startup wiring + migration call | `Banderas.Api/Program.cs` |

No new endpoints. No changes to validators, DTOs, or existing repository methods.

---

## Seed Records

The seeder inserts the following six flags. They are designed to exercise all three
rollout strategies across two environments — giving a developer or recruiter an
immediately meaningful API to explore.

| Name | Environment | Strategy | Enabled | StrategyConfig |
|------|-------------|----------|---------|----------------|
| `dark-mode` | Development | None | `true` | `"{}"` |
| `new-dashboard` | Development | Percentage | `true` | `{"percentage":30}` |
| `beta-features` | Development | RoleBased | `true` | `{"roles":["Admin","Beta"]}` |
| `maintenance-mode` | Development | None | `false` | `"{}"` |
| `dark-mode` | Staging | None | `true` | `"{}"` |
| `new-dashboard` | Staging | Percentage | `true` | `{"percentage":50}` |

All records are stamped with `IsSeeded = true` at insert time.

`StrategyConfig` is `"{}"` for all `None` strategy flags — this matches the value
the domain stores after normalization (`null` input → `"{}"` stored). The manifest
passes `"{}"` explicitly to avoid relying on that normalization as a side effect.

---

## Acceptance Criteria

---

### AC-1: IsSeeded Column and Migration

**Files:**
- `Banderas.Domain/Entities/Flag.cs`
- `Banderas.Infrastructure/Persistence/Configurations/FlagConfiguration.cs`
- `Banderas.Infrastructure/Persistence/Migrations/<timestamp>_AddIsSeededToFlag.cs`

**Domain:**
- Add `public bool IsSeeded { get; private set; }` to the `Flag` entity
- Default value: `false`
- No changes to any existing constructor or mutation method signatures
- `IsSeeded` is not exposed on any DTO or API response — it is an internal
  infrastructure concern only

**EF Core mapping:**
```csharp
builder.Property(f => f.IsSeeded)
    .IsRequired()
    .HasDefaultValue(false);
```

**Migration:**
- Generated via `dotnet ef migrations add AddIsSeededToFlag`
- Existing rows receive `IsSeeded = false` via the column default
- Migration must apply cleanly against the current schema with no data loss

---

### AC-2: DatabaseSeeder Class

**File:** `Banderas.Infrastructure/Seeding/DatabaseSeeder.cs`

- Class is `internal sealed`
- Constructor accepts `AppDbContext` and `ILogger<DatabaseSeeder>`
- Exposes a single public method: `Task SeedAsync(bool reset, CancellationToken ct = default)`
- Does not reference any Application layer types — uses `AppDbContext` directly
- Does not reference `IBanderasRepository`

```csharp
internal sealed class DatabaseSeeder(
    AppDbContext db,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(bool reset, CancellationToken ct = default)
    { ... }
}
```

---

### AC-3: Normal Mode — Per-Record Backfill

When `reset` is `false`:

- Iterate over each record in the seed manifest
- For each record, check whether an active (non-archived) flag with that `Name`
  and `Environment` already exists using `AnyAsync` on `AppDbContext.Flags`
- Existence check filters: `Name == record.Name && Environment == record.Environment && !IsArchived`
- If active record exists → skip, log at `Debug`:
  `"Seed record '{Name}' ({Environment}) already exists — skipping."`
- If no active record exists → construct a `Flag` entity via the domain constructor,
  set `IsSeeded = true`, add to context, save
- After processing all records, if any were inserted → log at `Information`:
  `"Seeded {Count} flag(s)."`
- If all records were skipped → log at `Information`:
  `"Seeding skipped — all seed records already present."`

**Archived seed flags are treated as missing** — if a seeded flag was archived,
the existence check returns false and the record is re-inserted on next startup.
This is intentional: the seeder maintains the demo baseline automatically.

---

### AC-4: Reset Mode — Wipe Seeded Rows and Re-insert

When `reset` is `true`:

- Log at `Warning` before delete:
  `"SEED_RESET=true — deleting all seeded records before re-seeding."`
- Delete all rows where `IsSeeded = true` using `ExecuteDeleteAsync`:
```csharp
await db.Flags.Where(f => f.IsSeeded).ExecuteDeleteAsync(ct);
```
- Re-insert all seed manifest records unconditionally, each stamped with
  `IsSeeded = true`
- Log at `Information` after insert:
  `"Re-seeded {Count} flag(s)."`

**Reset is scoped to `IsSeeded = true` rows only.** Flags where `IsSeeded = false`
are never touched — regardless of whether their name matches a seed record.

---

### AC-5: Startup Wiring in Program.cs

**File:** `Banderas.Api/Program.cs`

The following block is added after `app.MapControllers()`:

```csharp
if (app.Environment.IsDevelopment())
{
    await app.MigrateAsync();

    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    var reset = Environment.GetEnvironmentVariable("SEED_RESET") == "true";
    await seeder.SeedAsync(reset);
}
```

- `MigrateAsync()` runs first — schema is guaranteed before seeding
- `SEED_RESET` is read via `Environment.GetEnvironmentVariable()`, not
  `IConfiguration` — keeps the reset signal separate from application config
- The seeder is resolved from a scoped `IServiceProvider` — required because
  `DatabaseSeeder` depends on the scoped `AppDbContext`
- If `app.MigrateAsync()` does not exist as an extension method, implement it
  as a local extension in `Banderas.Api/Extensions/WebApplicationExtensions.cs`:

```csharp
public static async Task MigrateAsync(this WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

---

### AC-6: DI Registration

**File:** `Banderas.Infrastructure/DependencyInjection.cs`

Add to the `AddInfrastructure()` extension method:

```csharp
services.AddScoped<DatabaseSeeder>();
```

---

### AC-7: Seed Record Coverage

The seeder inserts exactly the six records defined in the [Seed Records](#seed-records)
table. Each record is constructed using the `Flag` domain constructor — not via
object initializers that bypass domain invariants or raw SQL.

After construction, `IsSeeded` is set to `true` before the entity is added to the
context. Because `IsSeeded` has a private setter, the seeder must set it via one
of the following approaches (Claude Code should determine which is cleanest given
the current domain shape):

- A constructor overload that accepts `isSeeded`
- An `internal` method `MarkAsSeeded()` on the `Flag` entity
- An `internal` setter promoted from `private` — accessible within Infrastructure
  because both projects are in the same solution

The chosen approach must not expose `IsSeeded` mutation on the public API surface.

---

### AC-8: Logging

All log messages use structured logging with named properties — no string
interpolation.

| Scenario | Level | Message Template |
|----------|-------|-----------------|
| Active record exists (per record) | Debug | `"Seed record '{Name}' ({Environment}) already exists — skipping."` |
| All records already present | Information | `"Seeding skipped — all seed records already present."` |
| Records inserted (normal mode) | Information | `"Seeded {Count} flag(s)."` |
| Reset triggered | Warning | `"SEED_RESET=true — deleting all seeded records before re-seeding."` |
| Reset complete | Information | `"Re-seeded {Count} flag(s)."` |

No user-identifying data is logged. Seed records contain no PII.

---

## File Layout

```
Banderas.Domain/
  Entities/
    Flag.cs                                         ← modified (IsSeeded property)

Banderas.Infrastructure/
  Persistence/
    Configurations/
      FlagConfiguration.cs                          ← modified (IsSeeded mapping)
    Migrations/
      <timestamp>_AddIsSeededToFlag.cs              ← new migration
  Seeding/
    DatabaseSeeder.cs                               ← new

Banderas.Api/
  Extensions/
    WebApplicationExtensions.cs                     ← new (MigrateAsync helper)
  Program.cs                                        ← modified (Development block)

Banderas.Infrastructure/
  DependencyInjection.cs                            ← modified (register DatabaseSeeder)
```

---

## Implementation Notes

- `ExecuteDeleteAsync` requires EF Core 7+. Already available (EF Core 10).
- `IsSeeded` must not appear on `FlagResponse` or any other DTO — it is an
  internal infrastructure concern only. API consumers have no knowledge of it.
- The seeder calls `AppDbContext` directly — do not route through
  `IBanderasRepository`. The repository is the Application layer's boundary;
  the seeder lives in Infrastructure and may use `DbContext` directly.
- The manifest literals (`"dark-mode"`, `"Admin"`, etc.) are trusted constant
  data defined by the engineer — `InputSanitizer` is not required for seed
  manifest values. This is an explicit exception to the sanitization rule, which
  applies to untrusted HTTP input surfaces only.
- CSharpier formatting must pass — run `dotnet csharpier .` before committing.
- The migration must be generated against the running Postgres devcontainer, not
  an in-memory provider. Use the devcontainer terminal.

---

## Out of Scope

- Seed data for Staging or Production environments
- A dedicated reset CLI command or API endpoint
- Changes to existing validators, DTOs, or repository methods
- Seed data for integration tests — test data is owned by each test class
  via Testcontainers setup
- Exposing `IsSeeded` on any API response

---

## Definition of Done

- [ ] `IsSeeded` property exists on `Flag` entity with `private` setter
- [ ] EF Core column mapping added to `FlagConfiguration.cs`
- [ ] Migration generated and applies cleanly with no data loss
- [ ] `DatabaseSeeder` exists at `Infrastructure/Seeding/DatabaseSeeder.cs`
- [ ] `DatabaseSeeder` registered as scoped in `AddInfrastructure()`
- [ ] `MigrateAsync()` extension exists and is called first in the Development block
- [ ] Seeder is called in `Program.cs` inside an `IsDevelopment()` guard
- [ ] Normal mode is per-record — backfills missing or archived seed records
- [ ] Reset mode deletes only `IsSeeded = true` rows — manual flags untouched
- [ ] All six seed records inserted covering all three strategies and two environments
- [ ] `IsSeeded = true` stamped on every seeder-inserted row
- [ ] `IsSeeded` not present on any DTO or API response
- [ ] All log messages use structured logging with named properties
- [ ] `dotnet build` passes with 0 warnings, 0 errors
- [ ] All 110 existing tests still pass
- [ ] CSharpier check passes
