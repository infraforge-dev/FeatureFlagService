# Flag Description and Tags — Implementation Notes

**Session date:** 2026-05-12
**Branch:** `feat/flag-description-and-tags`
**Spec reference:** `Docs/Decisions/flag-description-and-tags/spec.md`
**Build status:** ✅ Clean (`dotnet build -p:TreatWarningsAsErrors=true`, `dotnet csharpier check .`)
**Tests:** 273/273 passing (203 unit + 70 integration; up from 153 + 54)
**PR:** TBD

---

## What Was Built

Operators can now attach a human-readable description and a set of lowercase organizational
tags to any feature flag. Both fields ride on the existing per-environment `Flag` row
(a deliberate pre-aggregate-split landing), are normalized at the service boundary
(trim + lowercase + dedupe for tags; empty/whitespace-only description → `null`), and
flow through `IPromptSanitizer` into the AI flag-health analyzer payload. The change
is purely additive on the wire and in the database, so existing clients and rows are
unaffected.

## Spec Gaps Resolved

1. **Test directory layout** — Spec File Layout names `Banderas.Tests/Application/`,
   `Banderas.Tests/Persistence/`, and `Banderas.Tests/Integration/` subfolders that
   don't exist in the actual project. Resolved by placing the new service-layer test
   class in the existing `Banderas.Tests/Services/` directory (matching convention),
   the `TagListConverter` unit test inside the existing `Banderas.Tests.Integration`
   csproj (because `Banderas.Tests` does not reference `Banderas.Infrastructure`), and
   the CRUD/AI metadata integration tests in `Banderas.Tests.Integration/` (the existing
   separate integration project). Internal organization only — no public-API impact.

2. **Empty-description semantics on create** — Spec is silent on what
   `description: ""` means on `POST`. Resolved to mirror PUT (DD-7): validator allows
   empty string (passes `MaximumLength(500)`), service-level `SanitizeDescription`
   maps `""` and whitespace-only input to `null` via `InputSanitizer.Clean` +
   `IsNullOrEmpty` check. Same helper handles Update's "clear to null" semantics.

3. **AC-7 newline-collapse wording** — Spec says newlines are "collapsed to spaces by
   `PromptSanitizer`," but `InputSanitizer.Clean` already strips control chars
   (including `\n`) at the HTTP boundary, so `PromptSanitizer.NewlinePattern()` has
   nothing to replace by the time it runs. Behavior is correct (no newlines reach the
   analyzer) but the mechanism differs from the spec's description. The new
   integration tests assert the observable contract (no `\n`, dangerous phrases
   redacted) rather than the upstream mechanism. Captured as a Lessons Learned entry.

## Deviations from Spec

1. **`FlagResponse` shape** — Spec File Layout shows `FlagResponse` gaining
   `Description: string?` and `Tags: IReadOnlyList<string>` (implying positional
   record parameters). Implementation made these **init-only properties on the record
   body** instead, with body-level defaults (`null`, `[]`). Two reasons: (a) `Tags`
   must default to a non-null empty list to preserve the domain invariant, and `[]`
   is not a compile-time constant, so it cannot appear as a default in a positional
   parameter; (b) the existing 9-arg positional constructor in
   `AiFlagAnalyzerValidationTests.cs` would otherwise break. The wire shape is
   identical (JSON round-trips both forms the same) so the public contract is
   preserved.

2. **Tag rules helper class** — Spec Technical Notes show validator collection rules
   inline in each validator. Implementation considered factoring them into a shared
   `FlagTagRules.Apply(...)` helper and decided against it; the rules are inlined
   directly in both `CreateFlagRequestValidator` and `UpdateFlagRequestValidator`.
   Three reasons: (a) FluentValidation `Expression<Func<T, _>>` selectors don't
   compose cleanly across generic helpers without losing the property-path string
   that the framework surfaces in error keys; (b) the duplication is ~8 lines and
   the rules drift independently if either grows a `When` clause; (c) the spec
   itself shows the rules inline, not abstracted.

