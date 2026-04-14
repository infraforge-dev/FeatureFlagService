# Input Validation Hardening — Implementation Notes

**Session date:** 2026-04-03
**Branch:** `fix/input-validation-hardening`
**Spec reference:** `Docs/Decisions/input-validation-hardening - PR# 37/spec.md`
**Build status:** Passed — 0 warnings, 0 errors
**Tests:** 8/8 passing
**PR:** 37

---

## Table of Contents

- [What Was Built](#what-was-built)
- [Spec Gaps Resolved](#spec-gaps-resolved)
- [Deviations from Spec](#deviations-from-spec)
- [File-by-File Changes](#file-by-file-changes)
- [Definition of Done — Status](#definition-of-done--status)

---

## What Was Built

Three open validation gaps resolved in a single PR:

1. **KI-NEW-001** — `BeValidPercentageConfig` and `BeValidRoleConfig` were private
   static methods duplicated identically in both `CreateFlagRequestValidator` and
   `UpdateFlagRequestValidator`. Extracted into a shared `StrategyConfigRules`
   internal static class. Both validators now call the shared methods.

2. **KI-008** — `GET /api/flags/{name}`, `PUT /api/flags/{name}`, and
   `DELETE /api/flags/{name}` accepted any string as a route parameter with no
   character validation. A new `RouteParameterGuard` helper enforces the flag name
   allowlist (`^[a-zA-Z0-9\-_]+$`) at the top of each affected controller action.
   Invalid names return `400` with a `ProblemDetails` response via
   `GlobalExceptionMiddleware`.

3. **Name uniqueness** — `DuplicateFlagNameException` was defined in
   `Bandera.Domain/Exceptions/` but never thrown. A duplicate `POST` fell through
   to the database, which returned a `500` from an unhandled Postgres unique
   constraint violation. The service layer now calls `ExistsAsync` before `AddAsync`
   and throws `DuplicateFlagNameException` on a match. A concurrent-request race
   condition (TOCTOU) is also handled — see [Spec Gaps Resolved](#spec-gaps-resolved).

---

## Spec Gaps Resolved

### Gap 1 — `JsonDocument` not disposed in `StrategyConfigRules`

The spec's code for both `BeValidPercentageConfig` and `BeValidRoleConfig` called
`JsonDocument.Parse(config)` but did not dispose the result. Microsoft's documentation
states:

> *"JsonDocument utilizes resources from pooled memory. Failure to properly dispose
> this object will result in the memory not being returned to the pool, which will
> increase GC impact across various parts of the framework."*

Both methods are called on every `POST /api/flags` and `PUT /api/flags/{name}`.
Corrected to `using JsonDocument doc = JsonDocument.Parse(config);` in both methods.

### Gap 2 — TOCTOU race condition on name uniqueness check

The spec's `CreateFlagAsync` guards against duplicate names via `ExistsAsync`, but
between that check returning `false` and `SaveChangesAsync` completing, a concurrent
request can create the same flag. The DB unique constraint then fires, EF Core throws
`DbUpdateException`, and the middleware returns `500`.

The fix lives in `BanderaRepository.SaveChangesAsync`. The repository captures
any pending `Added` entries before the save, then intercepts
`DbUpdateException` wrapping a `PostgresException` with `SqlState "23505"` and
rethrows as `DuplicateFlagNameException` with full name and environment context.

This was placed in the Infrastructure layer — not the service layer — because the
Application project has no EF Core reference. Catching `DbUpdateException` in the
service would have introduced an Infrastructure dependency into the Application layer,
violating Clean Architecture.

### Gap 3 — `ValidateName` had no null guard

The spec's `RouteParameterGuard.ValidateName` called `Regex.IsMatch(name)` without
a null check. ASP.NET Core model binding will not produce a null route segment under
normal operation, but a null input would throw `ArgumentNullException` rather than
`BanderaValidationException`, producing an unintended `500`. Added
`ArgumentNullException.ThrowIfNull(name)` as the first line of the method.

---

## Deviations from Spec

### `StrategyConfigRules` pulled from Phase 2 into Phase 1

`roadmap.md` listed `StrategyConfigRules` extraction under Phase 2 and `current-state.md`
marked KI-NEW-001 as "Deferred — not a Phase 1 blocker". The spec intentionally pulls it
forward into this PR. The duplication risk is real — the methods would diverge the moment
one validator is updated without the other — and the fix is contained to three files with
no behavioural change. Included in this PR.

### TOCTOU handling placed in repository, not service

The spec does not address the concurrent-request race condition. When the decision was
made to handle it, the natural location appeared to be `CreateFlagAsync` in the service.
However, the Application layer has no EF Core reference. To avoid introducing that
dependency, the catch was placed in `BanderaRepository.SaveChangesAsync` instead,
where `DbUpdateException` and `PostgresException` are already in scope.

---

## File-by-File Changes

### New files

| File | Purpose |
|---|---|
| `Bandera.Application/Validators/StrategyConfigRules.cs` | Shared `internal static` class — `BeValidPercentageConfig` and `BeValidRoleConfig` extracted from both validators |
| `Bandera.Domain/Exceptions/BanderaValidationException.cs` | 400 domain exception for route parameter allowlist failures |
| `Bandera.Api/Helpers/RouteParameterGuard.cs` | Static guard — compiled regex allowlist on `{name}` route parameters |

### Modified files

| File | Change |
|---|---|
| `Bandera.Application/Validators/CreateFlagRequestValidator.cs` | Removed `BeValidPercentageConfig` and `BeValidRoleConfig` private methods; `.Must()` calls updated to `StrategyConfigRules.*`; removed unused `using System.Text.Json` |
| `Bandera.Application/Validators/UpdateFlagRequestValidator.cs` | Same as above |
| `Bandera.Domain/Interfaces/IBanderaRepository.cs` | Added `ExistsAsync(string name, EnvironmentType environment, CancellationToken ct)` with XML doc comment |
| `Bandera.Domain/Exceptions/DuplicateFlagNameException.cs` | Constructor updated from `(string flagName)` to `(string flagName, EnvironmentType environment)`; message now includes environment |
| `Bandera.Infrastructure/Persistence/BanderaRepository.cs` | Implemented `ExistsAsync` using `AnyAsync`; `SaveChangesAsync` catches `DbUpdateException` wrapping Postgres `23505` and rethrows as `DuplicateFlagNameException` |
| `Bandera.Application/Services/BanderaService.cs` | `CreateFlagAsync` — sanitize name, call `ExistsAsync`, throw `DuplicateFlagNameException` on match, then construct and persist |
| `Bandera.Api/Controllers/BanderasController.cs` | `RouteParameterGuard.ValidateName(name)` added as first statement in `GetByNameAsync`, `UpdateAsync`, and `ArchiveAsync`; added `using Bandera.Api.Helpers` |
| `Bandera.Api/Middleware/GlobalExceptionMiddleware.cs` | Verified — `409 Conflict` case already present; no change required |

---

## Definition of Done — Status

- [x] `StrategyConfigRules.cs` created with `BeValidPercentageConfig` and `BeValidRoleConfig` as `internal static` methods
- [x] Both duplicated private methods removed from `CreateFlagRequestValidator`
- [x] Both duplicated private methods removed from `UpdateFlagRequestValidator`
- [x] Both validators call `StrategyConfigRules.BeValidPercentageConfig` and `StrategyConfigRules.BeValidRoleConfig` in their `.Must()` chains
- [x] `BanderaValidationException` created in `Bandera.Domain/Exceptions/`
- [x] `RouteParameterGuard.ValidateName()` created in `Bandera.Api/Helpers/`
- [x] `RouteParameterGuard.ValidateName(name)` is the first call in `GetByNameAsync`, `UpdateAsync`, and `ArchiveAsync`
- [x] `ExistsAsync` added to `IBanderaRepository` with XML doc comment
- [x] `ExistsAsync` implemented in `BanderaRepository` using `AnyAsync`
- [x] `DuplicateFlagNameException` constructor updated to accept `(string flagName, EnvironmentType environment)`
- [x] `CreateFlagAsync` calls `ExistsAsync` and throws `DuplicateFlagNameException` before `AddAsync`
- [x] `GlobalExceptionMiddleware.GetTitleForStatusCode` contains the `409` case — verified, already present
- [ ] `POST /api/flags` with a duplicate name returns `409` with `application/problem+json` and a `detail` naming the flag and environment — verified by integration test (Phase 2)
- [ ] `GET /api/flags/{name}` with `name = "bad name!"` returns `400` with `application/problem+json` — verified by integration test (Phase 2)
- [ ] `PUT /api/flags/{name}` with `name = "bad name!"` returns `400` with `application/problem+json` — verified by integration test (Phase 2)
- [ ] `DELETE /api/flags/{name}` with `name = "bad name!"` returns `400` with `application/problem+json` — verified by integration test (Phase 2)
- [x] `dotnet build Bandera.sln` → 0 errors, 0 warnings
- [x] All existing tests passing: `dotnet test --filter "Category!=Integration"` → 8/8
- [x] `dotnet csharpier check .` → 0 violations
