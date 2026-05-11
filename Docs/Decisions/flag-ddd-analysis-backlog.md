# Flag.cs — DDD Analysis Backlog

**Session date:** 2026-04-30  
**Branch:** `refactor/flag-domain-model`  
**Source:** Grill-me DDD analysis of `Flag.cs`
**Status:** Exploration only — not yet scoped or specced

---

## Summary of Findings

`Flag.cs` was analyzed through a DDD lens and found to be mixing two distinct concerns:

1. **Flag definition** — what a flag *is* (name, description, tags, variations)
2. **Flag environment configuration** — how a flag *behaves* per environment (enabled state, rules, fallthrough, archival)

This rigidity means every new rollout strategy requires touching the enum, the entity, the deserializer, and the strategy dispatcher simultaneously. LaunchDarkly's model was used as a reference — they separate definition from environment-specific behavior entirely, allowing rules and targeting to evolve without touching the core flag entity.

---

## Key DDD Concepts Identified

| Concept | Application to Banderas |
|---|---|
| Aggregate Root | `Flag` and `FlagEnvironmentConfig` are separate ARs — they have independent consistency boundaries |
| Value Object | `StrategyConfig` should become typed Value Objects per strategy; `Variation` is a Value Object |
| Make Illegal States Unrepresentable | Invalid strategy config should be caught at Value Object construction, not at runtime |
| Domain Exception | `FlagDomainException` — speaks business rule language, not infrastructure language |
| Terminal State | Archived is a terminal state — no mutations allowed after archival |
| Reference by Identity | Aggregates reference each other by `Guid` only, never by object reference |
| Single Responsibility | `Flag` was carrying definition + environment behavior + seeding concern — three responsibilities |
| Separated Definition vs. Configuration | Flag = definition; `FlagEnvironmentConfig` = per-environment behavior |

---

## Proposed `Flag` (Pure Definition)

```csharp
public class Flag
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public IReadOnlyList<string> Tags { get; private set; }
    public IReadOnlyList<Variation> Variations { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}
```

---

## Proposed `FlagEnvironmentConfig` (New Aggregate Root)

```csharp
public class FlagEnvironmentConfig
{
    public Guid Id { get; private set; }
    public Guid FlagId { get; private set; }        // reference by ID only — no navigation property
    public EnvironmentType Environment { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool IsArchived { get; private set; }
    public IReadOnlyList<TargetingRule> Rules { get; private set; }
    public Fallthrough Fallthrough { get; private set; }
    public DateTime? ArchivedAt { get; private set; }
}
```

---

## Backlog Items

> Potential changes identified during DDD analysis.  
> Not yet scoped, specced, or assigned to a PR.  
> Sequence is approximate — some items have dependencies.

- [X] **Introduce `FlagDomainException`** — dedicated domain exception type; thrown by domain invariant violations; lives in `Banderas.Domain`
- [X] **Enforce archived state as terminal** — guard clause at top of `Archive()`, `SetEnabled()`, `UpdateStrategy()`, `Update()`, and `UpdateName()`; throw `FlagDomainException` if already archived (PR #59)
- [X] **Remove `IsSeeded` from `Flag`** — move to infrastructure seeding concern; its presence on the domain entity is a boundary violation _(shipped as EF Core shadow property; second `Flag` constructor deleted; PR TBD)_
- [X] **Consolidate `SetEnabled()` + `UpdateStrategy()` + `Update()`** — separate by concern not field; name changes (`UpdateName()`) are a distinct operation; rollout config changes are one operation _(shipped: `SetEnabled` and `UpdateStrategy` deleted, `Update` renamed to `Reconfigure`; PR TBD)_
- [ ] **Convert `StrategyConfig` from raw `string` to typed Value Objects** — one Value Object per strategy type (e.g. `PercentageConfig`, `RoleBasedConfig`); invalid config caught at construction
- [ ] **Enforce config/strategy type consistency inside `Flag`** — `Flag` rejects a `PercentageConfig` when `StrategyType` is `RoleBased`; illegal state becomes unrepresentable
- [ ] **Add `Description` to `Flag` definition** — environment-agnostic metadata
- [ ] **Add `Tags` collection to `Flag` definition** — environment-agnostic organizational labels
- [ ] **Introduce `Variation` as a Value Object on `Flag`** — possible return values defined once on the definition; referenced by index in environment config rules
- [ ] **Introduce `FlagEnvironmentConfig` as a separate Aggregate Root** — owns `IsEnabled`, `IsArchived`, `ArchivedAt`, `Rules`, `Fallthrough` per environment; has its own consistency boundary
- [ ] **`Flag` becomes a pure definition** — name, description, tags, variations, timestamps only; no environment-specific state
- [ ] **Reference aggregates by `Guid` only** — no navigation properties across aggregate boundaries; `FlagEnvironmentConfig.FlagId` is a `Guid`, not a `Flag`
- [ ] **Introduce `IFlagEnvironmentConfigRepository`** — separate from `IBanderasRepository`; owns all persistence operations for the new aggregate
- [ ] **Revisit C# 14 extension members** — potential future mechanism for adding behavior to `Flag` without modifying the entity directly; defer until after core refactor is stable

---

## Interview Talking Points

### On the refactoring decision
*"When I reviewed our `Flag` entity through a DDD lens, I noticed it was violating the Single Responsibility Principle at the domain level — it was mixing flag definition with per-environment behavioral configuration. That rigidity was already bleeding into the service and controller layers. Every time we wanted to add a new rollout strategy, we had to touch the enum, the entity, the deserializer, and hope nothing broke silently. Before the codebase grew more complex, I separated `Flag` into a pure definition aggregate and introduced `FlagEnvironmentConfig` as its own aggregate root. That boundary meant environment-specific behavior could evolve independently without touching the flag definition — which is exactly how production systems like to model it."*

### On aggregate boundary decisions
*"I asked whether two things need to coordinate to stay consistent. If changing one requires knowing about the other, they belong in the same aggregate. If they're completely independent — like a flag definition and its per-environment configuration — they're separate aggregates with separate repositories. Forcing them together just creates unnecessary coupling and coordination overhead."*

### On make illegal states unrepresentable
*"Rather than validating data after the fact, we designed types so bad data can never exist in the first place. A `PercentageConfig` with no threshold can't be constructed. An archived flag can't be re-enabled. The type system enforces the business rules — not a validator somewhere downstream."*
