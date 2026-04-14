# Service Interface DTO Refactor — Implementation Notes

**Session date:** 2026-03-26
**Branch:** `refactor/service-interface-dtos`
**Spec reference:** `docs/Decisions/refactor-service-interface-dtos/spec.md`
**Build status:** Passed — 0 warnings, 0 errors
**Tests:** 8/8 passing
**Smoke test:** Passed — POST, GET, PUT, DELETE all return correct responses

[Pull Request #27](https://github.com/amodelandme/Bandera/pull/27)

---

## 1. What Was Implemented

All items in scope per the spec were completed:

| File | Change |
|---|---|
| `Bandera.Application/Interfaces/IBanderaService.cs` | Replaced — all `Flag` entity references removed; signatures now use `FlagResponse`, `CreateFlagRequest`, `UpdateFlagRequest` |
| `Bandera.Application/Services/BanderaService.cs` | Updated — `Flag` construction moved from controller into `CreateFlagAsync`; `UpdateFlagAsync` now accepts `UpdateFlagRequest`; all return paths call `ToResponse()` internally |
| `Bandera.Api/Controllers/BanderasController.cs` | Simplified — all `.ToResponse()` calls removed; `Flag` entity construction removed; `UpdateFlagAsync` call collapsed from 5 primitives to single `request` argument |

`FlagMappings.cs`, `Flag.cs`, all DTOs, `EvaluationController`, strategies, evaluator, and the entire infrastructure layer were untouched.

Two additional files were modified during smoke test troubleshooting (see section 3):

| File | Change |
|---|---|
| `Bandera.Api/appsettings.Development.json` | `Host` changed from `localhost` to `postgres` — the Docker Compose service name |
| `.devcontainer/devcontainer.json` | `postStartCommand` updated to join the Postgres Docker network on container start; `dotnet-ef` added to `.config/dotnet-tools.json` |

---

## 2. Deviations from the Spec

### 2.1 `using Bandera.Domain.Enums` Added to `BanderaService.cs`

The initial write of `BanderaService.cs` used fully-qualified `Bandera.Domain.Enums.EnvironmentType` in all four method signatures instead of the short form. A `using Bandera.Domain.Enums;` directive was added and all four occurrences replaced with the unqualified `EnvironmentType`. No architectural impact.

### 2.2 `using Bandera.Domain.Entities` Removed from the Controller

The spec did not explicitly list removing this `using` directive, but it became a stale import once `Flag` entity construction moved into the service. It was removed as part of the controller cleanup. The build confirmed no remaining references to `Bandera.Domain.Entities` in the Api layer.

---

## 3. Infrastructure Issues Encountered During Smoke Test

These issues are unrelated to the refactor itself but were discovered and resolved during the smoke test verification step. They are documented here as they affect the dev environment and required permanent fixes.

### 3.1 Devcontainer Cannot Reach Postgres on `localhost`

**Symptom:** API returned `500 — Failed to connect to 127.0.0.1:5432 — Connection refused` on every request.

**Root cause:** The devcontainer and the Postgres container (started via `docker compose up -d`) are on separate Docker networks. Inside the devcontainer, `localhost` resolves to the devcontainer itself — not to the host or the Postgres container. Port `5432` is mapped on the host (`0.0.0.0:5432->5432/tcp`), but the devcontainer's network namespace cannot reach the host's loopback interface.

**What was tried first:**
- `Host=host.docker.internal` — does not resolve on Linux devcontainers
- `Host=172.18.0.2` (Postgres container IP) — reachable via TCP test but fragile; IP will change if the container is recreated

**Resolution applied:**
1. Manually connected the devcontainer to `bandera_default` (the Docker Compose network) using `docker network connect`
2. Changed `Host` in `appsettings.Development.json` from `localhost` to `postgres` — the Docker Compose service name, which resolves reliably on the shared network
3. Updated `postStartCommand` in `devcontainer.json` to run `docker network connect bandera_default $(cat /etc/hostname) 2>/dev/null || true` on every container start, making this automatic going forward

**Prerequisite for future rebuilds:** The Postgres container (`docker compose up -d`) must be running before or shortly after the devcontainer starts for the `postStartCommand` network join to succeed. If the devcontainer starts before Postgres, run `docker compose up -d` and then `docker network connect bandera_default $(cat /etc/hostname)` from inside the devcontainer manually.

**Architect note:** The connection string `Host=postgres` only works because the devcontainer is on the same Docker network as the Compose stack. If the devcontainer networking changes (e.g., switching to a full docker-compose devcontainer setup), this hostname should continue to work. `localhost` should not be restored — it will not work in this environment.

---

### 3.2 `dotnet ef` Not Available in the Devcontainer

**Symptom:** `dotnet ef` was not listed as a dotnet CLI command — the tool was not installed.

**Root cause:** `dotnet-ef` is a separate tool that must be explicitly installed. It was not in the project's `.config/dotnet-tools.json` manifest, and the devcontainer `postCreateCommand` runs `dotnet tool restore` against that manifest, so it was never installed.

**Resolution:**
- Added `dotnet-ef` version `10.0.5` to `.config/dotnet-tools.json` via `dotnet tool install dotnet-ef --local`
- On all future container rebuilds, `dotnet tool restore` in `postCreateCommand` will install it automatically

**Usage:** As a local tool, invoke it as `dotnet ef` (not `dotnet-ef` directly). The local tool manifest entry handles resolution.

---

### 3.3 Migration Had Not Been Applied

**Symptom:** After Docker was stopped and restarted, the `featureflags` database existed (Postgres volume persisted) but had no tables — `dotnet ef migrations list` confirmed `InitialCreate` was pending.

**Resolution:** Applied the migration:
```bash
dotnet ef database update \
  --project Bandera.Infrastructure \
  --startup-project Bandera.Api
```

**Note for future rebuilds:** The Postgres volume persists across container restarts so this is a one-time setup step. If the volume is ever deleted, run `dotnet ef database update` again after `docker compose up -d`.

---

## 4. Architecture Decisions Validated This Session

### Domain entity never crosses the service boundary

`IBanderaService` no longer references `Flag` in any method signature. The only domain types remaining on the interface are `EnvironmentType` (an enum) and `FeatureEvaluationContext` (a value object, unchanged per spec). The entity boundary is now clean.

### Mapping responsibility consolidated in the service

`ToResponse()` is called in exactly three places in `Bandera` — once in `GetFlagAsync`, once in `GetAllFlagsAsync` (via `.Select`), and once in `CreateFlagAsync`. The extension method in `FlagMappings.cs` is unchanged; it is now called from a single, correct location rather than scattered across callers.

### `CreatedAtAction` routing data sourced from `FlagResponse`

After the refactor, `CreateFlagAsync` returns a `FlagResponse` instead of a `Flag`. The `CreatedAtAction` call in the controller builds its route values from `created.Name` and `created.Environment` — both are present on `FlagResponse`, so no routing data was lost. This was identified as a potential edge case during pre-implementation analysis and confirmed correct by the smoke test.

### `UpdateFlagAsync` — 5-primitive signature replaced with DTO

The previous signature (`bool isEnabled, RolloutStrategy strategyType, string strategyConfig, ...`) required callers to destructure `UpdateFlagRequest` manually. The new signature accepts `UpdateFlagRequest` directly. The internal call to `flag.Update(...)` is unchanged — it still receives the three individual values, extracted inside the service where that belongs.

---

## 5. Acceptance Criteria — Verified

| Criterion | Status |
|---|---|
| `IBanderaService` has no `Flag` type in any method signature | ✅ |
| `BanderasController` contains zero `.ToResponse()` calls | ✅ |
| `BanderaService.CreateFlagAsync` constructs `Flag` internally | ✅ |
| `BanderaService.UpdateFlagAsync` accepts `UpdateFlagRequest`, not primitives | ✅ |
| `dotnet build` — 0 errors, 0 warnings | ✅ |
| All 8 existing tests pass | ✅ |
| Manual smoke test — POST, GET, PUT, DELETE correct responses | ✅ |

---

## 6. Known Issues Carried Forward

### KI-002 — `FeatureEvaluator` implicit precondition
Unchanged. Still documented via XML doc comment on `FeatureEvaluator.Evaluate`.

### KI-003 — `StrategyConfig` validation deferred to runtime
Unchanged. No validation at write time. Phase 1 requirement.

### KI-007 — Devcontainer Networking Requires Postgres to Start First

**Severity:** Low — inconvenience, not a bug
**Status:** Mitigated — `postStartCommand` automates the network join on each container start

The `postStartCommand` network join runs after the devcontainer starts. If Postgres is not yet running at that moment (e.g., after a full machine restart where the devcontainer starts before Docker Compose), the join will silently fail and the API will not be able to reach the database.

**Workaround:** Run `docker compose up -d` first, then open the devcontainer. If the devcontainer is already running, execute from the VS Code terminal:
```bash
docker network connect bandera_default $(cat /etc/hostname)
```

**Longer-term fix:** Migrate the devcontainer to a full docker-compose devcontainer setup so both services start together and share a network by default. Deferred — not a Phase 1 blocker.

---

## 7. What Is Intentionally Out of Scope (This Session)

- `FluentValidation` on request DTOs (Phase 1 — KI-003)
- Integration tests (Phase 1)
- Global exception middleware (Phase 1)
- Any change to domain entities, DTOs, strategies, evaluator, or infrastructure

---

*Bandera | refactor/service-interface-dtos | Phase 1 Prep*
