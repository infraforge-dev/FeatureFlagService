# Specification: Flag Description and Tags

**Document:** Docs/Decisions/flag-description-and-tags/spec.md
**Status:** Draft
**Branch:** `feat/flag-description-and-tags`
**PR:** TBD
**Phase:** Phase 2 — Testing & Reliability (DDD-backlog increment)
**Depends on:** None (sits on top of consolidated `Flag` mutation surface; PR #58 concurrency token; typed `StrategyConfig` VO)
**Author:** Developer
**Date:** 2026-05-12

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

> As a feature-flag operator, I want to attach a human-readable **description** and a
> set of organizational **tags** to a flag, so that my team can understand a flag's
> purpose at a glance and group related flags (by squad, by release, by experiment)
> without having to decode the flag name alone.

Secondary beneficiaries:

- **The AI flag-health endpoint** (`POST /api/flags/health`) gets operator-authored
  intent in its prompt context — assessments can reason about *purpose*, not just
  *name + strategy*.
- **Future SDK and UI consumers** (Phase 7) get the metadata fields they need to
  render meaningful flag listings instead of opaque name columns.

---

## Background and Goals

The `Flag.cs` DDD analysis (`Docs/Decisions/flag-ddd-analysis-backlog.md`, 2026-04-30)
identified two **environment-agnostic metadata** capabilities as the next missing
piece on `Flag` once typed `StrategyConfig` and the `Reconfigure` mutation
consolidation landed:

- *"Add `Description` to `Flag` definition — environment-agnostic metadata"*
- *"Add `Tags` collection to `Flag` definition — environment-agnostic organizational labels"*

Today the only identity a flag carries is the `(Name, Environment)` pair. There is
nowhere on the entity to record:

- *Why does this flag exist?* — intent (description)
- *Who owns it / what release is it gated on?* — grouping (tags)

The motivation for landing these now, ahead of the larger `Flag` → `FlagDefinition` /
`FlagEnvironmentConfig` aggregate split:

1. **Closes two DDD-backlog items** with a single, low-risk additive change.
2. **Strengthens AI flag-health analysis quality** — operator-authored intent is the
   highest-leverage prompt signal the model is currently missing.
3. **Unblocks Phase 7 SDK ergonomics** — the SDK contract for "list flags I can
   reason about" needs description + tags on `FlagResponse`.
4. **Forward-compatible** — when the `FlagDefinition` aggregate eventually lands,
   these columns migrate to the new aggregate (covered as a known follow-up; see
   [Out of Scope](#out-of-scope)).

**Non-goal:** This spec does not split the `Flag` aggregate. Description and Tags
land on the existing per-environment `Flag` row. That tension is named, accepted,
and forwarded to the future aggregate-split spec.

---

## Design Decisions

### DD-1 — Bundle Description + Tags into a single spec

**Decision.** One spec, one branch, one PR, one EF migration covering both fields.

**Tradeoff.** Splitting into `spec-01-description` + `spec-02-tags` would let two
agents tackle them in parallel, but the surface they touch — DTOs, validators,
mappings, EF config, repository writes, seed data, smoke test, AI prompt, tests —
is ~80% identical. Splitting doubles ceremony for no isolation benefit. A single
PR with both fields keeps the diff coherent and the migration count to one.

**Why not split.** The two fields don't have distinct phase boundaries. They land
together in user-visible value (operator sees both on the same screen) and together
on the wire (`FlagResponse`). Splitting would also create a transient state in which
half the metadata story is implemented — exactly the kind of intermediate state the
phased-spec discipline is meant to avoid.

---

### DD-2 — Land on per-environment `Flag` now, migrate to `FlagDefinition` later

**Decision.** Description and Tags are added as columns on the existing `flags`
table. Each environment's `Flag` row carries its own description/tags independently.

**Tradeoff.** The DDD analysis frames Description and Tags as *definition-level*
(environment-agnostic) metadata. Honest definition-level semantics require the
`Flag` → `FlagDefinition` / `FlagEnvironmentConfig` aggregate split, which is a
much larger refactor that is sequenced later in the backlog.

**What we accept temporarily.** Two `Flag` rows for the same `Name` across different
`Environment`s can carry divergent description/tags. The operator workflow will
treat this as "set description/tags on each env row independently," same as
`StrategyConfig` today. When the aggregate split lands, a forward-migration will
move these columns to the new `FlagDefinition` aggregate and consolidate any
divergent values (the operator-facing migration strategy is the split spec's
concern, not this one).

**Why not defer until after the aggregate split.** The split is a substantial
refactor with its own risk surface. Blocking these two backlog items behind it
delays AI-prompt-quality and SDK-ergonomics wins by an indefinite amount. Adding
nullable/empty-defaulted columns now is zero-downtime, zero-data-loss, and
trivially forward-migratable.

**Why not "enforce parity across envs" as a service-level rule.** Cross-environment
coordination on every write approximates an aggregate boundary without actually
being one — it leaks the aggregate-split decision into the service layer where it
will rot until the real split happens. Worse, it adds a class of consistency
failures (one env's write succeeds, another fails partway). Better to be honest
about the per-env divergence now and fix it structurally later.

---

### DD-3 — Tags stored as `jsonb` on the `flags` table

**Decision.** `Tags` persists as a `jsonb` column (`List<string>` ↔ JSON array)
via an EF Core `ValueConverter` on the existing `flags` table.

**Tradeoff.** Three persistence shapes were considered:

| Shape | Pros | Cons |
|---|---|---|
| **`jsonb` column (chosen)** | Single-row read; matches the `StrategyConfig` precedent; trivial migration; GIN-indexable later; zero new repository surface | Tag-based queries require `jsonb` operators (not needed in Phase 2) |
| Postgres `text[]` array | Idiomatic Postgres; GIN-indexable out of the box | Introduces a new persistence pattern (we don't use array columns anywhere else); slight EF Core convention differences; less symmetric with `StrategyConfig` |
| `flag_tags` join table | Strong relational querying; "find flags with tag X" is trivial | Every flag read becomes a join; breaks the "one row per flag" invariant the rest of the code assumes; new repository methods; bigger migration |

**Why `jsonb`.** The codebase already proves the pattern with `StrategyConfigConverter`
+ `HasColumnType("jsonb")` in `FlagConfiguration`. Adopting it for `Tags` keeps
infrastructure conventions consistent and lets future tag-querying work add a GIN
index without a schema change.

---

### DD-4 — Tag normalization: trim + control-char strip + lowercase + dedupe

**Decision.** Every incoming tag passes through `InputSanitizer.Clean` (trim +
strip ASCII control chars), is then `ToLowerInvariant()`-folded, then de-duplicated
into a stable order before being written. `"Checkout"`, `" checkout "`, and
`"CHECKOUT"` all collapse to a single stored value `"checkout"`.

**Tradeoff.** Case-preservation would be prettier in a UI but creates silent
taxonomy fragmentation: `Checkout` and `checkout` would coexist as separate tags
indefinitely. Phase 2 priorities (reliability, predictability) outweigh
display-case fidelity, and the validator's char-class regex (`^[a-z0-9\-_]+$`)
already implies a lowercase contract.

**Consistency with existing patterns.** `RoleStrategy` already case-folds role
inputs for the same reason. The two-point sanitization pattern — validator runs on
*cleaned* values for `Must` checks, service does the *actual* mutation before
persistence — matches how `Name` is handled today.

---

### DD-5 — Field limits: Tags ≤ 20 × 50 chars, Description ≤ 500 chars

**Decision.** The validator enforces:

| Field | Limit | Rationale |
|---|---|---|
| `Description` | 500 characters, nullable | One or two meaningful sentences. Discourages prose docs in the flag store. Nullable so existing rows + creates without it remain valid |
| `Tags` count | 20 entries max | Comfortably handles squad + release + experiment + sprint + owner groupings without enabling tag spam |
| `Tags[i]` length | 50 characters max | Stops "long-sentence-as-a-tag" misuse |
| `Tags[i]` char class | `^[a-z0-9\-_]+$` (post-lowercase) | Matches `Flag.Name`'s allowlist minus uppercase (since we lowercase) |

**Tradeoff.** Stricter limits (10 tags / 30 chars / 250-char description) would
force concision but cramp legitimate use. More generous limits (matching
`StrategyConfig`'s 2000-char ceiling) would bloat `FlagResponse` and the AI prompt
context window — Description in particular gets embedded in every health-analysis
prompt, so it pays per character in token cost.

**Length checks run on raw input.** As with `Name`, `MaximumLength` validates the
raw value so a 51-char input fails even if cleaning would have trimmed it to 50.
Char-class regex runs on the cleaned, lowercased projection so padded/mixed-case
input that the service will normalize is accepted upstream.

---

### DD-6 — New `Flag.UpdateMetadata(description, tags)` mutation, distinct from `Reconfigure`

**Decision.** A third concern-named domain mutation joins `UpdateName` and
`Reconfigure`:

```csharp
public void UpdateMetadata(string? description, IReadOnlyList<string> tags)
```

Guards (archived → throws `FlagDomainException`), bumps `UpdatedAt`. Constructor
gains optional `description` + `tags` parameters for create-time initialization.

**Tradeoff.** Three alternatives were considered:

- **Extending `Reconfigure` to take description/tags** — Collapses two distinct
  concerns (rollout *behavior* vs definition *metadata*) back into one method.
  Directly contradicts the 2026-05-11 mutation-consolidation lesson, which warned
  against field-shaped god-methods that invite partial-update bugs.
- **Renaming `UpdateName` → `UpdateDefinition(name, description, tags)`** — Treats
  name + description + tags as one "definition" concern. Stronger DDD framing but
  forces every rename to restate all three fields — exactly the partial-update
  trap `Reconfigure` was designed to avoid.
- **`UpdateMetadata` as a separate method (chosen)** — Honors "name mutations by
  concern, not by field." `UpdateMetadata` is one concern: *operator-authored
  metadata for the flag's identity*. `Reconfigure` is another: *rollout behavior*.
  `UpdateName` is a third: *the flag's wire identity*. Each method is atomic in
  what it represents; the surface stays narrow.

---

### DD-7 — PUT replaces tags wholesale; null on either field means "no change"

**Decision.** PUT `/api/flags/{name}` semantics for the new fields:

| DTO field value | Server interpretation |
|---|---|
| `"description": "..."` | Replace description |
| `"description": null` (or omitted) | No change to existing description |
| `"description": ""` | Replace with `null` (clears the description) |
| `"tags": ["a", "b"]` | Replace tag set entirely |
| `"tags": null` (or omitted) | No change to existing tags |
| `"tags": []` | Replace with empty (clears all tags) |

**Tradeoff.** "Always replace; null and `[]` both mean clear" is stricter but
forces every client that doesn't care about tags to send the existing list back on
every PUT to avoid wiping them. PATCH-style merge semantics on a PUT verb violate
REST expectations and add endpoint surface. The chosen rule lets clients adopt the
new fields incrementally — existing clients that send the current PUT payload
shape are unaffected.

---

### DD-8 — PUT service plumbing: two domain mutations, one `SaveChanges`

**Decision.** `BanderasService.UpdateFlagAsync` performs the rollout reconfiguration
and the metadata update as two distinct domain calls, then a single
`SaveChangesAsync`:

```csharp
flag.Reconfigure(request.IsEnabled, request.StrategyType, strategyConfig);

if (request.Description is not null || request.Tags is not null)
{
    flag.UpdateMetadata(
        description: request.Description is not null
            ? Sanitize(request.Description)
            : flag.Description,
        tags: request.Tags is not null
            ? Normalize(request.Tags)
            : flag.Tags
    );
}

await _repository.SaveChangesAsync(ct);
```

**Tradeoff.** Atomicity at the DB layer (single transaction, single concurrency
token check, single audit boundary) is preserved. `UpdatedAt` may be set twice on
the entity in-memory; the final value reflects the latest mutation, which is what
callers expect. A "god-method wrapper" `Flag.UpdateAll(...)` was rejected for the
same reason as DD-6 — it reintroduces the field-shaped surface the recent
consolidation removed.

---

### DD-9 — AI flag-health prompt embeds Description + Tags, sanitized via `IPromptSanitizer`

**Decision.** `AiFlagAnalyzer.BuildPrompt` adds two fields to the per-flag payload
sent to the model:

```csharp
flags.Select(f => new
{
    f.Name,
    f.Description,        // new
    f.Tags,               // new
    f.IsEnabled,
    f.Environment,
    f.StrategyType,
    f.StrategyConfig,
    f.CreatedAt,
    f.UpdatedAt,
})
```

`BanderasService.AnalyzeFlagsAsync` extends its `with` projection so
`PromptSanitizer.Sanitize` is applied to `Description` (when non-null) and to each
`Tag` before the payload leaves the service-layer prompt-safety boundary.

**Tradeoff.** Tags are short, structured labels and carry low prompt-injection
risk on their own; Description is free-text and carries higher risk per character.
Both pass through the existing `PromptSanitizer` (newline normalization, dangerous-
phrase redaction, 500-char cap) which is the right enforcement point. Deferring
either field would create an asymmetry: operators would reasonably expect the AI
to use what they wrote, and "the AI ignores half the metadata we just shipped" is
a bad first impression.

The system prompt is updated to add description/tags to the list of inert data
the model must not interpret as instructions.

---

### DD-10 — Existing seed data gets realistic Description + Tags; smoke-test exercises both

**Decision.** All six seed flags in `DatabaseSeeder.SeedManifest` get a one-sentence
description and 2–3 representative tags. `Requests/smoke-test.http`'s POST and PUT
samples include the new fields; one variant deliberately omits them to demonstrate
backward compatibility.

**Tradeoff.** Updating seed data adds churn (`SeedRecord` gains two fields, all
six rows updated, plus a `SEED_RESET=true` invitation in the deployment notes for
the rare existing dev environment). The payoff is the demo-ready experience
emphasized in the product vision: `docker compose up` immediately shows the feature
in action; the AI health endpoint receives meaningful operator intent on day one.

---

## Architecture Overview

This is an **additive change to the existing layer cake**. No new components,
no new layer boundaries, no new aggregates (despite the DDD framing — see DD-2).

The pattern mirrors `StrategyConfig`'s end-to-end shape:

```
HTTP boundary           [ FluentValidation on Description + Tags rules ]
                                          ↓
DTOs                    CreateFlagRequest / UpdateFlagRequest / FlagResponse
                                          ↓
Service                 BanderasService.{Create,Update}FlagAsync
                          ├─ InputSanitizer.Clean(description) when non-null
                          ├─ Normalize(tags): Clean + ToLowerInvariant + Distinct
                          └─ Flag construction or Flag.UpdateMetadata(...)
                                          ↓
Domain                  Flag.Description (string?), Flag.Tags (IReadOnlyList<string>)
                          ├─ Ctor: optional description + tags
                          └─ UpdateMetadata: archived guard, UpdatedAt bump
                                          ↓
Persistence             FlagConfiguration: Description column,
                          Tags as jsonb via TagListConverter
                                          ↓
Migration               AddFlagDescriptionAndTags — nullable Description,
                          NOT NULL Tags with default '[]'

AI side path            BanderasService.AnalyzeFlagsAsync extends sanitization
                          → AiFlagAnalyzer.BuildPrompt includes Description + Tags
```

No Mermaid diagram is warranted — the diagram in `Docs/architecture.md` remains
accurate; this change adds two properties to the existing `Flag` entity and one
column to the existing table.

---

## Scope

**New code:**

- `Banderas.Infrastructure/Persistence/TagListConverter.cs` — EF Core
  `ValueConverter<IReadOnlyList<string>, string>` for `Tags` ↔ JSON array
- `Banderas.Infrastructure/Migrations/<timestamp>_AddFlagDescriptionAndTags.cs`
  (+ `.Designer.cs` + `BanderasDbContextModelSnapshot.cs` regeneration)

**Modified code (Domain):**

- `Banderas.Domain/Entities/Flag.cs` — add `Description` (`string?`) and `Tags`
  (`IReadOnlyList<string>`); extend public ctor; new `UpdateMetadata` method;
  initialize `Tags` to empty list in the EF Core private ctor

**Modified code (Application):**

- `Banderas.Application/DTOs/CreateFlagRequest.cs` — add `string? Description` and
  `IReadOnlyList<string>? Tags`
- `Banderas.Application/DTOs/UpdateFlagRequest.cs` — add `string? Description` and
  `IReadOnlyList<string>? Tags`
- `Banderas.Application/DTOs/FlagResponse.cs` — add `string? Description` and
  `IReadOnlyList<string> Tags`
- `Banderas.Application/DTOs/FlagMappings.cs` — propagate the new fields
- `Banderas.Application/Validators/CreateFlagRequestValidator.cs` — `Description`
  rule (≤500), `Tags` rules (count, per-entry length, char-class on cleaned+lower)
- `Banderas.Application/Validators/UpdateFlagRequestValidator.cs` — same rules,
  guarded by null-tolerant `When(...)` clauses (null = "no change")
- `Banderas.Application/Services/BanderasService.cs` — `CreateFlagAsync` passes
  sanitized description + normalized tags to `Flag` ctor;
  `UpdateFlagAsync` invokes `Flag.UpdateMetadata` conditionally after
  `Reconfigure`; `AnalyzeFlagsAsync` extends its `with` projection to sanitize
  the new fields

**Modified code (Infrastructure):**

- `Banderas.Infrastructure/Persistence/FlagConfiguration.cs` — `Description`
  column (nullable, ≤500), `Tags` as `jsonb NOT NULL` via `TagListConverter`
  with a sensible default
- `Banderas.Infrastructure/AI/AiFlagAnalyzer.cs` — `BuildPrompt` includes the new
  fields; `SystemPrompt` adds description/tags to the "inert data" rule
- `Banderas.Infrastructure/Seeding/DatabaseSeeder.cs` — `SeedRecord` gains
  `Description` + `Tags`; all six seed entries updated

**Tests (Banderas.Tests — unit):**

- `Domain/FlagUpdateMetadataTests.cs` — new file: archived-guard throws,
  `UpdatedAt` bump, tag-list replacement, null-description clears
- `Domain/FlagConstructorTests.cs` (existing or new) — defaults: tags default to
  empty when not supplied, description nullable
- `Validators/CreateFlagRequestValidatorTests.cs` — extend: description length,
  tag count, per-tag length, char-class, normalization-friendly inputs (padded,
  mixed case)
- `Validators/UpdateFlagRequestValidatorTests.cs` — extend with the same rules,
  plus null-tolerance assertions
- `Application/BanderasServiceMetadataTests.cs` — new file: create normalizes
  tags (lowercase + dedupe + trim); update with `tags: null` preserves existing
  tags; update with `tags: []` clears tags; description sanitization wired
- `Persistence/TagListConverterTests.cs` — new file: round-trip empty list,
  round-trip with entries, null-write protection
- `AI/AiFlagAnalyzerPromptTests.cs` (or extend
  `BanderasServiceAnalysisSanitizationTests`) — assert description + tags pass
  through `PromptSanitizer` before reaching the analyzer

**Tests (Banderas.Tests — integration):**

- `Integration/FlagCrudMetadataTests.cs` — new file:
  - POST with description + tags returns 201 with both fields on the response
  - POST without metadata returns 201 with `description: null` and `tags: []`
  - POST with 21 tags returns 400 `ValidationProblemDetails`
  - POST with a tag containing uppercase + spaces returns 201 with normalized form
  - PUT with `tags: null` preserves existing tags
  - PUT with `tags: []` clears tags
  - PUT with `description: ""` clears description
  - GET returns the persisted, normalized metadata
- `Integration/AiHealthMetadataPromptTests.cs` (or extend existing AI integration
  tests) — assert the stub analyzer receives sanitized description + tags on each
  flag

**Tests (existing — touched by the schema change):**

- Any test that constructs `Flag` directly or seeds via `BanderasDbContext` is
  audited for null-`Tags` construction (the EF Core ctor must initialize tags to
  empty); follows the lesson from 2026-05-07 ("after introducing a typed VO,
  audit all test files for null-construction patterns").
- `StubAiFlagAnalyzer` (in `BanderasApiFactory`) and any tests that compare
  prompt JSON snapshots are updated to expect the new fields.

**Smoke test:**

- `Requests/smoke-test.http` — POST and PUT samples include description + tags;
  one variant deliberately omits both fields to demonstrate backward compatibility

**Documentation (updated as part of `/post-work`, not this PR):**

- `Docs/architecture.md` — Domain Layer bullet for `Flag` updated; `Flag` mutation
  surface lists `UpdateMetadata`; AI-prompt section notes new fields
- `Docs/current-state.md` — Phase 2 progress entry; new DoD checkmarks
- `Docs/roadmap.md` — Phase 2 progress note: "Description + Tags landed on
  per-env Flag; aggregate-split spec inherits the migration"
- `Docs/Decisions/flag-ddd-analysis-backlog.md` — check off the two completed
  backlog items; cross-link to this spec

---

## Acceptance Criteria

### AC-1 — Create a flag with description and tags

**Given** a valid `CreateFlagRequest` with `description: "Checkout v2 experiment"`
and `tags: ["squad-checkout", "release-q2"]`

**When** the client `POST`s to `/api/flags`

**Then** the response is `201 Created` with a `FlagResponse` body containing
`description: "Checkout v2 experiment"` and `tags: ["squad-checkout", "release-q2"]`,
and the persisted row carries the same normalized values.

---

### AC-2 — Create without metadata is backward compatible

**Given** a `CreateFlagRequest` that omits `description` and `tags`

**When** the client `POST`s to `/api/flags`

**Then** the response is `201 Created` with `description: null` and `tags: []`;
existing clients see no behavioral change to other fields.

---

### AC-3 — Tag normalization: trim + lowercase + dedupe

**Given** `tags: ["Checkout", " checkout ", "CHECKOUT", "Release-Q2"]`

**When** the request is accepted

**Then** the persisted/returned tags equal `["checkout", "release-q2"]` (order
stable across reads).

---

### AC-4 — Validator rejects oversized inputs

| Input | Expected response |
|---|---|
| `description` length 501 | `400 ValidationProblemDetails` citing "Description must not exceed 500 characters" |
| `tags` count = 21 | `400` citing "Tags may not contain more than 20 entries" |
| `tags` element length 51 | `400` citing per-tag length |
| `tags` element `"Bad Tag!"` (after lowercase: `"bad tag!"` — fails char-class) | `400` citing the allowed characters |

All `400` responses use `application/problem+json`.

---

### AC-5 — Update PUT replaces tags wholesale, omitted means no change

**Given** a flag persisted with `tags: ["a", "b"]` and `description: "old"`

| PUT body fragment | Resulting state |
|---|---|
| `"tags": ["c"]`, `"description": "new"` | tags = `["c"]`, description = `"new"` |
| `"tags": null`, `"description": null` | tags = `["a", "b"]`, description = `"old"` (no change) |
| `"tags": []`, `"description": ""` | tags = `[]`, description = `null` |

`UpdatedAt` advances on any change; the concurrency token check (PR #58 token)
still runs once for the PUT.

---

### AC-6 — Archived flag rejects metadata mutation

**Given** an archived flag

**When** any mutation that would invoke `Flag.UpdateMetadata` runs

**Then** `FlagDomainException` is thrown and the middleware maps it to
`409 Conflict` ProblemDetails — consistent with the existing
`Reconfigure`/`UpdateName`/`Archive` archived-state contract.

---

### AC-7 — AI flag-health prompt embeds sanitized description and tags

**Given** a flag with `description: "Controls checkout v2.\nOwner: payments-squad"`
and `tags: ["squad-checkout", "release-q2"]`

**When** `POST /api/flags/health` runs

**Then** the prompt the analyzer receives contains the description (with newlines
collapsed to spaces by `PromptSanitizer`) and each tag passed through
`PromptSanitizer.Sanitize`. Test asserts the projected payload, not the model
response. Documented dangerous phrases (`"ignore previous"`, `"system:"`, etc.)
appearing in description or any tag are replaced with `[REDACTED]`.

---

### AC-8 — Description supports null-and-empty distinction

| Stored description | DTO field | Behavior |
|---|---|---|
| `null` | (any) | `FlagResponse.Description = null` |
| `"text"` | PUT `description: null` | unchanged: `"text"` |
| `"text"` | PUT `description: ""` | cleared: `null` |
| `"  text with  internal spaces  "` | POST | persisted as `"text with  internal spaces"` (trim, internal spaces preserved) |

---

### AC-9 — Seed data exposes the feature on `docker compose up`

**Given** a freshly-seeded database

**When** `GET /api/flags?environment=Development` runs

**Then** every seed flag returns a non-null description and a non-empty tag list;
`POST /api/flags/health` returns an analysis that references the seeded
descriptions/tags (manual demo confirmation, not a CI assertion).

---

### AC-10 — Test suite gates remain green

`dotnet build -p:TreatWarningsAsErrors=true` succeeds; CSharpier check passes;
all unit tests pass; all integration tests pass; new tests bring the unit count
to ≥165 and integration count to ≥60 (the exact numbers depend on how the new
tests are organized — the gate is "all green and additions are non-trivial").

---

## File Layout

```
Banderas.Domain/
└── Entities/
    └── Flag.cs                                          [MODIFIED]

Banderas.Application/
├── DTOs/
│   ├── CreateFlagRequest.cs                             [MODIFIED]
│   ├── UpdateFlagRequest.cs                             [MODIFIED]
│   ├── FlagResponse.cs                                  [MODIFIED]
│   └── FlagMappings.cs                                  [MODIFIED]
├── Services/
│   └── BanderasService.cs                               [MODIFIED]
└── Validators/
    ├── CreateFlagRequestValidator.cs                    [MODIFIED]
    └── UpdateFlagRequestValidator.cs                    [MODIFIED]

Banderas.Infrastructure/
├── Persistence/
│   ├── FlagConfiguration.cs                             [MODIFIED]
│   └── TagListConverter.cs                              [NEW]
├── Migrations/
│   ├── <timestamp>_AddFlagDescriptionAndTags.cs         [NEW]
│   ├── <timestamp>_AddFlagDescriptionAndTags.Designer.cs[NEW]
│   └── BanderasDbContextModelSnapshot.cs                [MODIFIED]
├── AI/
│   └── AiFlagAnalyzer.cs                                [MODIFIED]
└── Seeding/
    └── DatabaseSeeder.cs                                [MODIFIED]

Banderas.Tests/
├── Domain/
│   └── FlagUpdateMetadataTests.cs                       [NEW]
├── Validators/
│   ├── CreateFlagRequestValidatorTests.cs               [MODIFIED]
│   └── UpdateFlagRequestValidatorTests.cs               [MODIFIED]
├── Application/
│   └── BanderasServiceMetadataTests.cs                  [NEW]
├── Persistence/
│   └── TagListConverterTests.cs                         [NEW]
├── AI/
│   └── AiFlagAnalyzerPromptTests.cs                     [NEW or MODIFIED]
└── Integration/
    ├── FlagCrudMetadataTests.cs                         [NEW]
    └── BanderasApiFactory.cs                            [MODIFIED if StubAi assertions extend]

Requests/
└── smoke-test.http                                      [MODIFIED]

Docs/Decisions/flag-description-and-tags/
└── spec.md                                              [THIS FILE]

Docs/ (touched by /post-work, not in this PR)
├── architecture.md
├── current-state.md
├── roadmap.md
└── Decisions/flag-ddd-analysis-backlog.md
```

---

## Technical Notes

### EF Core conversion shape for `Tags`

`TagListConverter` mirrors `StrategyConfigConverter`:

```csharp
public sealed class TagListConverter
    : ValueConverter<IReadOnlyList<string>, string>
{
    private static readonly JsonSerializerOptions Options = new();

    public TagListConverter()
        : base(
            tags => JsonSerializer.Serialize(tags, Options),
            json => JsonSerializer.Deserialize<List<string>>(json, Options)
                ?? new List<string>()
        )
    { }
}
```

`FlagConfiguration` registers the converter, sets the column type to `jsonb`, marks
the column `IsRequired()`, and provides a default value of `"[]"` for the EF Core
model — the migration emits that same default so existing rows are populated
non-null on apply.

### Migration content (sketch)

```csharp
migrationBuilder.AddColumn<string>(
    name: "Description",
    table: "flags",
    type: "text",  // or character varying(500)
    maxLength: 500,
    nullable: true);

migrationBuilder.AddColumn<string>(
    name: "Tags",
    table: "flags",
    type: "jsonb",
    nullable: false,
    defaultValue: "[]");
```

Existing rows pick up `Description = NULL` and `Tags = '[]'` on apply. Zero-downtime,
no backfill required. The migration is forward-only — `Down` is generated but
unused per project convention.

### Validator collection-rule layout

FluentValidation's collection rules use `RuleForEach` for per-element checks and
`RuleFor(x => x.Tags)` for collection-level rules:

```csharp
RuleFor(x => x.Tags)
    .Must(tags => tags is null || tags.Count <= 20)
    .WithMessage("Tags may not contain more than 20 entries.");

RuleForEach(x => x.Tags)
    .MaximumLength(50)
    .WithMessage("Each tag must not exceed 50 characters.")
    .Must(tag =>
        System.Text.RegularExpressions.Regex.IsMatch(
            (InputSanitizer.Clean(tag) ?? string.Empty).ToLowerInvariant(),
            @"^[a-z0-9\-_]+$"
        )
    )
    .WithMessage("Tags may only contain lowercase letters, numbers, hyphens, and underscores.");
```

For `UpdateFlagRequestValidator`, wrap rules in
`When(x => x.Tags is not null, ...)` and `When(x => x.Description is not null, ...)`
so a null payload skips validation (matches DD-7 "null = no change").

### `InputSanitizer.CleanCollection` already exists

The helper at `Banderas.Application/Validators/InputSanitizer.cs:34` returns
`IEnumerable<string>` after `Clean`-ing each entry and dropping nulls/empties.
Tag normalization in `BanderasService` composes it with `.Select(t => t.ToLowerInvariant()).Distinct().ToList()`.

### Backward compatibility on the wire

`FlagResponse` gains two non-breaking fields (`Description: string?`,
`Tags: IReadOnlyList<string>`). Existing JSON consumers ignore unknown fields by
default and the new fields appear in every response. Request DTOs accept the new
fields as optional (`string?` and `IReadOnlyList<string>?`), so existing client
payloads continue to deserialize unchanged.

### NuGet lockfile

The change uses only packages already referenced by the solution
(`FluentValidation`, `System.Text.Json`, EF Core). No `packages.lock.json` update
is required, but the CI's `--locked-mode` restore should still pass — verify
locally before pushing.

### AI prompt token budget

Adding Description (≤500 chars per flag) and Tags (≤20 × 50 chars = 1000 chars per
flag) increases the prompt payload by up to ~1.5KB per flag in the worst case.
At six seed flags that's ~9KB headroom, well within the model's context window.
No batching changes required.

### Reference: prior lessons that apply

- **2026-05-11 — "Name mutation methods after the concern, not the fields."**
  Directly informs DD-6. `UpdateMetadata` is named for *what* it represents
  (operator-authored definition metadata), not for *which fields* it touches.
- **2026-05-07 — "EF Core Value Converters cannot access sibling properties."**
  Does not apply here — `TagListConverter` doesn't depend on any sibling property.
- **2026-05-07 — "Audit all test files for null-construction patterns."** Applies
  on this PR: any test that builds `Flag` directly must initialize `Tags` to a
  non-null empty list (the EF Core ctor handles its own initialization; public
  ctor defaults the parameter to `[]`).
- **2026-05-05 — "Test what clients see, not what guards throw."** Applies to
  the archived-flag metadata mutation: the 404 from the repository filter is the
  client-visible behavior on a `DELETE`d flag's subsequent PUT; the 409 from
  `Flag.UpdateMetadata`'s archived guard is covered via a domain-level unit test
  and (if relevant) the synthetic `IStartupFilter` endpoint already in place.
- **2026-04-28 — "AI boundary validation needs direct analyzer coverage."**
  Applies to DD-9: extend `AiFlagAnalyzer` tests (or
  `BanderasServiceAnalysisSanitizationTests`) to assert the sanitization wiring
  rather than only the HTTP-level 200 path.

---

## Out of Scope

The following are **explicitly deferred** to later specs/phases:

- **`Flag` → `FlagDefinition` / `FlagEnvironmentConfig` aggregate split.** The
  larger DDD-backlog refactor that would make Description and Tags genuinely
  environment-agnostic. Will require a forward migration that moves the new
  columns from `flags` to the future `flag_definitions` table. Tracked in
  `flag-ddd-analysis-backlog.md`.
- **`Variation` value object on `Flag`** — multivariate flag support. Phase 2+
  backlog item; this spec does not introduce variations.
- **Tag-based query endpoints** (`GET /api/flags?tag=squad-checkout`) — no new
  query parameters or endpoints are added. The persistence shape (`jsonb`)
  permits adding GIN-indexed tag search in a future spec without a schema change.
- **Phase 4 evaluation-trace endpoint surfacing description/tags** — deferred to
  Phase 4 alongside `FlagQuery`.
- **Authentication, authorization, audit trail for metadata mutations** — Phase 3.
- **Editing tags individually via PATCH** — out of scope; PUT-replace is the
  semantics for this spec. PATCH may be revisited in Phase 7 SDK design.
- **Migration of existing per-env divergent description/tags into a single
  definition row** — owned by the aggregate-split spec; this spec accepts
  per-env divergence as a temporary state.

---

## Learning Opportunities

1. **EF Core `ValueConverter` for collection types stored as `jsonb`.** The
   `TagListConverter` demonstrates the same pattern `StrategyConfigConverter`
   uses but for a `List<T>` rather than a typed Value Object — useful for any
   future column that holds a small list of primitives.
2. **FluentValidation collection rules: `RuleFor` vs `RuleForEach` + `When`.**
   This is the first DTO with both a collection-level rule (count) and per-element
   rules (length, char-class) plus a null-tolerant `When` guard for the update
   case. Worth understanding because Phase 5 targeting rules will use the same
   shape (attribute name + values list + per-value validation).
3. **Postgres `jsonb` defaults + nullable column migrations.** The migration
   exercises both shapes in one file — nullable text column for `Description` and
   `NOT NULL jsonb DEFAULT '[]'` for `Tags`. The default-value mechanism is what
   makes the migration backward-compatible with existing rows. Future migrations
   that add NOT NULL columns to populated tables will reuse this pattern.

---

## DX / Tooling Idea

`Requests/smoke-test.http` currently has a single "create flag" template per
strategy type. A small, buildable improvement: add a **commented "minimal vs
rich" pair** for one of the create samples:

```http
### Minimal: backward-compatible create — no metadata
POST {{host}}/api/flags
Content-Type: application/json

{
  "name": "minimal-example",
  "environment": "Development",
  "isEnabled": true,
  "strategyType": "None"
}

### Rich: same flag with description + tags
POST {{host}}/api/flags
Content-Type: application/json

{
  "name": "rich-example",
  "environment": "Development",
  "isEnabled": true,
  "strategyType": "None",
  "description": "A second flag created for the smoke-test walkthrough.",
  "tags": ["smoke-test", "demo"]
}
```

This gives a new contributor a one-screen demonstration of the additive nature of
the metadata fields, reinforces the "null = no change / [] = clear" PUT semantics
later in the file, and costs ~20 lines of `.http` to maintain.

---

## Definition of Done

### Domain

- [ ] `Flag.Description` (`string?`) and `Flag.Tags` (`IReadOnlyList<string>`)
      added; both default to `null` and empty list respectively in the EF Core
      private ctor
- [ ] Public `Flag` ctor accepts optional `description` + `tags`; tags default
      to `[]` when omitted
- [ ] `Flag.UpdateMetadata(string?, IReadOnlyList<string>)` added with archived-
      state guard (throws `FlagDomainException`) and `UpdatedAt` bump
- [ ] No public setters introduced; encapsulation preserved

### Application

- [ ] `CreateFlagRequest`, `UpdateFlagRequest`, `FlagResponse` carry the new
      fields with the agreed nullability
- [ ] `FlagMappings.ToResponse` propagates description + tags
- [ ] `CreateFlagRequestValidator` enforces description length (≤500), tag count
      (≤20), per-tag length (≤50), and char-class (`^[a-z0-9\-_]+$` on cleaned
      lowercased value)
- [ ] `UpdateFlagRequestValidator` enforces the same rules guarded by
      `When(x => x.Tags is not null, ...)` and
      `When(x => x.Description is not null, ...)` (null = no change)
- [ ] `BanderasService.CreateFlagAsync` sanitizes description (when non-null) and
      normalizes tags (Clean + ToLowerInvariant + Distinct) before constructing
      `Flag`
- [ ] `BanderasService.UpdateFlagAsync` calls `Reconfigure` then conditionally
      calls `UpdateMetadata` with normalized values; both within a single
      `SaveChangesAsync`
- [ ] `BanderasService.AnalyzeFlagsAsync`'s `with` projection sanitizes
      description (when non-null) and each tag via `IPromptSanitizer`

### Infrastructure

- [ ] `TagListConverter` lives at `Banderas.Infrastructure/Persistence/`
- [ ] `FlagConfiguration` maps `Description` (nullable, max-length 500) and `Tags`
      (`jsonb`, `IsRequired`, `TagListConverter`, default `"[]"`)
- [ ] EF Core migration `AddFlagDescriptionAndTags` adds both columns; existing
      rows populated correctly on `dotnet ef database update`
- [ ] `BanderasDbContextModelSnapshot.cs` reflects the new columns
- [ ] `AiFlagAnalyzer.BuildPrompt` emits description + tags into the per-flag
      payload; `SystemPrompt` adds them to the inert-data rule
- [ ] `DatabaseSeeder.SeedManifest`: all six entries carry realistic description
      + tags; smoke `docker compose up` shows them via GET

### Tests

- [ ] Unit tests cover: `Flag.UpdateMetadata` (archived guard, UpdatedAt bump,
      tag-list replacement, null-description clear); validator rules for both
      DTOs; service-layer normalization; `TagListConverter` round-trip; AI
      prompt-projection sanitization
- [ ] Integration tests cover: POST with/without metadata; tag normalization;
      validator rejections returning `application/problem+json`; PUT replace /
      no-change / clear semantics for both fields; archived flag's PUT-metadata
      attempt results in the documented HTTP shape; AI endpoint reaches the stub
      analyzer with sanitized metadata in its payload
- [ ] No existing test broken by the schema change (audit pass complete per
      2026-05-07 lesson)
- [ ] `153 → ≥165` unit and `54 → ≥60` integration counts achieved

### Build & CI

- [ ] `dotnet build -p:TreatWarningsAsErrors=true` succeeds
- [ ] `dotnet csharpier check .` passes
- [ ] `dotnet test` (unit + integration) all green locally
- [ ] `packages.lock.json` unchanged or regenerated cleanly; `--locked-mode`
      restore passes in CI
- [ ] `Requests/smoke-test.http` exercised end-to-end against a freshly-seeded
      database

### Documentation (handed to `/post-work`, not in this PR's commit log)

- [ ] `Docs/architecture.md`: `Flag` domain section updated; mutation surface
      lists `UpdateMetadata`; AI-prompt section notes new fields
- [ ] `Docs/current-state.md`: Phase 2 progress entry; DoD checks for the two
      backlog items; tally update
- [ ] `Docs/roadmap.md`: Phase 2 progress line updated; mention forward-
      migration owed to the aggregate-split spec
- [ ] `Docs/Decisions/flag-ddd-analysis-backlog.md`: the two completed bullets
      ticked off with cross-link to this spec

---
