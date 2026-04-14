# Specification: Seed Data for Local Development (v3)

**Document:** `Docs/Decisions/seed-data/spec-v3.md`
**Branch:** `feature/phase1-finish-line`
**Phase:** 1 — Developer Experience
**Status:** Ready for Implementation
**Replaces:** v2 — revised after second AI reviewer pass
**Author:** Jose / Claude Architect Session
**Date:** 2026-04-13

---

## Revision Log — v2 → v3

| Finding | v2 | v3 Fix |
|---------|-----|--------|
| Reset collision on shared identity | Reset deleted `IsSeeded = true` rows then re-inserted unconditionally — would fail unique index if a manual flag occupied the same slot | Reset skips slots occupied by active non-seeded flags; logs a local override warning instead of failing |
| Wrong DbContext class name | Spec referenced `AppDbContext` throughout | Corrected to `BanderasDbContext` everywhere |
| Wrong FlagConfiguration path | Spec referenced `Persistence/Configurations/FlagConfiguration.cs` | Corrected to `Banderas.Infrastructure/Persistence/FlagConfiguration.cs` |
| `internal` across assembly boundary | AC-7 suggested `internal` setter — invalid across Domain/Infrastructure boundary without `InternalsVisibleTo` | Resolved to constructor overload accepting `isSeeded` — explicit, no assembly coupling |

---

## Table of Contents

