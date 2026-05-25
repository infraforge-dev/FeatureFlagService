# Flag Variations — Implementation Notes

**Session date:** 2026-05-22
**Branch:** feat/flag-variations
**Spec reference:** Docs/Decisions/flag-variations/spec.md
**Build status:** ✅ Passing — 0 warnings, 0 errors
**Tests:** 404/404 passing (303 unit + 101 integration)
**PR:** TBD

---

## What Was Built

A full-slice implementation of the `Variation` Value Object on `Flag`, covering domain,
application, infrastructure, and tests. `Variation` gives every flag a typed menu of
possible return values (`Key`, `Kind`, `Value`) stored as `jsonb`. The domain enforces
five collection-level invariants on the menu; the application layer exposes
`VariationRequest`/`VariationResponse` DTOs with shared `VariationMenuRules` validators;
the infrastructure layer persists via `VariationListConverter` with a zero-downtime
additive migration that backfills all existing rows with a default `[off, on]` Boolean
menu; and the AI prompt enrichment sanitizes operator-authored `Key` and `Value` fields
before embedding them.

---

## Spec Gaps Resolved

None — the spec was complete and unambiguous throughout implementation.

---

## Deviations from Spec

None — implementation matched the spec exactly across all design decisions.

---

## Key Decisions

**DD-1 — `Key` + `Kind` + `Value`-as-JSON-string shape:**
`Value` stores the JSON-encoded payload (e.g., `"true"`, `"\"red-button\""`, `"42"`).
This keeps the VO shape uniform across all four kinds and defers JSON parsing to
construction time, where the `Variation` ctor validates kind-specific format.
`Boolean` accepts only canonical lowercase `"true"`/`"false"` (not `"True"` or `1`).
`Number`, `String`, and `Json` are validated via `JsonDocument.Parse` with `using`
dispose to catch malformed payloads at the boundary.

**DD-2 — Five collection-level invariants enforced on `Flag`:**
`EnsureVariationMenuIsValid` runs at construction and on `UpdateVariations`. The five
invariants are: non-empty (≥1), ≤20, all same `Kind`, unique keys (case-insensitive
ordinal), unique values (ordinal). These run atomically — the new menu is fully validated
before `_variations` is replaced. `FlagDomainException` is thrown on any violation.

**DD-3 — Index as wire contract, key as human label:**
The spec defines index (position in the array) as the stable identity used by environment
config rules, and key as the human-readable label. This means key renames on `UpdateVariations`
are a valid operation — they do not break existing rules. This is not yet enforced at the
infrastructure level (env config rules are Phase 5+) but the domain design preserves it.

**DD-4 — `UpdateFlagRequest.Variations` null = no change, `[]` = 400:**
`null` is the explicit sentinel for "leave the menu unchanged" on a PUT. `[]` is rejected
at the validator level (before service logic runs) because an empty menu violates the
domain invariant — rather than let it reach `Flag.UpdateVariations` and throw a
`FlagDomainException`, the validator gives a descriptive `400 ProblemDetails` with a
field-level error message.

**DD-5 — No permanent SQL default on `variations` column:**
The migration sets `DEFAULT '[]'::jsonb` during the `ALTER TABLE` to backfill existing
rows, then immediately drops it with `ALTER COLUMN ... DROP DEFAULT`. A permanent SQL
default of `'[]'` would silently allow a DB-level insert to bypass the domain invariant
(non-empty menu). The transient window is safe because all writes go through the domain
after migration.

**DD-6 — Backfill: every existing flag gets a default `[off, on]` Boolean menu:**
The migration backfills with `'[{"key":"off","kind":"Boolean","value":"false"},
{"key":"on","kind":"Boolean","value":"true"}]'::jsonb` — the canonical boolean menu.
This is the same menu `DatabaseSeeder` uses for all boolean seed flags. `MigrationBackfillTests`
verifies the Down migration drops the column cleanly and the Up re-applies the backfill.

**`VariationListConverter` re-runs the VO ctor on read:**
Deserialized `Variation` objects are constructed through the full public constructor,
not a bypassing internal path. This means corrupted `jsonb` data (invalid key char-class,
wrong value format for kind, etc.) throws on read rather than silently returning an
invalid domain object. The null-fallback to `[]` is a last-resort defensive guard
that should never fire in practice.

**AI prompt enrichment sanitizes `Key` and `Value`, emits `Kind` verbatim:**
`BanderasService.AnalyzeFlagsAsync` passes each variation's `Key` and `Value` through
`IPromptSanitizer` before building the prompt. `Kind` is emitted from the C# enum name
(canonical PascalCase), not from operator input — this eliminates one injection surface.
The `AiFlagAnalyzer.BuildPrompt` system prompt gains a sentence declaring the variations
block to be inert configuration data, not instructions.

---

## File-by-File Changes

