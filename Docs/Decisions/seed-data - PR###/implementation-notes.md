# Seed Data for Local Development — Implementation Notes

**Session date:** 2026-04-13
**Branch:** `feature/phase1-finish-line`
**Spec reference:** `Docs/Decisions/seed-data - PR###/spec-v3.md`
**Build status:** Passed — 0 warnings, 0 errors
**Tests:** 113/113 passing (81 unit + 32 integration)
**PR:** `PR###` — update after PR creation

---

## Deviations from Spec

### DEV-001 — `DatabaseSeeder` Must Be `public` for Api Startup Resolution

**Spec says:** `DatabaseSeeder` is `internal sealed`.

**What actually happened:** `Program.cs` in `Banderas.Api` resolves `DatabaseSeeder`
from DI and calls `SeedAsync(...)` directly during startup. `Banderas.Api` and
`Banderas.Infrastructure` are separate assemblies, so an `internal` seeder type
and its members are not visible at the call site. The build fails with `CS0122`
if `DatabaseSeeder` remains `internal`.

**Fix applied:** Changed `DatabaseSeeder` to `public sealed`.

**Why this is acceptable:** This is an assembly visibility requirement, not an
architectural broadening of responsibility. The seeder is still registered only in
Infrastructure, used only from startup wiring, and not exposed on any API surface.

---

## Build Verification

- `dotnet ef migrations add AddIsSeededToFlag --project "Banderas.Infrastructure" --startup-project "Banderas.Api"` -> passed
- `dotnet ef database update --project "Banderas.Infrastructure" --startup-project "Banderas.Api" --connection "Host=postgres;Port=5432;Database=featureflags;Username=postgres;Password=postgres"` -> passed
- `dotnet csharpier check .` -> passed
- `dotnet build Banderas.sln -p:TreatWarningsAsErrors=true` -> 0 warnings, 0 errors
- `dotnet test Banderas.sln` -> 113/113 passing

---

## Notes

- The `AddIsSeededToFlag` migration was generated and applied against the running
  Postgres devcontainer, not an in-memory provider.
- The implementation follows `spec-v3.md` aside from the `public` visibility change
  documented above.
