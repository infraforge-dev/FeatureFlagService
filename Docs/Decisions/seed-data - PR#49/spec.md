# Specification: Seed Data for Local Development

**Document:** `Docs/Decisions/seed-data/spec.md`
**Branch:** `feature/phase1-finish-line`
**Phase:** 1 — Developer Experience
**Status:** Ready for Implementation
**Author:** Jose / Claude Architect Session
**Date:** 2026-04-13

[Pull Request #49](https://github.com/amodelandme/FeatureFlagService/pull/49)

---

## Table of Contents

- [User Story](#user-story)
- [Background & Goals](#background--goals)
- [Design Decisions](#design-decisions)
  - [DD-1: Automatic Startup Seeding, Development Only](#dd-1-automatic-startup-seeding-development-only)
  - [DD-2: Idempotency — Skip if Data Exists](#dd-2-idempotency--skip-if-data-exists)
  - [DD-3: Reset Mode via Environment Variable](#dd-3-reset-mode-via-environment-variable)
  - [DD-4: Layer Placement](#dd-4-layer-placement)
- [Scope](#scope)
- [Seed Records](#seed-records)
- [Acceptance Criteria](#acceptance-criteria)
  - [AC-1: DatabaseSeeder Class](#ac-1-databaseseeder-class)
  - [AC-2: Normal Mode — Idempotent Insert](#ac-2-normal-mode--idempotent-insert)
  - [AC-3: Reset Mode — Wipe and Re-seed](#ac-3-reset-mode--wipe-and-re-seed)
  - [AC-4: Environment Guard](#ac-4-environment-guard)
  - [AC-5: Startup Wiring in Program.cs](#ac-5-startup-wiring-in-programcs)
  - [AC-6: Seed Record Coverage](#ac-6-seed-record-coverage)
  - [AC-7: Logging](#ac-7-logging)
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

This spec introduces a `DatabaseSeeder` that solves both problems: it seeds a
curated set of representative flags on startup (Development only), and supports a
controlled reset when a developer needs a clean slate.

---

## Design Decisions

### DD-1: Automatic Startup Seeding, Development Only

The seeder runs automatically during application startup. No manual CLI step is
required. This preserves the `docker compose up` onboarding experience described
in the product vision.

The seeder is **guarded by `IHostEnvironment.IsDevelopment()`**. It will not run
in Staging or Production under any circumstances. This makes the feature safe to
ship without additional configuration management.

**Rejected alternative — EF Core `HasData()`:** `HasData()` is baked into
migrations and runs in all environments unless carefully managed. It also cannot
support reset logic or environment-conditional behavior without significant
workarounds. Not appropriate for this use case.

**Rejected alternative — CLI command only:** Requires a manual step after startup,
breaking the `docker compose up` onboarding flow.

---

### DD-2: Idempotency — Skip if Data Exists

On every startup, the seeder checks whether seed records already exist before
inserting. If any seed records are found, seeding is skipped entirely. Running
the app twice produces the same database state as running it once.

The existence check is performed by flag name — if a flag with the seed name
already exists (including archived), the insert for that record is skipped.

---

### DD-3: Reset Mode via Environment Variable

A developer who needs a clean slate can trigger a full reset by setting the
environment variable `SEED_RESET=true` before starting the app. In reset mode:

1. All existing seed records are deleted (matched by name from the seed manifest)
2. The full seed set is re-inserted from scratch

Reset is scoped to seed records only — flags created manually by developers are
not touched.

The variable is passable via Docker Compose without modifying source code:

```bash
SEED_RESET=true docker compose up
```

Or declared temporarily in `docker-compose.override.yml` for a session.

---

### DD-4: Layer Placement

| Concern | Layer | Location |
|---------|-------|----------|
| `DatabaseSeeder` class | Infrastructure | `Infrastructure/Seeding/DatabaseSeeder.cs` |
| Startup trigger | Api | `Program.cs` |

The seeder belongs in Infrastructure because it has a direct dependency on
`AppDbContext` (EF Core). The Api layer owns `Program.cs` and is responsible for
calling the seeder during startup orchestration.

---

## Scope

| # | What | File(s) Affected |
|---|------|-----------------|
| 1 | `DatabaseSeeder` class | `Infrastructure/Seeding/DatabaseSeeder.cs` |
| 2 | Startup wiring | `FeatureFlag.Api/Program.cs` |
| 3 | Seed record manifest (inline) | `Infrastructure/Seeding/DatabaseSeeder.cs` |

No new migrations. No new DTOs. No new endpoints. No changes to existing domain
logic or validators.

---

## Seed Records

The seeder inserts the following flags. They are designed to exercise all three
rollout strategies across multiple environments — giving a developer or recruiter
an immediately meaningful API to explore.

| Name | Environment | Strategy | Enabled | Notes |
|------|-------------|----------|---------|-------|
| `dark-mode` | Development | None | `true` | Simple on/off flag |
| `new-dashboard` | Development | Percentage | `true` | 30% rollout |
| `beta-features` | Development | RoleBased | `true` | Admin and Beta roles |
| `maintenance-mode` | Development | None | `false` | Disabled flag example |
| `dark-mode` | Staging | None | `true` | Cross-environment demo |
| `new-dashboard` | Staging | Percentage | `true` | 50% rollout in Staging |

---

## Acceptance Criteria

---

### AC-1: DatabaseSeeder Class

**File:** `FeatureFlag.Infrastructure/Seeding/DatabaseSeeder.cs`

- Class is `internal sealed`
- Constructor accepts `AppDbContext` and `ILogger<DatabaseSeeder>`
- Exposes a single public method: `Task SeedAsync(bool reset, CancellationToken ct)`
- Does not reference any Application layer types — uses EF Core directly

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

### AC-2: Normal Mode — Idempotent Insert

When `reset` is `false`:

- For each seed record, check if a flag with that name and environment already
  exists in the database (including archived flags)
- If it exists → skip that record, log at `Debug` level:
  `"Seed record '{Name}' ({Environment}) already exists — skipping."`
- If it does not exist → insert and save
- If all records already exist → log at `Information` level:
  `"Seeding skipped — all seed records already present."`
- If any records were inserted → log at `Information` level:
  `"Seeded {Count} flag(s)."`

The check uses `AnyAsync` on `AppDbContext.Flags` filtered by `Name` and
`Environment`. It does **not** use the repository — the seeder calls `DbContext`
directly.

---

### AC-3: Reset Mode — Wipe and Re-seed

When `reset` is `true`:

- Collect the names of all seed records from the manifest
- Delete all flags from the database whose `Name` is in the seed manifest AND
  whose `Environment` matches — using `ExecuteDeleteAsync` for efficiency
- Re-insert all seed records unconditionally
- Log at `Warning` level before delete:
  `"SEED_RESET=true — deleting existing seed records before re-seeding."`
- Log at `Information` level after insert:
  `"Re-seeded {Count} flag(s)."`

Reset is scoped to seed records only. Flags not in the seed manifest are
untouched regardless of reset mode.

---

### AC-4: Environment Guard

**File:** `FeatureFlag.Api/Program.cs`

- The seeder is only invoked if `app.Environment.IsDevelopment()` returns `true`
- If not in Development, no seeding code executes and no log output is produced
  related to seeding
- The guard is applied at the call site in `Program.cs`, not inside `DatabaseSeeder`
  itself — the seeder has no knowledge of environment

```csharp
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    var reset = Environment.GetEnvironmentVariable("SEED_RESET") == "true";
    await seeder.SeedAsync(reset);
}
```

---

### AC-5: Startup Wiring in Program.cs

- `DatabaseSeeder` is registered in DI as a scoped service in
  `Infrastructure/DependencyInjection.cs` (the `AddInfrastructure()` extension method)
- Registration:

```csharp
services.AddScoped<DatabaseSeeder>();
```

- `SeedAsync` is called after `app.MapControllers()` and after
  `await app.MigrateAsync()` (if a migration helper exists) — seeding always
  runs on a fully migrated schema
- The call site in `Program.cs` creates a scoped `IServiceProvider` via
  `app.Services.CreateScope()` to resolve the scoped `DatabaseSeeder`

---

### AC-6: Seed Record Coverage

The seeder inserts exactly the six records defined in the [Seed Records](#seed-records)
table above. Each record must be constructed as a valid `Flag` entity using the
domain's existing factory or constructor — not via raw SQL or object initializers
that bypass domain invariants.

Seed record `StrategyConfig` values:

| Name | Environment | StrategyConfig |
|------|-------------|----------------|
| `dark-mode` | Development | `null` |
| `new-dashboard` | Development | `{"percentage":30}` |
| `beta-features` | Development | `{"roles":["Admin","Beta"]}` |
| `maintenance-mode` | Development | `null` |
| `dark-mode` | Staging | `null` |
| `new-dashboard` | Staging | `{"percentage":50}` |

`StrategyConfig` is `null` for `None` strategy flags (consistent with the wire
contract established in PR #37).

---

### AC-7: Logging

All log messages produced by the seeder use structured logging with named
properties — no string interpolation.

| Scenario | Level | Message Template |
|----------|-------|-----------------|
| Record already exists (per record) | Debug | `"Seed record '{Name}' ({Environment}) already exists — skipping."` |
| All records already present | Information | `"Seeding skipped — all seed records already present."` |
| Records inserted | Information | `"Seeded {Count} flag(s)."` |
| Reset triggered | Warning | `"SEED_RESET=true — deleting existing seed records before re-seeding."` |
| Reset complete | Information | `"Re-seeded {Count} flag(s)."` |

No raw user data is logged. Seed records contain no user-identifying information.

---

## File Layout

```
FeatureFlag.Infrastructure/
  Seeding/
    DatabaseSeeder.cs         ← new

FeatureFlag.Api/
  Program.cs                  ← modified (environment guard + seeder call)

FeatureFlag.Infrastructure/
  DependencyInjection.cs      ← modified (register DatabaseSeeder as scoped)
```

---

## Implementation Notes

- The seeder uses `AppDbContext` directly — do not route through
  `IFeatureFlagRepository`. The repository interface is the Application layer's
  boundary; the seeder lives in Infrastructure and may call `DbContext` directly.
- `ExecuteDeleteAsync` requires EF Core 7+. Already available in this project
  (EF Core 10).
- The seeder must await each `SaveChangesAsync` call. Do not batch inserts across
  environments in a single `SaveChanges` call — insert and save per record or per
  batch to keep error attribution clear.
- Do not call `HasData()` in any migration as part of this work.
- The `SEED_RESET` env var is read via `Environment.GetEnvironmentVariable()` at
  the `Program.cs` call site — not injected via `IConfiguration`. This keeps the
  reset signal clearly separated from application configuration.
- CSharpier formatting must pass — run `dotnet csharpier .` before committing.

---

## Out of Scope

- Seed data for Staging or Production environments
- A dedicated reset CLI command
- Any new API endpoint for triggering seed or reset
- Changes to existing migrations
- Seed data for integration tests — test data is owned by each test class via
  Testcontainers setup

---

## Definition of Done

- [ ] `DatabaseSeeder` exists at `Infrastructure/Seeding/DatabaseSeeder.cs`
- [ ] `DatabaseSeeder` is registered as scoped in `AddInfrastructure()`
- [ ] Seeder is called in `Program.cs` inside an `IsDevelopment()` guard
- [ ] Normal mode is idempotent — running the app twice does not duplicate records
- [ ] Reset mode deletes and re-inserts all seed records when `SEED_RESET=true`
- [ ] Reset does not touch flags outside the seed manifest
- [ ] All six seed records are inserted covering all three strategies
- [ ] All log messages use structured logging with named properties
- [ ] `dotnet build` passes with 0 warnings, 0 errors
- [ ] `dotnet test` — all 110 existing tests still pass
- [ ] CSharpier check passes