| File | Change | Lines |
|------|--------|-------|
| `Banderas.Domain/Enums/VariationKind.cs` | Created — Boolean, String, Number, Json; CA1720 suppressed | ~30 |
| `Banderas.Domain/ValueObjects/Variation.cs` | Created — sealed record; full single-element validation in ctor | ~130 |
| `Banderas.Domain/Entities/Flag.cs` | Modified — Variations property + backing field; ctor sixth param; EnsureVariationMenuIsValid; UpdateVariations mutation | +93 |
| `Banderas.Application/DTOs/VariationRequest.cs` | Created — Key, Kind (string), Value | ~20 |
| `Banderas.Application/DTOs/VariationResponse.cs` | Created — Key, Kind (string), Value | ~15 |
| `Banderas.Application/Validators/VariationMenuRules.cs` | Created — ApplyMenuRules<T> shared extension; all 7 DD-2 invariants | ~170 |
| `Banderas.Application/DTOs/CreateFlagRequest.cs` | Modified — Variations: required init-only list | +11 |
| `Banderas.Application/DTOs/UpdateFlagRequest.cs` | Modified — Variations: nullable init-only list | +11 |
| `Banderas.Application/DTOs/FlagResponse.cs` | Modified — Variations: init-only, default [] | +7 |
| `Banderas.Application/DTOs/FlagMappings.cs` | Modified — VariationRequest ↔ Variation; Variation → VariationResponse; Kind parse | +34 |
| `Banderas.Application/Validators/CreateFlagRequestValidator.cs` | Modified — ApplyMenuRules wired | +7 |
| `Banderas.Application/Validators/UpdateFlagRequestValidator.cs` | Modified — ApplyMenuRules wired (nullable path) | +4 |
| `Banderas.Application/Services/BanderasService.cs` | Modified — pass variations into Flag ctor; UpdateVariations on update; sanitize Key/Value in AnalyzeFlagsAsync | +27 |
| `Banderas.Infrastructure/Persistence/VariationListConverter.cs` | Created — ValueConverter with camelCase+enum-as-string; re-runs VO ctor on read | ~60 |
| `Banderas.Infrastructure/Persistence/FlagConfiguration.cs` | Modified — HasConversion(VariationListConverter) + HasBackingField; jsonb NOT NULL | +15 |
| `Banderas.Infrastructure/Migrations/20260522205830_AddFlagVariations.cs` | Created — additive migration; transient default + backfill + drop default | ~60 |
| `Banderas.Infrastructure/Migrations/20260522205830_AddFlagVariations.Designer.cs` | Created — EF snapshot | ~100 |
| `Banderas.Infrastructure/Migrations/BanderasDbContextModelSnapshot.cs` | Modified — Variations column in snapshot | auto |
| `Banderas.Infrastructure/AI/AiFlagAnalyzer.cs` | Modified — per-flag Variations block in BuildPrompt; inert-data system sentence | +32 |
| `Banderas.Infrastructure/Seeding/DatabaseSeeder.cs` | Modified — all seed flags declare explicit variations menus | +varies |
| `Banderas.Infrastructure/Banderas.Infrastructure.csproj` | Modified — no new package refs (reuses existing System.Text.Json) | +4 |
| `Banderas.Tests/Domain/ValueObjects/VariationTests.cs` | Created — single-element invariant coverage | ~180 |
| `Banderas.Tests/Domain/FlagVariationsTests.cs` | Created — collection invariants + UpdateVariations | ~120 |
| `Banderas.Tests/Application/FlagMappingsVariationsTests.cs` | Created — round-trip mapping + Kind parse | ~80 |
| `Banderas.Tests/Services/BanderasServiceVariationsTests.cs` | Created — create/update propagation | ~100 |
| `Banderas.Tests/Validators/VariationRequestValidatorTests.cs` | Created — all 7 invariants via FluentValidation | ~265 |
| `Banderas.Tests/Validators/ValidatorTestExtensions.cs` | Created — shared assertion helpers | ~40 |
| `Banderas.Tests.Integration/FlagCrudVariationsTests.cs` | Created — POST/PUT/GET shape, null/[]/populated paths | ~varies |
| `Banderas.Tests.Integration/AiHealthVariationsPromptTests.cs` | Created — sanitized Key/Value, Kind verbatim, inert sentence | ~varies |
| `Banderas.Tests.Integration/MigrationBackfillTests.cs` | Created — Down drops column; Up backfills menu | ~varies |
| `Banderas.Tests.Integration/VariationListConverterTests.cs` | Created — round-trip serialization, null-fallback | ~varies |
| `Banderas.Tests.Integration/ContractTests.cs` | Modified — variations field pinned across all 4 success response types | +varies |
| Various existing test files | Modified — supply required variations param; FlagBuilder updated | +varies |
| `Requests/smoke-test.http` | Modified — POST/PUT samples with variations; null/populated PUT variants | +73 |
| `Docs/Decisions/flag-variations/spec.md` | Committed — historical artifact | +659 |

---

## Risks and Follow-Ups

- **Index-as-wire-contract not yet enforced at infrastructure level** — the spec names
  the array index as the stable identity for env config rules. This is a deliberate Phase 5+
  concern. Until `FlagEnvironmentConfig` is introduced, there is no config-level reference
  to protect. No code change needed now; noted here for the aggregate split spec.

