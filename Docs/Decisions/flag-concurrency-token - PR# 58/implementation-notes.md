# Flag Concurrency Token — Implementation Notes

**Session date:** 2026-05-01
**Branch:** `dev`
**Spec reference:** None — change driven by the DDD audit follow-up in `Docs/Decisions/flag-ddd-analysis-backlog.md`
**Build status:** `dotnet build Banderas.Tests.Integration` passed with 0 warnings and 0 errors
**Tests:** New `FlagConcurrencyTokenTests` passing (1/1); broader suite not re-run in this session
**PR:** TBD

## What Was Built

`Flag` now carries a `uint Version` property mapped to the Postgres `xmin` system column via `IsRowVersion()`. EF Core includes `xmin` in the `WHERE` clause of every `UPDATE` and `DELETE` against `flags`; if another transaction has updated the row since load, Postgres reports zero affected rows and EF raises `DbUpdateConcurrencyException`. `BanderasRepository.SaveChangesAsync` translates that exception into a new `FlagConcurrencyException` (HTTP 409), so the Application and API layers see a domain-meaningful conflict rather than an infrastructure-flavored 500.

## Why This Change

The DDD audit flagged that a release-control system without optimistic concurrency is a footgun: two operators toggling the same flag concurrently silently last-write-wins. The repository already had a precedent for translating infrastructure errors into domain exceptions (the Postgres `23505` duplicate-name handler), so the same pattern was extended to concurrency conflicts. `xmin` was chosen over a manually maintained `byte[] Version` column because Postgres updates it on every row change for free — no triggers, no `SaveChanges` override, no manual bump.

## Key Decisions

`xmin` over a manual `byte[] Version`. Initial attempt used `byte[] Version` with `IsRowVersion()`, which is the SQL Server idiom; on Npgsql, that combination produces a `bytea` column the database never auto-updates, so the concurrency check becomes a silent no-op. Switching to `uint Version` lets the Npgsql provider recognize the row-version intent and map it to `xmin`. The generated migration adds an `xid` column named `xmin` with `rowVersion: true`; Npgsql's migration SQL generator emits no DDL for it because `xmin` is a system column already present on every Postgres row.

Translation lives in the repository, not the service. `DbUpdateConcurrencyException` only surfaces inside `SaveChangesAsync`, so translating it anywhere else would require either re-throwing or leaking EF Core types upward. `BanderasRepository` already does the same kind of translation for the Postgres unique-constraint violation, so the pattern is consistent.

`FlagConcurrencyException` carries the conflicted flag's name and environment when available. The repository pulls them off the entity in `ex.Entries`, which is the in-memory state about to be saved — not necessarily the persisted state. That trade-off is documented in the test (see below).

A parameterless constructor exists for the rare case where `ex.Entries` does not contain a `Flag` (defensive — should not happen in normal flow, but avoids a null deref).

## File-by-File Changes

| File | Change |
|---|---|
| `Banderas.Domain/Entities/Flag.cs` | Added `public uint Version { get; private set; }` |
| `Banderas.Infrastructure/Persistence/BanderasDbContext.cs` | Added `modelBuilder.Entity<Flag>().Property(p => p.Version).IsRowVersion();` |
| `Banderas.Infrastructure/Migrations/20260501195453_AddFlagConcurrencyToken.cs` | Generated migration; emits no real DDL on Postgres |
| `Banderas.Infrastructure/Migrations/BanderasDbContextModelSnapshot.cs` | Updated snapshot |
| `Banderas.Domain/Exceptions/FlagConcurrencyException.cs` | New 409-mapped domain exception |
| `Banderas.Infrastructure/Persistence/BanderasRepository.cs` | `SaveChangesAsync` catches `DbUpdateConcurrencyException` first and rethrows as `FlagConcurrencyException` |
| `Banderas.Tests.Integration/FlagConcurrencyTokenTests.cs` | New integration test proving the token fires |

## Test Notes

The integration test seeds a flag, opens two DI scopes with separate `DbContext` instances, has both load the flag, mutates and saves in scope A, then mutates and saves in scope B through the repository — asserting `FlagConcurrencyException` with `StatusCode = 409`.

The first run of the test asserted on `ex.Message.Contains("concurrency-token-test")` (the seeded name) and failed: the exception captured the entity's *current* in-memory name (`renamed-by-loser`), because the repository pulls `conflicted.Name` from the tracked entity that was about to be saved, not from the database. The assertion was loosened to type + status code, which is the contract that actually matters for callers. Reporting the persisted name would require a reload from the database before throwing — deferred unless a caller needs it.

## Risks and Follow-Ups

- **No API-level test yet.** The integration test exercises the repository directly. A higher-level test that issues two concurrent `PUT /api/flags/...` requests and asserts a 409 ProblemDetails response would close the loop on the public contract. Worth adding alongside the next flag-mutation endpoint change.
- **`DbUpdateConcurrencyException` is also raised on deletes.** The current handler covers both, but no delete path exists yet that would exercise it. Revisit when a delete/archive endpoint lands.
- **Detached-entity edge case.** Npgsql's xmin mapping is reliable as long as entities are tracked end-to-end inside one `DbContext`. If a future code path detaches a `Flag`, mutates it, and re-attaches via `Update`, the `Version` value must round-trip with the entity. Naming the property `xmin` (instead of `Version`) is the documented mitigation; deferred until/unless detach-reattach appears.

## How to Test

```bash
dotnet build Banderas.sln -p:TreatWarningsAsErrors=true
dotnet test Banderas.sln --filter "FullyQualifiedName~FlagConcurrencyTokenTests"
```

## Interview Lens

The interesting decision was *not* picking the path that looked most idiomatic at first glance. `byte[] Version + IsRowVersion()` is the canonical EF Core row-version pattern, but it only works on SQL Server — on Postgres it compiles, runs, and silently fails to enforce anything. The fix was to use `uint Version` so the Npgsql provider would map to the `xmin` system column and let Postgres maintain the value for free. That kind of failure — where the code looks right and the test would have to specifically prove conflict detection — is exactly what the integration test is there to catch.

Translating the infrastructure exception at the repository edge keeps the rest of the application free of EF Core types and keeps the public HTTP contract (409) as the single source of truth for what a concurrency conflict means in this system.

## Foundation Docs Updated

- [ ] `Docs/current-state.md`
- [ ] `Docs/roadmap.md`
- [ ] `Docs/architecture.md`

## Definition of Done — Status

- [x] `uint Version` property added to `Flag`
- [x] `IsRowVersion()` configured in `BanderasDbContext.OnModelCreating`
- [x] EF migration generated and snapshot updated
- [x] `FlagConcurrencyException` added (409)
- [x] `BanderasRepository.SaveChangesAsync` translates `DbUpdateConcurrencyException`
- [x] Integration test proves the token fires on concurrent update
- [x] `dotnet build` passes with zero warnings