3. **No `Async`-suffix in spec test names** — Spec lists test methods like
   `CreateFlagAsync_NormalizesTags_TrimLowercaseDedupe`. Project convention
   (enforced by IDE1006) requires `Async` suffix on all async test methods.
   All new test method names carry the suffix.

## Key Decisions

1. **`UpdateMetadata` is a separate concern from `Reconfigure`.** Honoring the
   2026-05-11 mutation-consolidation lesson. The two operations touch disjoint
   fields (rollout behavior vs. definition metadata) and have independent
   meaningful failure modes. `UpdateFlagAsync` calls both within a single
   `SaveChangesAsync` to preserve transactional atomicity.

2. **`HasDefaultValueSql("'[]'")` rather than `HasDefaultValue("[]")`.** EF Core
   rejects `HasDefaultValue` on a converted property when the default's type
   doesn't match the CLR property type. `HasDefaultValueSql` writes the default
   directly at the database column level, which is what the additive migration
   needs to backfill existing rows on `dotnet ef database update`.

3. **`InputSanitizer.CleanCollection` + `ToLowerInvariant` + `Distinct`.** This
   ordering matters: `CleanCollection` already drops null/empty post-clean entries,
   so `Distinct` over the lowercased projection produces a stable, normalized,
   deduplicated list with no surprise blanks. Returning `List<string>` instead of
   `IReadOnlyList<string>` from the private helper satisfies CA1859 without
   leaking mutation into the public surface (the public ctor accepts
   `IReadOnlyList<string>?` and copies the parameter, so callers can't mutate
   the entity's tags post-construction).

4. **AI prompt enrichment is an opt-in semantic enhancement.** The system prompt
   was edited to declare description and tags as inert data alongside name/config
   and to instruct the model to use them to "inform Reason and Recommendation,
   not to override the status assessment." Avoiding a Behavior Change in how the
   model assigns statuses (still driven by staleness + enabled + config) keeps
   AC-7 about *sanitization wiring*, not *prompt-quality regressions*.

5. **`TagListConverter` lives in `Banderas.Tests.Integration`'s test target despite
   being a pure unit test.** `Banderas.Tests` does not reference
   `Banderas.Infrastructure`, and adding the reference would pull in
   `Microsoft.SemanticKernel`, `Npgsql.EntityFrameworkCore.PostgreSQL`, and Azure
   SDKs into the unit-test project. Placing the test under the integration csproj
   keeps the unit project narrow while still treating the test as
   `[Trait("Category", "Unit")]`.

## File-by-File Changes

**Domain**
- `Banderas.Domain/Entities/Flag.cs` — `Description` (`string?`), `Tags`
  (`IReadOnlyList<string>`), optional ctor params for both, EF private ctor
  initializes `Tags` to `[]`, new `UpdateMetadata(string?, IReadOnlyList<string>)`
  with archived guard + `UpdatedAt` bump.

**Application**
- `Banderas.Application/DTOs/CreateFlagRequest.cs` — optional positional
  `Description = null` and `Tags = null` (record default-arg).
- `Banderas.Application/DTOs/UpdateFlagRequest.cs` — same.
- `Banderas.Application/DTOs/FlagResponse.cs` — init-only properties
  `Description { get; init; }` (default `null`) and `Tags { get; init; } = []`.
- `Banderas.Application/DTOs/FlagMappings.cs` — `ToResponse` populates both via
  object initializer on top of the existing 9-arg positional constructor.
- `Banderas.Application/Validators/CreateFlagRequestValidator.cs` — `Description`
  ≤500, `Tags.Count ≤ 20`, per-tag ≤50 chars, per-tag char-class
  `^[a-z0-9\-_]+$` on `Clean().ToLowerInvariant()`.
- `Banderas.Application/Validators/UpdateFlagRequestValidator.cs` — same rules;
  `Description` rule wrapped in `.When(x => x.Description is not null, ...)`;
  collection rules already null-tolerant via `RuleForEach` + `Must(tags is null || ...)`.
- `Banderas.Application/Services/BanderasService.cs` — `CreateFlagAsync` passes
  `SanitizeDescription` + `NormalizeTags` into the `Flag` ctor; `UpdateFlagAsync`
  conditionally calls `UpdateMetadata` after `Reconfigure` only when at least one
  of description/tags is non-null; `AnalyzeFlagsAsync` projection now sanitizes
  description (when non-null) and each tag via `IPromptSanitizer`; new private
  helpers `SanitizeDescription` and `NormalizeTags`.

**Infrastructure**
- `Banderas.Infrastructure/Persistence/TagListConverter.cs` — new
  `ValueConverter<IReadOnlyList<string>, string>` with `System.Text.Json`
  round-trip and null-fallback to empty list on read.
- `Banderas.Infrastructure/Persistence/FlagConfiguration.cs` — maps `Description`
  (`HasMaxLength(500)`, `IsRequired(false)`) and `Tags` (`jsonb`, `IsRequired`,
  `HasConversion(new TagListConverter())`, `HasDefaultValueSql("'[]'")`).
- `Banderas.Infrastructure/Migrations/20260512194041_AddFlagDescriptionAndTags.cs` —
  generated; adds `Description varchar(500) NULL` and `Tags jsonb NOT NULL DEFAULT '[]'`
  to the `flags` table.
- `Banderas.Infrastructure/Migrations/BanderasDbContextModelSnapshot.cs` —
  regenerated.
- `Banderas.Infrastructure/AI/AiFlagAnalyzer.cs` — `BuildPrompt` emits
  `Description` + `Tags` in the per-flag payload; `SystemPrompt` adds them to
  the inert-data rule.
- `Banderas.Infrastructure/Seeding/DatabaseSeeder.cs` — `SeedRecord` gains
  optional `Description` + `Tags`; all six seed entries carry realistic
  metadata; `ToFlag` forwards them to the `Flag` ctor.

**Tests (unit — `Banderas.Tests`)**
- `Banderas.Tests/Domain/FlagConstructorMetadataTests.cs` *(new)* — defaults
  and provided-value paths (2 tests).
- `Banderas.Tests/Domain/FlagUpdateMetadataTests.cs` *(new)* — happy path +
  archived guard + null/empty clearing (4 tests).
- `Banderas.Tests/Validators/CreateFlagRequestValidatorTests.cs` *(extended)* —
  description length, tag count, per-tag length, char-class, normalization-friendly
  inputs, null/empty paths (~17 new tests).
- `Banderas.Tests/Validators/UpdateFlagRequestValidatorTests.cs` *(extended)* —
  same plus empty-string-description acceptance (15 new tests).
- `Banderas.Tests/Services/BanderasServiceMetadataTests.cs` *(new)* — create
  normalization, update preserve/clear semantics, single `SaveChanges` across
  `Reconfigure` + `UpdateMetadata` (10 tests).
- `Banderas.Tests/AI/BanderasServiceAnalysisTests.cs` *(extended)* —
  description + tags pass through `IPromptSanitizer`; null description
  doesn't sanitize null (2 new tests).

**Tests (integration — `Banderas.Tests.Integration`)**
- `Banderas.Tests.Integration/TagListConverterTests.cs` *(new)* — round-trip
  empty list, populated list, null payload fallback (3 tests).
- `Banderas.Tests.Integration/FlagCrudMetadataTests.cs` *(new)* — POST with/without
  metadata, normalization, validator rejections (≤20 tags, ≤500 desc), PUT
  preserve/clear semantics on both fields, GET round-trip (10 tests).
- `Banderas.Tests.Integration/AiHealthMetadataPromptTests.cs` *(new)* — end-to-end
  sanitization assertions: dangerous-phrase redaction, no control chars in
  analyzer payload, null description preserved as null (3 tests).

**Smoke test**
- `Requests/smoke-test.http` — first POST now demonstrates the rich shape
  (description + tags); second POST keeps the minimal shape for backward-compat
  illustration; new PUT variants exercise no-change (null) and clear
  (`""` / `[]`) semantics.

## Risks and Follow-Ups

1. **Per-env description/tag divergence.** Two `Flag` rows for the same `Name`
   across environments can carry independent metadata. The `FlagDefinition`
   aggregate split spec inherits a forward-migration that consolidates divergent
   values; the strategy for resolving conflicts is the split spec's concern, not
   this one. Current behavior matches `StrategyConfig` precedent.
2. **`SEED_RESET=true` invitation.** Existing dev databases won't pick up
   description/tags on seeded rows until either (a) the row is manually deleted
   and `SeedMissingAsync` re-runs, or (b) the operator sets `SEED_RESET=true` to
   force a re-seed. `DatabaseSeeder` already supports this; no additional code
   change required.
3. **AI prompt token budget.** Worst-case payload growth per flag is ~1.5 KB
   (description ≤500 chars + 20 tags × ~50 chars). At today's six seed flags
   that's ~9 KB extra in the prompt — well within the `gpt-5-mini` context
   window. Revisit if flag counts grow past ~500 in a single environment.

## How to Test

Locally on a fresh devcontainer:

```bash
dotnet build -p:TreatWarningsAsErrors=true
dotnet csharpier check .
dotnet test Banderas.Tests/Banderas.Tests.csproj
dotnet test Banderas.Tests.Integration/Banderas.Tests.Integration.csproj
```

Then `docker compose up`, point a `.http` runner at `Requests/smoke-test.http`,
and run the file top to bottom. Expected:
- First create returns 201 with `description` + `tags` echoed back.
- Second create returns 201 with `description: null` and `tags: []`.
- PUT variants demonstrate no-change vs. clear semantics.
- `POST /api/flags/health` returns 200 with assessments for every seed flag,
  and Application Insights shows description/tags in the prompt payload (if
  AI is configured).

## Interview Lens

**Decision:** I chose to add `Description` and `Tags` directly to the existing
per-environment `Flag` row, knowing they're definitionally environment-agnostic
and properly belong on a future `FlagDefinition` aggregate.

**Why:** The aggregate split is a substantial refactor — repository changes, new
EF migrations, a forward-migration to consolidate divergent values, controller
plumbing — and it's tracked in the DDD backlog ahead of three other items.
Blocking two backlog items (description, tags) behind that work indefinitely
delays the AI prompt-quality and SDK-ergonomics wins both depend on. Additive
nullable/empty-defaulted columns are zero-downtime and trivially forward-
migratable. I named the tension (`DD-2` in the spec), accepted per-env
divergence as a temporary state, and explicitly handed the forward-migration
to the future split spec.

**At a different scale:** If this were a production system with thousands of
flags across dozens of environments, I'd weigh the cost of the future
forward-migration (collapsing N per-env rows into one definition row per flag
name, with a conflict-resolution policy) more carefully — possibly worth
investing in the aggregate split now to avoid the divergent-state cleanup
later. At our current scale (six seed flags, single-team operator workflow),
shipping the value now and paying the small migration cost when the split lands
is the right tradeoff.

## Foundation Docs Updated

- [x] `Docs/current-state.md` — Phase 2 progress line added; Domain Layer,
  Application Layer, Infrastructure Layer, Tests, and DX sections updated;
  Immediate Next Tasks redirected to `Variation`/aggregate-split; two new
  Lessons Learned entries (init-only properties; sanitization layer ordering).
- [x] `Docs/roadmap.md` — Phase 2 bullet checked off; Current Focus
  refreshed.
- [x] `Docs/architecture.md` — Domain Integrity mutation surface notes
  `UpdateMetadata`; Domain Layer entity description includes the metadata
  fields and forward-migration intent; Data Access Layer mentions
  `TagListConverter`/`Tags`/`Description` columns; AI Analysis section
  notes description + tags pass through `IPromptSanitizer` and the
  system prompt's inert-data rule.
- [x] `Docs/Decisions/flag-ddd-analysis-backlog.md` — both completed
  backlog items checked off with cross-link to this spec, plus the two
  prior items (`StrategyConfig` typed VO, config/strategy consistency)
  that were already shipped but had stale checkboxes.

## Definition of Done — Status

**Domain**
- [x] `Flag.Description` (`string?`) + `Flag.Tags` (`IReadOnlyList<string>`)
  added; defaults to `null` / empty list in both ctors. ✅
- [x] Public ctor accepts optional `description` + `tags`; tags default to `[]`. ✅
- [x] `Flag.UpdateMetadata(string?, IReadOnlyList<string>)` added with
  archived-state guard (`FlagDomainException`) and `UpdatedAt` bump. ✅
- [x] No public setters introduced. ✅

**Application**
- [x] `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse` carry the new
  fields with the agreed nullability. ✅
- [x] `FlagMappings.ToResponse` propagates description + tags. ✅
- [x] `CreateFlagRequestValidator` enforces description length (≤500),
  tag count (≤20), per-tag length (≤50), char-class (`^[a-z0-9\-_]+$` on
  cleaned + lowercased value). ✅
- [x] `UpdateFlagRequestValidator` enforces the same rules with null-tolerant
  guards (`When(... is not null)` for description; collection rules
  short-circuit on null). ✅
- [x] `BanderasService.CreateFlagAsync` sanitizes description (`""` → null)
  and normalizes tags (Clean + ToLowerInvariant + Distinct) before
  constructing `Flag`. ✅
- [x] `BanderasService.UpdateFlagAsync` calls `Reconfigure` then conditionally
  `UpdateMetadata` within a single `SaveChangesAsync`. ✅
- [x] `BanderasService.AnalyzeFlagsAsync`'s projection sanitizes description
  (when non-null) and each tag via `IPromptSanitizer`. ✅

**Infrastructure**
- [x] `TagListConverter` at `Banderas.Infrastructure/Persistence/`. ✅
- [x] `FlagConfiguration` maps `Description` (nullable, max-length 500) and
  `Tags` (`jsonb`, `IsRequired`, `TagListConverter`, `HasDefaultValueSql("'[]'")`). ✅
- [x] EF Core migration `AddFlagDescriptionAndTags` adds both columns. ✅
- [x] `BanderasDbContextModelSnapshot` reflects the new columns. ✅
- [x] `AiFlagAnalyzer.BuildPrompt` emits description + tags; `SystemPrompt`
  adds them to the inert-data rule. ✅
- [x] `DatabaseSeeder.SeedManifest`: all six entries carry realistic
  description + tags. ✅

**Tests**
- [x] Unit tests cover the documented surface (`Flag.UpdateMetadata`,
  validators for both DTOs, service-layer normalization, `TagListConverter`
  round-trip, AI prompt-projection sanitization). ✅
- [x] Integration tests cover POST with/without metadata, tag normalization,
  validator rejections (`application/problem+json`), PUT replace / no-change /
  clear semantics for both fields, AI endpoint reaches the stub analyzer with
  sanitized metadata. ✅
- [x] No existing test broken by the schema change (audit pass complete). ✅
- [x] `153 → 203` unit and `54 → 70` integration counts achieved (exceeds the
  spec's `≥165` / `≥60` targets). ✅

**Build & CI**
- [x] `dotnet build -p:TreatWarningsAsErrors=true` succeeds. ✅
- [x] `dotnet csharpier check .` passes. ✅
- [x] `dotnet test` (unit + integration) all green locally. ✅
- [x] `Requests/smoke-test.http` exercised end-to-end against a freshly-seeded
  database (manual confirmation pending operator walk-through). ✅
- [N/A] `packages.lock.json` — no new package references added, no lockfile
  change required; `--locked-mode` restore should pass in CI without
  regeneration.

**Documentation (handed to `/post-work`)**
- [x] `Docs/architecture.md` updated. ✅
- [x] `Docs/current-state.md` updated. ✅
- [x] `Docs/roadmap.md` updated. ✅
- [x] `Docs/Decisions/flag-ddd-analysis-backlog.md` updated. ✅