- **`Kind` homogeneity means menu evolution requires care** — once a flag has a `Boolean`
  menu, all variations must stay `Boolean`. Changing the kind requires a full atomic
  replacement with a new-kind menu. This is the intended design (DD-2 invariant 3),
  but operators should be aware via documentation when the SDK ships.

- **`VariationListConverter` null-fallback** — the converter returns an empty list if the
  JSON is null. This cannot happen via normal application writes (the column is NOT NULL),
  but could surface if a row is directly modified in the DB. The empty list would then fail
  the non-empty domain invariant on next write. The guard was added defensively; the correct
  fix if this occurs is a DB patch to restore a valid menu.

- **AI prompt size growth** — each flag with 20 variations at max value length (2000 chars
  each) adds ~40KB to the prompt. At current seed scale (6 flags × 2 variations) this is
  negligible. The Phase 4 `FlagQuery` record should eventually support a `IncludeVariations`
  flag to allow the health endpoint to omit variation detail when prompts grow large.

---

## How to Test

```bash
# Unit tests only
dotnet test Banderas.Tests/Banderas.Tests.csproj

# Integration tests only
dotnet test Banderas.Tests.Integration/Banderas.Tests.Integration.csproj --filter "Category=Integration"

# Variation-specific tests
dotnet test Banderas.Tests/Banderas.Tests.csproj --filter "FullyQualifiedName~Variation"
dotnet test Banderas.Tests.Integration/Banderas.Tests.Integration.csproj --filter "FullyQualifiedName~Variation"

# Migration backfill test
dotnet test Banderas.Tests.Integration/Banderas.Tests.Integration.csproj --filter "FullyQualifiedName~MigrationBackfill"
```

---

## Interview Lens

The central engineering decision was how to enforce the "all variations must be the same
Kind" invariant without a leaky domain model. The two candidates were: (a) validate at the
`Flag` level after receiving a heterogeneous list from the application layer, or (b) prevent
heterogeneous lists from ever reaching the domain by making `VariationMenuRules` reject
mixed-kind inputs in the validator. We do both: the validator rejects it at the HTTP
boundary with a descriptive field-level 400, and `EnsureVariationMenuIsValid` rejects it
at the domain level with a `FlagDomainException`. This is defense-in-depth — neither layer
trusts the other, which means seed data and internal service calls (e.g., future SDK) also
can't bypass the invariant.

The second key decision was the migration default strategy (DD-5 and DD-6). A permanent
`DEFAULT '[]'` would have been simpler but would allow the column to accept an empty array
from any path that bypasses the domain — including raw SQL inserts and any future seeding
logic that forgets to set variations. The transient default + immediate drop pattern makes
the database enforce "variations must be set explicitly by whoever writes the row," which
aligns the DB constraint with the domain invariant as closely as possible without adding a
CHECK constraint (which would require expressing the non-empty rule in SQL, duplicating the
domain logic in a less readable form).

---

## Foundation Docs Updated

- [x] `Docs/current-state.md` — status summary, test counts, domain/application/infrastructure
      detail, current focus, next tasks
- [x] `Docs/roadmap.md` — Phase 2 variations item checked; Current Focus updated
- [x] `Docs/architecture.md` — `Variation` VO and `VariationKind` enum added to Domain inventory
- [x] `Docs/Decisions/flag-ddd-analysis-backlog.md` — Variation bullet checked off
- [ ] `Docs/architecture.md` — no structural layer changes (no new layers or external dependencies)

---

## Definition of Done — Status

- [x] ✅ `dotnet build -p:TreatWarningsAsErrors=true` — 0 warnings, 0 errors
- [x] ✅ `dotnet csharpier check .` — 0 violations
- [x] ✅ `dotnet restore --locked-mode` — clean
- [x] ✅ `Variation` sealed record with single-element invariants enforced in ctor
- [x] ✅ `VariationKind` enum (Boolean | String | Number | Json)
- [x] ✅ `Flag.Variations` property + `EnsureVariationMenuIsValid` collection-level invariants
- [x] ✅ `Flag.UpdateVariations` — archived guard; atomic replacement; bumps UpdatedAt
- [x] ✅ `VariationRequest` / `VariationResponse` DTOs
- [x] ✅ `VariationMenuRules.ApplyMenuRules<T>` shared validator extension (7 invariants)
- [x] ✅ `CreateFlagRequest.Variations` required; `UpdateFlagRequest.Variations` nullable
- [x] ✅ `FlagResponse.Variations` always present
- [x] ✅ `VariationListConverter` with camelCase + enum-as-string; re-runs VO ctor on read
- [x] ✅ Migration `20260522205830_AddFlagVariations` — additive, zero-downtime, backfill + drop default
- [x] ✅ AI prompt emits sanitized per-flag Variations block; system prompt inert-data sentence
- [x] ✅ Seeder updated — all seed flags explicit; one demo flag with three-variation Number menu
- [x] ✅ 303 unit tests + 101 integration tests passing (404/404 green)
- [x] ✅ `Requests/smoke-test.http` — POST/PUT samples with default and non-default menus
- [x] ✅ All foundation docs updated