- [User Story](#user-story)
- [Background & Goals](#background--goals)
- [Design Decisions](#design-decisions)
  - [DD-1: Automatic Startup Seeding, Development Only](#dd-1-automatic-startup-seeding-development-only)
  - [DD-2: Provenance Marker — IsSeeded](#dd-2-provenance-marker--isseeded)
  - [DD-3: Seed Identities Are Reserved Baseline Slots](#dd-3-seed-identities-are-reserved-baseline-slots)
  - [DD-4: Normal Mode — Per-Record Backfill](#dd-4-normal-mode--per-record-backfill)
  - [DD-5: Reset Mode — Target IsSeeded Only, Skip Collisions](#dd-5-reset-mode--target-isseeded-only-skip-collisions)
  - [DD-6: Migration on Startup](#dd-6-migration-on-startup)
  - [DD-7: IsSeeded Stamping via Constructor Overload](#dd-7-isseeded-stamping-via-constructor-overload)
  - [DD-8: Layer Placement](#dd-8-layer-placement)
- [Scope](#scope)
- [Seed Records](#seed-records)
- [Acceptance Criteria](#acceptance-criteria)
  - [AC-1: IsSeeded Column and Migration](#ac-1-isseeded-column-and-migration)
  - [AC-2: Flag Constructor Overload](#ac-2-flag-constructor-overload)
  - [AC-3: DatabaseSeeder Class](#ac-3-databaseseeder-class)
  - [AC-4: Normal Mode — Per-Record Backfill](#ac-4-normal-mode--per-record-backfill)
  - [AC-5: Reset Mode — Wipe Seeded Rows, Skip Collisions](#ac-5-reset-mode--wipe-seeded-rows-skip-collisions)
  - [AC-6: Startup Wiring in Program.cs](#ac-6-startup-wiring-in-programcs)
  - [AC-7: DI Registration](#ac-7-di-registration)
  - [AC-8: Seed Record Coverage](#ac-8-seed-record-coverage)
  - [AC-9: Logging](#ac-9-logging)
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
set of representative flags on startup (Development only), supports a controlled reset
that targets only seeder-owned rows, and never touches flags created manually by
developers.

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
developer flag that happens to share the same name and environment.

**Migration default:** `false`. Existing rows receive `false` automatically.

---

### DD-3: Seed Identities Are Reserved Baseline Slots

Each combination of `Name` + `Environment` in the seed manifest represents a
**reserved baseline slot** — a known identity that the seeder manages. This is
a local development convention, not a runtime enforcement.

Developers may create flags in seed slots if they choose. The seeder respects
their data and will never overwrite or delete it. Instead, the seeder treats any
active row in a seed slot as "satisfied" — whether or not the row is seeder-owned.

---

### DD-4: Normal Mode — Per-Record Backfill

On every startup (normal mode), the seeder checks each seed record individually.
Records that already have an active (non-archived) row in their slot — whether
seeded or manually created — are skipped. Records whose slot is empty (no active
row exists) are inserted.

This is a **self-healing** behavior. If a developer archives a seeded flag, the
next startup restores it. The demo baseline is always recoverable without a
full reset.

**Rejected: Skip all seeding if any seed records exist** — fragile. A single
existing record suppresses all other inserts. Demo baseline degrades silently.

---

### DD-5: Reset Mode — Target IsSeeded Only, Skip Collisions

Reset mode is triggered by the environment variable `SEED_RESET=true`. When active:

1. All rows where `IsSeeded = true` are deleted via `ExecuteDeleteAsync`
2. For each manifest record, check whether an active non-seeded row already
   occupies the slot
3. If a non-seeded active row occupies the slot → skip insertion, log at `Warning`
4. If the slot is free → insert the manifest record with `IsSeeded = true`

This preserves the guarantee: **manually created flags are never deleted, regardless
of reset mode.**

The variable is passed without modifying source code:

```bash
SEED_RESET=true docker compose up
```

---

### DD-6: Migration on Startup

The app does not currently migrate automatically. On a fresh `docker compose up`,
the database has no schema — the seeder would crash before it could insert anything.

`app.MigrateAsync()` is added to the Development startup block, immediately before
the seeder call. Schema is guaranteed to exist before seeding is attempted.

This guard is Development-only for the same reason as the seeder: in Production,
migrations are a controlled deployment step reviewed and approved before the app
starts — not an automatic side effect of startup.

---

### DD-7: IsSeeded Stamping via Constructor Overload

`Domain` and `Infrastructure` are separate assemblies. An `internal` setter on
`Flag` would not be accessible from `DatabaseSeeder` without `InternalsVisibleTo`,
which adds unnecessary assembly coupling.

Instead, the `Flag` entity receives a second constructor overload that accepts
`isSeeded`. The seeder uses this overload. All other callers — controllers,
service layer, existing tests — continue to use the original constructor, which
defaults `isSeeded` to `false`.

---

### DD-8: Layer Placement

| Concern | Layer | Location |
|---------|-------|----------|
| `Flag.IsSeeded` property + constructor overload | Domain | `Domain/Entities/Flag.cs` |
| EF Core column mapping | Infrastructure | `Infrastructure/Persistence/FlagConfiguration.cs` |
| Migration | Infrastructure | `Infrastructure/Persistence/Migrations/` |
| `DatabaseSeeder` class | Infrastructure | `Infrastructure/Seeding/DatabaseSeeder.cs` |
| DI registration | Infrastructure | `Infrastructure/DependencyInjection.cs` |
| `MigrateAsync()` extension | Api | `Banderas.Api/Extensions/WebApplicationExtensions.cs` |
| Startup trigger + guard | Api | `Banderas.Api/Program.cs` |

---

## Scope

| # | What | File(s) Affected |
|---|------|-----------------|
| 1 | `IsSeeded` property + constructor overload on `Flag` | `Domain/Entities/Flag.cs` |
| 2 | EF Core column mapping for `IsSeeded` | `Infrastructure/Persistence/FlagConfiguration.cs` |
| 3 | New EF Core migration | `Infrastructure/Persistence/Migrations/` |
| 4 | `DatabaseSeeder` class | `Infrastructure/Seeding/DatabaseSeeder.cs` |
| 5 | DI registration | `Infrastructure/DependencyInjection.cs` |
| 6 | `MigrateAsync()` extension method | `Banderas.Api/Extensions/WebApplicationExtensions.cs` |
| 7 | Startup wiring + Development guard | `Banderas.Api/Program.cs` |

No new endpoints. No changes to validators, DTOs, existing repository methods,
or `FlagResponse`.

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

All records are stamped `IsSeeded = true` at insert time.

`StrategyConfig` is `"{}"` for all `None` strategy flags — this matches the value
the domain stores after normalization. The manifest passes `"{}"` explicitly rather
than `null` to avoid relying on normalization as an implicit side effect.

---

## Acceptance Criteria

---

### AC-1: IsSeeded Column and Migration

**Files:**
- `Banderas.Domain/Entities/Flag.cs`
- `Banderas.Infrastructure/Persistence/FlagConfiguration.cs`
- `Banderas.Infrastructure/Persistence/Migrations/<timestamp>_AddIsSeededToFlag.cs`

**Domain property:**
- Add `public bool IsSeeded { get; private set; }` to the `Flag` entity
- Default value: `false`
- Not exposed on any DTO or API response — internal infrastructure concern only

**EF Core mapping** (add to `FlagConfiguration.cs`):
```csharp
builder.Property(f => f.IsSeeded)
    .IsRequired()
    .HasDefaultValue(false);
```

**Migration:**
- Generated via `dotnet ef migrations add AddIsSeededToFlag --project Banderas.Infrastructure --startup-project Banderas.Api`
- Existing rows receive `IsSeeded = false` via the column default
- Migration applies cleanly with no data loss
- Migration must be generated against the running Postgres devcontainer
  (`Host=postgres`) — not an in-memory provider

---

### AC-2: Flag Constructor Overload

**File:** `Banderas.Domain/Entities/Flag.cs`

Add a second constructor overload that accepts `isSeeded`. The existing constructor
signature is unchanged — all current callers continue to work without modification.

```csharp
// Existing constructor — unchanged, isSeeded defaults to false
public Flag(
    string name,
    EnvironmentType environment,
    bool isEnabled,
    RolloutStrategy strategyType,
    string? strategyConfig)
    : this(name, environment, isEnabled, strategyType, strategyConfig, isSeeded: false)
{ }

// New overload — used by DatabaseSeeder only
public Flag(
    string name,
    EnvironmentType environment,
    bool isEnabled,
    RolloutStrategy strategyType,
    string? strategyConfig,
    bool isSeeded)
{
    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Name cannot be empty.", nameof(name));

    Name = name;
    Environment = environment;
    IsEnabled = isEnabled;
    StrategyType = strategyType;
    StrategyConfig = strategyConfig ?? "{}";
    IsSeeded = isSeeded;
}
```

The existing constructor delegates to the new one — no logic duplication.

---

### AC-3: DatabaseSeeder Class

**File:** `Banderas.Infrastructure/Seeding/DatabaseSeeder.cs`

- Class is `internal sealed`
- Constructor accepts `BanderasDbContext` and `ILogger<DatabaseSeeder>`
- Exposes a single public method: `Task SeedAsync(bool reset, CancellationToken ct = default)`
- Does not reference any Application layer types
- Does not reference `IBanderasRepository`

```csharp
internal sealed class DatabaseSeeder(
    BanderasDbContext db,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(bool reset, CancellationToken ct = default)
    { ... }
}
```

---

### AC-4: Normal Mode — Per-Record Backfill

When `reset` is `false`:

- Iterate over each record in the seed manifest
- For each record, check whether an active (non-archived) flag exists in the slot
  using `AnyAsync`:
  ```csharp
  await db.Flags.AnyAsync(
      f => f.Name == record.Name
        && f.Environment == record.Environment
        && !f.IsArchived, ct);
  ```
- If an active row exists (seeded or manual) → skip, log at `Debug`:
  `"Seed slot '{Name}' ({Environment}) is occupied — skipping."`
- If no active row exists → construct `Flag` via the `isSeeded` constructor overload,
  add to context
- After processing all records, call `SaveChangesAsync` once
- If any records were inserted → log at `Information`:
  `"Seeded {Count} flag(s)."`
- If all records were skipped → log at `Information`:
  `"Seeding skipped — all seed slots are occupied."`

**Archived seed flags are treated as empty slots** — the existence check filters
`!IsArchived`, so an archived seeded flag is backfilled on next startup.

---

### AC-5: Reset Mode — Wipe Seeded Rows, Skip Collisions

When `reset` is `true`:

1. Log at `Warning` before delete:
   `"SEED_RESET=true — deleting all seeded records before re-seeding."`

2. Delete all rows where `IsSeeded = true`:
   ```csharp
   await db.Flags
       .Where(f => f.IsSeeded)
       .ExecuteDeleteAsync(ct);
   ```

3. For each manifest record, check whether a non-seeded active row occupies the slot:
   ```csharp
   await db.Flags.AnyAsync(
       f => f.Name == record.Name
         && f.Environment == record.Environment
         && !f.IsArchived
         && !f.IsSeeded, ct);
   ```

4. If a non-seeded active row occupies the slot → skip, log at `Warning`:
   `"Seed slot '{Name}' ({Environment}) is occupied by a manual flag — skipping. Delete the manual flag and re-run SEED_RESET=true to restore this baseline slot."`

5. If the slot is free → construct `Flag` via the `isSeeded` constructor overload,
   add to context

6. After processing all records, call `SaveChangesAsync` once

7. Log at `Information` after insert:
   `"Re-seeded {Count} flag(s)."`

**Manual flags (`IsSeeded = false`) are never deleted under any circumstances.**

---

### AC-6: Startup Wiring in Program.cs

**File:** `Banderas.Api/Program.cs`

Add the following block after `app.MapControllers()`:

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

- `MigrateAsync()` runs first — schema is guaranteed before seeding is attempted
- `SEED_RESET` is read via `Environment.GetEnvironmentVariable()`, not
  `IConfiguration` — keeps the reset signal separate from application config
- The seeder is resolved from a new `IServiceScope` — required because
  `DatabaseSeeder` depends on the scoped `BanderasDbContext`

**MigrateAsync extension method:**

If `MigrateAsync()` does not exist as an extension on `WebApplication`, create it:

**File:** `Banderas.Api/Extensions/WebApplicationExtensions.cs`

```csharp
using Banderas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Banderas.Api.Extensions;

internal static class WebApplicationExtensions
{
    internal static async Task MigrateAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<BanderasDbContext>();
        await db.Database.MigrateAsync();
    }
}
```

---

### AC-7: DI Registration

**File:** `Banderas.Infrastructure/DependencyInjection.cs`

Add to the `AddInfrastructure()` extension method:

```csharp
services.AddScoped<DatabaseSeeder>();
```

---

### AC-8: Seed Record Coverage

The seeder inserts exactly the six records defined in the [Seed Records](#seed-records)
table. Each is constructed via the `Flag` constructor overload with `isSeeded: true`.

Implementation pattern for each manifest record:

```csharp
new Flag(
    name: "dark-mode",
    environment: EnvironmentType.Development,
    isEnabled: true,
    strategyType: RolloutStrategy.None,
    strategyConfig: "{}",
    isSeeded: true)
```

---

### AC-9: Logging

All log messages use structured logging with named properties — no string
interpolation.

| Scenario | Level | Message Template |
|----------|-------|-----------------|
| Active row exists in slot (normal mode) | Debug | `"Seed slot '{Name}' ({Environment}) is occupied — skipping."` |
| All slots occupied (normal mode) | Information | `"Seeding skipped — all seed slots are occupied."` |
| Records inserted (normal mode) | Information | `"Seeded {Count} flag(s)."` |
| Reset triggered | Warning | `"SEED_RESET=true — deleting all seeded records before re-seeding."` |
| Slot occupied by manual flag (reset mode) | Warning | `"Seed slot '{Name}' ({Environment}) is occupied by a manual flag — skipping. Delete the manual flag and re-run SEED_RESET=true to restore this baseline slot."` |
| Reset complete | Information | `"Re-seeded {Count} flag(s)."` |

No user-identifying data is logged. Seed records contain no PII.

---

## File Layout

```
Banderas.Domain/
  Entities/
    Flag.cs                                              ← modified (IsSeeded + constructor overload)

Banderas.Infrastructure/
  Persistence/
    FlagConfiguration.cs                                 ← modified (IsSeeded mapping)
    Migrations/
      <timestamp>_AddIsSeededToFlag.cs                   ← new
  Seeding/
    DatabaseSeeder.cs                                    ← new
  DependencyInjection.cs                                 ← modified (register DatabaseSeeder)

Banderas.Api/
  Extensions/
    WebApplicationExtensions.cs                          ← new (MigrateAsync)
  Program.cs                                             ← modified (Development block)
```

---

## Implementation Notes

- `ExecuteDeleteAsync` requires EF Core 7+. Already available (EF Core 10).
- `IsSeeded` must not appear on `FlagResponse` or any other DTO. The column is
  an internal infrastructure concern — API consumers have no knowledge of it.
- The seeder calls `BanderasDbContext` directly — do not route through
  `IBanderasRepository`. The repository is the Application layer's boundary.
- The manifest literals are trusted constant data defined by the engineer —
  `InputSanitizer` is not required. The sanitization rule applies to untrusted
  HTTP input surfaces only.
- `SaveChangesAsync` is called once per mode (after all inserts in normal mode;
  after all inserts in reset mode) — not once per record.
- The migration must be generated against the running Postgres devcontainer —
  not an in-memory provider. Use the devcontainer terminal and confirm
  `Host=postgres` is set in `appsettings.Development.json`.
- CSharpier formatting must pass — run `dotnet csharpier .` before committing.

---

## Out of Scope

- Seed data for Staging or Production environments
- A dedicated reset CLI command or API endpoint
- Changes to existing validators, DTOs, or repository methods
- Exposing `IsSeeded` on any API response
- Seed data for integration tests — test data is owned by each test class
  via Testcontainers setup

---

## Definition of Done

- [ ] `IsSeeded` property exists on `Flag` entity with `private` setter, default `false`
- [ ] `Flag` constructor overload accepting `isSeeded` exists; existing constructor unchanged
- [ ] EF Core column mapping added to `FlagConfiguration.cs`
- [ ] Migration generated, applies cleanly, existing rows receive `IsSeeded = false`
- [ ] `DatabaseSeeder` exists at `Infrastructure/Seeding/DatabaseSeeder.cs`
- [ ] `DatabaseSeeder` registered as scoped in `AddInfrastructure()`
- [ ] `MigrateAsync()` extension exists in `Banderas.Api/Extensions/`
- [ ] `MigrateAsync()` called before seeder in the Development startup block
- [ ] Seeder invoked inside `IsDevelopment()` guard in `Program.cs`
- [ ] Normal mode is per-record — backfills empty or archived slots only
- [ ] Reset mode deletes only `IsSeeded = true` rows — manual flags never touched
- [ ] Reset mode skips and logs slots occupied by non-seeded active flags
- [ ] All six seed records inserted covering all three strategies and two environments
- [ ] Every seeder-inserted row stamped `IsSeeded = true`
- [ ] `IsSeeded` not present on any DTO or API response
- [ ] All log messages use structured logging with named properties
- [ ] `dotnet build` passes with 0 warnings, 0 errors
- [ ] All 110 existing tests still pass
- [ ] CSharpier check passes
